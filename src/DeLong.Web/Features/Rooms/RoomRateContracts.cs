using DeLong.Web.Domain.Enums;

namespace DeLong.Web.Features.Rooms;

public sealed class CreateRoomRateRequest
{
    public string Name { get; init; } = string.Empty;
    public string StartTime { get; init; } = string.Empty;
    public string EndTime { get; init; } = string.Empty;
    public RoomRateType Type { get; init; } = RoomRateType.TimeSlot;
    public decimal Price { get; init; }
    public int SortOrder { get; init; }
}

public sealed class UpdateRoomRateRequest
{
    public string Name { get; init; } = string.Empty;
    public string StartTime { get; init; } = string.Empty;
    public string EndTime { get; init; } = string.Empty;
    public RoomRateType Type { get; init; } = RoomRateType.TimeSlot;
    public decimal Price { get; init; }
    public int SortOrder { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed record RoomRateOperationError(string Code, string Message);
