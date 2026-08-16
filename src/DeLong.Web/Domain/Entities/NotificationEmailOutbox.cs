using System.ComponentModel.DataAnnotations;

namespace DeLong.Web.Domain.Entities;

public sealed class NotificationEmailOutbox : EntityBase
{
    public Guid PropertyId { get; set; }
    public Property Property { get; set; } = null!;

    public Guid NotificationId { get; set; }
    public PropertyNotification Notification { get; set; } = null!;

    [MaxLength(2000)]
    public string ToRecipients { get; set; } = string.Empty;

    [MaxLength(300)]
    public string Subject { get; set; } = string.Empty;

    public string BodyText { get; set; } = string.Empty;

    public int AttemptCount { get; set; }
    public DateTime NextAttemptAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? SentAtUtc { get; set; }

    [MaxLength(2000)]
    public string? LastError { get; set; }
}
