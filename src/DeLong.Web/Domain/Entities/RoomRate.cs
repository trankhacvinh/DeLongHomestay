using DeLong.Web.Domain.Enums;

namespace DeLong.Web.Domain.Entities;

public sealed class RoomRate : EntityBase
{
    public Guid RoomId { get; set; }
    public Room Room { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public RoomRateType Type { get; set; } = RoomRateType.TimeSlot;

    // Kept during the Booking V2 migration window so existing rows remain backward compatible.
    // New code must use Type instead of this flag.
    public bool IsOvernight { get; set; }

    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
