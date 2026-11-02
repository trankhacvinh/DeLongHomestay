using DeLong.Web.Common.Auditing;
using DeLong.Web.Common.Operations;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Vouchers;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DeLong.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class VoucherConcurrencyTests
{
    [RoomLockPostgresFact]
    [Trait("Category", "Integration")]
    public async Task Voucher_list_prioritizes_active_then_newest_and_preserves_filters_and_property_scope()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION")!).Options);
        await db.Database.MigrateAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var property = new Property { Code = $"VL-{suffix}", Name = "Voucher list test" };
        var otherProperty = new Property { Code = $"VO-{suffix}", Name = "Other voucher property" };
        var start = new DateTime(2031, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Voucher NewVoucher(Property owner, string code, VoucherStatus status) => new()
        {
            Property = owner,
            Code = code,
            NormalizedCode = code,
            DiscountPercent = 10m,
            AppliesTo = VoucherApplicability.TimeSlot,
            StartsAtUtc = start,
            EndsAtUtc = start.AddMonths(1),
            Status = status
        };
        var activeOld = NewVoucher(property, "ACTIVE-OLD", VoucherStatus.Active);
        var paused = NewVoucher(property, "PAUSED", VoucherStatus.Paused);
        var activeNew = NewVoucher(property, "ACTIVE-NEW", VoucherStatus.Active);
        var archived = NewVoucher(property, "ARCHIVED", VoucherStatus.Archived);
        var draft = NewVoucher(property, "DRAFT", VoucherStatus.Draft);
        var other = NewVoucher(otherProperty, "OTHER", VoucherStatus.Active);
        Voucher[] vouchers = [activeOld, paused, activeNew, archived, draft, other];
        db.Vouchers.AddRange(vouchers);
        await db.SaveChangesAsync();
        for (var index = 0; index < vouchers.Length; index++)
            vouchers[index].CreatedAtUtc = start.AddDays(index);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var all = await service.GetAllAsync(property.Id, null, null, null, null);
        Assert.Equal(new[] { activeNew.Id, activeOld.Id, draft.Id, archived.Id, paused.Id }, all.Select(x => x.Id));
        var active = await service.GetAllAsync(property.Id, VoucherStatus.Active, null, null, null);
        Assert.Equal(new[] { activeNew.Id, activeOld.Id }, active.Select(x => x.Id));
        var drafts = await service.GetAllAsync(property.Id, VoucherStatus.Draft, null, null, null);
        Assert.Equal(draft.Id, Assert.Single(drafts).Id);
        var searched = await service.GetAllAsync(property.Id, null, "ACTIVE", start, start.AddMonths(1));
        Assert.Equal(new[] { activeNew.Id, activeOld.Id }, searched.Select(x => x.Id));
        Assert.Empty(await service.GetAllAsync(property.Id, null, null, start.AddMonths(2), null));
        Assert.Empty(await service.GetAllAsync(property.Id, null, null, null, start.AddDays(-1)));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Hundred_percent_voucher_can_be_redeemed_and_manually_restored_only_after_cancellation()
    {
        var connectionString = Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var property = new Property { Code = $"V100-{suffix}", Name = $"Voucher 100 Test {suffix}", TimeZoneId = "Asia/Ho_Chi_Minh" };
        var room = new Room { Property = property, Code = $"R100-{suffix}", Name = "Voucher 100 room" };
        var customer = Customer(property, $"0903{Random.Shared.Next(100000, 999999)}", "Khách voucher 100");
        var booking = Booking(property, room, customer, $"BK-V100-{suffix}", new DateTime(2026, 11, 1, 1, 0, 0, DateTimeKind.Utc));
        var voucher = new Voucher
        {
            Property = property,
            Code = $"FREE-{suffix}",
            NormalizedCode = $"FREE-{suffix}".ToUpperInvariant(),
            DiscountPercent = 100m,
            AppliesTo = VoucherApplicability.TimeSlot,
            StartsAtUtc = DateTime.UtcNow.AddDays(-1),
            EndsAtUtc = DateTime.UtcNow.AddDays(10),
            Status = VoucherStatus.Active
        };
        db.AddRange(property, room, customer, booking, voucher);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var (result, reserveError) = await service.ReserveForBookingAsync(property.Id, booking.Id, voucher.Code, null);
        Assert.Null(reserveError);
        Assert.NotNull(result);
        Assert.Equal(0m, result.TotalAfterDiscount);

        await service.MarkRedeemedAsync(booking.Id);
        await db.SaveChangesAsync();
        var redemption = await db.VoucherRedemptions.SingleAsync(x => x.BookingId == booking.Id);
        Assert.Equal(VoucherRedemptionStatus.Redeemed, redemption.Status);

        Assert.Equal("booking_not_cancelled", (await service.RestoreAsync(property.Id, redemption.Id, "Chưa hủy", null))?.Code);
        booking.Status = BookingStatus.Cancelled;
        await db.SaveChangesAsync();
        Assert.Null(await service.RestoreAsync(property.Id, redemption.Id, "Khách đổi lịch", null));
        Assert.Equal(VoucherRedemptionStatus.ManuallyRestored, redemption.Status);
        Assert.Contains(await db.AuditLogs.ToListAsync(), x => x.EntityId == voucher.Id && x.Action == "RedemptionRestored");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Two_bookings_competing_for_last_voucher_usage_allow_only_one_reservation()
    {
        var connectionString = Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
        await using (var setup = new AppDbContext(options))
        {
            await setup.Database.MigrateAsync();
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var property = new Property { Code = $"VC-{suffix}", Name = $"Voucher Test {suffix}", TimeZoneId = "Asia/Ho_Chi_Minh" };
            var room = new Room { Property = property, Code = $"R-{suffix}", Name = "Voucher room" };
            var firstCustomer = Customer(property, $"0901{Random.Shared.Next(100000, 999999)}", "Khách một");
            var secondCustomer = Customer(property, $"0902{Random.Shared.Next(100000, 999999)}", "Khách hai");
            var firstBooking = Booking(property, room, firstCustomer, $"BK-VC-A-{suffix}", new DateTime(2026, 10, 1, 1, 0, 0, DateTimeKind.Utc));
            var secondBooking = Booking(property, room, secondCustomer, $"BK-VC-B-{suffix}", new DateTime(2026, 10, 1, 5, 0, 0, DateTimeKind.Utc));
            var voucher = new Voucher
            {
                Property = property,
                Code = $"LAST-{suffix}",
                NormalizedCode = $"LAST-{suffix}".ToUpperInvariant(),
                DiscountPercent = 25m,
                AppliesTo = VoucherApplicability.TimeSlot,
                StartsAtUtc = DateTime.UtcNow.AddDays(-1),
                EndsAtUtc = DateTime.UtcNow.AddDays(10),
                TotalUsageLimit = 1,
                Status = VoucherStatus.Active
            };
            setup.AddRange(property, room, firstCustomer, secondCustomer, firstBooking, secondBooking, voucher);
            await setup.SaveChangesAsync();

            var attempts = await Task.WhenAll(
                ReserveAsync(options, property.Id, firstBooking.Id, voucher.Code),
                ReserveAsync(options, property.Id, secondBooking.Id, voucher.Code));

            Assert.Single(attempts, x => x.Success);
            Assert.Single(attempts, x => x.ErrorCode == "voucher_usage_exhausted");

            await using var verificationDb = new AppDbContext(options);
            var list = await CreateService(verificationDb).GetAllAsync(property.Id, null, voucher.Code, null, null);
            Assert.Single(list);
            Assert.Equal(0, list[0].RemainingCount);
        }
    }

    private static async Task<(bool Success, string? ErrorCode)> ReserveAsync(
        DbContextOptions<AppDbContext> options,
        Guid propertyId,
        Guid bookingId,
        string code)
    {
        await using var db = new AppDbContext(options);
        var service = CreateService(db);
        var (result, error) = await service.ReserveForBookingAsync(propertyId, bookingId, code, null);
        return (result is not null, error?.Code);
    }

    private static VoucherService CreateService(AppDbContext db)
    {
        var configuration = new ConfigurationBuilder().Build();
        var paths = new StoragePaths(Path.GetTempPath(), Path.GetTempPath(), new PathString("/test"), false, false, false);
        return new VoucherService(db, new AuditService(db), paths, configuration);
    }

    private static Customer Customer(Property property, string phone, string name) => new()
    {
        Property = property,
        Name = name,
        Phone = phone,
        NormalizedPhone = phone
    };

    private static Booking Booking(Property property, Room room, Customer customer, string code, DateTime checkIn) => new()
    {
        Property = property,
        Room = room,
        Customer = customer,
        Code = code,
        CheckInUtc = checkIn,
        CheckOutUtc = checkIn.AddHours(3),
        Status = BookingStatus.Held,
        Type = BookingType.TimeSlot,
        RoomAmount = 400_000m
    };
}
