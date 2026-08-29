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
    ReportExcelExportService excelExportService,
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
        var context = await LoadReportAsync(month, propertyId, scope, cancellationToken);
        if (context is null) return Forbid();
        PropertyId = context.WorkingProperty.Id;

        PageDataJson = JsonSerializer.Serialize(
            new
            {
                propertyId = PropertyId,
                propertyName = context.WorkingProperty.Name,
                timeZoneId = context.WorkingProperty.TimeZoneId,
                month = context.Month.ToString("yyyy-MM"),
                scope = context.ScopeKey,
                scopeName = context.ScopeName,
                properties = context.AccessibleProperties,
                report = context.Report
            },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return Page();
    }

    public async Task<IActionResult> OnGetExportAsync(
        string? month,
        Guid? propertyId,
        string? scope,
        CancellationToken cancellationToken)
    {
        var context = await LoadReportAsync(month, propertyId, scope, cancellationToken);
        if (context is null) return Forbid();
        var file = excelExportService.Create(context.Report, context.ScopeName, context.Month);
        return File(file.Content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", file.FileName);
    }

    private async Task<ReportPageContext?> LoadReportAsync(
        string? month,
        Guid? propertyId,
        string? scope,
        CancellationToken cancellationToken)
    {
        var workingProperty = await currentPropertyService.ResolveAsync(User, propertyId, cancellationToken);
        if (workingProperty is null) return null;

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
        IReadOnlyList<CurrentPropertyDto> targetProperties = allScope ? accessible : [selectedProperty];
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

        return new ReportPageContext(
            workingProperty,
            accessible,
            selectedMonth,
            allScope ? "all" : selectedProperty.Id.ToString(),
            allScope ? "Tất cả cơ sở" : selectedProperty.Name,
            allScope ? Combine(scopedReports) : scopedReports[0].Report);
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

        var byStatus = reports
            .SelectMany(x => x.Report.ByStatus)
            .GroupBy(x => x.Status)
            .Select(group => new ReportStatusDto(
                group.Key,
                group.Sum(x => x.BookingCount),
                group.Sum(x => x.BookingValue)))
            .OrderByDescending(x => x.BookingCount)
            .ToList();

        var byPaymentMethod = reports
            .SelectMany(x => x.Report.ByPaymentMethod)
            .GroupBy(x => x.Method)
            .Select(group => new ReportPaymentMethodDto(
                group.Key,
                group.Sum(x => x.TransactionCount),
                group.Sum(x => x.GrossReceipts)))
            .OrderByDescending(x => x.GrossReceipts)
            .ToList();

        var byExpenseCategory = reports
            .SelectMany(x => x.Report.ByExpenseCategory)
            .GroupBy(x => x.Category)
            .Select(group => new ReportExpenseCategoryDto(
                group.Key,
                group.Sum(x => x.TransactionCount),
                group.Sum(x => x.Amount)))
            .OrderByDescending(x => x.Amount)
            .ToList();

        var byWeekday = reports
            .SelectMany(x => x.Report.ByWeekday)
            .GroupBy(x => new { x.DayOfWeek, x.Label })
            .OrderBy(group => group.Key.DayOfWeek)
            .Select(group => new ReportWeekdayDto(
                group.Key.DayOfWeek,
                group.Key.Label,
                group.Sum(x => x.BookingCount),
                group.Sum(x => x.BookingValue),
                group.Sum(x => x.NetReceipts),
                Math.Round(group.Sum(x => x.BookedHours), 1)))
            .ToList();

        var outstandingBuckets = reports
            .SelectMany(x => x.Report.OutstandingBuckets)
            .GroupBy(x => new { x.Key, x.Label })
            .Select(group => new ReportOutstandingBucketDto(
                group.Key.Key,
                group.Key.Label,
                group.Sum(x => x.BookingCount),
                group.Sum(x => x.Amount)))
            .OrderBy(x => Array.IndexOf(["upcoming", "current", "overdue30", "overdue31"], x.Key))
            .ToList();

        var daily = reports
            .SelectMany(x => x.Report.Daily)
            .GroupBy(x => x.Date)
            .OrderBy(group => group.Key)
            .Select(group => new ReportDailyDto(
                group.Key,
                group.Sum(x => x.BookingCount),
                group.Sum(x => x.BookingValue),
                group.Sum(x => x.NetReceipts),
                group.Sum(x => x.Expenses),
                group.Sum(x => x.NetCashFlow)))
            .ToList();

        var weekly = reports
            .SelectMany(x => x.Report.Weekly)
            .GroupBy(x => new { x.WeekStart, x.WeekEnd })
            .OrderBy(group => group.Key.WeekStart)
            .Select(group => new ReportWeeklyDto(
                group.Key.WeekStart,
                group.Key.WeekEnd,
                group.Sum(x => x.BookingValue),
                group.Sum(x => x.NetReceipts),
                group.Sum(x => x.Expenses)))
            .ToList();

        var bookingCount = reports.Sum(x => x.Report.BookingCount);
        var cancelledCount = reports.Sum(x => x.Report.CancelledBookingCount);
        var bookingValue = reports.Sum(x => x.Report.BookingValue);
        var roomCount = reports.Sum(x => x.Report.ActiveRoomCount);
        var totalBookingCount = bookingCount + cancelledCount;
        var periodReceipts = new ReportPeriodReceiptsDto(
            reports.Sum(x => x.Report.PeriodReceipts.Today),
            reports.Sum(x => x.Report.PeriodReceipts.ThisWeek),
            reports.Sum(x => x.Report.PeriodReceipts.SelectedMonth),
            reports.Sum(x => x.Report.PeriodReceipts.PreviousMonth),
            null);
        periodReceipts = periodReceipts with
        {
            MonthChangePercent = periodReceipts.PreviousMonth == 0
                ? (periodReceipts.SelectedMonth == 0 ? 0d : null)
                : Math.Round((double)((periodReceipts.SelectedMonth - periodReceipts.PreviousMonth) /
                                      Math.Abs(periodReceipts.PreviousMonth) * 100), 1)
        };

        return new ReportSnapshotDto(
            bookingCount,
            cancelledCount,
            bookingValue,
            bookingCount == 0 ? 0 : Math.Round(bookingValue / bookingCount, 0),
            Math.Round(reports.Sum(x => x.Report.BookedHours), 1),
            roomCount,
            roomCount == 0 ? 0 : Math.Round(reports.Sum(x => x.Report.OccupancyRate * x.Report.ActiveRoomCount) / roomCount, 1),
            totalBookingCount == 0 ? 0 : Math.Round((double)cancelledCount / totalBookingCount * 100, 1),
            reports.Sum(x => x.Report.GrossReceipts),
            reports.Sum(x => x.Report.Refunds),
            reports.Sum(x => x.Report.NetReceipts),
            reports.Sum(x => x.Report.Expenses),
            reports.Sum(x => x.Report.NetCashFlow),
            reports.Sum(x => x.Report.Outstanding),
            periodReceipts,
            byRoom,
            bySource,
            byStatus,
            byPaymentMethod,
            byExpenseCategory,
            byWeekday,
            outstandingBuckets,
            daily,
            weekly,
            trend);
    }

    private static bool TryParseMonth(string? value, out DateOnly month)
    {
        month = default;
        if (string.IsNullOrWhiteSpace(value) || value.Length != 7) return false;
        return DateOnly.TryParseExact($"{value}-01", "yyyy-MM-dd", out month);
    }

    private sealed record ScopedReport(CurrentPropertyDto Property, ReportSnapshotDto Report);
    private sealed record ReportPageContext(
        CurrentPropertyDto WorkingProperty,
        IReadOnlyList<CurrentPropertyDto> AccessibleProperties,
        DateOnly Month,
        string ScopeKey,
        string ScopeName,
        ReportSnapshotDto Report);
}
