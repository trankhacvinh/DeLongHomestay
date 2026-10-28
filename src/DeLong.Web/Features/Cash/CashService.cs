using System.Globalization;
using DeLong.Web.Common.Auditing;
using DeLong.Web.Common.Persistence;
using DeLong.Web.Common.Security;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Cash;

public sealed class CashService(AppDbContext db, AuditService audit)
{
    public async Task<(Guid? Result, string? Error)> AddAsync(Guid propertyId, CashReceiptRequest request, Guid? actorId, CancellationToken ct)
    {
        if (request.RequestKey == Guid.Empty || request.OccurredAt == default || request.Amount <= 0 || request.Amount > 10_000_000_000m ||
            decimal.Round(request.Amount, 2) != request.Amount || string.IsNullOrWhiteSpace(request.Description) || request.Description.Trim().Length > 500 || request.Note?.Length > 2000)
            return (null, "Nhập ngày, nội dung và số tiền dương hợp lệ (tối đa 2 chữ số lẻ).");
        if (request.RoomId.HasValue && !await db.Rooms.AsNoTracking().AnyAsync(x => x.Id == request.RoomId && x.PropertyId == propertyId, ct)) return (null, "Phòng không thuộc chi nhánh.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await RequestFingerprint.LockAsync(db, $"cash:{propertyId}:{request.RequestKey}", ct);
        var hash = RequestFingerprint.Create(request);
        var previous = await db.CashReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.PropertyId == propertyId && x.RequestKey == request.RequestKey, ct);
        if (previous is not null) return previous.RequestHash == hash ? (previous.Id, null) : (null, "Mã gửi lại đã dùng cho nội dung khác.");
        var row = new CashReceipt { PropertyId = propertyId, RequestKey = request.RequestKey, RequestHash = hash, OccurredAtUtc = request.OccurredAt.UtcDateTime,
            RoomId = request.RoomId, Amount = request.Amount, Description = request.Description.Trim(), Note = request.Note?.Trim(), RecordedByUserId = actorId };
        db.CashReceipts.Add(row);
        audit.Add(propertyId, "CashReceipt", row.Id, "Created", actorId, after: new { row.OccurredAtUtc, row.Amount, row.Description, row.RoomId, row.Note });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return (row.Id, null);
    }

