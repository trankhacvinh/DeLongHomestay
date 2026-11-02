using System.Globalization;
using System.Text.Json;
using DeLong.Web.Common.Security;
using DeLong.Web.Features.Cancellations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Admin.Cancellations;

[Authorize(Policy = "ViewCancellations")]
public sealed class IndexModel(CancellationService service, CurrentPropertyService properties) : PageModel
{
    public string PageDataJson { get; private set; } = "{}";

    public async Task<IActionResult> OnGetAsync(Guid? propertyId, string? scope, CancellationToken cancellationToken)
    {
        var resolved = await service.ResolveScopeAsync(User, properties, propertyId, scope, cancellationToken);
        if (resolved is null) return Forbid();
        var zone = TimeZoneInfo.FindSystemTimeZoneById(resolved.WorkingProperty.TimeZoneId);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone));
        var from = new DateOnly(today.Year, today.Month, 1);
        var to = from.AddMonths(1).AddDays(-1);
        var (result, error) = await service.ListAsync(resolved, new CancellationFilter { From = from, To = to }, false, cancellationToken);
        if (error is not null) return BadRequest(error.Message);
        PageDataJson = JsonSerializer.Serialize(new
        {
            propertyId = resolved.WorkingProperty.Id,
            scope = resolved.IsAll ? "all" : resolved.Selected[0].Id.ToString(),
            from = from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            to = to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            properties = resolved.Accessible,
            canManage = User.IsInRole("Admin") || User.IsInRole("Manager") || User.IsInRole("Staff"),
            result
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return Page();
    }
}
