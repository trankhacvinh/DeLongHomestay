using DeLong.Web.Domain.Enums;

namespace DeLong.Web.Features.Housekeeping;

public sealed record ChangeHousekeepingStatusRequest(HousekeepingStatus Status);

public sealed record HousekeepingRoomDto(
    Guid RoomId,
    string RoomCode,
    string RoomName,
    HousekeepingStatus Status,
    DateTime? UpdatedAtUtc,
    Guid? UpdatedByUserId);
