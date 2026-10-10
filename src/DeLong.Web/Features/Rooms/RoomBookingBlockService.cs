using DeLong.Web.Common.Auditing;
using DeLong.Web.Common.Caching;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Operations;
using Microsoft.EntityFrameworkCore;
using ZiggyCreatures.Caching.Fusion;

namespace DeLong.Web.Features.Rooms;

public sealed record RoomBlockWindow(TimeOnly Start, TimeOnly End);
public sealed record SaveRoomBlockRequest(IReadOnlyList<Guid> RoomIds, string Reason,
    DateTimeOffset Start, DateTimeOffset End, bool RepeatDaily, DateOnly? FromDate,
    DateOnly? ToDate, IReadOnlyList<RoomBlockWindow>? Windows, bool AcknowledgeExistingBookings = false,
    IReadOnlyList<Guid>? AcknowledgedBookingIds = null);
public sealed record RoomBlockConflict(Guid Id, string Code, string RoomName, DateTime CheckInUtc, DateTime CheckOutUtc);
public sealed record RoomBlockError(string Code, string Message, IReadOnlyList<RoomBlockConflict>? Conflicts = null);
public sealed record RoomBlockDto(Guid Id, Guid BatchId, Guid RoomId, string RoomName,
    DateTime StartUtc, DateTime EndUtc, bool RepeatDaily, string Reason, string? CreatedByName,
    DateTime CreatedAtUtc, DateTime? CancelledAtUtc);

