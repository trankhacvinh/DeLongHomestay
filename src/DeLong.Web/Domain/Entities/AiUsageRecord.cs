using DeLong.Web.Domain.Enums;

namespace DeLong.Web.Domain.Entities;

public sealed class AiUsageRecord : EntityBase
{
    public Guid PropertyId { get; set; }
    public Guid UserId { get; set; }
    public Guid? ConversationId { get; set; }
    public AiProviderKind Provider { get; set; }
    public string Model { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public decimal EstimatedCostUsd { get; set; }
    public long DurationMs { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorCode { get; set; }
}
