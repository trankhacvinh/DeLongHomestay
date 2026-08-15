using System.Text.Json;
using DeLong.Web.Common.Security;
using DeLong.Web.Features.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Admin.Reports;

[Authorize(Policy = "ViewReports")]
public sealed class IndexModel(
    ReportService reportService,
    CurrentPropertyService currentPropertyService) : PageModel
{
    public Guid PropertyId { get; private set; }
    public string PageDataJson { get; private set; } = "{}";

    public async Task<IActionResult> OnGetAsync(
        string? month,
        Guid? propertyId,
        string? scope,
        CancellationToken cancellationToken)
    {
        var workingProperty = await currentPropertyService.ResolveAsync(User, propertyId, cancellationToken);
        if (workingProperty is null) return Forbid();
        PropertyId = workingProperty.Id;

        var accessible = await currentPropertyService.GetAccessibleAsync(User, cancellationToken);
        var allScope = string.Equals(scope, "all", StringComparison.OrdinalIgnoreCase) && accessible.Count > 1;
        CurrentPropertyDto? selectedProperty = null;
        if (!allScope && Guid.TryParse(scope, out var scopedId))
            selectedProperty = accessible.SingleOrDefault(x => x.Id == scopedId);
        selectedProperty ??= workingProperty;

        var workingTimeZone = TimeZoneInfo.FindSystemTimeZoneById(workingProperty.TimeZoneId);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, workingTimeZone);
        var selectedMonth = TryParseMonth(month, out var parsedMonth)
            ? parsedMonth
            : new DateOnly(localNow.Year, localNow.Month, 1);

        IReadOnlyList<CurrentPropertyDto> targetProperties = allScope
            ? accessible
            : new[] { selectedProperty! };
        var scopedReports = new List<ScopedReport>();
        foreach (var property in targetProperties)
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(property.TimeZoneId);
            var nextMonth = selectedMonth.AddMonths(1);
            var localFrom = DateTime.SpecifyKind(selectedMonth.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
            var localTo = DateTime.SpecifyKind(nextMonth.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
            var fromUtc = TimeZoneInfo.ConvertTimeToUtc(localFrom, timeZone);
            var toUtc = TimeZoneInfo.ConvertTimeToUtc(localTo, timeZone);
            var report = await reportService.GetAsync(property.Id, fromUtc, toUtc, timeZone, cancellationToken);
            scopedReports.Add(new ScopedReport(property, report));
        }

        var combined = allScope ? Combine(scopedReports) : scopedReports[0].Report;
        var scopeKey = allScope ? "all" : selectedProperty.Id.ToString();
        var scopeName = allScope ? "Tất cả cơ sở" : selectedProperty.Name;

        PageDataJson = JsonSerializer.Serialize(
            new
            {
                propertyId = PropertyId,
                propertyName = workingProperty.Name,
                timeZoneId = workingProperty.TimeZoneId,
                month = selectedMonth.ToString("yyyy-MM"),
                scope = scopeKey,
                scopeName,
                properties = accessible,
                report = combined
            },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return Page();
    }

    private static ReportSnapshotDto Combine(IReadOnlyList<ScopedReport> reports)
    {
        var byRoom = reports
            .SelectMany(x => x.Report.ByRoom.Select(room => room with
            {
                RoomName = $"{x.Property.Name} · {room.RoomName}"
            }))
            .OrderByDescending(x => x.BookingValue)
            .ToList();

        var bySource = reports
            .SelectMany(x => x.Report.BySource)
            .GroupBy(x => x.Source)
            .Select(group => new ReportSourceDto(
                group.Key,
                group.Sum(x => x.BookingCount),
                group.Sum(x => x.BookingValue)))
            .OrderByDescending(x => x.BookingValue)
            .ToList();

        var trend = reports
            .SelectMany(x => x.Report.Trend)
            .GroupBy(x => x.Month)
            .OrderBy(group => group.Key)
            .Select(group => new ReportTrendDto(
                group.Key,
                group.Sum(x => x.NetReceipts),
                group.Sum(x => x.Expenses),
                group.Sum(x => x.NetCashFlow)))
            .ToList();

        return new ReportSnapshotDto(
            reports.Sum(x => x.Report.BookingCount),
            reports.Sum(x => x.Report.BookingValue),
            Math.Round(reports.Sum(x => x.Report.BookedHours), 1),
            reports.Sum(x => x.Report.NetReceipts),
            reports.Sum(x => x.Report.Expenses),
            reports.Sum(x => x.Report.NetCashFlow),
            reports.Sum(x => x.Report.Outstanding),
            byRoom,
            bySource,
            trend);
    }

    private static bool TryParseMonth(string? value, out DateOnly month)
    {
        month = default;
        if (string.IsNullOrWhiteSpace(value) || value.Length != 7) return false;
        return DateOnly.TryParseExact($"{value}-01", "yyyy-MM-dd", out month);
    }

    private sealed record ScopedReport(CurrentPropertyDto Property, ReportSnapshotDto Report);
}
