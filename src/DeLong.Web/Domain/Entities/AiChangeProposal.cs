using DeLong.Web.Domain.Enums;

namespace DeLong.Web.Domain.Entities;

public sealed class AiChangeProposal : EntityBase
{
    public Guid PropertyId { get; set; }
    public Guid UserId { get; set; }
    public Guid ConversationId { get; set; }
    public AiProposalType Type { get; set; }
    public AiProposalStatus Status { get; set; } = AiProposalStatus.Pending;
    public string Summary { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? AppliedAtUtc { get; set; }
    public DateTime? RejectedAtUtc { get; set; }
    public string? FailureReason { get; set; }
}
