using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Bookings;
using DeLong.Web.Features.Operations;
using Xunit;

namespace DeLong.Tests;

public sealed class BookingScheduleDisplayTests
{
    private static DateTime At(int hour, int minute = 0) => new(2026, 10, 26, hour, minute, 0, DateTimeKind.Utc);
    private static readonly (DateTime Start, DateTime End)[] Presets = [(At(3, 30), At(6, 30)), (At(7), At(10)), (At(10, 30), At(13, 30))];

    [Theory]
    [InlineData(3, 30, 6, 30, false, false)]
    [InlineData(3, 30, 7, 30, true, false)]
    [InlineData(4, 0, 6, 30, true, false)]
    [InlineData(3, 30, 10, 0, false, true)]
    [InlineData(7, 0, 13, 30, false, true)]
    [InlineData(3, 30, 13, 30, false, true)]
    [InlineData(3, 30, 11, 0, true, false)]
    public void Actual_times_distinguish_single_flexible_and_full_multiple_presets(int sh, int sm, int eh, int em, bool flexible, bool mixed)
    {
        Assert.Equal(new BookingScheduleDisplay(flexible, mixed), BookingScheduleDisplay.Classify(BookingType.TimeSlot, At(sh, sm), At(eh, em), Presets, []));
    }

    [Fact]
    public void Saved_snapshots_preserve_historical_classification_but_edited_times_are_reclassified()
    {
        var snapshots = new[] { (At(3, 30), At(6, 30)), (At(7), At(10)) };
        Assert.Equal(new(false, true), BookingScheduleDisplay.Classify(BookingType.TimeSlot, At(3, 30), At(10), [], snapshots));
        Assert.Equal(new(true, false), BookingScheduleDisplay.Classify(BookingType.TimeSlot, At(3, 30), At(9), Presets, snapshots));
        Assert.Equal(new(false, false), BookingScheduleDisplay.Classify(BookingType.MultiDay, At(3), At(10), Presets, []));
    }

    [Fact]
    public void Manual_palette_uses_saved_hex_values_and_no_obsolete_color_names()
    {
        var palette = BookingDisplayColors.ForSettings(new("#FFFF8B", "#FFFF00", "#FFA55F", "#FFA55F", "#FF2929", "#FAFE01", "#F34DFF", "#945600"));
        Assert.Contains(palette, x => x.Value == "#945600" && x.Name.Contains("Nhiều khung"));
        Assert.Contains(palette, x => x.Value == "#FAFE01" && x.Name.Contains("Giờ linh động"));
        Assert.DoesNotContain(palette, x => x.Name.Contains("Xanh lá") || x.Name.Contains("Xanh dương") || x.Name.Contains("Hoàn tất"));
        Assert.Equal(palette.Count, palette.Select(x => x.Value).Distinct().Count());
        Assert.True(BookingDisplayColors.IsValid("#945600"));
        Assert.False(BookingDisplayColors.IsValid("#945600;opacity:0"));
    }

    [Fact]
    public void Overlapping_legacy_presets_do_not_hide_a_valid_two_preset_sequence()
    {
        (DateTime Start, DateTime End)[] presets = [..Presets, (At(7), At(9))];
        Assert.Equal(new(false, true), BookingScheduleDisplay.Classify(BookingType.TimeSlot, At(3, 30), At(10), presets, []));
    }
}
