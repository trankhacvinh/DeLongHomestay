using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Pricing;
using Xunit;

namespace DeLong.Tests.Unit;

public sealed class PricingCalculatorTests
{
    private static readonly DateOnly Monday = new(2027, 1, 4);
    private static readonly DateOnly Saturday = new(2027, 1, 2);

    [Fact]
    public void Empty_selection_is_rejected()
    {
        var (_, error) = Calculate([], Settings(), []);

        Assert.Equal("pricing_selection_empty", error?.Code);
    }

    [Fact]
    public void Invalid_configuration_is_rejected()
    {
        var (_, error) = Calculate(Slots(Monday, 1), Settings(activeRatesPerDay: 0), [Policy(Monday)]);

        Assert.Equal("pricing_configuration_invalid", error?.Code);
    }

    [Theory]
    [InlineData(false, 250, 320, 320)]
    [InlineData(true, 250, 320, 250)]
    public void Weekend_uses_the_configured_rate_source(bool useWeekdayOnWeekend, decimal weekday, decimal weekend, decimal expected)
    {
        var selected = Slots(Saturday, 1, weekday, weekend, useWeekdayOnWeekend);

        var (result, error) = Calculate(selected, Settings(), [Policy(Saturday, PricingDayProfile.Weekend)]);

        Assert.Null(error);
        Assert.Equal(expected, result!.RoomAmount);
        Assert.Equal("weekend", result.Segments.Single().PricingRule);
    }

    [Fact]
    public void Missing_weekend_rate_is_rejected()
    {
        var selected = Slots(Saturday, 1, 250m, null, false);

        var (_, error) = Calculate(selected, Settings(), [Policy(Saturday, PricingDayProfile.Weekend)]);

        Assert.Equal("weekend_price_missing", error?.Code);
    }

    [Fact]
    public void Three_slots_receive_the_configured_discount()
    {
        var (result, error) = Calculate(Slots(Monday, 3, 100m), Settings(), [Policy(Monday)]);

        Assert.Null(error);
        Assert.Equal(270m, result!.RoomAmount);
        Assert.Equal(30m, result.Segments.Sum(x => x.ComboDiscountAmount));
        Assert.All(result.Segments, x => Assert.Equal(10m, x.ComboDiscountPercent));
    }

    [Fact]
    public void Two_slots_do_not_receive_three_slot_discount()
    {
        var (result, error) = Calculate(Slots(Monday, 2, 100m), Settings(), [Policy(Monday)]);

        Assert.Null(error);
        Assert.Equal(200m, result!.RoomAmount);
        Assert.All(result.Segments, x => Assert.Equal(0m, x.ComboDiscountAmount));
    }

    [Fact]
    public void Combo_is_evaluated_per_service_date_not_across_dates()
    {
        var nextDay = Monday.AddDays(1);
        var selected = Slots(Monday, 2, 100m).Concat(Slots(nextDay, 1, 100m)).ToList();

        var (result, error) = Calculate(selected, Settings(), [Policy(Monday), Policy(nextDay)]);

        Assert.Null(error);
        Assert.Equal(300m, result!.RoomAmount);
        Assert.All(result.Segments, x => Assert.Equal(0m, x.ComboDiscountPercent));
    }

    [Fact]
    public void Special_day_can_disable_three_slot_combo()
    {
        var (result, error) = Calculate(Slots(Monday, 3, 100m), Settings(), [Policy(Monday, allowThreeSlotCombo: false)]);

        Assert.Null(error);
        Assert.Equal(300m, result!.RoomAmount);
        Assert.All(result.Segments, x => Assert.Equal(0m, x.ComboDiscountAmount));
    }

    [Fact]
    public void Custom_combo_count_and_percent_are_honored()
    {
        var settings = Settings(multiSlotCount: 2, multiSlotDiscountPercent: 5m);

        var (result, error) = Calculate(Slots(Monday, 2, 100m), settings, [Policy(Monday)]);

        Assert.Null(error);
        Assert.Equal(190m, result!.RoomAmount);
        Assert.All(result.Segments, x => Assert.Equal(5m, x.ComboDiscountPercent));
    }

