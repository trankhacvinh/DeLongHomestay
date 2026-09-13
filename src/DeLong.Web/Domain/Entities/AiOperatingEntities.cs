using DeLong.Web.Domain.Enums;

namespace DeLong.Web.Domain.Entities;

public sealed class PropertyAiKnowledgeSnapshot : EntityBase
{
    public Guid PropertyId { get; set; }
    public long Version { get; set; } = 1;
    public string ContentJson { get; set; } = "{}";
    public string ContentHash { get; set; } = string.Empty;
    public DateTime BuiltAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsDirty { get; set; } = true;
}

public sealed class AiResponseCache : EntityBase
{
    public Guid PropertyId { get; set; }
    public AiAudience Audience { get; set; }
    public string CacheKey { get; set; } = string.Empty;
    public string Intent { get; set; } = string.Empty;
    public string ParametersJson { get; set; } = "{}";
    public string ResponseJson { get; set; } = "{}";
    public string InvalidationTag { get; set; } = string.Empty;
    public long DataVersion { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime LastHitAtUtc { get; set; } = DateTime.UtcNow;
    public int HitCount { get; set; }
}

public sealed class AiConversationSummary : EntityBase
{
    public Guid ConversationId { get; set; }
    public string Summary { get; set; } = string.Empty;
    public Guid? LastMessageId { get; set; }
    public int CoveredMessageCount { get; set; }
}

public sealed class AiToolExecutionLog : EntityBase
{
    public Guid PropertyId { get; set; }
    public Guid? ConversationId { get; set; }
    public Guid? UserId { get; set; }
    public AiAudience Audience { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public string ParametersJson { get; set; } = "{}";
    public bool IsSuccess { get; set; }
    public string? ErrorCode { get; set; }
    public long DurationMs { get; set; }
}

public sealed class AiBookingDraft : EntityBase
{
    public Guid PropertyId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public string StateJson { get; set; } = "{}";
    public DateTime ExpiresAtUtc { get; set; }
}

public sealed class AiUsageReservation : EntityBase
{
    public Guid PropertyId { get; set; }
    public AiAudience Audience { get; set; }
    public int ReservedTokens { get; set; }
    public decimal ReservedCostUsd { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
}
