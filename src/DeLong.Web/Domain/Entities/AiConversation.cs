namespace DeLong.Web.Domain.Entities;

public sealed class AiConversation : EntityBase
{
    public Guid PropertyId { get; set; }
    public Property Property { get; set; } = null!;
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public ICollection<AiMessage> Messages { get; set; } = [];
}

public sealed class AiMessage : EntityBase
{
    public Guid ConversationId { get; set; }
    public AiConversation Conversation { get; set; } = null!;
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? ToolName { get; set; }
    public Guid? ProposalId { get; set; }
}

