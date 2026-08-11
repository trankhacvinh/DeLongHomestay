using DeLong.Web.Domain.Enums;

namespace DeLong.Web.Domain.Entities;

public sealed class Booking : EntityBase
{
    public Guid PropertyId { get; set; }
    public Property Property { get; set; } = null!;

    public Guid RoomId { get; set; }
    public Room Room { get; set; } = null!;

    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public string Code { get; set; } = string.Empty;
    public DateTime CheckInUtc { get; set; }
    public DateTime CheckOutUtc { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Held;
    public decimal RoomAmount { get; set; }
    public decimal ExtraAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? Source { get; set; }
    public string? Note { get; set; }

    public ICollection<Payment> Payments { get; set; } = [];

    public decimal TotalAmount => RoomAmount + ExtraAmount - DiscountAmount;
}
