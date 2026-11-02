using System.Security.Claims;
using DeLong.Web.Common.Auditing;
using DeLong.Web.Common.Security;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Bookings;
using DeLong.Web.Features.Cancellations;
using DeLong.Web.Features.Customers;
using DeLong.Web.Features.Notifications;
using DeLong.Web.Features.Payments;
using DeLong.Web.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeLong.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class BookingCancellationRefundTests
{
    private static AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION")!).Options);

    private sealed record Fixture(Property Property, Room Room, ApplicationUser Staff);

    private static async Task<Fixture> SeedAsync(AppDbContext db, bool notifications = false)
    {
        await db.Database.MigrateAsync();
        var key = Guid.NewGuid().ToString("N")[..10];
        var property = new Property { Code = key, Name = "Cancel " + key, SiteSlug = key, PublicMinimumRemainingMinutes = 0, IsActive = true, TimeZoneId = "Asia/Ho_Chi_Minh" };
        var room = new Room { Property = property, Code = key, Name = "Blue", Slug = key, IsActive = true, IsPublished = true };
        room.Rates.Add(new RoomRate { Name = "Afternoon", StartTime = new(14, 0), EndTime = new(17, 0), Price = 250000, Type = RoomRateType.TimeSlot, IsActive = true });
        var staff = new ApplicationUser { Id = Guid.NewGuid(), UserName = "staff-" + key, DisplayName = "Lan lễ tân" };
        db.AddRange(property, room, staff);
        if (notifications)
        {
            db.Add(new PropertyNotificationSettings
            {
                Property = property, EmailBookingEnabled = true, EmailRecipients = "owner@example.test", ManagerCancellationEmailEnabled = true,
                TelegramBookingEnabled = true, TelegramBotTokenProtected = "protected", TelegramChatIds = "12345"
            });
        }
        await db.SaveChangesAsync();
        return new(property, room, staff);
    }

    private static BookingService Bookings(AppDbContext db, bool notifications = false)
    {
        var audit = new AuditService(db);
        var notification = notifications ? new BookingNotificationService(db, new NotificationRealtimeBroker(), NullLogger<BookingNotificationService>.Instance) : null;
        return new BookingService(db, new CustomerService(db), audit, notificationService: notification);
    }

    private static CancellationService Cancellations(AppDbContext db) => new(db, new PaymentService(db, auditService: new AuditService(db)), new AuditService(db));

    private static async Task<Guid> CreateBookingAsync(AppDbContext db, Fixture f, decimal paid, int dayOffset = 20, string phone = "0935527193", string name = "Khách thử")
    {
        var start = new DateTimeOffset(DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)).AddDays(dayOffset).ToDateTime(new TimeOnly(7, 0)), TimeSpan.Zero);
        var created = await Bookings(db).CreateAsync(f.Property.Id, new CreateBookingRequest
        {
            RoomId = f.Room.Id, CustomerName = name, CustomerPhone = phone, CheckIn = start, CheckOut = start.AddHours(3),
            RoomAmount = 500000, Status = BookingStatus.Confirmed
        });
        Assert.Null(created.Error);
        if (paid > 0)
        {
            var (_, error) = await new PaymentService(db, auditService: new AuditService(db)).AddAsync(f.Property.Id, created.Booking!.Id,
                new CreatePaymentRequest { Amount = paid, Method = PaymentMethod.BankTransfer }, f.Staff.Id);
            Assert.Null(error);
        }
        return created.Booking!.Id;
    }

    private static CancelDetails Cancel(string? reason, string? mode = null, decimal? amount = null, PaymentMethod? method = null) =>
        new(reason, mode is null ? null : new CancelRefundOption(mode, amount, method));

    private static CancellationScope Scope(Property property, params Property[] more)
    {
        var all = new[] { property }.Concat(more).Select(x => new CurrentPropertyDto(x.Id, x.Code, x.Name, x.TimeZoneId)).ToList();
        return new CancellationScope(all[0], all, all, all.Count > 1);
    }

    private static CancellationFilter Today(string? search = null, string? status = null) => new()
    {
        From = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)).AddDays(-1),
        To = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)).AddDays(1),
        Search = search, Status = status
    };

    [RoomLockPostgresFact]
    public async Task Cancelling_requires_a_reason_and_a_refund_choice_when_the_guest_already_paid()
    {
        await using var db = Db();
        var f = await SeedAsync(db);
        var id = await CreateBookingAsync(db, f, paid: 300000);
        var service = Bookings(db);

        Assert.Equal("reason_required", (await service.ChangeStatusAsync(f.Property.Id, id, BookingStatus.Cancelled, f.Staff.Id)).Error!.Code);
        Assert.Equal("reason_required", (await service.ChangeStatusAsync(f.Property.Id, id, BookingStatus.Cancelled, f.Staff.Id, cancel: Cancel("   "))).Error!.Code);
        Assert.Equal("refund_choice_required", (await service.ChangeStatusAsync(f.Property.Id, id, BookingStatus.Cancelled, f.Staff.Id, cancel: Cancel("Khách báo bận"))).Error!.Code);
        Assert.Equal("refund_exceeds_paid", (await service.ChangeStatusAsync(f.Property.Id, id, BookingStatus.Cancelled, f.Staff.Id, cancel: Cancel("Khách báo bận", "later", 300001))).Error!.Code);

        db.ChangeTracker.Clear();
        Assert.Equal(BookingStatus.Confirmed, await db.Bookings.Where(x => x.Id == id).Select(x => x.Status).SingleAsync());
        Assert.Empty(await db.BookingCancellations.Where(x => x.BookingId == id).ToListAsync());
    }

    [RoomLockPostgresFact]
    public async Task Unpaid_booking_cancels_with_a_record_that_needs_no_refund()
    {
        await using var db = Db();
        var f = await SeedAsync(db);
        var id = await CreateBookingAsync(db, f, paid: 0);

        Assert.Null((await Bookings(db).ChangeStatusAsync(f.Property.Id, id, BookingStatus.Cancelled, f.Staff.Id, cancel: Cancel("Khách đổi ý"))).Error);

        var (list, error) = await Cancellations(db).ListAsync(Scope(f.Property), Today(), false, default);
        Assert.Null(error);
        var row = Assert.Single(list!.Rows);
        Assert.Equal("NotRequired", row.RefundStatus);
        Assert.Equal("Lan lễ tân", row.CancelledByName);
        Assert.Equal("Khách đổi ý", row.Reason);
        Assert.Equal(0, row.PaidAmount);
    }

    [RoomLockPostgresFact]
    public async Task Refund_now_records_the_payment_with_the_cancellation_and_nets_the_booking_to_zero()
    {
        await using var db = Db();
        var f = await SeedAsync(db);
        var id = await CreateBookingAsync(db, f, paid: 300000);

        Assert.Null((await Bookings(db).ChangeStatusAsync(f.Property.Id, id, BookingStatus.Cancelled, f.Staff.Id,
            cancel: Cancel("Khách báo bận", "now", 300000, PaymentMethod.Cash))).Error);

        db.ChangeTracker.Clear();
        var refund = await db.Payments.SingleAsync(x => x.BookingId == id && x.Type == PaymentType.Refund);
        Assert.Equal(300000, refund.Amount);
        Assert.Equal(PaymentMethod.Cash, refund.Method);
        Assert.Equal(f.Staff.Id, refund.RecordedByUserId);

        var row = Assert.Single((await Cancellations(db).ListAsync(Scope(f.Property), Today(), false, default)).Result!.Rows);
        Assert.Equal("Refunded", row.RefundStatus);
        Assert.Equal(300000, row.PaidAmount);
        Assert.Equal(300000, row.RefundedAmount);
        Assert.Equal(0, row.RemainingRefundAmount);
        Assert.Equal(0, row.KeptAmount);
        Assert.Equal(refund.CreatedAtUtc, row.LastRefundEnteredAtUtc);
        Assert.Equal("Lan lễ tân", row.LastRefundByName);
    }

    [RoomLockPostgresFact]
    public async Task Refund_later_stays_pending_until_refunds_are_recorded_and_the_amount_can_be_edited()
    {
        await using var db = Db();
        var f = await SeedAsync(db);
        var id = await CreateBookingAsync(db, f, paid: 400000);
        Assert.Null((await Bookings(db).ChangeStatusAsync(f.Property.Id, id, BookingStatus.Cancelled, f.Staff.Id, cancel: Cancel("Khách báo bận", "later", 400000))).Error);
        var service = Cancellations(db);
        var row = Assert.Single((await service.ListAsync(Scope(f.Property), Today(), false, default)).Result!.Rows);
        Assert.Equal("Pending", row.RefundStatus);
        Assert.Equal(400000, row.RemainingRefundAmount);

        Assert.Equal("refund_exceeds_paid", (await service.AddRefundAsync(f.Property.Id, row.Id, new RecordCancellationRefundRequest { Amount = 400001 }, f.Staff.Id, default))!.Code);
        Assert.Null(await service.AddRefundAsync(f.Property.Id, row.Id, new RecordCancellationRefundRequest { Amount = 150000, Method = PaymentMethod.Cash }, f.Staff.Id, default));
        row = Assert.Single((await service.ListAsync(Scope(f.Property), Today(), false, default)).Result!.Rows);
        Assert.Equal("Partial", row.RefundStatus);
        Assert.Equal(250000, row.RemainingRefundAmount);

        // Sửa số tiền của khoản đã nhập: 150k -> 400k thì đã hoàn đủ.
        var refundId = Assert.Single(row.Refunds).PaymentId;
        Assert.Equal("refund_exceeds_paid", (await service.EditRefundAsync(f.Property.Id, row.Id, refundId, new EditCancellationRefundRequest { Amount = 400001 }, f.Staff.Id, default))!.Code);
        Assert.Null(await service.EditRefundAsync(f.Property.Id, row.Id, refundId, new EditCancellationRefundRequest { Amount = 400000, Method = PaymentMethod.BankTransfer, Note = "Sửa số tiền" }, f.Staff.Id, default));
        row = Assert.Single((await service.ListAsync(Scope(f.Property), Today(), false, default)).Result!.Rows);
        Assert.Equal("Refunded", row.RefundStatus);
        Assert.Equal(400000, row.RefundedAmount);

        db.ChangeTracker.Clear();
        Assert.True(await db.AuditLogs.AnyAsync(x => x.EntityId == id && x.Action == "PaymentRefundEdited"));
        Assert.Equal(0, await new PaymentService(db).GetNetPaidAsync(f.Property.Id, id));
    }

    [RoomLockPostgresFact]
    public async Task Due_amount_can_be_lowered_to_keep_a_deposit_and_voiding_a_refund_reopens_it()
    {
        await using var db = Db();
        var f = await SeedAsync(db);
        var id = await CreateBookingAsync(db, f, paid: 500000);
        Assert.Null((await Bookings(db).ChangeStatusAsync(f.Property.Id, id, BookingStatus.Cancelled, f.Staff.Id, cancel: Cancel("Khách báo bận", "later", 500000))).Error);
        var service = Cancellations(db);
        var row = Assert.Single((await service.ListAsync(Scope(f.Property), Today(), false, default)).Result!.Rows);

        Assert.Equal("refund_exceeds_paid", (await service.UpdateDueAsync(f.Property.Id, row.Id, new UpdateRefundDueRequest { Amount = 500001 }, f.Staff.Id, default))!.Code);
        Assert.Equal("validation", (await service.UpdateDueAsync(f.Property.Id, row.Id, new UpdateRefundDueRequest { Amount = -1 }, f.Staff.Id, default))!.Code);
        Assert.Null(await service.UpdateDueAsync(f.Property.Id, row.Id, new UpdateRefundDueRequest { Amount = 300000, Note = "Giữ 200k phí hủy" }, f.Staff.Id, default));
        row = Assert.Single((await service.ListAsync(Scope(f.Property), Today(), false, default)).Result!.Rows);
        Assert.Equal(300000, row.RemainingRefundAmount);
        Assert.Equal(200000, row.KeptAmount);

        Assert.Null(await service.AddRefundAsync(f.Property.Id, row.Id, new RecordCancellationRefundRequest { Amount = 300000 }, f.Staff.Id, default));
        row = Assert.Single((await service.ListAsync(Scope(f.Property), Today(), false, default)).Result!.Rows);
        Assert.Equal("Refunded", row.RefundStatus);
        Assert.Equal(200000, row.KeptAmount);

        var voided = await new PaymentService(db, auditService: new AuditService(db)).VoidAsync(f.Property.Id, row.Refunds[0].PaymentId, "Nhập nhầm", f.Staff.Id);
        Assert.Null(voided.Error);
        row = Assert.Single((await service.ListAsync(Scope(f.Property), Today(), false, default)).Result!.Rows);
        Assert.Equal("Pending", row.RefundStatus);
        Assert.Equal(300000, row.RemainingRefundAmount);

        Assert.Null(await service.UpdateDueAsync(f.Property.Id, row.Id, new UpdateRefundDueRequest { Amount = 0 }, f.Staff.Id, default));
        row = Assert.Single((await service.ListAsync(Scope(f.Property), Today(), false, default)).Result!.Rows);
        Assert.Equal("Kept", row.RefundStatus);
        Assert.Equal(500000, row.KeptAmount);
    }

    [RoomLockPostgresFact]
    public async Task Keeping_the_deposit_creates_no_refund_and_is_counted_as_kept()
    {
        await using var db = Db();
        var f = await SeedAsync(db);
        var id = await CreateBookingAsync(db, f, paid: 200000);
        Assert.Null((await Bookings(db).ChangeStatusAsync(f.Property.Id, id, BookingStatus.Cancelled, f.Staff.Id, cancel: Cancel("Hủy sát giờ", "keep"))).Error);

        db.ChangeTracker.Clear();
        Assert.Empty(await db.Payments.Where(x => x.BookingId == id && x.Type == PaymentType.Refund).ToListAsync());
        var result = (await Cancellations(db).ListAsync(Scope(f.Property), Today(), false, default)).Result!;
        var row = Assert.Single(result.Rows);
        Assert.Equal("Kept", row.RefundStatus);
        Assert.Equal(200000, result.Totals.KeptAmount);
        Assert.Equal(0, result.Totals.RemainingRefundAmount);
    }

    [RoomLockPostgresFact]
    public async Task Restoring_a_booking_removes_it_from_the_list_and_a_second_cancel_creates_a_new_record()
    {
        await using var db = Db();
        var f = await SeedAsync(db);
        var id = await CreateBookingAsync(db, f, paid: 0);
        var service = Bookings(db);
        Assert.Null((await service.ChangeStatusAsync(f.Property.Id, id, BookingStatus.Cancelled, f.Staff.Id, cancel: Cancel("Lần một"))).Error);
        Assert.Null((await service.ChangeStatusAsync(f.Property.Id, id, BookingStatus.Requested, f.Staff.Id)).Error);
        Assert.Empty((await Cancellations(db).ListAsync(Scope(f.Property), Today(), false, default)).Result!.Rows);

        Assert.Null((await service.ChangeStatusAsync(f.Property.Id, id, BookingStatus.Confirmed, f.Staff.Id)).Error);
        Assert.Null((await service.ChangeStatusAsync(f.Property.Id, id, BookingStatus.Cancelled, f.Staff.Id, cancel: Cancel("Lần hai"))).Error);
        var row = Assert.Single((await Cancellations(db).ListAsync(Scope(f.Property), Today(), false, default)).Result!.Rows);
        Assert.Equal("Lần hai", row.Reason);
        db.ChangeTracker.Clear();
        Assert.Equal(2, await db.BookingCancellations.CountAsync(x => x.BookingId == id));
    }

    [RoomLockPostgresFact]
    public async Task List_filters_by_search_status_and_staff_and_sums_the_totals_and_never_leaks_other_branches()
    {
        await using var db = Db();
        var f = await SeedAsync(db);
        var other = await SeedAsync(db);
        var a = await CreateBookingAsync(db, f, paid: 300000, dayOffset: 21, phone: "0911111111", name: "Nguyễn Văn A");
        var b = await CreateBookingAsync(db, f, paid: 0, dayOffset: 22, phone: "0922222222", name: "Trần Thị B");
        var foreign = await CreateBookingAsync(db, other, paid: 100000, dayOffset: 21, phone: "0933333333", name: "Khách chi nhánh khác");
        Assert.Null((await Bookings(db).ChangeStatusAsync(f.Property.Id, a, BookingStatus.Cancelled, f.Staff.Id, cancel: Cancel("A hủy", "later", 300000))).Error);
        Assert.Null((await Bookings(db).ChangeStatusAsync(f.Property.Id, b, BookingStatus.Cancelled, f.Staff.Id, cancel: Cancel("B hủy"))).Error);
        Assert.Null((await Bookings(db).ChangeStatusAsync(other.Property.Id, foreign, BookingStatus.Cancelled, other.Staff.Id, cancel: Cancel("Chi nhánh khác", "keep"))).Error);
        var service = Cancellations(db);

        var all = (await service.ListAsync(Scope(f.Property), Today(), false, default)).Result!;
        Assert.Equal(2, all.Totals.Count);
        Assert.Equal(1_000_000, all.Totals.TotalAmount);
        Assert.Equal(300000, all.Totals.PaidAmount);
        Assert.Equal(300000, all.Totals.RemainingRefundAmount);
        Assert.Equal(1, all.Totals.PendingCount);
        Assert.DoesNotContain(all.Rows, x => x.CustomerName.Contains("chi nhánh khác"));

        Assert.Equal("Nguyễn Văn A", Assert.Single((await service.ListAsync(Scope(f.Property), Today(search: "0911111"), false, default)).Result!.Rows).CustomerName);
        Assert.Single((await service.ListAsync(Scope(f.Property), Today(search: "trần"), false, default)).Result!.Rows);
        Assert.Empty((await service.ListAsync(Scope(f.Property), Today(search: "%"), false, default)).Result!.Rows);
        Assert.Equal("Nguyễn Văn A", Assert.Single((await service.ListAsync(Scope(f.Property), Today(status: "Open"), false, default)).Result!.Rows).CustomerName);
        Assert.Equal("Trần Thị B", Assert.Single((await service.ListAsync(Scope(f.Property), Today(status: "NotRequired"), false, default)).Result!.Rows).CustomerName);
        Assert.Equal(2, (await service.ListAsync(Scope(f.Property), new CancellationFilter { From = Today().From, To = Today().To, ActorUserId = f.Staff.Id }, false, default)).Result!.Rows.Count);
        Assert.Empty((await service.ListAsync(Scope(f.Property), new CancellationFilter { From = Today().From, To = Today().To, ActorUserId = other.Staff.Id }, false, default)).Result!.Rows);

        // Lọc ngày: khoảng ngày trong quá khứ không có hủy nào.
        var past = new CancellationFilter { From = Today().From.AddDays(-30), To = Today().From.AddDays(-20) };
        Assert.Empty((await service.ListAsync(Scope(f.Property), past, false, default)).Result!.Rows);
        // Lọc theo ngày nhận phòng.
        var checkin = new CancellationFilter { From = Today().To.AddDays(20), To = Today().To.AddDays(20), DateField = "checkin" };
        Assert.Single((await service.ListAsync(Scope(f.Property), checkin, false, default)).Result!.Rows);

        var both = (await service.ListAsync(Scope(f.Property, other.Property), Today(), false, default)).Result!;
        Assert.Equal(3, both.Totals.Count);
    }

    [RoomLockPostgresFact]
    public async Task Mutations_are_rejected_for_a_cancellation_that_belongs_to_another_branch()
    {
        await using var db = Db();
        var f = await SeedAsync(db);
        var other = await SeedAsync(db);
        var id = await CreateBookingAsync(db, f, paid: 100000);
        Assert.Null((await Bookings(db).ChangeStatusAsync(f.Property.Id, id, BookingStatus.Cancelled, f.Staff.Id, cancel: Cancel("Hủy", "later", 100000))).Error);
        var service = Cancellations(db);
        var row = Assert.Single((await service.ListAsync(Scope(f.Property), Today(), false, default)).Result!.Rows);

        Assert.Equal("not_found", (await service.AddRefundAsync(other.Property.Id, row.Id, new RecordCancellationRefundRequest { Amount = 1000 }, other.Staff.Id, default))!.Code);
        Assert.Equal("not_found", (await service.UpdateDueAsync(other.Property.Id, row.Id, new UpdateRefundDueRequest { Amount = 0 }, other.Staff.Id, default))!.Code);
        Assert.Equal("not_found", (await service.EditRefundAsync(other.Property.Id, row.Id, Guid.NewGuid(), new EditCancellationRefundRequest { Amount = 1000 }, other.Staff.Id, default))!.Code);
    }

    [RoomLockPostgresFact]
    public async Task Scope_resolution_only_exposes_branches_the_user_has_access_to()
    {
        await using var db = Db();
        var f = await SeedAsync(db);
        var other = await SeedAsync(db);
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "single-" + Guid.NewGuid().ToString("N")[..8] };
        db.Add(user);
        db.Add(new UserPropertyAccess { UserId = user.Id, PropertyId = f.Property.Id });
        await db.SaveChangesAsync();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())], "test"));
        var properties = new CurrentPropertyService(db, new HttpContextAccessor());
        var service = Cancellations(db);

        var own = await service.ResolveScopeAsync(principal, properties, f.Property.Id, "all", default);
        Assert.NotNull(own);
        Assert.False(own!.IsAll);
        Assert.Equal([f.Property.Id], own.Selected.Select(x => x.Id));
        Assert.Null(await service.ResolveScopeAsync(principal, properties, other.Property.Id, null, default));
        Assert.Null(await service.ResolveScopeAsync(principal, properties, f.Property.Id, other.Property.Id.ToString(), default));

        db.Add(new UserPropertyAccess { UserId = user.Id, PropertyId = other.Property.Id });
        await db.SaveChangesAsync();
        var multi = await service.ResolveScopeAsync(principal, properties, f.Property.Id, "all", default);
        Assert.True(multi!.IsAll);
        Assert.Equal(2, multi.Selected.Count);
    }

    [RoomLockPostgresFact]
    public async Task Legacy_cancelled_bookings_are_backfilled_from_the_audit_trail_but_auto_cancelled_ones_are_not()
    {
        await using var db = Db();
        var f = await SeedAsync(db);
        var manual = await CreateBookingAsync(db, f, paid: 250000, dayOffset: 25, phone: "0944444444", name: "Hủy cũ");
        var auto = await CreateBookingAsync(db, f, paid: 0, dayOffset: 26, phone: "0955555555", name: "Hết hạn thanh toán");
        // Mô phỏng dữ liệu trước khi có hồ sơ hủy: đổi trạng thái trực tiếp + dòng audit StatusChanged do nhân viên.
        foreach (var id in new[] { manual, auto })
            await db.Bookings.Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, BookingStatus.Cancelled));
        new AuditService(db).Add(f.Property.Id, "Booking", manual, "StatusChanged", f.Staff.Id,
            new { status = "Confirmed" }, new { status = "Cancelled" });
        new AuditService(db).Add(f.Property.Id, "Booking", auto, "PaymentExpired", null, new { status = "Held" }, new { status = "Cancelled" });
        await db.SaveChangesAsync();

        var result = (await Cancellations(db).ListAsync(Scope(f.Property), Today(), false, default)).Result!;
        var row = Assert.Single(result.Rows);
        Assert.Equal("Hủy cũ", row.CustomerName);
        Assert.True(row.IsBackfilled);
        Assert.Equal("Lan lễ tân", row.CancelledByName);
        Assert.Equal("Pending", row.RefundStatus);
        Assert.Equal(250000, row.RemainingRefundAmount);
    }

    [RoomLockPostgresFact]
    public async Task Cancelling_queues_a_manager_email_and_a_detailed_telegram_message()
    {
        await using var db = Db();
        var f = await SeedAsync(db, notifications: true);
        var id = await CreateBookingAsync(db, f, paid: 300000);
        Assert.Null((await Bookings(db, notifications: true).ChangeStatusAsync(f.Property.Id, id, BookingStatus.Cancelled, f.Staff.Id,
            cancel: Cancel("Khách báo bận", "later", 300000))).Error);

        db.ChangeTracker.Clear();
        var notification = await db.PropertyNotifications.SingleAsync(x => x.BookingId == id && x.Type.StartsWith("booking-cancelled"));
        var email = await db.NotificationEmailOutbox.SingleAsync(x => x.NotificationId == notification.Id);
        Assert.Equal("owner@example.test", email.ToRecipients);
        Assert.Contains("Lan lễ tân", email.BodyText);
        Assert.Contains("Khách báo bận", email.BodyText);
        Assert.Contains("Chờ hoàn", email.BodyText);
        var telegram = await db.NotificationTelegramOutbox.SingleAsync(x => x.NotificationId == notification.Id);
        Assert.Contains("Người hủy: Lan lễ tân", telegram.MessageText);
        Assert.Contains("Lý do: Khách báo bận", telegram.MessageText);
        Assert.Contains("Đã thanh toán", telegram.MessageText);

        // Hủy → khôi phục → hủy lại vẫn báo lại, vì mỗi lần hủy là một hồ sơ riêng.
        var bookings = Bookings(db, notifications: true);
        Assert.Null((await bookings.ChangeStatusAsync(f.Property.Id, id, BookingStatus.Requested, f.Staff.Id)).Error);
        Assert.Null((await bookings.ChangeStatusAsync(f.Property.Id, id, BookingStatus.Confirmed, f.Staff.Id)).Error);
        Assert.Null((await bookings.ChangeStatusAsync(f.Property.Id, id, BookingStatus.Cancelled, f.Staff.Id, cancel: Cancel("Hủy lần hai", "keep"))).Error);
        Assert.Equal(2, await db.PropertyNotifications.CountAsync(x => x.BookingId == id && x.Type.StartsWith("booking-cancelled")));
    }

    [RoomLockPostgresFact]
    public async Task Manager_email_respects_the_cancellation_email_switch()
    {
        await using var db = Db();
        var f = await SeedAsync(db, notifications: true);
        await db.PropertyNotificationSettings.Where(x => x.PropertyId == f.Property.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ManagerCancellationEmailEnabled, false));
        var id = await CreateBookingAsync(db, f, paid: 0);
        Assert.Null((await Bookings(db, notifications: true).ChangeStatusAsync(f.Property.Id, id, BookingStatus.Cancelled, f.Staff.Id, cancel: Cancel("Khách báo bận"))).Error);

        db.ChangeTracker.Clear();
        Assert.Empty(await db.NotificationEmailOutbox.Where(x => x.PropertyId == f.Property.Id).ToListAsync());
        Assert.Single(await db.NotificationTelegramOutbox.Where(x => x.PropertyId == f.Property.Id).ToListAsync());
    }

    [RoomLockPostgresFact]
    public async Task Recording_a_refund_on_a_booking_sends_a_refund_telegram_message()
    {
        await using var db = Db();
        var f = await SeedAsync(db, notifications: true);
        var id = await CreateBookingAsync(db, f, paid: 300000);
        var notification = new BookingNotificationService(db, new NotificationRealtimeBroker(), NullLogger<BookingNotificationService>.Instance);
        var payments = new PaymentService(db, notification, NullLogger<PaymentService>.Instance, new AuditService(db));
        var (_, error) = await payments.AddAsync(f.Property.Id, id, new CreatePaymentRequest { Type = PaymentType.Refund, Amount = 100000, Method = PaymentMethod.Cash }, f.Staff.Id);
        Assert.Null(error);

        db.ChangeTracker.Clear();
        var message = await db.PropertyNotifications.SingleAsync(x => x.BookingId == id && x.Type.StartsWith("booking-refunded"));
        var telegram = await db.NotificationTelegramOutbox.SingleAsync(x => x.NotificationId == message.Id);
        Assert.Contains("ĐÃ GHI NHẬN HOÀN TIỀN", telegram.MessageText);
        Assert.Contains("Lan lễ tân", telegram.MessageText);
    }

    [RoomLockPostgresFact]
    public async Task Finance_and_report_metrics_separate_money_still_owed_from_money_kept()
    {
        await using var db = Db();
        var f = await SeedAsync(db);
        var owed = await CreateBookingAsync(db, f, paid: 300000, dayOffset: 21, phone: "0966666666");
        var kept = await CreateBookingAsync(db, f, paid: 200000, dayOffset: 22, phone: "0977777777");
        Assert.Null((await Bookings(db).ChangeStatusAsync(f.Property.Id, owed, BookingStatus.Cancelled, f.Staff.Id, cancel: Cancel("Hủy", "later", 300000))).Error);
        Assert.Null((await Bookings(db).ChangeStatusAsync(f.Property.Id, kept, BookingStatus.Cancelled, f.Staff.Id, cancel: Cancel("Hủy", "keep"))).Error);

        var from = DateTime.UtcNow.AddDays(-1);
        var to = DateTime.UtcNow.AddDays(1);
        var metrics = await CancellationMetrics.GetAsync(db, f.Property.Id, from, to, default);
        Assert.Equal(300000, metrics.RefundsOwed);
        Assert.Equal(1, metrics.OwedBookingCount);
        Assert.Equal(200000, metrics.KeptInPeriod);
        Assert.Equal(0, (await CancellationMetrics.GetAsync(db, f.Property.Id, from.AddDays(-10), from.AddDays(-5), default)).KeptInPeriod);

        var finance = await new DeLong.Web.Features.Finance.FinanceService(db).GetSnapshotAsync(f.Property.Id, from, to);
        Assert.Equal(300000, finance.Summary.RefundsOwed);
        Assert.Equal(200000, finance.Summary.CancellationKept);
        // Dòng tiền vẫn theo ngày giao dịch: cả 500k đã thu, chưa hoàn khoản nào.
        Assert.Equal(500000, finance.Summary.NetReceipts);
    }
}

public sealed class CancellationRefundStatusTests
{
    [Theory]
    [InlineData(0, 0, 0, "NotRequired")]
    [InlineData(300000, 300000, 0, "Pending")]
    [InlineData(300000, 300000, 100000, "Partial")]
    [InlineData(300000, 300000, 300000, "Refunded")]
    [InlineData(300000, 200000, 250000, "Refunded")]
    [InlineData(300000, 0, 0, "Kept")]
    public void Status_is_derived_from_paid_due_and_refunded(double paid, double due, double refunded, string expected) =>
        Assert.Equal(expected, CancellationRefundStatuses.Resolve((decimal)paid, (decimal)due, (decimal)refunded));
}
