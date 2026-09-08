using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Pricing;
using Xunit;

namespace DeLong.Tests.Unit;

public sealed class PricingPolicyRulesTests
{
    private const int SaturdaySundayMask = (1 << (int)DayOfWeek.Saturday) | (1 << (int)DayOfWeek.Sunday);

    [Theory]
    [InlineData(2027, 1, 1, PricingDayProfile.Weekday)]
    [InlineData(2027, 1, 2, PricingDayProfile.Weekend)]
    [InlineData(2027, 1, 3, PricingDayProfile.Weekend)]
    [InlineData(2027, 1, 4, PricingDayProfile.Weekday)]
    public void Automatic_profile_uses_configured_weekend_mask(int year, int month, int day, PricingDayProfile expected)
    {
        var policy = PricingPolicyRules.Resolve(new DateOnly(year, month, day), SaturdaySundayMask, []);

        Assert.Equal(expected, policy.DayProfile);
    }

    [Fact]
    public void Automatic_special_day_keeps_the_calendar_profile_and_applies_business_flags()
    {
        var saturday = new DateOnly(2027, 1, 2);
        var special = Special(saturday, saturday, PricingDayProfile.Automatic, "Tết", 15m,
            SpecialDayBookingMode.FullDayOnly, false);

        var policy = PricingPolicyRules.Resolve(saturday, SaturdaySundayMask, [special]);

        Assert.Equal(PricingDayProfile.Weekend, policy.DayProfile);
        Assert.Equal(special.Id, policy.SpecialPricingDayId);
        Assert.Equal("Tết", policy.SpecialDayName);
        Assert.Equal(15m, policy.SurchargePercent);
        Assert.Equal(SpecialDayBookingMode.FullDayOnly, policy.BookingMode);
        Assert.False(policy.AllowThreeSlotCombo);
    }

    [Theory]
    [InlineData(PricingDayProfile.Weekday)]
    [InlineData(PricingDayProfile.Weekend)]
    public void Special_day_can_explicitly_override_the_calendar_profile(PricingDayProfile overrideProfile)
    {
        var saturday = new DateOnly(2027, 1, 2);
        var special = Special(saturday, saturday, overrideProfile, "Giá riêng");

        var policy = PricingPolicyRules.Resolve(saturday, SaturdaySundayMask, [special]);

        Assert.Equal(overrideProfile, policy.DayProfile);
    }

    [Theory]
    [InlineData(1, 3, 3, 5, true)]
    [InlineData(1, 3, 1, 1, true)]
    [InlineData(1, 3, 3, 3, true)]
    [InlineData(1, 3, 4, 5, false)]
    [InlineData(4, 5, 1, 3, false)]
    public void Date_range_overlap_is_inclusive(int start, int end, int existingStart, int existingEnd, bool expected)
    {
        var month = new DateOnly(2027, 1, 1);

        var result = PricingPolicyRules.Overlaps(
            month.AddDays(start - 1), month.AddDays(end - 1),
            month.AddDays(existingStart - 1), month.AddDays(existingEnd - 1));

        Assert.Equal(expected, result);
    }

    private static SpecialPricingDay Special(
        DateOnly start,
        DateOnly end,
        PricingDayProfile profile,
        string name,
        decimal surcharge = 0m,
        SpecialDayBookingMode mode = SpecialDayBookingMode.Normal,
        bool allowThreeSlotCombo = true) =>
        new()
        {
            StartDate = start,
            EndDate = end,
            BasePriceProfile = profile,
            Name = name,
            SurchargePercent = surcharge,
            BookingMode = mode,
            AllowThreeSlotCombo = allowThreeSlotCombo
        };
}
