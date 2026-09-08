using DeLong.Web.Domain.Enums;

namespace DeLong.Web.Domain.Entities;

public sealed class SpecialPricingDay : EntityBase
{
    public Guid PropertyId { get; set; }
    public Property Property { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Name { get; set; } = string.Empty;
    public SpecialDayCategory Category { get; set; } = SpecialDayCategory.Special;
    public PricingDayProfile BasePriceProfile { get; set; } = PricingDayProfile.Automatic;
    public decimal SurchargePercent { get; set; }
    public SpecialDayBookingMode BookingMode { get; set; } = SpecialDayBookingMode.Normal;
    public bool AllowThreeSlotCombo { get; set; } = true;
    public string? Note { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsArchived { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}