public sealed class RoomBookingBlockService(AppDbContext db, AuditService audit, IFusionCache? cache = null)
{
    public Task<List<RoomBlockDto>> GetAllAsync(Guid propertyId, CancellationToken ct) =>
        db.RoomBookingBlocks.AsNoTracking().Where(x => x.PropertyId == propertyId)
            .OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.StartUtc)
            .Select(x => new RoomBlockDto(x.Id, x.BatchId, x.RoomId, x.Room.Name, x.StartUtc, x.EndUtc,
                x.RepeatDaily, x.Reason,
                db.Users.Where(u => u.Id == x.CreatedByUserId).Select(u => u.DisplayName).FirstOrDefault(),
                x.CreatedAtUtc, x.CancelledAtUtc)).ToListAsync(ct);

    public static IReadOnlyList<(DateTime Start, DateTime End)> Expand(SaveRoomBlockRequest request, TimeZoneInfo zone)
    {
        if (!request.RepeatDaily) return [(request.Start.UtcDateTime, request.End.UtcDateTime)];
        if (request.FromDate is null || request.ToDate is null || request.ToDate < request.FromDate ||
            request.ToDate.Value.DayNumber - request.FromDate.Value.DayNumber > 365 ||
            request.Windows is not { Count: > 0 and <= 24 })
            throw new ArgumentException("Chọn ngày và khung giờ hợp lệ; tối đa 366 ngày và 24 khung giờ.");
        var ranges = new List<(DateTime Start, DateTime End)>();
        for (var date = request.FromDate.Value; date <= request.ToDate.Value; date = date.AddDays(1))
            foreach (var window in request.Windows.Distinct())
            {
                var endDate = window.End <= window.Start ? date.AddDays(1) : date;
                ranges.Add((TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(window.Start), zone),
                    TimeZoneInfo.ConvertTimeToUtc(endDate.ToDateTime(window.End), zone)));
            }
        return ranges;
    }

    public async Task<RoomBlockError?> SaveAsync(Guid propertyId, Guid? batchId, SaveRoomBlockRequest request,
        Guid actorId, CancellationToken ct, bool notify = true)
    {
        if (request.RoomIds is not { Count: > 0 and <= 50 } || request.RoomIds.Any(x => x == Guid.Empty) ||
            string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 500)
            return new("validation", "Chọn phòng và nhập lý do từ 1 đến 500 ký tự (tối đa 50 phòng).");
        var property = await db.Properties.AsNoTracking().SingleOrDefaultAsync(x => x.Id == propertyId, ct);
        if (property is null) return new("not_found", "Không tìm thấy chi nhánh.");
        IReadOnlyList<(DateTime Start, DateTime End)> ranges;
        try { ranges = Expand(request, TimeZoneInfo.FindSystemTimeZoneById(property.TimeZoneId)); }
        catch (ArgumentException ex) { return new("validation", ex.Message); }
        if (ranges.Any(x => x.End <= x.Start || x.End - x.Start > TimeSpan.FromDays(366)) ||
            ranges.Max(x => x.End) <= DateTime.UtcNow)
            return new("validation", "Giờ kết thúc phải sau giờ bắt đầu; lịch khóa phải còn hiệu lực và tối đa 366 ngày.");
        var old = batchId.HasValue
            ? await db.RoomBookingBlocks.Where(x => x.PropertyId == propertyId && x.BatchId == batchId && x.CancelledAtUtc == null).ToListAsync(ct)
            : [];
        if (batchId.HasValue && old.Count == 0) return new("not_found", "Lịch khóa đã kết thúc hoặc không tồn tại.");
        var roomIds = request.RoomIds.Distinct().ToArray();
        if (ranges.Count * roomIds.Length > 5000) return new("validation", "Lịch khóa quá lớn. Vui lòng chia thành các đợt nhỏ hơn (tối đa 5.000 khoảng khóa).");
        var guards = new List<RoomBookingGuard>();
        try
        {
            foreach (var id in roomIds.Concat(old.Select(x => x.RoomId)).Distinct().Order())
            {
                var guard = await RoomBookingGuard.AcquireAsync(db, propertyId, id, ct);
                guards.Add(guard);
                if (guard.Room is null || (roomIds.Contains(id) && !guard.Room.IsActive))
                    return new("room_not_found", "Phòng không tồn tại trong chi nhánh hoặc đã ngừng hoạt động.");
            }
            // Re-read after the room locks: concurrent edits cannot revive an ended batch.
            if (batchId.HasValue)
            {
                foreach (var item in old) await db.Entry(item).ReloadAsync(ct);
                if (old.Any(x => x.CancelledAtUtc != null)) return new("conflict", "Lịch khóa vừa được thay đổi. Vui lòng tải lại.");
            }
            var first = ranges.Min(x => x.Start); var last = ranges.Max(x => x.End);
            var candidates = await db.Bookings.AsNoTracking().Where(x => x.PropertyId == propertyId && roomIds.Contains(x.RoomId) &&
                (x.Status == BookingStatus.Requested || x.Status == BookingStatus.Held || x.Status == BookingStatus.Confirmed || x.Status == BookingStatus.CheckedIn) &&
                x.CheckInUtc < last && first < x.CheckOutUtc)
                .Select(x => new RoomBlockConflict(x.Id, x.Code, x.Room.Name, x.CheckInUtc, x.CheckOutUtc)).ToListAsync(ct);
            var conflicts = candidates.Where(x => ranges.Any(r => x.CheckInUtc < r.End && r.Start < x.CheckOutUtc)).ToList();
            if (conflicts.Count > 0 && (!request.AcknowledgeExistingBookings ||
                conflicts.Any(x => request.AcknowledgedBookingIds?.Contains(x.Id) != true)))
                return new("existing_bookings", "Khoảng khóa trùng các đơn dưới đây. Các đơn vẫn được giữ nguyên; xác nhận sau khi đã sắp xếp với khách.", conflicts);
            var idBatch = batchId ?? Guid.CreateVersion7();
            var now = DateTime.UtcNow;
            foreach (var item in old) { item.CancelledAtUtc = now; item.CancelledByUserId = actorId; }
            foreach (var roomId in roomIds)
                foreach (var range in ranges)
                    db.RoomBookingBlocks.Add(new RoomBookingBlock { PropertyId = propertyId, RoomId = roomId, BatchId = idBatch,
                        StartUtc = range.Start, EndUtc = range.End, RepeatDaily = request.RepeatDaily,
                        Reason = request.Reason.Trim(), CreatedByUserId = actorId });
            audit.Add(propertyId, "RoomBookingBlock", idBatch, batchId.HasValue ? "Updated" : "Created", actorId,
                before: old.Select(x => new { x.RoomId, x.StartUtc, x.EndUtc, x.Reason }).ToArray(),
                after: new { RoomIds = roomIds, Ranges = ranges.Select(x => new { x.Start, x.End }).ToArray(), request.Reason, request.AcknowledgeExistingBookings });
            await db.SaveChangesAsync(ct);
            await guards[0].CommitAsync(ct);
            if (notify) Invalidate(propertyId);
            return null;
        }
        finally { foreach (var guard in guards) await guard.DisposeAsync(); }
    }

    public async Task<RoomBlockError?> CancelAsync(Guid propertyId, Guid batchId, Guid actorId, CancellationToken ct, bool notify = true)
    {
        var items = await db.RoomBookingBlocks.Where(x => x.PropertyId == propertyId && x.BatchId == batchId && x.CancelledAtUtc == null).ToListAsync(ct);
        if (items.Count == 0) return new("not_found", "Không tìm thấy lịch khóa đang hiệu lực.");
        var guards = new List<RoomBookingGuard>();
        try
        {
            foreach (var roomId in items.Select(x => x.RoomId).Distinct().Order())
                guards.Add(await RoomBookingGuard.AcquireAsync(db, propertyId, roomId, ct));
            foreach (var item in items) await db.Entry(item).ReloadAsync(ct);
            if (items.Any(x => x.CancelledAtUtc != null)) return new("conflict", "Lịch khóa vừa được thay đổi. Vui lòng tải lại.");
            foreach (var item in items)
            {
                item.CancelledAtUtc ??= DateTime.UtcNow;
                item.CancelledByUserId = actorId;
            }
            audit.Add(propertyId, "RoomBookingBlock", batchId, "Ended", actorId,
                after: items.Select(x => new { x.RoomId, x.StartUtc, x.EndUtc, x.CancelledAtUtc }).ToArray());
            await db.SaveChangesAsync(ct);
            await guards[0].CommitAsync(ct);
            if (notify) Invalidate(propertyId);
            return null;
        }
        finally { foreach (var guard in guards) await guard.DisposeAsync(); }
    }

    private void Invalidate(Guid propertyId)
    {
        cache?.RemoveByTag(PublicCacheKeys.Tag);
        OperationsRealtimeBroker.Shared.Publish(OperationsRealtimeEvent.Create(propertyId, OperationsEventTypes.RoomBookingLockChanged));
    }
}
