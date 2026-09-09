using System.Net;
using System.Text.Json;
using DeLong.Web.Common.Auditing;
using DeLong.Web.Common.Operations;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.AdminAi;
using DeLong.Web.Features.Rooms;
using DeLong.Web.Features.Pricing;
using DeLong.Web.Features.Site;
using DeLong.Web.Features.Vouchers;
using DeLong.Web.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DeLong.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class AdminAiFlowTests
{
    private static string Proposal(string type, object payload) => JsonSerializer.Serialize(new
    {
        message = "Bản xem trước để duyệt.", proposal = new { type, summary = "Thay đổi theo yêu cầu", payloadJson = JsonSerializer.Serialize(payload) }
    });
    private static object Weekend => new { allRooms = true, allRates = true, changes = new { weekendPrice = 300000 } };

    [PricingPostgreSqlFact]
    public async Task Bulk_weekend_preview_is_read_only_scoped_and_applies_exact_prices_once()
    {
        await using var f = await Fixture.Create(Proposal("ConfigureRoomRates", Weekend));
        var (chat, error) = await f.Service.ChatAsync(f.Property.Id, f.User.Id, new(null, "giá cuối tuần các phòng 300k"), default);
        Assert.Null(error); Assert.NotNull(chat!.Proposal);
        Assert.All(await f.Db.RoomRates.AsNoTracking().Where(x => x.Room.PropertyId == f.Property.Id).ToListAsync(), x => Assert.Null(x.WeekendPrice));
        var preview = JsonSerializer.SerializeToElement(chat.Proposal.Payload);
        Assert.Equal(2, preview.GetProperty("preparedChanges").GetArrayLength());
        Assert.Equal(250000m, preview.GetProperty("preparedChanges")[0].GetProperty("before").GetProperty("price").GetDecimal());
        Assert.Equal(300000m, preview.GetProperty("preparedChanges")[0].GetProperty("after").GetProperty("weekendPrice").GetDecimal());
        var (applied, applyError) = await f.Service.ApplyAsync(f.Property.Id, f.User.Id, chat.Proposal.Id, default);
        Assert.Null(applyError); Assert.Equal(AiProposalStatus.Applied, applied!.Status);
        var rates = await f.Db.RoomRates.AsNoTracking().Where(x => x.Room.PropertyId == f.Property.Id).ToListAsync();
        Assert.All(rates, x => { Assert.Equal(250000, x.Price); Assert.Equal(300000, x.WeekendPrice); Assert.False(x.UseWeekdayPriceOnWeekend); });
        Assert.Null((await f.Db.RoomRates.AsNoTracking().SingleAsync(x => x.Id == f.OtherRate.Id)).WeekendPrice);
        Assert.NotNull((await f.Service.ApplyAsync(f.Property.Id, f.User.Id, chat.Proposal.Id, default)).Error);
    }

    [PricingPostgreSqlFact]
    public async Task Invalid_output_keeps_conversation_and_retry_receives_original_request_and_tokens()
    {
        await using var f = await Fixture.Create("invalid", "{", Proposal("ConfigureRoomRates", Weekend));
        var (failed, error) = await f.Service.ChatAsync(f.Property.Id, f.User.Id, new(null, "cấu hình giá cuối tuần 300k"), default);
        Assert.Null(error); Assert.True(failed!.CanRetry);
        var (retry, retryError) = await f.Service.ChatAsync(f.Property.Id, f.User.Id, new(failed.ConversationId, "retry"), default);
        Assert.Null(retryError); Assert.NotNull(retry!.Proposal);
        Assert.Equal(failed.ConversationId, retry.ConversationId);
        Assert.Contains("300k", f.Handler.Inputs.Last());
        Assert.Equal(3, await f.Db.AiUsageRecords.CountAsync(x => x.ConversationId == retry.ConversationId));
        Assert.Equal(30, await f.Db.AiUsageRecords.Where(x => x.ConversationId == retry.ConversationId).SumAsync(x => x.InputTokens));
    }

    [PricingPostgreSqlFact]
    public async Task Repair_uses_one_extra_call_and_unknown_secret_fields_never_create_proposal()
    {
        await using var f = await Fixture.Create("invalid", Proposal("ConfigureRoomRates", Weekend));
        var (chat, error) = await f.Service.ChatAsync(f.Property.Id, f.User.Id, new(null, "đổi giá"), default);
        Assert.Null(error); Assert.NotNull(chat!.Proposal); Assert.Equal(2, f.Handler.Inputs.Count);
        f.Handler.Responses.Enqueue(Proposal("UpdateRoom", new { allRooms = true, changes = new { protectedApiKey = "no" } }));
        f.Handler.Responses.Enqueue(Proposal("UpdateRoom", new { allRooms = true, changes = new { protectedApiKey = "no" } }));
        var (rejected, _) = await f.Service.ChatAsync(f.Property.Id, f.User.Id, new(chat.ConversationId, "sửa bí mật"), default);
        Assert.Null(rejected!.Proposal); Assert.True(rejected.CanRetry);
    }

    [PricingPostgreSqlFact]
    public async Task Stale_preview_and_cross_property_target_are_rejected()
    {
        await using var f = await Fixture.Create(Proposal("ConfigureRoomRates", Weekend));
        var (chat, _) = await f.Service.ChatAsync(f.Property.Id, f.User.Id, new(null, "đổi giá"), default);
        f.Rates[0].Price = 260000; await f.Db.SaveChangesAsync();
        var (_, error) = await f.Service.ApplyAsync(f.Property.Id, f.User.Id, chat!.Proposal!.Id, default);
        Assert.Contains("đã thay đổi", error);
        f.Handler.Responses.Enqueue(Proposal("ConfigureRoomRates", new { roomReferences = new[] { f.OtherRate.RoomId.ToString() }, allRates = true, changes = new { weekendPrice = 300000 } }));
        f.Handler.Responses.Enqueue(f.Handler.Responses.Peek());
        var (bad, _) = await f.Service.ChatAsync(f.Property.Id, f.User.Id, new(chat.ConversationId, "đổi cơ sở khác"), default);
        Assert.Null(bad!.Proposal);
    }

    [PricingPostgreSqlFact]
    public async Task Failed_second_batch_action_rolls_back_first_price_change()
    {
        var voucher = new { code = "EXISTS", discountPercent = 10, appliesTo = 7, startsAtUtc = DateTime.UtcNow.AddDays(-1),
            endsAtUtc = DateTime.UtcNow.AddDays(3), status = "Active" };
        await using var f = await Fixture.Create(Proposal("Batch", new { operations = new object[]
        {
            new { type = "ConfigureRoomRates", payload = Weekend },
            new { type = "CreateVoucher", payload = voucher }
        } }));
        f.Db.Vouchers.Add(new Voucher { PropertyId = f.Property.Id, Code = "EXISTS", NormalizedCode = "EXISTS",
            DiscountPercent = 10, AppliesTo = VoucherApplicability.TimeSlot, StartsAtUtc = DateTime.UtcNow, EndsAtUtc = DateTime.UtcNow.AddDays(4) });
        await f.Db.SaveChangesAsync();
        var (chat, _) = await f.Service.ChatAsync(f.Property.Id, f.User.Id, new(null, "đổi giá và tạo voucher"), default);
        Assert.NotNull(chat!.Proposal);
        Assert.NotNull((await f.Service.ApplyAsync(f.Property.Id, f.User.Id, chat.Proposal.Id, default)).Error);
        Assert.All(await f.Db.RoomRates.AsNoTracking().Where(x => x.Room.PropertyId == f.Property.Id).ToListAsync(), x => Assert.Null(x.WeekendPrice));
    }

    [PricingPostgreSqlFact]
    public async Task Full_day_capacity_and_new_rate_preserve_existing_configuration()
    {
        await using var f = await Fixture.Create(Proposal("UpdateRoom", new { allRooms = true, changes = new { fullDayPrice = 800000, capacity = 4 } }),
            Proposal("CreateRoomRate", new { allRooms = true, changes = new { name = "Khung mới", startTime = "14:00", endTime = "17:00", type = "TimeSlot", price = 220000 } }));
        var (chat, _) = await f.Service.ChatAsync(f.Property.Id, f.User.Id, new(null, "đổi combo và sức chứa"), default);
        Assert.Null((await f.Service.ApplyAsync(f.Property.Id, f.User.Id, chat!.Proposal!.Id, default)).Error);
        var rooms = await f.Db.Rooms.AsNoTracking().Where(x => x.PropertyId == f.Property.Id).ToListAsync();
        Assert.All(rooms, r => { Assert.Equal(4, r.Capacity); Assert.Equal(800000, r.FullDayPrice); Assert.True(r.FullDayPricingEnabled); });
        var (next, _) = await f.Service.ChatAsync(f.Property.Id, f.User.Id, new(chat.ConversationId, "thêm khung"), default);
        Assert.Null((await f.Service.ApplyAsync(f.Property.Id, f.User.Id, next!.Proposal!.Id, default)).Error);
        Assert.Equal(4, await f.Db.RoomRates.CountAsync(x => x.Room.PropertyId == f.Property.Id));
    }

    [PricingPostgreSqlFact]
    public async Task Voucher_special_day_and_combo_updates_are_partial_and_audited()
    {
        await using var f = await Fixture.Create();
        var voucher = new Voucher { PropertyId = f.Property.Id, Code = "VIP1", NormalizedCode = "VIP1", Status = VoucherStatus.Active,
            DiscountPercent = 10, AppliesTo = VoucherApplicability.TimeSlot, StartsAtUtc = DateTime.UtcNow, EndsAtUtc = DateTime.UtcNow.AddDays(30), TotalUsageLimit = 100 };
        var day = new SpecialPricingDay { PropertyId = f.Property.Id, Name = "Valentine", StartDate = new(2027, 2, 14),
            EndDate = new(2027, 2, 14), SurchargePercent = 10 };
        f.Db.AddRange(voucher, day); await f.Db.SaveChangesAsync();
        f.Handler.Responses.Enqueue(Proposal("Batch", new { operations = new object[]
        {
            new { type = "UpdateVoucher", payload = new { reference = "VIP1", changes = new { status = "Paused" } } },
            new { type = "UpdateSpecialPricingDay", payload = new { reference = "Valentine", changes = new { bookingMode = "FullDayOnly", surchargePercent = 15 } } },
            new { type = "UpdatePricingSettings", payload = new { changes = new { threeSlotDiscountPercent = 12 } } }
        } }));
        var (chat, _) = await f.Service.ChatAsync(f.Property.Id, f.User.Id, new(null, "tạm dừng voucher và chỉnh ngày lễ, combo"), default);
        Assert.NotNull(chat!.Proposal);
        Assert.Null((await f.Service.ApplyAsync(f.Property.Id, f.User.Id, chat.Proposal.Id, default)).Error);
        var saved = await f.Db.Vouchers.AsNoTracking().SingleAsync(x => x.Id == voucher.Id);
        Assert.Equal(VoucherStatus.Paused, saved.Status); Assert.Equal(100, saved.TotalUsageLimit); Assert.Equal(10, saved.DiscountPercent);
        var special = await f.Db.SpecialPricingDays.AsNoTracking().SingleAsync(x => x.Id == day.Id);
        Assert.Equal(SpecialDayBookingMode.FullDayOnly, special.BookingMode); Assert.Equal(15, special.SurchargePercent);
        Assert.Equal(12, (await f.Db.PropertyPricingSettings.AsNoTracking().SingleAsync(x => x.PropertyId == f.Property.Id)).ThreeSlotDiscountPercent);
        Assert.True(await f.Db.AuditLogs.AnyAsync(x => x.EntityId == chat.Proposal.Id));
    }

    [PricingPostgreSqlFact]
    public async Task Reject_expiry_and_other_user_cannot_apply_or_reuse_preview()
    {
        await using var f = await Fixture.Create(Proposal("ConfigureRoomRates", Weekend), Proposal("ConfigureRoomRates", Weekend));
        var (chat, _) = await f.Service.ChatAsync(f.Property.Id, f.User.Id, new(null, "đổi giá"), default);
        Assert.NotNull((await f.Service.ApplyAsync(f.Property.Id, Guid.NewGuid(), chat!.Proposal!.Id, default)).Error);
        Assert.Null((await f.Service.RejectAsync(f.Property.Id, f.User.Id, chat.Proposal.Id, default)).Error);
        Assert.NotNull((await f.Service.ApplyAsync(f.Property.Id, f.User.Id, chat.Proposal.Id, default)).Error);
        var (next, _) = await f.Service.ChatAsync(f.Property.Id, f.User.Id, new(chat.ConversationId, "đổi lại"), default);
        var pending = await f.Db.AiChangeProposals.SingleAsync(x => x.Id == next!.Proposal!.Id);
        pending.ExpiresAtUtc = DateTime.UtcNow.AddSeconds(-1); await f.Db.SaveChangesAsync();
        var (expired, error) = await f.Service.ApplyAsync(f.Property.Id, f.User.Id, pending.Id, default);
        Assert.NotNull(error); Assert.Equal(AiProposalStatus.Expired, expired!.Status);
        Assert.All(await f.Db.RoomRates.AsNoTracking().Where(x => x.Room.PropertyId == f.Property.Id).ToListAsync(), x => Assert.Null(x.WeekendPrice));
    }

    [PricingPostgreSqlFact]
    public async Task Site_and_seo_preview_is_partial_stale_safe_and_preserves_custom_code()
    {
        await using var f = await Fixture.Create(Proposal("UpdateSiteSettings", new
        {
            changes = new { metaTitle = "De Long Homestay - Đặt phòng", metaDescription = "Đặt phòng trực tuyến.", robotsIndex = true }
        }));
        f.Db.Set<PropertySiteSettings>().Add(new PropertySiteSettings
        {
            PropertyId = f.Property.Id, SiteName = "Tên cũ", CustomCss = ".safe{color:red}", CustomJs = "window.safe=true"
        });
        await f.Db.SaveChangesAsync();

        var (chat, chatError) = await f.Service.ChatAsync(f.Property.Id, f.User.Id, new(null, "cập nhật SEO"), default);
        Assert.Null(chatError); Assert.NotNull(chat!.Proposal);
        Assert.Null((await f.Service.ApplyAsync(f.Property.Id, f.User.Id, chat.Proposal.Id, default)).Error);

        var saved = await f.Db.Set<PropertySiteSettings>().AsNoTracking().SingleAsync(x => x.PropertyId == f.Property.Id);
        Assert.Equal("Tên cũ", saved.SiteName);
        Assert.Equal("De Long Homestay - Đặt phòng", saved.MetaTitle);
        Assert.Equal(".safe{color:red}", saved.CustomCss);
        Assert.Equal("window.safe=true", saved.CustomJs);
    }

    private sealed class QueueHandler(params string[] responses) : HttpMessageHandler
    {
        public Queue<string> Responses { get; } = new(responses);
        public List<string> Inputs { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = await request.Content!.ReadAsStringAsync(ct);
            using var document = JsonDocument.Parse(body);
            Inputs.Add(document.RootElement.GetProperty("input").GetString()!);
            var text = Responses.Count > 0 ? Responses.Dequeue() : "invalid";
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new
            {
                status = "completed", output = new[] { new { content = new[] { new { type = "output_text", text } } } },
                usage = new { input_tokens = 10, output_tokens = 5 }
            })) };
        }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public required AppDbContext Db { get; init; }
        public required AdminAiService Service { get; init; }
        public required Property Property { get; init; }
        public required ApplicationUser User { get; init; }
        public required RoomRate[] Rates { get; init; }
        public required RoomRate OtherRate { get; init; }
        public required QueueHandler Handler { get; init; }
        public required HttpClient Http { get; init; }
        public static async Task<Fixture> Create(params string[] responses)
        {
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION")).Options);
            await db.Database.MigrateAsync();
            var suffix = Guid.NewGuid().ToString("N")[..10];
            var property = new Property { Code = "AI-" + suffix, Name = "AI test", TimeZoneId = "Asia/Ho_Chi_Minh" };
            var other = new Property { Code = "OTHER-" + suffix, Name = "Other" };
            var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = suffix, DisplayName = "AI tester" };
            var rooms = new[] { new Room { Property = property, Code = "A", Name = "Phòng A" }, new Room { Property = property, Code = "B", Name = "Phòng B" } };
            var rates = rooms.Select(r => new RoomRate { Room = r, Name = "Qua đêm", StartTime = new(21, 0), EndTime = new(9, 0), Type = RoomRateType.Overnight, Price = 250000 }).ToArray();
            var otherRate = new RoomRate { Room = new Room { Property = other, Code = "C", Name = "Phòng C" }, Name = "Qua đêm", Price = 400000 };
            var protector = new AiCredentialProtector(new EphemeralDataProtectionProvider());
            db.AddRange(property, other, user, otherRate); db.AddRange(rates);
            db.PropertyAiProfiles.Add(new PropertyAiProfile { Property = property, Provider = AiProviderKind.OpenAi, Model = "test-model",
                IsEnabled = true, ProtectedApiKey = protector.Protect("test-only") });
            await db.SaveChangesAsync();
            var audit = new AuditService(db);
            var pricing = new PricingService(db, audit);
            var handler = new QueueHandler(responses); var http = new HttpClient(handler);
            var service = new AdminAiService(db, new AdminAiSettingsService(db, protector), new AiProviderClient(http),
                new RoomService(db), new RoomRateService(db), new RoomContentService(db, null!), new SiteContentService(db), pricing,
                new VoucherService(db, audit, new StoragePaths(Path.GetTempPath(), Path.GetTempPath(), new PathString("/test"), false, false, false), new ConfigurationBuilder().Build()), audit);
            return new() { Db = db, Service = service, Property = property, User = user, Rates = rates, OtherRate = otherRate, Handler = handler, Http = http };
        }
        public async ValueTask DisposeAsync() { Http.Dispose(); await Db.DisposeAsync(); }
    }
}
