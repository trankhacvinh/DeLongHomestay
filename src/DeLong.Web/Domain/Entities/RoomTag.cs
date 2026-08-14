namespace DeLong.Web.Domain.Entities;

public sealed class RoomTag : EntityBase
{
    public Guid PropertyId { get; set; }
    public Property Property { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ICollection<RoomTagAssignment> Rooms { get; set; } = new List<RoomTagAssignment>();
}

public sealed class RoomTagAssignment
{
    public Guid RoomId { get; set; }
    public Room Room { get; set; } = null!;
    public Guid RoomTagId { get; set; }
    public RoomTag RoomTag { get; set; } = null!;
}
