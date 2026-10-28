namespace DeLong.Web.Domain.Entities;

public sealed class CashReceipt : EntityBase
{
    public Guid PropertyId { get; set; }
    public Property Property { get; set; } = null!;
    public Guid? RoomId { get; set; }
    public Room? Room { get; set; }
    public Guid RequestKey { get; set; }
    public string RequestHash { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Note { get; set; }
    public Guid? RecordedByUserId { get; set; }
    public bool IsVoided { get; set; }
    public DateTime? VoidedAtUtc { get; set; }
    public Guid? VoidedByUserId { get; set; }
    public string? VoidReason { get; set; }
}
