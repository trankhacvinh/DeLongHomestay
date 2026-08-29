namespace DeLong.Web.Domain.Entities;

public sealed class BookingRateSegment : EntityBase
{
    public Guid BookingId { get; set; }
    public Booking Booking { get; set; } = null!;

    public Guid? RoomRateId { get; set; }
    public RoomRate? RoomRate { get; set; }

    public DateOnly ServiceDate { get; set; }
    public DateTime CheckInUtc { get; set; }
    public DateTime CheckOutUtc { get; set; }
    public int SortOrder { get; set; }
    public string RateName { get; set; } = string.Empty;
    public decimal ListPrice { get; set; }
    public decimal AppliedAmount { get; set; }
    public string PricingRule { get; set; } = "standard";
}
