namespace DeLong.Web.Features.PublicBooking;

public sealed record PublicSlotPricingItem(DateOnly ServiceDate, int RateIndex, decimal ListPrice);
public sealed record PublicSlotPricingResult(decimal AppliedAmount, string PricingRule);

public static class PublicSlotPricingCalculator
{
    public static IReadOnlyList<PublicSlotPricingResult> Calculate(
        IReadOnlyList<PublicSlotPricingItem> slots,
        int ratesPerDay,
        bool fullDayPricingEnabled,
        decimal? fullDayPrice,
        IReadOnlyList<MultiSlotDiscountTierDto> tiers)
    {
        var results = slots.Select(x => new MutableResult(x.ListPrice)).ToList();
        var fullDayIndexes = new HashSet<int>();
        if (fullDayPricingEnabled && fullDayPrice is > 0 && ratesPerDay > 0)
        {
            foreach (var group in slots.Select((slot, index) => (slot, index)).GroupBy(x => x.slot.ServiceDate))
            {
                var day = group.OrderBy(x => x.slot.RateIndex).ToList();
                if (day.Count != ratesPerDay || day.Select(x => x.slot.RateIndex).Distinct().Count() != ratesPerDay) continue;
                Allocate(day.Select(x => x.index).ToList(), slots, results, fullDayPrice.Value);
                foreach (var item in day) fullDayIndexes.Add(item.index);
            }
        }

        var remaining = Enumerable.Range(0, slots.Count).Where(x => !fullDayIndexes.Contains(x)).ToList();
        var tier = tiers.Where(x => x.MinimumSlots <= remaining.Count).OrderByDescending(x => x.MinimumSlots).FirstOrDefault();
        if (tier is not null && tier.DiscountPercent > 0)
        {
            var factor = (100m - tier.DiscountPercent) / 100m;
            foreach (var index in remaining)
            {
                results[index].Amount = decimal.Round(slots[index].ListPrice * factor, 0, MidpointRounding.AwayFromZero);
                results[index].Rule = $"multi-slot-{tier.MinimumSlots}-{tier.DiscountPercent:0.##}";
            }
        }

        return results.Select(x => new PublicSlotPricingResult(x.Amount, x.Rule)).ToList();
    }

    private static void Allocate(
        IReadOnlyList<int> indexes,
        IReadOnlyList<PublicSlotPricingItem> slots,
        IReadOnlyList<MutableResult> results,
        decimal total)
    {
        var listTotal = indexes.Sum(x => slots[x].ListPrice);
        var allocated = 0m;
        for (var position = 0; position < indexes.Count; position++)
        {
            var index = indexes[position];
            var amount = position == indexes.Count - 1
                ? total - allocated
                : listTotal > 0
                    ? decimal.Round(total * slots[index].ListPrice / listTotal, 0, MidpointRounding.AwayFromZero)
                    : decimal.Round(total / indexes.Count, 0, MidpointRounding.AwayFromZero);
            results[index].Amount = amount;
            results[index].Rule = "full-day";
            allocated += amount;
        }
    }

    private sealed class MutableResult(decimal amount)
    {
        public decimal Amount { get; set; } = amount;
        public string Rule { get; set; } = "standard";
    }
}
