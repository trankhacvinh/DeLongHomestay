using DeLong.Web.Domain.Enums;

namespace DeLong.Web.Features.Pricing;

public static class PricingCalculator
{
    public static (BookingPricingResult? Result, PricingOperationError? Error) Calculate(
        IReadOnlyList<PricingSelectionInput> selected,
        PricingCalculationSettings settings,
        IReadOnlyDictionary<DateOnly, PricingDayPolicy> policies)
    {
        if (selected.Count == 0)
            return (null, new("pricing_selection_empty", "Vui lòng chọn ít nhất một khung giờ."));
        if (settings.ActiveRatesPerDay <= 0 || settings.MultiSlotCount is < 2 or > 20 || settings.MultiSlotDiscountPercent is < 0 or > 100)
            return (null, new("pricing_configuration_invalid", "Cấu hình giá hoặc combo không hợp lệ."));

        var output = new List<PricingSelectionResult>(selected.Count);
        foreach (var dayGroup in selected.GroupBy(x => x.ServiceDate).OrderBy(x => x.Key))
        {
            if (!policies.TryGetValue(dayGroup.Key, out var policy))
                return (null, new("pricing_policy_missing", "Không xác định được loại ngày để tính giá."));
            var day = dayGroup.OrderBy(x => x.RateIndex).ToList();
            if (day.Select(x => x.RateIndex).Distinct().Count() != day.Count)
                return (null, new("pricing_selection_duplicate", "Danh sách khung giờ bị trùng."));
            if (day.Any(x => x.RateIndex < 0 || x.RateIndex >= settings.ActiveRatesPerDay))
                return (null, new("pricing_selection_invalid", "Danh sách khung giờ không thuộc cấu hình hiện tại."));
            if (policy.SurchargePercent is < 0 or > 100)
                return (null, new("pricing_policy_invalid", "Mức phụ thu của ngày đặc biệt không hợp lệ."));
            var isFullDay = day.Count == settings.ActiveRatesPerDay && day.Select(x => x.RateIndex).Order().SequenceEqual(Enumerable.Range(0, settings.ActiveRatesPerDay));
            if (policy.BookingMode == SpecialDayBookingMode.FullDayOnly && !isFullDay)
                return (null, new("full_day_only", $"{policy.SpecialDayName ?? dayGroup.Key.ToString("dd/MM/yyyy")} chỉ cho phép đặt combo cả ngày."));

            var listPrices = day.Select(x => ResolveRatePrice(x, policy.DayProfile)).ToArray();
            if (listPrices.Any(x => x <= 0))
                return (null, new("weekend_price_missing", "Một khung giờ chưa được cấu hình giá cho loại ngày đã chọn."));

            var applied = listPrices.ToArray();
            var comboPercent = 0m;
            var pricingRule = policy.DayProfile == PricingDayProfile.Weekend ? "weekend" : "weekday";
            if (isFullDay && settings.FullDayPricingEnabled)
            {
                var fullDayPrice = policy.DayProfile == PricingDayProfile.Weekend && !settings.UseWeekdayFullDayPriceOnWeekend
                    ? settings.WeekendFullDayPrice
                    : settings.WeekdayFullDayPrice;
                if (fullDayPrice is not > 0)
                    return (null, new("full_day_price_missing", "Phòng chưa được cấu hình giá combo cả ngày cho loại ngày đã chọn."));
                Allocate(applied, listPrices, fullDayPrice.Value);
                pricingRule = policy.DayProfile == PricingDayProfile.Weekend ? "weekend-full-day" : "weekday-full-day";
            }
            else if (settings.MultiSlotDiscountEnabled && policy.AllowThreeSlotCombo && day.Count == settings.MultiSlotCount)
            {
                comboPercent = settings.MultiSlotDiscountPercent;
                var factor = (100m - comboPercent) / 100m;
                var comboTotal = decimal.Round(listPrices.Sum() * factor, 0, MidpointRounding.AwayFromZero);
                Allocate(applied, listPrices, comboTotal);
                pricingRule = $"{pricingRule}-combo-{settings.MultiSlotCount}-{comboPercent:0.##}";
            }

            var surcharges = new decimal[applied.Length];
            var surchargeTotal = decimal.Round(applied.Sum() * policy.SurchargePercent / 100m, 0, MidpointRounding.AwayFromZero);
            if (surchargeTotal > 0)
                Allocate(surcharges, applied, surchargeTotal);
            for (var index = 0; index < day.Count; index++)
            {
                var comboDiscount = listPrices[index] - applied[index];
                output.Add(new PricingSelectionResult(
                    day[index].ServiceDate, day[index].RateId, listPrices[index], applied[index], surcharges[index],
                    comboPercent, comboDiscount, policy.DayProfile, policy.SpecialPricingDayId,
                    policy.SpecialDayName, policy.SurchargePercent, pricingRule));
            }
        }

        var roomAmount = output.Sum(x => x.AppliedAmount);
        var surchargeAmount = output.Sum(x => x.SpecialSurchargeAmount);
        return (new BookingPricingResult(output, roomAmount, surchargeAmount, roomAmount + surchargeAmount), null);
    }

    private static decimal ResolveRatePrice(PricingSelectionInput input, PricingDayProfile profile) =>
        profile == PricingDayProfile.Weekend && !input.UseWeekdayPriceOnWeekend
            ? input.WeekendPrice ?? 0
            : input.WeekdayPrice;

    private static void Allocate(decimal[] output, decimal[] weights, decimal total)
    {
        var totalWeight = weights.Sum();
        var allocated = 0m;
        for (var index = 0; index < output.Length; index++)
        {
            output[index] = index == output.Length - 1
                ? total - allocated
                : totalWeight > 0
                    ? decimal.Round(total * weights[index] / totalWeight, 0, MidpointRounding.AwayFromZero)
                    : decimal.Round(total / output.Length, 0, MidpointRounding.AwayFromZero);
            allocated += output[index];
        }
    }
}
