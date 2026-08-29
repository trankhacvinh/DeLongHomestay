using DeLong.Web.Features.PublicBooking;
using Xunit;

namespace DeLong.Tests.Unit;

public sealed class PublicSlotSelectionRulesTests
{
    [Fact]
    public void Allows_forward_sequence_across_overnight_boundary()
    {
        var date = new DateOnly(2026, 8, 26);

        var error = PublicSlotSelectionRules.ValidateConsecutive(
            [(date, 2), (date, 3), (date.AddDays(1), 0), (date.AddDays(1), 1)],
            ratesPerDay: 4,
            maximumDays: 3);

        Assert.Null(error);
    }

    [Fact]
    public void Rejects_skipped_or_reverse_slots()
    {
        var date = new DateOnly(2026, 8, 26);

        var skipped = PublicSlotSelectionRules.ValidateConsecutive([(date, 0), (date, 2)], 4, 3);
        var reverse = PublicSlotSelectionRules.ValidateConsecutive([(date, 2), (date, 1)], 4, 3);

        Assert.Equal("slots_not_consecutive", skipped?.Code);
        Assert.Equal("slots_not_consecutive", reverse?.Code);
    }

    [Fact]
    public void Rejects_selection_beyond_configured_day_limit()
    {
        var date = new DateOnly(2026, 8, 26);

        var error = PublicSlotSelectionRules.ValidateConsecutive(
            [(date, 3), (date.AddDays(1), 0), (date.AddDays(1), 1), (date.AddDays(1), 2), (date.AddDays(1), 3), (date.AddDays(2), 0)],
            ratesPerDay: 4,
            maximumDays: 2);

        Assert.Equal("slot_range_too_long", error?.Code);
    }
}
