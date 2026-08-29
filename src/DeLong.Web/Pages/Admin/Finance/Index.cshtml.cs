using System.Text.Json;
using DeLong.Web.Common.Security;
using DeLong.Web.Features.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Admin.Finance;

[Authorize(Policy = "ViewFinance")]
public sealed class IndexModel(
    FinanceService financeService,
    FinanceExcelExportService excelExportService,
    CurrentPropertyService currentPropertyService) : PageModel
{
    public Guid PropertyId { get; private set; }
    public string PageDataJson { get; private set; } = "{}";

    public async Task<IActionResult> OnGetAsync(
        string? month,
        string? period,
        string? date,
        Guid? propertyId,
        string? scope,
        CancellationToken cancellationToken)
    {
        var context = await LoadFinanceAsync(month, period, date, propertyId, scope, cancellationToken);
        if (context is null) return Forbid();
        PropertyId = context.WorkingProperty.Id;

        PageDataJson = JsonSerializer.Serialize(
            new
            {
                propertyId = PropertyId,
                propertyName = context.WorkingProperty.Name,
                timeZoneId = context.WorkingProperty.TimeZoneId,
                period = context.Range.Period,
                anchorDate = context.Range.Anchor.ToString("yyyy-MM-dd"),
                rangeStart = context.Range.Start.ToString("yyyy-MM-dd"),
                rangeEnd = context.Range.EndExclusive.AddDays(-1).ToString("yyyy-MM-dd"),
                scope = context.ScopeKey,
                scopeName = context.ScopeName,
                canMutateScope = context.CanMutateScope,
                properties = context.AccessibleProperties,
                context.Snapshot.Summary,
                context.Snapshot.Payments,
                context.Snapshot.Expenses
            },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        return Page();
    }

    public async Task<IActionResult> OnGetExportAsync(
        string? month,
        string? period,
        string? date,
        Guid? propertyId,
        string? scope,
        CancellationToken cancellationToken)
    {
        var context = await LoadFinanceAsync(month, period, date, propertyId, scope, cancellationToken);
        if (context is null) return Forbid();
        var propertyNames = context.AccessibleProperties.ToDictionary(x => x.Id, x => x.Name);
        var propertyTimeZones = context.AccessibleProperties.ToDictionary(x => x.Id, x => TimeZoneInfo.FindSystemTimeZoneById(x.TimeZoneId));
        var file = excelExportService.Create(context.Snapshot, context.ScopeName, context.Range, propertyNames, propertyTimeZones);
        return File(file.Content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", file.FileName);
    }

    private async Task<FinancePageContext?> LoadFinanceAsync(
        string? month,
        string? period,
        string? date,
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
        var selectedRange = FinancePeriodResolver.Resolve(period, date, month, DateOnly.FromDateTime(localNow));

        IReadOnlyList<CurrentPropertyDto> targetProperties = allScope
            ? accessible
            : new[] { selectedProperty! };
        var snapshots = new List<FinanceSnapshotDto>();
        foreach (var property in targetProperties)
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(property.TimeZoneId);
            var localFrom = DateTime.SpecifyKind(selectedRange.Start.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
            var localTo = DateTime.SpecifyKind(selectedRange.EndExclusive.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
            var fromUtc = TimeZoneInfo.ConvertTimeToUtc(localFrom, timeZone);
            var toUtc = TimeZoneInfo.ConvertTimeToUtc(localTo, timeZone);
            snapshots.Add(await financeService.GetSnapshotAsync(property.Id, fromUtc, toUtc, cancellationToken));
        }

        var summary = new FinanceSummaryDto(
            snapshots.Sum(x => x.Summary.Receipts),
            snapshots.Sum(x => x.Summary.Refunds),
            snapshots.Sum(x => x.Summary.NetReceipts),
            snapshots.Sum(x => x.Summary.Expenses),
            snapshots.Sum(x => x.Summary.NetCashFlow),
            snapshots.Sum(x => x.Summary.Outstanding));
        var payments = snapshots.SelectMany(x => x.Payments).OrderByDescending(x => x.OccurredAtUtc).ToList();
        var expenses = snapshots.SelectMany(x => x.Expenses).OrderByDescending(x => x.OccurredAtUtc).ToList();
        var scopeKey = allScope ? "all" : selectedProperty.Id.ToString();
        var scopeName = allScope ? "Tất cả cơ sở" : selectedProperty.Name;
        var canMutateScope = !allScope && selectedProperty.Id == workingProperty.Id;

        return new FinancePageContext(
            workingProperty,
            accessible,
            selectedRange,
            scopeKey,
            scopeName,
            canMutateScope,
            new FinanceSnapshotDto(summary, payments, expenses));
    }

    private sealed record FinancePageContext(
        CurrentPropertyDto WorkingProperty,
        IReadOnlyList<CurrentPropertyDto> AccessibleProperties,
        FinancePeriodRange Range,
        string ScopeKey,
        string ScopeName,
        bool CanMutateScope,
        FinanceSnapshotDto Snapshot);
}
