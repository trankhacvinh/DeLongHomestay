using DeLong.Web.Features.AdminAi;
using Xunit;

namespace DeLong.Tests.Unit;

public sealed class AiReportPeriodResolverTests
{
    private static readonly DateOnly Today = new(2026, 9, 11);

    [Theory]
    [InlineData("day", "2026-09-11", "2026-09-11", "2026-09-10", "2026-09-10")]
    [InlineData("week", "2026-09-07", "2026-09-11", "2026-08-31", "2026-09-04")]
    [InlineData("month", "2026-09-01", "2026-09-11", "2026-08-01", "2026-08-11")]
    [InlineData("quarter", "2026-07-01", "2026-09-11", "2026-04-01", "2026-06-12")]
    [InlineData("year", "2026-01-01", "2026-09-11", "2025-01-01", "2025-09-11")]
    public void Resolves_current_and_equal_length_previous_periods(string period, string from, string to, string previousFrom, string previousTo)
    {
        var result = AiReportPeriodResolver.Resolve(period, Today);

        Assert.Equal(DateOnly.Parse(from), result.From);
        Assert.Equal(DateOnly.Parse(to), result.To);
        Assert.Equal(DateOnly.Parse(previousFrom), result.PreviousFrom);
        Assert.Equal(DateOnly.Parse(previousTo), result.PreviousTo);
    }

    [Fact]
    public void Previous_month_is_capped_at_its_last_day_when_current_month_is_longer()
    {
        var result = AiReportPeriodResolver.Resolve("month", new DateOnly(2026, 3, 31));

        Assert.Equal(new DateOnly(2026, 3, 1), result.From);
        Assert.Equal(new DateOnly(2026, 3, 31), result.To);
        Assert.Equal(new DateOnly(2026, 2, 1), result.PreviousFrom);
        Assert.Equal(new DateOnly(2026, 2, 28), result.PreviousTo);
    }
}
