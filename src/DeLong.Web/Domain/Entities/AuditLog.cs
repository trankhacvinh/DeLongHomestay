namespace DeLong.Web.Domain.Entities;

public sealed class AuditLog : EntityBase
{
    public Guid PropertyId { get; set; }
    public Property Property { get; set; } = null!;

    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public Guid? ActorUserId { get; set; }
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
}
