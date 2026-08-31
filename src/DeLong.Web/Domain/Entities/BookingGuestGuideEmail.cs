using System.ComponentModel.DataAnnotations;

namespace DeLong.Web.Domain.Entities;

public sealed class BookingGuestGuideEmail : EntityBase
{
    public Guid PropertyId { get; set; }
    public Property Property { get; set; } = null!;

    public Guid BookingId { get; set; }
    public Booking Booking { get; set; } = null!;

    [MaxLength(320)]
    public string RecipientEmail { get; set; } = string.Empty;

    [MaxLength(40)]
    public string Trigger { get; set; } = "AutomaticConfirmation";

    [MaxLength(40)]
    public string TemplateKey { get; set; } = "CheckInGuide";

    [MaxLength(300)]
    public string Subject { get; set; } = string.Empty;

    public string BodyText { get; set; } = string.Empty;
    public string? BodyHtml { get; set; }

    public Guid? RequestedByUserId { get; set; }
    public int AttemptCount { get; set; }
    public DateTime NextAttemptAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? SentAtUtc { get; set; }

    [MaxLength(2000)]
    public string? LastError { get; set; }
}