    [Fact]
    public void Combo_rounds_once_on_the_daily_total()
    {
        var selected = new[]
        {
            Slot(Monday, 0, 101m),
            Slot(Monday, 1, 102m),
            Slot(Monday, 2, 103m)
        };

        var (result, error) = Calculate(selected, Settings(), [Policy(Monday)]);

        Assert.Null(error);
        Assert.Equal(275m, result!.RoomAmount);
        Assert.Equal(31m, result.Segments.Sum(x => x.ComboDiscountAmount));
        Assert.Equal(275m, result.Segments.Sum(x => x.AppliedAmount));
    }

    [Theory]
    [InlineData(false, 780, 910, 910)]
    [InlineData(true, 780, 910, 780)]
    public void Full_day_uses_the_configured_weekend_price_source(bool useWeekdayOnWeekend, decimal weekdayFullDay, decimal weekendFullDay, decimal expected)
    {
        var settings = Settings(weekdayFullDayPrice: weekdayFullDay, useWeekdayFullDayOnWeekend: useWeekdayOnWeekend, weekendFullDayPrice: weekendFullDay);

        var (result, error) = Calculate(Slots(Saturday, 4, 250m, 320m), settings, [Policy(Saturday, PricingDayProfile.Weekend)]);

        Assert.Null(error);
        Assert.Equal(expected, result!.RoomAmount);
        Assert.All(result.Segments, x => Assert.Equal("weekend-full-day", x.PricingRule));
    }

    [Fact]
    public void Missing_weekend_full_day_price_is_rejected()
    {
        var settings = Settings(useWeekdayFullDayOnWeekend: false, weekendFullDayPrice: null);

        var (_, error) = Calculate(Slots(Saturday, 4, 250m, 320m), settings, [Policy(Saturday, PricingDayProfile.Weekend)]);

        Assert.Equal("full_day_price_missing", error?.Code);
    }

    [Fact]
    public void Full_day_requires_the_exact_active_rate_indices()
    {
        var selected = Slots(Monday, 4, 100m, startIndex: 1);

        var (result, error) = Calculate(selected, Settings(), [Policy(Monday)]);

        Assert.Null(result);
        Assert.Equal("pricing_selection_invalid", error?.Code);
    }

    [Fact]
    public void Full_day_only_rejects_partial_or_wrong_indices_and_accepts_all_active_slots()
    {
        var policy = Policy(Monday, bookingMode: SpecialDayBookingMode.FullDayOnly);

        var (_, partialError) = Calculate(Slots(Monday, 3), Settings(), [policy]);
        var (_, wrongIndicesError) = Calculate(Slots(Monday, 4, startIndex: 1), Settings(), [policy]);
        var (full, fullError) = Calculate(Slots(Monday, 4), Settings(), [policy]);

        Assert.Equal("full_day_only", partialError?.Code);
        Assert.Equal("pricing_selection_invalid", wrongIndicesError?.Code);
        Assert.Null(fullError);
        Assert.Equal(780m, full!.RoomAmount);
    }

    [Fact]
    public void Surcharge_is_applied_after_combo_discount()
    {
        var policy = Policy(Saturday, PricingDayProfile.Weekend, surchargePercent: 10m, name: "Valentine");

        var (result, error) = Calculate(Slots(Saturday, 3, 100m, 200m), Settings(), [policy]);

        Assert.Null(error);
        Assert.Equal(540m, result!.RoomAmount);
        Assert.Equal(54m, result.SpecialSurchargeAmount);
        Assert.Equal(594m, result.TotalBeforeVoucher);
        Assert.All(result.Segments, x =>
        {
            Assert.Equal("Valentine", x.SpecialDayName);
            Assert.Equal(10m, x.SpecialSurchargePercent);
        });
    }

    [Fact]
    public void Surcharge_rounds_once_on_the_daily_amount()
    {
        var selected = new[] { Slot(Monday, 0, 101m), Slot(Monday, 1, 102m) };

        var (result, error) = Calculate(selected, Settings(), [Policy(Monday, surchargePercent: 10m)]);

        Assert.Null(error);
        Assert.Equal(203m, result!.RoomAmount);
        Assert.Equal(20m, result.SpecialSurchargeAmount);
        Assert.Equal(223m, result.TotalBeforeVoucher);
    }

