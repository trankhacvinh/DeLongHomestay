using System.Text.Json;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Rooms;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.AdminAi;

public sealed partial class AdminAiService
{
    private sealed record RoomLockInput(string[]? RoomReferences, bool? IsLocked, string? Reason,
        Guid? BatchId, DateTimeOffset? Start, DateTimeOffset? End, bool RepeatDaily,
        DateOnly? FromDate, DateOnly? ToDate, IReadOnlyList<RoomBlockWindow>? Windows);
    private sealed record RoomLockState(Guid Id, string Code, string Name, bool IsBookingLocked,
        string? BookingLockReason, DateTime? BookingLockedAtUtc);
    private sealed record BlockState(Guid Id, Guid RoomId, DateTime StartUtc, DateTime EndUtc, string Reason, DateTime UpdatedAtUtc);
    private sealed record PreparedRoomLockChange(IReadOnlyList<Guid> RoomIds, SetRoomBookingLockRequest? RoomLock,
        Guid? BatchId, SaveRoomBlockRequest? Schedule, IReadOnlyList<RoomLockState> BeforeRooms,
        IReadOnlyList<BlockState> BeforeBlocks, IReadOnlyList<RoomBlockConflict> Conflicts, string TimeZoneId);
    private sealed record PreparedRoomLockPayload(PreparedRoomLockChange PreparedRoomLock);

    private static bool IsRoomLockOperation(AiProposalType type) => type is AiProposalType.SetRoomBookingLock or
        AiProposalType.CreateRoomBookingSchedule or AiProposalType.UpdateRoomBookingSchedule or AiProposalType.EndRoomBookingSchedule;

    private static IEnumerable<PreparedRoomLockChange> RoomLockChanges(AiProposalType type, JsonElement payload)
    {
        if (IsRoomLockOperation(type))
            yield return payload.Deserialize<PreparedRoomLockPayload>(Json)!.PreparedRoomLock;
        else if (type == AiProposalType.Batch)
            foreach (var item in payload.Deserialize<BatchProposal>(Json)!.Operations)
                foreach (var change in RoomLockChanges(item.Type, item.Payload)) yield return change;
    }

    private static bool ContainsRoomLockOperation(AiChangeProposal proposal) =>
        RoomLockChanges(proposal.Type, JsonSerializer.Deserialize<JsonElement>(proposal.PayloadJson)).Any();

    private Task<List<RoomLockState>> RoomLockStatesAsync(Guid propertyId, IEnumerable<Guid> ids, CancellationToken ct) =>
        db.Rooms.AsNoTracking().Where(x => x.PropertyId == propertyId && ids.Contains(x.Id))
            .OrderBy(x => x.Id).Select(x => new RoomLockState(x.Id, x.Code, x.Name, x.IsBookingLocked,
                x.BookingLockReason, x.BookingLockedAtUtc)).ToListAsync(ct);

    private Task<List<BlockState>> BlockStatesAsync(Guid propertyId, Guid batchId, CancellationToken ct) =>
        db.RoomBookingBlocks.AsNoTracking().Where(x => x.PropertyId == propertyId && x.BatchId == batchId && x.CancelledAtUtc == null)
            .OrderBy(x => x.Id).Select(x => new BlockState(x.Id, x.RoomId, x.StartUtc, x.EndUtc, x.Reason, x.UpdatedAtUtc)).ToListAsync(ct);

