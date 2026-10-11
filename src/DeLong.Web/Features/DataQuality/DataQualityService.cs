using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DeLong.Web.Common.Auditing;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Notifications;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.DataQuality;

public sealed record ReviewQualityRequest(string Status, string Note);

public sealed class DataQualityService(AppDbContext db, AuditService audit, NotificationRealtimeBroker? broker = null)
{
    public async Task<object> ListAsync(Guid propertyId, CancellationToken ct, int page = 1, string filter = "pending", string? search = null)
    {
        var all = db.DataQualityIssues.AsNoTracking().Where(x => x.PropertyId == propertyId);
        var query = filter switch {
            "pending" => all.Where(x => x.IsDetected && (x.Status == "Open" || x.Status == "InProgress")),
            "cleared" => all.Where(x => !x.IsDetected),
            "InProgress" or "Resolved" or "Accepted" => all.Where(x => x.Status == filter),
            _ => all
        };
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim()[..Math.Min(100, search.Trim().Length)].ToLower();
            query = query.Where(x => x.Title.ToLower().Contains(term) || x.EvidenceJson.ToLower().Contains(term));
        }
        var count = await query.CountAsync(ct);
        const int pageSize = 100;
        var pages = Math.Max(1, (count + pageSize - 1) / pageSize);
        page = Math.Clamp(page, 1, pages);
        return new {
            checkedAtUtc = await db.AuditLogs.AsNoTracking().Where(x => x.PropertyId == propertyId && x.EntityType == "DataQualityScan")
                .MaxAsync(x => (DateTime?)x.CreatedAtUtc, ct),
            total = await all.CountAsync(ct), count, page, pages,
            pending = await all.CountAsync(x => x.IsDetected && (x.Status == "Open" || x.Status == "InProgress"), ct),
            items = await query.OrderByDescending(x => x.IsDetected && x.Status != "Accepted")
                .ThenBy(x => x.Severity == "High" ? 0 : 1).ThenByDescending(x => x.UpdatedAtUtc).ThenBy(x => x.Id)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(x => new { x.Id, x.Rule, x.Severity, x.BookingId, x.RelatedBookingId, x.Title,
                    x.EvidenceJson, x.Status, x.IsDetected, x.LastCheckedAtUtc, x.CreatedAtUtc,
                    x.ReviewNote, x.ReviewedAtUtc, x.ReviewedByUserId }).ToListAsync(ct)
        };
    }

    public async Task<int> ScanAsync(Guid propertyId, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            try { return await ScanOnceAsync(propertyId, ct); }
            catch (Exception ex) when (attempt < 2 && (ex is Npgsql.PostgresException { SqlState: "40001" or "23505" } ||
                ex is DbUpdateException { InnerException: Npgsql.PostgresException { SqlState: "40001" or "23505" } }))
            { db.ChangeTracker.Clear(); }
        }
    }

    private async Task<int> ScanOnceAsync(Guid propertyId, CancellationToken ct)
    {
        // Serialize scans/reviews across app instances; booking/payment reads use one consistent snapshot.
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({propertyId.ToString()}, 17))", ct);
        var now = DateTime.UtcNow;
        var facts = await db.Bookings.AsNoTracking().Where(x => x.PropertyId == propertyId)
            .Select(x => new BookingQualityFact(x.Id, x.RoomId, x.CustomerId, x.Code, x.Room.Name,
                x.CheckInUtc, x.CheckOutUtc, x.Status,
                x.RoomAmount + x.SpecialSurchargeAmount + x.ExtraAmount - x.DiscountAmount,
                x.Payments.Where(p => !p.IsVoided).Sum(p => p.Type == PaymentType.Receipt ? p.Amount : -p.Amount)))
            .ToListAsync(ct);
        var findings = DataQualityRules.Evaluate(facts).ToList();
        var late = await db.Pay2SPaymentIntents.AsNoTracking().Where(x => x.PropertyId == propertyId &&
            x.Status == Pay2SPaymentIntentStatus.PaidAfterExpiry && x.LatePaymentResolvedAtUtc == null)
            .Select(x => new { x.Id, x.BookingId, x.Booking.Code, x.Amount, x.PaymentId, x.CallbackReceivedAtUtc, x.ReleaseAtUtc })
            .ToListAsync(ct);
        foreach (var x in late)
            findings.Add(new($"late_payment:{x.Id:N}", "late_payment", "High", x.BookingId, null,
                "Thanh toán QR sau hạn chưa được xử lý", JsonSerializer.Serialize(x, new JsonSerializerOptions(JsonSerializerDefaults.Web))));
        var existing = await db.DataQualityIssues.Where(x => x.PropertyId == propertyId).ToDictionaryAsync(x => x.Fingerprint, ct);
        var detected = findings.Select(x => x.Fingerprint).ToHashSet();
        var alerts = 0;
        foreach (var f in findings)
        {
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(f.EvidenceJson)));
            if (!existing.TryGetValue(f.Fingerprint, out var issue))
            {
                issue = new() { PropertyId = propertyId, Fingerprint = f.Fingerprint, Rule = f.Rule,
                    Severity = f.Severity, BookingId = f.BookingId, RelatedBookingId = f.RelatedBookingId };
                db.DataQualityIssues.Add(issue);
                alerts++;
                audit.Add(propertyId, "DataQualityIssue", issue.Id, "Detected", null, after: f);
            }
            else if (!issue.IsDetected || issue.EvidenceHash != hash)
            {
                audit.Add(propertyId, "DataQualityIssue", issue.Id, "EvidenceChanged", null,
                    before: new { issue.Status, issue.EvidenceJson }, after: f);
                issue.Status = "Open";
                issue.ReviewNote = null; issue.ReviewedAtUtc = null; issue.ReviewedByUserId = null;
                issue.UpdatedAtUtc = now;
                alerts++;
            }
            issue.Title = f.Title; issue.EvidenceJson = f.EvidenceJson; issue.EvidenceHash = hash;
            issue.IsDetected = true; issue.LastCheckedAtUtc = now;
        }
        foreach (var issue in existing.Values.Where(x => !detected.Contains(x.Fingerprint)))
        {
            if (issue.IsDetected)
            {
                audit.Add(propertyId, "DataQualityIssue", issue.Id, "NoLongerDetected", null, before: new { issue.Status });
                issue.UpdatedAtUtc = now;
            }
            issue.IsDetected = false; issue.LastCheckedAtUtc = now;
        }
        PropertyNotification? notification = null;
        if (alerts > 0)
        {
            notification = new() { PropertyId = propertyId, Type = "data-quality",
                Title = $"Có {alerts} mục dữ liệu cần kiểm tra", Message = "Mở danh sách kiểm tra dữ liệu để đối soát thủ công. Hệ thống chưa thay đổi đơn hoặc tiền.",
                ActionUrl = $"/Admin/DataQuality?propertyId={propertyId}" };
            db.PropertyNotifications.Add(notification);
        }
        audit.Add(propertyId, "DataQualityScan", propertyId, "Scanned", null, after: new { Count = findings.Count, NewAlerts = alerts, CheckedAtUtc = now });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        if (notification is not null) broker?.Publish(new NotificationRealtimeEvent(notification.Id, propertyId, notification.Type, notification.CreatedAtUtc));
        return findings.Count;
    }

    public async Task<string?> ReviewAsync(Guid propertyId, Guid id, ReviewQualityRequest request, Guid actor, CancellationToken ct)
    {
        var note = request.Note?.Trim() ?? "";
        if (request.Status is not ("InProgress" or "Resolved" or "Accepted") || note.Length is < 3 or > 1000)
            return "Chọn trạng thái hợp lệ và nhập ghi chú từ 3–1.000 ký tự.";
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({propertyId.ToString()}, 17))", ct);
        var issue = await db.DataQualityIssues.SingleOrDefaultAsync(x => x.PropertyId == propertyId && x.Id == id, ct);
        if (issue is null) return "Không tìm thấy mục kiểm tra trong chi nhánh.";
        if (request.Status == "Resolved" && issue.IsDetected)
            return "Vấn đề vẫn được phát hiện. Hãy xử lý dữ liệu và chạy kiểm tra lại; hoặc đánh dấu đã kiểm tra hợp lệ kèm lý do.";
        audit.Add(propertyId, "DataQualityIssue", id, "Reviewed", actor,
            before: new { issue.Status, issue.ReviewNote }, after: new { request.Status, Note = note });
        issue.Status = request.Status; issue.ReviewNote = note; issue.ReviewedByUserId = actor;
        issue.ReviewedAtUtc = issue.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return null;
    }
}
