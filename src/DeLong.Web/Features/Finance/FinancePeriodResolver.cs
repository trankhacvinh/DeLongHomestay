namespace DeLong.Web.Features.Finance;

public sealed record FinancePeriodRange(
    string Period,
    DateOnly Anchor,
    DateOnly Start,
    DateOnly EndExclusive);

public static class FinancePeriodResolver
{
    public static FinancePeriodRange Resolve(string? period, string? date, string? month, DateOnly fallback)
    {
        var normalizedPeriod = NormalizePeriod(period);
        var anchor = ResolveAnchorDate(date, month, fallback);
        var (start, end) = ResolveRange(anchor, normalizedPeriod);
        return new FinancePeriodRange(normalizedPeriod, anchor, start, end);
    }

    private static string NormalizePeriod(string? value) => value?.ToLowerInvariant() switch
    {
        "day" => "day",
        "week" => "week",
        "quarter" => "quarter",
        _ => "month"
    };

    private static DateOnly ResolveAnchorDate(string? date, string? month, DateOnly fallback)
    {
        if (!string.IsNullOrWhiteSpace(date) && DateOnly.TryParseExact(date, "yyyy-MM-dd", out var parsedDate))
            return parsedDate;
        if (!string.IsNullOrWhiteSpace(month) && DateOnly.TryParseExact($"{month}-01", "yyyy-MM-dd", out var parsedMonth))
            return parsedMonth;
        return fallback;
    }

    private static (DateOnly Start, DateOnly End) ResolveRange(DateOnly anchor, string period) => period switch
    {
        "day" => (anchor, anchor.AddDays(1)),
        "week" => ResolveWeek(anchor),
        "quarter" => ResolveQuarter(anchor),
        _ => (new DateOnly(anchor.Year, anchor.Month, 1), new DateOnly(anchor.Year, anchor.Month, 1).AddMonths(1))
    };

    private static (DateOnly Start, DateOnly End) ResolveWeek(DateOnly anchor)
    {
        var offset = ((int)anchor.DayOfWeek + 6) % 7;
        var start = anchor.AddDays(-offset);
        return (start, start.AddDays(7));
    }

    private static (DateOnly Start, DateOnly End) ResolveQuarter(DateOnly anchor)
    {
        var firstMonth = (anchor.Month - 1) / 3 * 3 + 1;
        var start = new DateOnly(anchor.Year, firstMonth, 1);
        return (start, start.AddMonths(3));
    }
}
