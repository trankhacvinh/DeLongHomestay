namespace DeLong.Web.Features.Notifications;

public sealed record NotificationItemDto(
    Guid Id,
    string Type,
    Guid? BookingId,
    string Title,
    string Message,
    string? ActionUrl,
    DateTime CreatedAtUtc,
    bool IsRead);

public sealed record NotificationFeedDto(
    IReadOnlyList<NotificationItemDto> Items,
    int UnreadCount);

public sealed record NotificationSettingsDto(
    bool InAppBookingEnabled,
    bool EmailBookingEnabled,
    string EmailRecipients,
    string SmtpHost,
    int SmtpPort,
    bool SmtpUseSsl,
    string SmtpUsername,
    bool SmtpPasswordConfigured,
    string SmtpFromEmail,
    string SmtpFromName,
    DateTime? LastEmailSentAtUtc,
    string? LastEmailError,
    DateTime? LastEmailErrorAtUtc);

public sealed class UpdateNotificationSettingsRequest
{
    public bool InAppBookingEnabled { get; set; } = true;
    public bool EmailBookingEnabled { get; set; }
    public string? EmailRecipients { get; set; }
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public bool SmtpUseSsl { get; set; } = true;
    public string? SmtpUsername { get; set; }
    public string? SmtpPassword { get; set; }
    public bool ClearSmtpPassword { get; set; }
    public string? SmtpFromEmail { get; set; }
    public string? SmtpFromName { get; set; }
}

public sealed record NotificationRealtimeEvent(
    Guid NotificationId,
    Guid PropertyId,
    string Type,
    DateTime CreatedAtUtc);

public sealed record SmtpDeliveryProfile(
    string Host,
    int Port,
    bool UseSsl,
    string? Username,
    string? Password,
    string FromEmail,
    string FromName);

public sealed record NotificationOperationError(string Code, string Message);
