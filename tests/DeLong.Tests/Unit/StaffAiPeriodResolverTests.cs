using DeLong.Web.Features.PublicAi;
using Xunit;

namespace DeLong.Tests.Unit;

public sealed class StaffAiPeriodResolverTests
{
    private static readonly DateOnly Wednesday = new(2026, 9, 9);

    [Theory]
    [InlineData("booking hôm nay", "2026-09-09", "2026-09-09", "Hôm nay")]
    [InlineData("check-in ngày mai", "2026-09-10", "2026-09-10", "Ngày mai")]
    [InlineData("lịch cuối tuần", "2026-09-12", "2026-09-13", "Cuối tuần")]
    [InlineData("tóm tắt tuần này", "2026-09-07", "2026-09-13", "Tuần này")]
    public void Resolves_supported_operating_periods(string text, string expectedFrom, string expectedTo, string label)
    {
        var result = StaffAiPeriodResolver.Resolve(text, null, Wednesday);

        Assert.Equal(DateOnly.Parse(expectedFrom), result.From);
        Assert.Equal(DateOnly.Parse(expectedTo), result.To);
        Assert.Equal(label, result.Label);
    }

    [Fact]
    public void Weekend_on_sunday_resolves_current_weekend_not_next_week()
    {
        var result = StaffAiPeriodResolver.Resolve("cuối tuần", null, new DateOnly(2026, 9, 13));

        Assert.Equal(new DateOnly(2026, 9, 12), result.From);
        Assert.Equal(new DateOnly(2026, 9, 13), result.To);
    }

    [Fact]
    public void Short_follow_up_keeps_the_previous_period_until_user_changes_it()
    {
        var result = StaffAiPeriodResolver.Resolve("còn phòng cần dọn?", "weekend", Wednesday);

        Assert.Equal("weekend", result.Key);
        Assert.Equal(new DateOnly(2026, 9, 12), result.From);
        Assert.Equal(new DateOnly(2026, 9, 13), result.To);
    }
}
