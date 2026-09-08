using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;

namespace DeLong.Web.Features.Pricing;

public static class PricingPolicyRules
{
    public static PricingDayPolicy Resolve(DateOnly date, int weekendMask, IReadOnlyList<SpecialPricingDay> rules)
    {
        var special = rules.FirstOrDefault(x => x.StartDate <= date && date <= x.EndDate);
        var automatic = (weekendMask & (1 << (int)date.DayOfWeek)) != 0
            ? PricingDayProfile.Weekend
            : PricingDayProfile.Weekday;
        var profile = special?.BasePriceProfile is PricingDayProfile.Weekday or PricingDayProfile.Weekend
            ? special.BasePriceProfile
            : automatic;

        return new PricingDayPolicy(date, profile, special?.Id, special?.Name, special?.SurchargePercent ?? 0,
            special?.BookingMode ?? SpecialDayBookingMode.Normal, special?.AllowThreeSlotCombo ?? true);
    }

    public static bool Overlaps(DateOnly startDate, DateOnly endDate, DateOnly existingStartDate, DateOnly existingEndDate) =>
        existingStartDate <= endDate && startDate <= existingEndDate;
}
