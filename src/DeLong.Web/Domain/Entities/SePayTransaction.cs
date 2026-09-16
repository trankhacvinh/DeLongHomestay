namespace DeLong.Web.Domain.Entities;

public sealed class SePayTransaction : EntityBase
{
    public Guid PropertyId { get; set; }
    public long TransactionId { get; set; }
    public Guid? IntentId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public string? ResolutionNote { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
}
