namespace DeLong.Web.Domain.Entities;

public sealed class Amenity : EntityBase
{
    public Guid PropertyId { get; set; }
    public Property Property { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public string? IconKey { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<RoomAmenity> Rooms { get; set; } = new List<RoomAmenity>();
}

public sealed class RoomAmenity
{
    public Guid RoomId { get; set; }
    public Room Room { get; set; } = null!;
    public Guid AmenityId { get; set; }
    public Amenity Amenity { get; set; } = null!;
}
