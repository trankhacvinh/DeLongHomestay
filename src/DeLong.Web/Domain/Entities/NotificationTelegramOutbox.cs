using System.ComponentModel.DataAnnotations;

namespace DeLong.Web.Domain.Entities;

public sealed class NotificationTelegramOutbox : EntityBase
{
    public Guid PropertyId { get; set; }
    public Property Property { get; set; } = null!;

    public Guid NotificationId { get; set; }
    public PropertyNotification Notification { get; set; } = null!;

    [MaxLength(2000)]
    public string ChatIds { get; set; } = string.Empty;

    [MaxLength(4096)]
    public string MessageText { get; set; } = string.Empty;

    public int AttemptCount { get; set; }
    public DateTime NextAttemptAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? SentAtUtc { get; set; }

    [MaxLength(2000)]
    public string? LastError { get; set; }
}
