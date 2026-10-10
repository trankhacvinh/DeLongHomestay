namespace DeLong.Web.Domain.Entities;

public sealed class RoomBookingBlock : EntityBase
{
    public Guid PropertyId { get; set; }
    public Guid RoomId { get; set; }
    public Room Room { get; set; } = null!;
    public Guid BatchId { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public bool RepeatDaily { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid? CreatedByUserId { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public Guid? CancelledByUserId { get; set; }
}
