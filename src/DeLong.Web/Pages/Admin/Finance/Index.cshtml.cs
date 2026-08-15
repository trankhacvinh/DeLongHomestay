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
        var snapshots = new List<FinanceSnapshotDto>();
        foreach (var property in targetProperties)
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(property.TimeZoneId);
            var nextMonth = selectedMonth.AddMonths(1);
            var localFrom = DateTime.SpecifyKind(selectedMonth.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
            var localTo = DateTime.SpecifyKind(nextMonth.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
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

        PageDataJson = JsonSerializer.Serialize(
            new
            {
                propertyId = PropertyId,
                propertyName = workingProperty.Name,
                timeZoneId = workingProperty.TimeZoneId,
                month = selectedMonth.ToString("yyyy-MM"),
                scope = scopeKey,
                scopeName,
                canMutateScope,
                properties = accessible,
                summary,
                payments,
                expenses
            },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        return Page();
    }

    private static bool TryParseMonth(string? value, out DateOnly month)
    {
        month = default;
        if (string.IsNullOrWhiteSpace(value) || value.Length != 7) return false;
        return DateOnly.TryParseExact($"{value}-01", "yyyy-MM-dd", out month);
    }
}
