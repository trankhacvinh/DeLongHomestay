using DeLong.Web.Identity;

namespace DeLong.Web.Domain.Entities;

public sealed class PropertyNotificationRead
{
    public Guid NotificationId { get; set; }
    public PropertyNotification Notification { get; set; } = null!;

    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public DateTime ReadAtUtc { get; set; } = DateTime.UtcNow;
}
