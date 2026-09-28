using DeLong.Web.Common.Auditing;
using DeLong.Web.Common.Operations;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Bookings;
using DeLong.Web.Features.Customers;
using DeLong.Web.Features.Notifications;
using DeLong.Web.Features.Payments;
using DeLong.Web.Features.Vouchers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeLong.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class SePayPaymentIntegrationTests
{
    private static readonly IDataProtectionProvider Protection = new EphemeralDataProtectionProvider();
    private const string Key = "sepay-test-key-0123456789-abcdefghijkl";

    [PostgresFact]
    [Trait("Category", "Integration")]
    public async Task Creation_webhook_duplicate_and_additional_transfer_preserve_payment_and_voucher()
    {
        await using var f = await Fixture.Create();
        var (created, error) = await f.Lifecycle.CreateIntentAsync(f.Property.Id, f.Booking.Id, true);
        Assert.Null(error);
        Assert.StartsWith("/payment/sepay?orderId=DH", created!.PayUrl);
        var intent = await f.Db.Pay2SPaymentIntents.SingleAsync(x => x.Id == created.Id);
        Assert.Equal(PaymentMethod.SePay, intent.Provider);
        var request = f.Request(intent.OrderId);
        Assert.Equal((200, "succeeded"), await f.Service.ReceiveAsync(f.Property.Id, request, "Apikey " + Key, default));
        Assert.Equal((200, "succeeded"), await f.Service.ReceiveAsync(f.Property.Id, request, "Apikey " + Key, default));
        Assert.Equal((200, "additional_transfer"), await f.Service.ReceiveAsync(f.Property.Id, request with { Id = request.Id + 1 }, "Apikey " + Key, default));
        Assert.Equal(1, await f.Db.Payments.CountAsync(x => x.BookingId == f.Booking.Id));
        Assert.Equal(BookingStatus.Confirmed, f.Booking.Status);
        Assert.Equal(VoucherRedemptionStatus.Redeemed, f.Redemption.Status);
        Assert.Equal(PaymentMethod.SePay, (await f.Db.Payments.SingleAsync(x => x.BookingId == f.Booking.Id)).Method);
    }

    [PostgresFact]
    [Trait("Category", "Integration")]
    public async Task Truncated_provider_code_stores_the_full_code_resolved_from_content()
    {
        await using var f = await Fixture.Create();
        var (created, error) = await f.Lifecycle.CreateIntentAsync(f.Property.Id, f.Booking.Id, true);
        Assert.Null(error);

        var request = f.Request(created!.OrderId) with
        {
            Code = created.OrderId[..^2],
            Content = created.OrderId
        };

        Assert.Equal((200, "succeeded"),
            await f.Service.ReceiveAsync(f.Property.Id, request, "Apikey " + Key, default));
        var transaction = await f.Db.SePayTransactions.SingleAsync(x => x.TransactionId == request.Id);
        Assert.Equal(created.OrderId, transaction.Code);
    }

    [PostgresTheory]
    [InlineData("auth", 401)]
    [InlineData("account", 400)]
    [InlineData("subaccount", 400)]
    [InlineData("amount", 200)]
    [InlineData("code", 200)]
    [InlineData("out", 200)]
    [Trait("Category", "Integration")]
    public async Task Invalid_or_unmatched_transfers_never_confirm_booking(string kind, int status)
    {
        await using var f = await Fixture.Create();
        var (intent, _) = await f.Lifecycle.CreateIntentAsync(f.Property.Id, f.Booking.Id, true);
        var request = f.Request(intent!.OrderId);
        request = kind switch
        {
            "account" => request with { AccountNumber = "different" },
            "subaccount" => request with { SubAccount = "other-va" },
            "amount" => request with { TransferAmount = 100 },
            "code" => request with { Code = "WRONG", Content = "WRONG" },
            "out" => request with { TransferType = "out" },
            _ => request
        };
        var result = await f.Service.ReceiveAsync(f.Property.Id, request, kind == "auth" ? "Apikey wrong" : "Apikey " + Key, default);
        Assert.Equal(status, result.Status);
        Assert.False(await f.Db.Payments.AnyAsync(x => x.BookingId == f.Booking.Id));
        Assert.Equal(BookingStatus.Held, f.Booking.Status);
        Assert.Equal(VoucherRedemptionStatus.Reserved, f.Redemption.Status);
        if (kind is "amount" or "code") Assert.True(await f.Db.SePayTransactions.AnyAsync(x => x.TransactionId == request.Id));
    }

    [PostgresFact]
    [Trait("Category", "Integration")]
    public async Task Expiry_releases_voucher_late_transfer_is_recorded_without_restoring_room()
    {
        await using var f = await Fixture.Create();
        var (created, _) = await f.Lifecycle.CreateIntentAsync(f.Property.Id, f.Booking.Id, true);
        var intent = await f.Db.Pay2SPaymentIntents.SingleAsync(x => x.Id == created!.Id);
        intent.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-5);
        intent.ReleaseAtUtc = DateTime.UtcNow.AddMinutes(-1);
        await f.Db.SaveChangesAsync();
        await f.Lifecycle.ExpirePendingAsync();
        Assert.Equal(BookingStatus.Cancelled, f.Booking.Status);
        Assert.Equal(VoucherRedemptionStatus.Released, f.Redemption.Status);
        Assert.Equal((200, "paid_after_expiry"), await f.Service.ReceiveAsync(f.Property.Id, f.Request(intent.OrderId), "Apikey " + Key, default));
        Assert.Equal(BookingStatus.Cancelled, f.Booking.Status);
        Assert.Equal(Pay2SPaymentIntentStatus.PaidAfterExpiry, intent.Status);
        Assert.True(await f.Db.Payments.AnyAsync(x => x.BookingId == f.Booking.Id && x.Method == PaymentMethod.SePay));
    }

    [PostgresFact]
    [Trait("Category", "Integration")]
    public async Task Old_session_remains_verifiable_after_profile_is_disabled_and_rotated()
    {
        await using var f = await Fixture.Create();
        var (created, _) = await f.Lifecycle.CreateIntentAsync(f.Property.Id, f.Booking.Id, true);
        var profile = await f.Db.PropertySePaySettings.SingleAsync(x => x.PropertyId == f.Property.Id);
        profile.Enabled = false;
        profile.WebhookKeyProtected = f.Settings.Protect("new-key-abcdefghijklmnopqrstuvwxyz");
        profile.BankAccountNumber = "different";
        await f.Db.SaveChangesAsync();
        Assert.Equal((200, "succeeded"), await f.Service.ReceiveAsync(f.Property.Id, f.Request(created!.OrderId), "Apikey " + Key, default));
    }

    [PostgresFact]
    [Trait("Category", "Integration")]
    public async Task Open_session_accepts_corrected_current_bank_and_va_destination()
    {
        await using var f = await Fixture.Create();
        var (created, _) = await f.Lifecycle.CreateIntentAsync(f.Property.Id, f.Booking.Id, true);
        var profile = await f.Db.PropertySePaySettings.SingleAsync(x => x.PropertyId == f.Property.Id);
        profile.BankAccountNumber = "1069029363";
        profile.QrAccountNumber = "QRPSEP1ZZZZ51200231";
        profile.SubAccount = "QRPSEP1ZZZZ51200231";
        await f.Db.SaveChangesAsync();

        var request = f.Request(created!.OrderId) with
        {
            AccountNumber = profile.BankAccountNumber,
            SubAccount = profile.SubAccount
        };

        Assert.Equal((200, "succeeded"),
            await f.Service.ReceiveAsync(f.Property.Id, request, "Apikey " + Key, default));
        Assert.Equal(BookingStatus.Confirmed, f.Booking.Status);
        Assert.True(await f.Db.Payments.AnyAsync(x =>
            x.BookingId == f.Booking.Id && x.Method == PaymentMethod.SePay));
    }

    [PostgresFact]
    [Trait("Category", "Integration")]
    public async Task Parallel_webhooks_commit_exactly_one_payment()
    {
        await using var f = await Fixture.Create();
        var (created, _) = await f.Lifecycle.CreateIntentAsync(f.Property.Id, f.Booking.Id, true);
        var request = f.Request(created!.OrderId);
        async Task<(int Status, string Outcome)> Send()
        {
            await using var db = Fixture.NewDb();
            return await Fixture.NewService(db).ReceiveAsync(f.Property.Id, request, "Apikey " + Key, default);
        }
        var results = await Task.WhenAll(Send(), Send(), Send());
        Assert.All(results, result => Assert.Equal(200, result.Status));
        Assert.Equal(1, await f.Db.Payments.CountAsync(x => x.BookingId == f.Booking.Id));
    }

    [PostgresFact]
    [Trait("Category", "Integration")]
    public async Task Manual_payment_closes_sepay_qr_and_bank_transfer_is_late()
    {
        await using var f = await Fixture.Create();
        var (created, _) = await f.Lifecycle.CreateIntentAsync(f.Property.Id, f.Booking.Id, true);
        await new PaymentService(f.Db).AddAsync(f.Property.Id, f.Booking.Id,
            new CreatePaymentRequest { Type = PaymentType.Receipt, Method = PaymentMethod.Cash, Amount = f.Booking.TotalAmount }, null);
        Assert.Equal((200, "paid_after_expiry"), await f.Service.ReceiveAsync(f.Property.Id, f.Request(created!.OrderId), "Apikey " + Key, default));
        Assert.Equal(BookingStatus.Held, f.Booking.Status);
        Assert.Equal(2, await f.Db.Payments.CountAsync(x => x.BookingId == f.Booking.Id));
    }

    [PostgresFact]
    [Trait("Category", "Integration")]
    public async Task Shared_bank_webhook_cannot_claim_another_propertys_payment_code()
    {
        await using var f = await Fixture.Create();
        var (created, _) = await f.Lifecycle.CreateIntentAsync(f.Property.Id, f.Booking.Id, true);
        var other = new Property { Code = "OTHER" + Guid.NewGuid().ToString("N")[..8], Name = "Other QA" };
        f.Db.AddRange(other, new PropertySePaySettings { PropertyId = other.Id, BankAccountNumber = "123456789", WebhookKeyProtected = f.Settings.Protect(Key) });
        await f.Db.SaveChangesAsync();
        var request = f.Request(created!.OrderId);
        Assert.Equal((200, "other_property_ignored"), await f.Service.ReceiveAsync(other.Id, request, "Apikey " + Key, default));
        Assert.False(await f.Db.SePayTransactions.AnyAsync(x => x.TransactionId == request.Id));
        Assert.Equal((200, "succeeded"), await f.Service.ReceiveAsync(f.Property.Id, request, "Apikey " + Key, default));
    }

    [PostgresFact]
    [Trait("Category", "Integration")]
    public async Task Missing_webhook_can_be_reconciled_from_provider_api_without_duplicate_payment()
    {
        await using var f = await Fixture.Create();
        var (created, _) = await f.Lifecycle.CreateIntentAsync(f.Property.Id, f.Booking.Id, true);
        var profile = await f.Db.PropertySePaySettings.SingleAsync(x => x.PropertyId == f.Property.Id);
        profile.ApiTokenProtected = f.Settings.Protect("qa-api-token");
        await f.Db.SaveChangesAsync();
        var request = f.Request(created!.OrderId);
        using var http = new HttpClient(new ApiHandler(request));
        var service = Fixture.NewService(f.Db, http);
        Assert.Equal((200, "succeeded"), await service.ReconcileAsync(f.Property.Id, request.Id, default));
        Assert.Equal((200, "succeeded"), await f.Service.ReceiveAsync(f.Property.Id, request, "Apikey " + Key, default));
        Assert.Equal(1, await f.Db.Payments.CountAsync(x => x.BookingId == f.Booking.Id));
    }

    [PostgresFact]
    [Trait("Category", "Integration")]
    public async Task Concurrent_late_refund_resolution_records_one_sepay_refund()
    {
        await using var f = await Fixture.Create();
        var (created, _) = await f.Lifecycle.CreateIntentAsync(f.Property.Id, f.Booking.Id, true);
        await Pay2SIntentLifecycleManager.CloseOpenIntentsAsync(f.Db, f.Property.Id, f.Booking.Id);
        await f.Db.SaveChangesAsync();
        await f.Service.ReceiveAsync(f.Property.Id, f.Request(created!.OrderId), "Apikey " + Key, default);
        async Task<(Pay2SIntentDto? Intent, Pay2SOperationError? Error)> Resolve()
        {
            await using var db = Fixture.NewDb();
            return await Fixture.NewLifecycle(db).ResolveLatePaymentAsync(f.Property.Id, created.Id, new("refund", "QA refund recorded"), null);
        }
        var results = await Task.WhenAll(Resolve(), Resolve());
        Assert.Single(results, x => x.Error is null);
        Assert.Single(results, x => x.Error?.Code == "intent_already_resolved");
        Assert.Equal(1, await f.Db.Payments.CountAsync(x => x.BookingId == f.Booking.Id && x.Type == PaymentType.Refund && x.Method == PaymentMethod.SePay));
    }

    [PostgresFact]
    [Trait("Category", "Integration")]
    public async Task Parallel_creation_reuses_one_pending_qr()
    {
        await using var f = await Fixture.Create();
        async Task<Pay2SIntentDto?> Create()
        {
            await using var db = Fixture.NewDb();
            var (intent, error) = await Fixture.NewLifecycle(db).CreateIntentAsync(f.Property.Id, f.Booking.Id, true);
            Assert.Null(error);
            return intent;
        }
        var results = await Task.WhenAll(Create(), Create());
        Assert.Equal(results[0]!.Id, results[1]!.Id);
        Assert.Equal(1, await f.Db.Pay2SPaymentIntents.CountAsync(x => x.BookingId == f.Booking.Id));
    }

    private sealed class ApiHandler(SePayWebhook row) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("qa-api-token", request.Headers.Authorization?.Parameter);
            Assert.Equal("https://my.sepay.vn/userapi/transactions/details/" + row.Id, request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(new { transaction = new {
                    id = row.Id.ToString(), account_number = row.AccountNumber, sub_account = row.SubAccount,
                    amount_in = row.TransferAmount.ToString(System.Globalization.CultureInfo.InvariantCulture), code = row.Code, transaction_content = row.Content } }))
            });
        }
    }

    public sealed class PostgresFactAttribute : FactAttribute
    {
        public PostgresFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION")))
                Skip = "Requires an isolated PostgreSQL database in DELONG_TEST_CONNECTION.";
        }
    }
    public sealed class PostgresTheoryAttribute : TheoryAttribute
    {
        public PostgresTheoryAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION")))
                Skip = "Requires an isolated PostgreSQL database in DELONG_TEST_CONNECTION.";
        }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public required AppDbContext Db { get; init; }
        public required Property Property { get; init; }
        public required Booking Booking { get; init; }
        public required VoucherRedemption Redemption { get; init; }
        public required SePaySettingsService Settings { get; init; }
        public required SePayService Service { get; init; }
        public required Pay2SService Lifecycle { get; init; }
        public static AppDbContext NewDb()
        {
            var connection = Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION");
            Assert.False(string.IsNullOrWhiteSpace(connection), "Set DELONG_TEST_CONNECTION to an isolated PostgreSQL QA database.");
            return new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
        }
        private static VoucherService Vouchers(AppDbContext db) => new(db, new AuditService(db),
            new StoragePaths(Path.GetTempPath(), Path.GetTempPath(), "/uploads", true, true, false), new ConfigurationBuilder().Build());
        public static SePayService NewService(AppDbContext db, HttpClient? http = null) => new(db, new SePaySettingsService(db, Protection), http ?? new HttpClient(),
            Vouchers(db), new BookingNotificationService(db, new NotificationRealtimeBroker(), NullLogger<BookingNotificationService>.Instance),
            new BookingGuestGuideEmailService(db, NullLogger<BookingGuestGuideEmailService>.Instance), new PaymentService(db));
        public static Pay2SService NewLifecycle(AppDbContext db) => new(db,
            new Pay2SSettingsService(db, new Pay2SCredentialProtector(Protection)), new Pay2SClient(new HttpClient()),
            new BookingService(db, new CustomerService(db), new AuditService(db)),
            new BookingNotificationService(db, new NotificationRealtimeBroker(), NullLogger<BookingNotificationService>.Instance),
            new BookingGuestGuideEmailService(db, NullLogger<BookingGuestGuideEmailService>.Instance), Vouchers(db), new PaymentService(db));
        public SePayWebhook Request(string code) => new(Random.Shared.NextInt64(100000000, 900000000000), "123456789", "", code, code, "in", Booking.TotalAmount);
        public static async Task<Fixture> Create()
        {
            var db = NewDb();
            await db.Database.MigrateAsync();
            var suffix = Guid.NewGuid().ToString("N")[..10];
            var property = new Property { Code = "SP" + suffix, Name = "SePay QA", TimeZoneId = "Asia/Ho_Chi_Minh" };
            var room = new Room { Property = property, Code = "R" + suffix, Name = "QA Room", Slug = "qa-" + suffix };
            var customer = new Customer { Property = property, Name = "QA Guest", Phone = "0901234567", NormalizedPhone = "0901234567" };
            var booking = new Booking { Property = property, Room = room, Customer = customer, Code = "B" + suffix,
                CheckInUtc = DateTime.UtcNow.AddDays(2), CheckOutUtc = DateTime.UtcNow.AddDays(2).AddHours(3), Status = BookingStatus.Held, RoomAmount = 250000 };
            var voucher = new Voucher { Property = property, Code = "V" + suffix, NormalizedCode = "V" + suffix.ToUpperInvariant(),
                DiscountPercent = 10, AppliesTo = VoucherApplicability.TimeSlot, StartsAtUtc = DateTime.UtcNow.AddDays(-1), EndsAtUtc = DateTime.UtcNow.AddDays(10) };
            var redemption = new VoucherRedemption { Property = property, Booking = booking, Customer = customer, Voucher = voucher,
                VoucherCode = voucher.Code, DiscountPercent = 10, EligibleRoomAmount = 250000, DiscountAmount = 25000 };
            var settings = new SePaySettingsService(db, Protection);
            db.AddRange(property, room, customer, booking, voucher, redemption,
                new PropertySePaySettings { PropertyId = property.Id, Enabled = true, BankAccountNumber = "123456789", QrAccountNumber = "123456789", WebhookKeyProtected = settings.Protect(Key) });
            await db.SaveChangesAsync();
            var lifecycle = NewLifecycle(db);
            return new Fixture { Db = db, Property = property, Booking = booking, Redemption = redemption, Settings = settings, Service = NewService(db), Lifecycle = lifecycle };
        }
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
