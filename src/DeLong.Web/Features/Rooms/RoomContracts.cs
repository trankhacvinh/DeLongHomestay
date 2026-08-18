using DeLong.Web.Domain.Enums;

namespace DeLong.Web.Features.Rooms;

public sealed record RoomRateDto(
    Guid Id,
    string Name,
    string StartTime,
    string EndTime,
    RoomRateType Type,
    bool IsOvernight,
    decimal Price,
    bool IsActive,
    int SortOrder);

public sealed record RoomDto(
    Guid Id,
    Guid PropertyId,
    string Code,
    string Name,
    int Capacity,
    int SortOrder,
    bool IsActive,
    HousekeepingStatus HousekeepingStatus,
    DateTime? HousekeepingUpdatedAtUtc,
    string? CoverThumbnailUrl,
    int ImageCount,
    IReadOnlyList<RoomRateDto> Rates);

public sealed record CreateRoomRequest(
    string Code,
    string Name,
    int Capacity,
    int SortOrder);

public sealed record UpdateRoomRequest(
    string Code,
    string Name,
    int Capacity,
    int SortOrder,
    bool IsActive);
