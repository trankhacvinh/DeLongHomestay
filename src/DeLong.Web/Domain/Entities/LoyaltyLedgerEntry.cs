namespace DeLong.Web.Domain.Entities;

public sealed class LoyaltyLedgerEntry : EntityBase
{
    public Guid UserId { get; set; }
    public Identity.ApplicationUser User { get; set; } = null!;
    public Guid PropertyId { get; set; }
    public Property Property { get; set; } = null!;
    public Guid? BookingId { get; set; }
    public Booking? Booking { get; set; }
    public int Points { get; set; }
    public string Reason { get; set; } = string.Empty;
}
