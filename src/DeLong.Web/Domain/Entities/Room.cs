namespace DeLong.Web.Domain.Entities;

public sealed class Room : EntityBase
{
    public Guid PropertyId { get; set; }
    public Property Property { get; set; } = null!;

    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Capacity { get; set; } = 2;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<RoomRate> Rates { get; set; } = new List<RoomRate>();
}