    private async Task<List<RoomBlockConflict>> RoomLockConflictsAsync(Guid propertyId, IReadOnlyList<Guid> roomIds,
        SetRoomBookingLockRequest? roomLock, SaveRoomBlockRequest? schedule, TimeZoneInfo zone, CancellationToken ct)
    {
        if (roomLock?.IsLocked != true && schedule is null) return [];
        var ranges = schedule is null ? null : RoomBookingBlockService.Expand(schedule, zone);
        var first = ranges?.Min(x => x.Start) ?? DateTime.UtcNow;
        var last = ranges?.Max(x => x.End) ?? DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc);
        var bookings = await db.Bookings.AsNoTracking().Where(x => x.PropertyId == propertyId && roomIds.Contains(x.RoomId) &&
            (x.Status == BookingStatus.Requested || x.Status == BookingStatus.Held || x.Status == BookingStatus.Confirmed || x.Status == BookingStatus.CheckedIn) &&
            x.CheckOutUtc > first && x.CheckInUtc < last).OrderBy(x => x.Id)
            .Select(x => new RoomBlockConflict(x.Id, x.Code, x.Room.Name, x.CheckInUtc, x.CheckOutUtc)).ToListAsync(ct);
        return bookings.Where(x => ranges is null || ranges.Any(r => x.CheckInUtc < r.End && r.Start < x.CheckOutUtc)).ToList();
    }

    private async Task<(AiOperationEnvelope? Value, string? Error)> PrepareRoomLockAsync(Guid propertyId, AiOperationEnvelope operation, CancellationToken ct)
    {
        var input = operation.Payload.Deserialize<RoomLockInput>(Json) ?? throw new InvalidOperationException("Thiếu thông tin khóa phòng.");
        if (input.Windows?.Any(x => x is null) == true) throw new InvalidOperationException("Khung giờ khóa không hợp lệ.");
        var property = await db.Properties.AsNoTracking().SingleAsync(x => x.Id == propertyId, ct);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(property.TimeZoneId);
        var updating = operation.Type is AiProposalType.UpdateRoomBookingSchedule or AiProposalType.EndRoomBookingSchedule;
        if (updating != input.BatchId.HasValue) throw new InvalidOperationException("Cần dùng đúng batchId của lịch khóa đang hiệu lực; tạo mới không dùng batchId.");
        var beforeBlocks = updating ? await BlockStatesAsync(propertyId, input.BatchId!.Value, ct) : [];
        if (updating && (beforeBlocks.Count == 0 || beforeBlocks.All(x => x.EndUtc <= DateTime.UtcNow)))
            throw new InvalidOperationException("Lịch khóa không còn hiệu lực hoặc không thuộc chi nhánh này.");
        var ending = operation.Type == AiProposalType.EndRoomBookingSchedule;
        var rooms = await db.Rooms.AsNoTracking().Where(x => x.PropertyId == propertyId && (ending || x.IsActive)).ToListAsync(ct);
        var selected = ending ? rooms.Where(x => beforeBlocks.Any(b => b.RoomId == x.Id)).ToList() :
            (input.RoomReferences ?? []).Select(reference => SingleReference(rooms, reference, x => x.Id, x => x.Code, x => x.Name)).DistinctBy(x => x.Id).ToList();
        if (selected.Count is < 1 or > 50) throw new InvalidOperationException("Chọn rõ từ 1 đến 50 phòng trong chi nhánh.");
        SetRoomBookingLockRequest? roomLock = null;
        SaveRoomBlockRequest? schedule = null;
        var roomIds = selected.Select(x => x.Id).Order().ToArray();
        if (operation.Type == AiProposalType.SetRoomBookingLock)
        {
            if (!input.IsLocked.HasValue) throw new InvalidOperationException("Thiếu trạng thái khóa/mở khóa.");
            roomLock = new(input.IsLocked.Value, input.Reason);
            if (roomLock.IsLocked && (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length > 500))
                throw new InvalidOperationException("Lý do khóa phòng phải từ 1 đến 500 ký tự.");
            if (roomLock.IsLocked && selected.Any(x => x.IsBookingLocked && x.BookingLockReason != input.Reason?.Trim()))
                throw new InvalidOperationException("Có phòng đã khóa với lý do khác. Mở khóa trước khi khóa lại hoặc quản lý lịch khóa theo thời gian.");
        }
        else if (!ending)
        {
            if (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length > 500)
                throw new InvalidOperationException("Lý do khóa phòng phải từ 1 đến 500 ký tự.");
            if (!input.RepeatDaily)
                foreach (var field in new[] { "start", "end" })
                {
                    if (!operation.Payload.TryGetProperty(field, out var value) || value.ValueKind != JsonValueKind.String ||
                        !System.Text.RegularExpressions.Regex.IsMatch(value.GetString()!, @"(?:Z|[+-]\d{2}:\d{2})$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                        throw new InvalidOperationException("Giờ bắt đầu/kết thúc phải kèm múi giờ, ví dụ 2031-01-10T14:00:00+07:00.");
                }
            schedule = new(roomIds, input.Reason.Trim(), input.Start ?? default, input.End ?? default,
                input.RepeatDaily, input.FromDate, input.ToDate, input.Windows);
            var ranges = RoomBookingBlockService.Expand(schedule, zone);
            if (ranges.Any(x => x.End <= x.Start || x.End - x.Start > TimeSpan.FromDays(366)) ||
                ranges.Max(x => x.End) <= DateTime.UtcNow || ranges.Count * roomIds.Length > 5000)
                throw new InvalidOperationException("Khoảng khóa không hợp lệ hoặc đã hết hạn; tối đa 366 ngày và 5.000 khoảng khóa.");
        }
        var beforeRooms = await RoomLockStatesAsync(propertyId, roomIds.Concat(beforeBlocks.Select(x => x.RoomId)), ct);
        var conflicts = await RoomLockConflictsAsync(propertyId, roomIds, roomLock, schedule, zone, ct);
        var change = new PreparedRoomLockChange(roomIds, roomLock, input.BatchId, schedule, beforeRooms, beforeBlocks, conflicts, property.TimeZoneId);
        var verb = ending ? "Kết thúc lịch khóa" : roomLock is not null ? roomLock.IsLocked ? "Khóa vô thời hạn" : "Mở khóa vô thời hạn" : updating ? "Sửa lịch khóa" : "Tạo lịch khóa";
        var summary = $"{verb}: {string.Join(", ", selected.Take(3).Select(x => x.Name))}{(selected.Count > 3 ? $" và {selected.Count - 3} phòng khác" : "")}.";
        if (schedule is not null)
        {
            var ranges = RoomBookingBlockService.Expand(schedule, zone);
            summary += $" {TimeZoneInfo.ConvertTimeFromUtc(ranges.Min(x => x.Start), zone):dd/MM/yyyy HH:mm} → {TimeZoneInfo.ConvertTimeFromUtc(ranges.Max(x => x.End), zone):dd/MM/yyyy HH:mm} ({property.TimeZoneId}); {ranges.Count} khoảng/phòng.";
        }
        summary += " Đơn đã tạo và thanh toán giữ nguyên.";
        if (roomLock?.IsLocked == false) summary += " Các lịch khóa theo thời gian vẫn giữ nguyên.";
        if (conflicts.Count > 0) summary += $" Có {conflicts.Count} đơn trùng; cần xác nhận đã sắp xếp với khách.";
        return (operation with { Summary = summary, Payload = JsonSerializer.SerializeToElement(new PreparedRoomLockPayload(change), Json) }, null);
    }

    private async Task<string?> ExecuteRoomLockAsync(AiChangeProposal proposal, Guid actorId, bool acknowledge, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<PreparedRoomLockPayload>(proposal.PayloadJson, Json);
        if (payload?.PreparedRoomLock is not { } change) return "Thiếu bản xem trước khóa phòng do máy chủ tạo.";
        var currentRooms = await RoomLockStatesAsync(proposal.PropertyId, change.BeforeRooms.Select(x => x.Id), ct);
        if (!JsonElement.DeepEquals(JsonSerializer.SerializeToElement(currentRooms, Json), JsonSerializer.SerializeToElement(change.BeforeRooms, Json)))
            return "Trạng thái phòng đã thay đổi. Hãy tạo bản xem trước mới.";
        if (change.BatchId is { } batchId)
        {
            var currentBlocks = await BlockStatesAsync(proposal.PropertyId, batchId, ct);
            if (!JsonElement.DeepEquals(JsonSerializer.SerializeToElement(currentBlocks, Json), JsonSerializer.SerializeToElement(change.BeforeBlocks, Json)))
                return "Lịch khóa đã thay đổi. Hãy tạo bản xem trước mới.";
        }
        var property = await db.Properties.AsNoTracking().SingleAsync(x => x.Id == proposal.PropertyId, ct);
        if (property.TimeZoneId != change.TimeZoneId) return "Múi giờ chi nhánh đã thay đổi. Hãy tạo bản xem trước mới.";
        var conflicts = await RoomLockConflictsAsync(proposal.PropertyId, change.RoomIds, change.RoomLock, change.Schedule,
            TimeZoneInfo.FindSystemTimeZoneById(property.TimeZoneId), ct);
        if (!JsonElement.DeepEquals(JsonSerializer.SerializeToElement(conflicts, Json), JsonSerializer.SerializeToElement(change.Conflicts, Json)))
            return "Danh sách đơn trùng đã thay đổi. Hãy tạo bản xem trước mới để kiểm tra các đơn hiện có.";
        if (conflicts.Count > 0 && !acknowledge) return "Cần xác nhận đã sắp xếp với khách và giữ nguyên các đơn trùng trước khi áp dụng.";
        if (change.RoomLock is not null)
        {
            foreach (var roomId in change.RoomIds)
            {
                var result = await roomService.SetBookingLockAsync(proposal.PropertyId, roomId, change.RoomLock, actorId, ct, notify: false);
                if (result.Error is not null) return result.Error;
            }
            return null;
        }
        var service = roomBookingBlockService ?? new RoomBookingBlockService(db, auditService);
        if (proposal.Type == AiProposalType.EndRoomBookingSchedule)
            return (await service.CancelAsync(proposal.PropertyId, change.BatchId!.Value, actorId, ct, notify: false))?.Message;
        if (change.Schedule is null) return "Thiếu thời gian khóa phòng.";
        return (await service.SaveAsync(proposal.PropertyId, change.BatchId, change.Schedule with {
            AcknowledgeExistingBookings = acknowledge, AcknowledgedBookingIds = change.Conflicts.Select(x => x.Id).ToArray()
        }, actorId, ct, notify: false))?.Message;
    }
}
