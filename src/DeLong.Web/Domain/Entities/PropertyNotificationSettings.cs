using System.ComponentModel.DataAnnotations;

namespace DeLong.Web.Domain.Entities;

public sealed class PropertyNotificationSettings : EntityBase
{
    public Guid PropertyId { get; set; }
    public Property Property { get; set; } = null!;

    public bool InAppBookingEnabled { get; set; } = true;
    public bool EmailBookingEnabled { get; set; }

    [MaxLength(2000)]
    public string? EmailRecipients { get; set; }

    [MaxLength(300)]
    public string? SmtpHost { get; set; }

    public int SmtpPort { get; set; } = 587;
    public bool SmtpUseSsl { get; set; } = true;

    [MaxLength(300)]
    public string? SmtpUsername { get; set; }

    public string? SmtpPasswordProtected { get; set; }

    [MaxLength(320)]
    public string? SmtpFromEmail { get; set; }

    [MaxLength(240)]
    public string? SmtpFromName { get; set; }

    [MaxLength(2000)]
    public string? LastEmailError { get; set; }

    public DateTime? LastEmailErrorAtUtc { get; set; }
    public DateTime? LastEmailSentAtUtc { get; set; }
}
