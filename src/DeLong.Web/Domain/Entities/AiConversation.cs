namespace DeLong.Web.Domain.Entities;

public sealed class AiConversation : EntityBase
{
    public Guid PropertyId { get; set; }
    public Property Property { get; set; } = null!;
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public ICollection<AiMessage> Messages { get; set; } = [];
    public ICollection<AiAttachment> Attachments { get; set; } = [];
}

public sealed class AiAttachment : EntityBase
{
    public Guid ConversationId { get; set; }
    public AiConversation Conversation { get; set; } = null!;
    public Guid UploadedByUserId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public byte[] Content { get; set; } = [];
    public string? ExtractedText { get; set; }
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
