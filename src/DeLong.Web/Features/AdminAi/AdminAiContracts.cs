using DeLong.Web.Domain.Enums;

namespace DeLong.Web.Features.AdminAi;

public sealed record AiProfileDto(bool IsEnabled, AiProviderKind Provider, string Model, bool ApiKeyConfigured, int MaxOutputTokens, int MonthlyTokenLimit);
public sealed record SaveAiProfileRequest(bool IsEnabled, AiProviderKind Provider, string Model, string? ApiKey, bool ClearApiKey, int MaxOutputTokens, int MonthlyTokenLimit);
public sealed record AiChatRequest(Guid? ConversationId, string Message);
public sealed record AiChatResponse(Guid ConversationId, string Message, AiProposalDto? Proposal, AiUsageSummaryDto Usage, bool CanRetry = false);
public sealed record AiProposalDto(Guid Id, AiProposalType Type, AiProposalStatus Status, string Summary, object Payload, DateTime ExpiresAtUtc, string? FailureReason);
public sealed record AiUsageSummaryDto(int Calls, long InputTokens, long OutputTokens, long TotalTokens, int MonthlyTokenLimit);
public sealed record AiMessageDto(Guid Id, string Role, string Content, DateTime CreatedAtUtc, Guid? ProposalId);
public sealed record AiConversationDto(Guid Id, string Title, DateTime UpdatedAtUtc);
public sealed record AiProviderResult(string Text, int InputTokens, int OutputTokens, string? ResponseId, string? FinishReason = null);