    public async Task<string?> VoidAsync(Guid propertyId, Guid id, string reason, Guid? actorId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1000) return "Nhập lý do hủy, tối đa 1000 ký tự.";
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await RequestFingerprint.LockAsync(db, $"cash-void:{propertyId}:{id}", ct);
        var row = await db.CashReceipts.SingleOrDefaultAsync(x => x.Id == id && x.PropertyId == propertyId, ct);
        if (row is null) return "Không tìm thấy khoản thu.";
        if (row.IsVoided) return null;
        row.IsVoided = true; row.VoidedAtUtc = DateTime.UtcNow; row.VoidedByUserId = actorId; row.VoidReason = reason.Trim();
        audit.Add(propertyId, "CashReceipt", row.Id, "Voided", actorId, after: new { row.VoidReason });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return null;
    }

    public async Task<(CashSnapshotDto? Result, string? Error)> GetAsync(ReportingPropertyScope scope, string month, Guid? roomId, CancellationToken ct)
    {
        if (!DateOnly.TryParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) || date.Year >= 9999)
            return (null, "Tháng không hợp lệ.");
        var selectedProperties = scope.IsAll ? scope.Properties : [scope.SelectedProperty];
        var ids = selectedProperties.Select(x => x.Id).ToArray();
        if (roomId.HasValue && !await db.Rooms.AsNoTracking().AnyAsync(x => x.Id == roomId && ids.Contains(x.PropertyId), ct)) return (null, "Phòng không thuộc phạm vi chi nhánh được xem.");
        var entries = new List<CashEntryDto>();
        foreach (var property in selectedProperties) {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(property.TimeZoneId);
            var from = TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue), zone);
            var to = TimeZoneInfo.ConvertTimeToUtc(date.AddMonths(1).ToDateTime(TimeOnly.MinValue), zone);
            entries.AddRange(await db.Payments.AsNoTracking().Where(x => x.PropertyId == property.Id && x.Method == PaymentMethod.Cash && x.OccurredAtUtc >= from && x.OccurredAtUtc < to && (!roomId.HasValue || x.Booking.RoomId == roomId))
                .Select(x => new CashEntryDto(x.Id, x.Type == PaymentType.Receipt ? "booking-receipt" : "booking-refund", property.Id, property.Name,
                    x.Booking.RoomId, x.Booking.Room.Name, x.OccurredAtUtc, x.Booking.Code, x.Note,
                    x.Type == PaymentType.Receipt ? x.Amount : 0, x.Type == PaymentType.Refund ? x.Amount : 0, x.IsVoided, x.VoidReason, x.RecordedByUserId, null, null)).ToListAsync(ct));
            entries.AddRange(await db.CashReceipts.AsNoTracking().Where(x => x.PropertyId == property.Id && x.OccurredAtUtc >= from && x.OccurredAtUtc < to && (!roomId.HasValue || x.RoomId == roomId))
                .Select(x => new CashEntryDto(x.Id, "other-receipt", property.Id, property.Name, x.RoomId, x.Room == null ? null : x.Room.Name,
                    x.OccurredAtUtc, x.Description, x.Note, x.Amount, 0, x.IsVoided, x.VoidReason, x.RecordedByUserId, null, null)).ToListAsync(ct));
            entries.AddRange(await db.Expenses.AsNoTracking().Where(x => x.PropertyId == property.Id && x.Method == PaymentMethod.Cash && x.OccurredAtUtc >= from && x.OccurredAtUtc < to && (!roomId.HasValue || x.RoomId == roomId))
                .Select(x => new CashEntryDto(x.Id, "expense", property.Id, property.Name, x.RoomId, x.Room == null ? null : x.Room.Name,
                    x.OccurredAtUtc, x.Category + ": " + x.Description, x.Note, 0, x.Amount, x.IsVoided, x.VoidReason, x.RecordedByUserId, null, x.Category)).ToListAsync(ct));
        }
        var actorIds = entries.Where(x => x.RecordedByUserId.HasValue).Select(x => x.RecordedByUserId!.Value).Distinct().ToArray();
        var actors = await db.Users.AsNoTracking().Where(x => actorIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.DisplayName != "" ? x.DisplayName : x.UserName, ct);
        entries = entries.Select(x => x with { RecordedByName = x.RecordedByUserId.HasValue ? actors.GetValueOrDefault(x.RecordedByUserId.Value) : null }).OrderByDescending(x => x.OccurredAtUtc).ThenBy(x => x.Id).ToList();
        var active = entries.Where(x => !x.IsVoided).ToList();
        var booking = active.Where(x => x.Source == "booking-receipt").Sum(x => x.Inflow);
        var other = active.Where(x => x.Source == "other-receipt").Sum(x => x.Inflow);
        var refunds = active.Where(x => x.Source == "booking-refund").Sum(x => x.Outflow);
        var expenses = active.Where(x => x.Source == "expense").Sum(x => x.Outflow);
        CashGroupDto Group(string key, string name, IEnumerable<CashEntryDto> rows) {
            var list = rows.ToList(); var received = list.Sum(x => x.Inflow);
            var refunded = list.Where(x => x.Source == "booking-refund").Sum(x => x.Outflow);
            var spent = list.Where(x => x.Source == "expense").Sum(x => x.Outflow);
            return new(key, name, received, refunded, spent, received - refunded - spent);
        }
        var zones = selectedProperties.ToDictionary(x => x.Id, x => TimeZoneInfo.FindSystemTimeZoneById(x.TimeZoneId));
        var byProperty = active.GroupBy(x => new { x.PropertyId, x.PropertyName }).Select(g => Group(g.Key.PropertyId.ToString(), g.Key.PropertyName, g)).ToList();
        var byRoom = active.GroupBy(x => new { x.PropertyId, x.PropertyName, x.RoomId, x.RoomName }).Select(g => Group($"{g.Key.PropertyId}:{g.Key.RoomId}", $"{g.Key.PropertyName} · {g.Key.RoomName ?? "Khoản chung"}", g)).ToList();
        var byDay = active.GroupBy(x => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(x.OccurredAtUtc, zones[x.PropertyId]))).OrderBy(x => x.Key).Select(g => Group(g.Key.ToString("yyyy-MM-dd"), g.Key.ToString("dd/MM/yyyy"), g)).ToList();
        var byExpense = active.Where(x => x.Source == "expense").GroupBy(x => x.ExpenseCategory ?? "Khoản chi").Select(g => Group(g.Key, g.Key, g)).ToList();
        return (new(month, new(booking, other, refunds, expenses, booking + other - refunds - expenses), entries, byProperty, byRoom, byDay, byExpense), null);
    }
}