    [Fact]
    public void Full_day_allocation_preserves_exact_total_and_never_creates_negative_segment()
    {
        var selected = new[]
        {
            Slot(Monday, 0, 101m),
            Slot(Monday, 1, 203m),
            Slot(Monday, 2, 307m),
            Slot(Monday, 3, 409m)
        };
        var settings = Settings(weekdayFullDayPrice: 781m);

        var (result, error) = Calculate(selected, settings, [Policy(Monday)]);

        Assert.Null(error);
        Assert.Equal(781m, result!.RoomAmount);
        Assert.Equal(781m, result.Segments.Sum(x => x.AppliedAmount));
        Assert.All(result.Segments, x => Assert.True(x.AppliedAmount >= 0m));
    }

    [Fact]
    public void Multiple_dates_apply_their_own_full_day_and_combo_rules()
    {
        var nextDay = Monday.AddDays(1);
        var selected = Slots(Monday, 4, 100m).Concat(Slots(nextDay, 3, 100m)).ToList();

        var (result, error) = Calculate(selected, Settings(weekdayFullDayPrice: 780m), [Policy(Monday), Policy(nextDay)]);

        Assert.Null(error);
        Assert.Equal(1_050m, result!.RoomAmount);
        Assert.Equal(780m, result.Segments.Where(x => x.ServiceDate == Monday).Sum(x => x.AppliedAmount));
        Assert.Equal(270m, result.Segments.Where(x => x.ServiceDate == nextDay).Sum(x => x.AppliedAmount));
    }

    [Fact]
    public void Missing_day_policy_is_rejected()
    {
        var (_, error) = Calculate(Slots(Monday, 1), Settings(), []);

        Assert.Equal("pricing_policy_missing", error?.Code);
    }

    [Fact]
    public void Duplicate_rate_index_on_the_same_date_is_rejected()
    {
        var selected = new[] { Slot(Monday, 0, 100m), Slot(Monday, 0, 100m) };

        var (_, error) = Calculate(selected, Settings(), [Policy(Monday)]);

        Assert.Equal("pricing_selection_duplicate", error?.Code);
    }

    [Fact]
    public void Invalid_special_surcharge_is_rejected()
    {
        var (_, error) = Calculate(Slots(Monday, 1), Settings(), [Policy(Monday, surchargePercent: 101m)]);

        Assert.Equal("pricing_policy_invalid", error?.Code);
    }

    private static (BookingPricingResult? Result, PricingOperationError? Error) Calculate(
        IReadOnlyList<PricingSelectionInput> selected,
        PricingCalculationSettings settings,
        IReadOnlyList<PricingDayPolicy> policies) =>
        PricingCalculator.Calculate(selected, settings, policies.ToDictionary(x => x.Date));

    private static PricingCalculationSettings Settings(
        int activeRatesPerDay = 4,
        decimal? weekdayFullDayPrice = 780m,
        bool useWeekdayFullDayOnWeekend = false,
        decimal? weekendFullDayPrice = 910m,
        int multiSlotCount = 3,
        decimal multiSlotDiscountPercent = 10m) =>
        new(activeRatesPerDay, true, weekdayFullDayPrice, useWeekdayFullDayOnWeekend, weekendFullDayPrice,
            true, multiSlotCount, multiSlotDiscountPercent);

    private static PricingDayPolicy Policy(
        DateOnly date,
        PricingDayProfile profile = PricingDayProfile.Weekday,
        decimal surchargePercent = 0m,
        SpecialDayBookingMode bookingMode = SpecialDayBookingMode.Normal,
        bool allowThreeSlotCombo = true,
        string? name = null) =>
        new(date, profile, name is null ? null : Guid.NewGuid(), name, surchargePercent, bookingMode, allowThreeSlotCombo);

    private static IReadOnlyList<PricingSelectionInput> Slots(
        DateOnly date,
        int count,
        decimal weekdayPrice = 100m,
        decimal? weekendPrice = 200m,
        bool useWeekdayOnWeekend = false,
        int startIndex = 0) =>
        Enumerable.Range(startIndex, count).Select(index => Slot(date, index, weekdayPrice, weekendPrice, useWeekdayOnWeekend)).ToList();

    private static PricingSelectionInput Slot(
        DateOnly date,
        int rateIndex,
        decimal weekdayPrice,
        decimal? weekendPrice = 200m,
        bool useWeekdayOnWeekend = false) =>
        new(date, rateIndex, Guid.NewGuid(), $"Ca {rateIndex + 1}", weekdayPrice, useWeekdayOnWeekend, weekendPrice);
}
