namespace DeLong.Web.Domain.Entities;

public sealed class DataQualityIssue : EntityBase
{
    public Guid PropertyId { get; set; }
    public Property Property { get; set; } = null!;
    public string Fingerprint { get; set; } = string.Empty;
    public string Rule { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public Guid BookingId { get; set; }
    public Guid? RelatedBookingId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string EvidenceJson { get; set; } = "{}";
    public string EvidenceHash { get; set; } = string.Empty;
    public string Status { get; set; } = "Open";
    public bool IsDetected { get; set; } = true;
    public DateTime LastCheckedAtUtc { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewNote { get; set; }
}
