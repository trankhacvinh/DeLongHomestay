using DeLong.Web.Domain.Enums;

namespace DeLong.Web.Features.AdminAi;

public sealed record AiProfileDto(bool IsEnabled, AiProviderKind Provider, string Model, bool ApiKeyConfigured, int MaxOutputTokens, int MonthlyTokenLimit,
    decimal MonthlyBudgetUsd, decimal InputCostPerMillionTokensUsd, decimal OutputCostPerMillionTokensUsd, int BudgetWarningPercent,
    bool IsPublicAiEnabled, int AdminBudgetReservePercent, int PublicRequestsPerMinute, int PublicRequestsPerDay);
public sealed record SaveAiProfileRequest(bool IsEnabled, AiProviderKind Provider, string Model, string? ApiKey, bool ClearApiKey, int MaxOutputTokens, int MonthlyTokenLimit,
    decimal MonthlyBudgetUsd, decimal InputCostPerMillionTokensUsd, decimal OutputCostPerMillionTokensUsd, int BudgetWarningPercent,
    bool IsPublicAiEnabled, int AdminBudgetReservePercent, int PublicRequestsPerMinute, int PublicRequestsPerDay);
public sealed record AiChatRequest(Guid? ConversationId, string Message, IReadOnlyList<Guid>? AttachmentIds = null);
public sealed record AiChatResponse(Guid ConversationId, string Message, AiProposalDto? Proposal, AiUsageSummaryDto Usage, bool CanRetry = false);
public sealed record AiProposalDto(Guid Id, AiProposalType Type, AiProposalStatus Status, string Summary, object Payload, DateTime ExpiresAtUtc, string? FailureReason);
public sealed record AiUsageSummaryDto(int Calls, long InputTokens, long OutputTokens, long TotalTokens, int MonthlyTokenLimit,
    decimal EstimatedCostUsd, decimal MonthlyBudgetUsd, decimal RemainingBudgetUsd, int UsedBudgetPercent, int BudgetWarningPercent, bool IsBudgetWarning, bool IsBudgetExceeded,
    int SuccessfulCalls, int FailedCalls, int BlockedCalls, int CacheHits, long CachedInputTokens, long AverageDurationMs,
    int ActiveReservations, long ReservedTokens, decimal ReservedCostUsd, IReadOnlyList<AiAudienceUsageDto> ByAudience);
public sealed record AiAudienceUsageDto(AiAudience Audience, int Calls, int SuccessfulCalls, int FailedCalls, int BlockedCalls,
    long InputTokens, long OutputTokens, long CachedInputTokens, int CacheHits, long AverageDurationMs, decimal EstimatedCostUsd);
public sealed record AiMessageDto(Guid Id, string Role, string Content, DateTime CreatedAtUtc, Guid? ProposalId, AiProposalDto? Proposal = null);
public sealed record AiConversationDto(Guid Id, string Title, DateTime UpdatedAtUtc);
public sealed record AiAttachmentDto(Guid Id, string FileName, string ContentType, long SizeBytes, DateTime CreatedAtUtc);
public sealed record AiProviderResult(string Text, int InputTokens, int OutputTokens, string? ResponseId, string? FinishReason = null, int CachedInputTokens = 0);
public sealed record AiProviderAttachment(string FileName, string ContentType, byte[] Content, string? ExtractedText);
public sealed record AiKnowledgeSnapshotDto(long Version, string ContentHash, DateTime BuiltAtUtc, bool IsDirty, object Content);
