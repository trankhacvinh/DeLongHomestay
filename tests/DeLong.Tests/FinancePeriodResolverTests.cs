using DeLong.Web.Features.Finance;
using Xunit;

namespace DeLong.Tests;

public sealed class FinancePeriodResolverTests
{
    [Theory]
    [InlineData("day", "2026-08-29", "2026-08-29", "2026-08-30")]
    [InlineData("week", "2026-08-29", "2026-08-24", "2026-08-31")]
    [InlineData("month", "2026-08-29", "2026-08-01", "2026-09-01")]
    [InlineData("quarter", "2026-08-29", "2026-07-01", "2026-10-01")]
    [InlineData("quarter", "2026-01-01", "2026-01-01", "2026-04-01")]
    public void Resolve_returns_expected_calendar_range(string period, string anchor, string expectedStart, string expectedEnd)
    {
        var result = FinancePeriodResolver.Resolve(period, anchor, null, new DateOnly(2025, 1, 1));

        Assert.Equal(period, result.Period);
        Assert.Equal(DateOnly.Parse(expectedStart), result.Start);
        Assert.Equal(DateOnly.Parse(expectedEnd), result.EndExclusive);
    }

    [Fact]
    public void Resolve_keeps_legacy_month_urls_and_defaults_unknown_period_to_month()
    {
        var result = FinancePeriodResolver.Resolve("invalid", null, "2026-05", new DateOnly(2025, 1, 1));

        Assert.Equal("month", result.Period);
        Assert.Equal(new DateOnly(2026, 5, 1), result.Anchor);
        Assert.Equal(new DateOnly(2026, 5, 1), result.Start);
        Assert.Equal(new DateOnly(2026, 6, 1), result.EndExclusive);
    }
}
