using System.ComponentModel.DataAnnotations;

namespace DeLong.Web.Domain.Entities;

public sealed class PropertyNotification : EntityBase
{
    public Guid PropertyId { get; set; }
    public Property Property { get; set; } = null!;

    public Guid? BookingId { get; set; }
    public Booking? Booking { get; set; }

    [MaxLength(50)]
    public string Type { get; set; } = "booking-requested";

    [MaxLength(240)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1200)]
    public string Message { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? ActionUrl { get; set; }

    public ICollection<PropertyNotificationRead> Reads { get; set; } = new List<PropertyNotificationRead>();
}
