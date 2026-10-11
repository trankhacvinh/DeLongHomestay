using DeLong.Web.Common.Auditing;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.DataQuality;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeLong.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class DataQualityTests
{
    private static AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION")!).Options);
    private static async Task<(Property Property, Booking Booking)> Seed(AppDbContext db)
    {
        await db.Database.MigrateAsync();
        var key = Guid.NewGuid().ToString("N");
        var p = new Property { Code = key, Name = "Quality test" };
        var room = new Room { Property = p, Code = "Blue", Name = "Blue" };
        var guest = new Customer { Property = p, Name = "Guest", Phone = "0987654321", NormalizedPhone = "0987654321" };
        var b = new Booking { Property = p, Room = room, Customer = guest, Code = key[..20], Status = BookingStatus.Completed,
            CheckInUtc = DateTime.UtcNow.AddDays(-2), CheckOutUtc = DateTime.UtcNow.AddDays(-1), RoomAmount = 250000 };
        db.Add(b); await db.SaveChangesAsync(); return (p, b);
    }

    [RoomLockPostgresFact]
    public async Task Scan_deduplicates_alerts_review_is_audited_and_changed_money_reopens_accepted_issue()
    {
        await using var db = Db(); var (p, b) = await Seed(db);
        var service = new DataQualityService(db, new AuditService(db));
        Assert.Equal(1, await service.ScanAsync(p.Id, default));
        Assert.Equal(1, await service.ScanAsync(p.Id, default));
        var issue = await db.DataQualityIssues.SingleAsync(x => x.PropertyId == p.Id);
        Assert.Equal(1, await db.PropertyNotifications.CountAsync(x => x.PropertyId == p.Id));
        var notifications = new DeLong.Web.Features.Notifications.BookingNotificationService(db,
            new DeLong.Web.Features.Notifications.NotificationRealtimeBroker(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DeLong.Web.Features.Notifications.BookingNotificationService>.Instance);
        Assert.Empty((await notifications.GetFeedAsync(p.Id, Guid.NewGuid())).Items);
        Assert.Single((await notifications.GetFeedAsync(p.Id, Guid.NewGuid(), includeDataQuality: true)).Items);
        Assert.NotNull(await service.ReviewAsync(p.Id, issue.Id, new("Resolved", "Done"), Guid.NewGuid(), default));
        Assert.Null(await service.ReviewAsync(p.Id, issue.Id, new("Accepted", "Cash checked separately"), Guid.NewGuid(), default));
        await service.ScanAsync(p.Id, default);
        Assert.Equal("Accepted", issue.Status);
        var payment = new Payment { PropertyId = p.Id, BookingId = b.Id, Amount = 100000, Type = PaymentType.Receipt };
        db.Add(payment); await db.SaveChangesAsync();
        await service.ScanAsync(p.Id, default);
        Assert.Equal("Open", issue.Status); Assert.Null(issue.ReviewNote);
        payment.Amount = 250000; await db.SaveChangesAsync();
        Assert.Equal(0, await service.ScanAsync(p.Id, default)); Assert.False(issue.IsDetected);
        Assert.Null(await service.ReviewAsync(p.Id, issue.Id, new("Resolved", "Receipt reconciled"), Guid.NewGuid(), default));
        Assert.NotEmpty(await new AuditService(db).GetEntityHistoryAsync(p.Id, "DataQualityIssue", issue.Id));
        Assert.Equal(BookingStatus.Completed, (await db.Bookings.SingleAsync(x => x.Id == b.Id)).Status);
        Assert.Single(await db.Payments.Where(x => x.BookingId == b.Id).ToListAsync());
        var other = new Property { Code = Guid.NewGuid().ToString("N"), Name = "Other" };
        db.Add(other); await db.SaveChangesAsync();
        Assert.NotNull(await service.ReviewAsync(other.Id, issue.Id, new("Accepted", "Wrong branch"), Guid.NewGuid(), default));
        Assert.Empty(await db.DataQualityIssues.Where(x => x.PropertyId == other.Id).ToListAsync());
    }

    [RoomLockPostgresFact]
    public async Task Late_qr_is_detected_until_actual_resolution_and_cancelled_receipt_is_separate()
    {
        await using var db = Db(); var (p, b) = await Seed(db);
        b.Status = BookingStatus.Cancelled;
        var receipt = new Payment { PropertyId = p.Id, Booking = b, Amount = 250000 };
        var intent = new Pay2SPaymentIntent { PropertyId = p.Id, Booking = b, Status = Pay2SPaymentIntentStatus.PaidAfterExpiry,
            Payment = receipt, Amount = 250000, OrderId = Guid.NewGuid().ToString("N"), RequestId = Guid.NewGuid().ToString("N"),
            ExpiresAtUtc = DateTime.UtcNow.AddHours(-1), ReleaseAtUtc = DateTime.UtcNow.AddHours(-1) };
        db.Add(intent); await db.SaveChangesAsync();
        var service = new DataQualityService(db, new AuditService(db));
        Assert.Equal(2, await service.ScanAsync(p.Id, default));
        intent.LatePaymentResolvedAtUtc = DateTime.UtcNow; intent.LatePaymentResolution = "Refund";
        await db.SaveChangesAsync();
        Assert.Equal(1, await service.ScanAsync(p.Id, default));
        Assert.False((await db.DataQualityIssues.SingleAsync(x => x.PropertyId == p.Id && x.Rule == "late_payment")).IsDetected);
        Assert.Equal(BookingStatus.Cancelled, b.Status); Assert.Equal(250000, receipt.Amount);
    }

    [RoomLockPostgresFact]
    public async Task List_paginates_and_searches_across_all_issues_in_one_property()
    {
        await using var db = Db(); var (p, b) = await Seed(db);
        for (var i = 0; i < 105; i++) db.DataQualityIssues.Add(new() {
            PropertyId = p.Id, BookingId = b.Id, Fingerprint = $"paging:{i}", Rule = "fixture", Severity = "Medium",
            Title = i == 104 ? "Unique target" : "Review", EvidenceJson = "{}", LastCheckedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var service = new DataQualityService(db, new AuditService(db));
        var page = System.Text.Json.JsonSerializer.SerializeToElement(await service.ListAsync(p.Id, default, 2, "all"));
        Assert.Equal(2, page.GetProperty("pages").GetInt32()); Assert.Equal(5, page.GetProperty("items").GetArrayLength());
        var found = System.Text.Json.JsonSerializer.SerializeToElement(await service.ListAsync(p.Id, default, 1, "pending", "unique target"));
        Assert.Equal(1, found.GetProperty("count").GetInt32());
        Assert.Equal(105, found.GetProperty("pending").GetInt32());
    }

    [RoomLockPostgresFact]
    public async Task Concurrent_scans_create_one_issue_and_one_alert()
    {
        await using var db = Db(); var (p, _) = await Seed(db);
        async Task Scan() { await using var context = Db(); await new DataQualityService(context, new AuditService(context)).ScanAsync(p.Id, default); }
        await Task.WhenAll(Scan(), Scan());
        Assert.Equal(1, await db.DataQualityIssues.CountAsync(x => x.PropertyId == p.Id));
        Assert.Equal(1, await db.PropertyNotifications.CountAsync(x => x.PropertyId == p.Id));
    }
}
