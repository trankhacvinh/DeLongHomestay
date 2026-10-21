using DeLong.Web.Features.Operations;
using DeLong.Web.Features.PublicBooking;
using Xunit;

namespace DeLong.Tests;

public sealed class PublicRemainingTimePolicyTests
{
    [Theory]
    [InlineData(119, true)]
    [InlineData(120, true)]
    [InlineData(121, false)]
    public void Partial_window_obeys_inclusive_threshold(int remaining, bool blocked)
    {
        var start = new DateTime(2031, 1, 1, 14, 0, 0, DateTimeKind.Utc);
        var end = start.AddHours(3);
        Assert.Equal(blocked, PublicRemainingTimePolicy.IsTooShort(start, end, new(end.AddMinutes(-remaining), end), 120));
    }

    [Fact]
    public void Whole_short_slot_and_disabled_threshold_remain_bookable()
    {
        var start = DateTime.UtcNow;
        var end = start.AddHours(2);
        Assert.False(PublicRemainingTimePolicy.IsTooShort(start, end, new(start, end), 120));
        Assert.False(PublicRemainingTimePolicy.IsTooShort(start, end, new(start.AddMinutes(30), end), 0));
    }

    [Fact]
    public void Overnight_slot_uses_elapsed_time_across_dates()
    {
        var start = new DateTime(2031, 1, 1, 21, 0, 0, DateTimeKind.Utc);
        var end = start.AddHours(12);
        Assert.True(PublicRemainingTimePolicy.IsTooShort(start, end, new(end.AddHours(-2), end), 120));
        Assert.False(PublicRemainingTimePolicy.IsTooShort(start, end, new(start, end.AddMinutes(-30)), 120));
    }
}
