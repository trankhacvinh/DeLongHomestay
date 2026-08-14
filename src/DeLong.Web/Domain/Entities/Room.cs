using DeLong.Web.Domain.Enums;

namespace DeLong.Web.Domain.Entities;

public sealed class Room : EntityBase
{
    public Guid PropertyId { get; set; }
    public Property Property { get; set; } = null!;

    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? ShortDescription { get; set; }
    public string? DescriptionHtml { get; set; }
    public bool IsPublished { get; set; }
    public int Capacity { get; set; } = 2;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public HousekeepingStatus HousekeepingStatus { get; set; } = HousekeepingStatus.Clean;
    public DateTime? HousekeepingUpdatedAtUtc { get; set; }
    public Guid? HousekeepingUpdatedByUserId { get; set; }

    public ICollection<RoomRate> Rates { get; set; } = new List<RoomRate>();
    public ICollection<RoomImage> Images { get; set; } = new List<RoomImage>();
    public ICollection<RoomAmenity> Amenities { get; set; } = new List<RoomAmenity>();
    public ICollection<RoomTagAssignment> Tags { get; set; } = new List<RoomTagAssignment>();
    public ICollection<RoomHighlight> Highlights { get; set; } = new List<RoomHighlight>();
}
