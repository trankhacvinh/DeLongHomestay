namespace DeLong.Web.Domain.Entities;

public sealed class RoomHighlight : EntityBase
{
    public Guid RoomId { get; set; }
    public Room Room { get; set; } = null!;
    public string Text { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
