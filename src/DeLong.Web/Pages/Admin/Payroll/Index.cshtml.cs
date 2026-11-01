using System.Globalization;
using System.Text.Json;
using DeLong.Web.Common.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Admin.Payroll;

public sealed class IndexModel(CurrentPropertyService currentPropertyService) : PageModel
{
    public string PageDataJson { get; private set; } = "{}";

    public async Task<IActionResult> OnGetAsync(Guid? propertyId, string? month, CancellationToken cancellationToken)
    {
        var property = await currentPropertyService.ResolveAsync(User, propertyId, cancellationToken);
        if (property is null) return RedirectToPage("/Admin/SelectProperty", new { returnUrl = "/Admin/Payroll" });

        var zone = TimeZoneInfo.FindSystemTimeZoneById(property.TimeZoneId);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone));
        if (string.IsNullOrWhiteSpace(month) || !DateOnly.TryParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            month = today.ToString("yyyy-MM", CultureInfo.InvariantCulture);

        PageDataJson = JsonSerializer.Serialize(new
        {
            propertyId = property.Id,
            propertyName = property.Name,
            timeZoneId = property.TimeZoneId,
            month,
            today = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            canManagePayroll = User.IsInRole("Admin")
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return Page();
    }
}
