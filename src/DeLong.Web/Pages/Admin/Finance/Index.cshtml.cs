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

    public async Task<IActionResult> OnGetAsync(string? month, Guid? propertyId, CancellationToken cancellationToken)
    {
        var property = await currentPropertyService.ResolveAsync(User, propertyId, cancellationToken);
        if (property is null) return Forbid();
        PropertyId = property.Id;

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(property.TimeZoneId);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
        var selectedMonth = TryParseMonth(month, out var parsedMonth)
            ? parsedMonth
            : new DateOnly(localNow.Year, localNow.Month, 1);

        var nextMonth = selectedMonth.AddMonths(1);
        var localFrom = DateTime.SpecifyKind(selectedMonth.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        var localTo = DateTime.SpecifyKind(nextMonth.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        var fromUtc = TimeZoneInfo.ConvertTimeToUtc(localFrom, timeZone);
        var toUtc = TimeZoneInfo.ConvertTimeToUtc(localTo, timeZone);

        var snapshot = await financeService.GetSnapshotAsync(PropertyId, fromUtc, toUtc, cancellationToken);
        PageDataJson = JsonSerializer.Serialize(
            new
            {
                propertyId = PropertyId,
                propertyName = property.Name,
                timeZoneId = property.TimeZoneId,
                month = selectedMonth.ToString("yyyy-MM"),
                summary = snapshot.Summary,
                payments = snapshot.Payments,
                expenses = snapshot.Expenses
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
