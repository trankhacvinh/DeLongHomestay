namespace DeLong.Web.Domain.Entities;

public sealed class PropertyPricingSettings : EntityBase
{
    public Guid PropertyId { get; set; }
    public Property Property { get; set; } = null!;
    public bool ThreeSlotDiscountEnabled { get; set; } = true;
    public int ThreeSlotCount { get; set; } = 3;
    public decimal ThreeSlotDiscountPercent { get; set; } = 10;
    public int WeekendDayMask { get; set; } = (1 << (int)DayOfWeek.Saturday) | (1 << (int)DayOfWeek.Sunday);
    public Guid? UpdatedByUserId { get; set; }
}
