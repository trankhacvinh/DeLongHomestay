using System.Text.Json;
using DeLong.Web.Common.Security;
using DeLong.Web.Features.DataQuality;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Admin;

[Authorize(Policy = "ManageDataQuality")]
public sealed class DataQualityModel(CurrentPropertyService properties, DataQualityService quality) : PageModel
{
    public string PageDataJson { get; private set; } = "{}";
    public async Task<IActionResult> OnGetAsync(Guid? propertyId, CancellationToken ct)
    {
        var property = await properties.ResolveAsync(User, propertyId, ct);
        if (property is null) return Forbid();
        PageDataJson = JsonSerializer.Serialize(new { propertyId = property.Id, timeZoneId = property.TimeZoneId,
            canUseAi = User.IsInRole("Admin"), result = await quality.ListAsync(property.Id, ct) },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return Page();
    }
}
