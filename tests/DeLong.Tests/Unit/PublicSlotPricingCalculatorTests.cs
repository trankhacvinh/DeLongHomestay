using DeLong.Web.Features.PublicBooking;
using Xunit;

namespace DeLong.Tests.Unit;

public sealed class PublicSlotPricingCalculatorTests
{
    [Fact]
    public void Full_day_price_replaces_all_four_slot_prices()
    {
        var date = new DateOnly(2026, 8, 26);
        var slots = Enumerable.Range(0, 4).Select(index => new PublicSlotPricingItem(date, index, 250_000m)).ToList();

        var result = PublicSlotPricingCalculator.Calculate(slots, 4, true, 800_000m, [new(2, 10m)]);

        Assert.Equal(800_000m, result.Sum(x => x.AppliedAmount));
        Assert.All(result, x => Assert.Equal("full-day", x.PricingRule));
    }

    [Fact]
    public void Highest_matching_tier_applies_only_to_non_full_day_slots()
    {
        var firstDate = new DateOnly(2026, 8, 26);
        var slots = Enumerable.Range(0, 4).Select(index => new PublicSlotPricingItem(firstDate, index, 250_000m))
            .Append(new PublicSlotPricingItem(firstDate.AddDays(1), 0, 200_000m))
            .Append(new PublicSlotPricingItem(firstDate.AddDays(1), 1, 200_000m))
            .ToList();

        var result = PublicSlotPricingCalculator.Calculate(slots, 4, true, 800_000m, [new(2, 5m), new(3, 10m)]);

        Assert.Equal(1_180_000m, result.Sum(x => x.AppliedAmount));
        Assert.Equal("full-day", result[0].PricingRule);
        Assert.Equal("multi-slot-2-5", result[4].PricingRule);
    }
}
