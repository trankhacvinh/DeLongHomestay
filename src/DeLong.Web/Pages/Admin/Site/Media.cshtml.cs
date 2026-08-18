using System.Text.Json;
using DeLong.Web.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Admin.Site;

[Authorize(Policy = "ManageSiteContent")]
public sealed class MediaModel(CurrentPropertyService currentPropertyService) : PageModel
{
    public string PageDataJson { get; private set; } = "{}";

    public async Task<IActionResult> OnGetAsync(Guid? propertyId, CancellationToken ct)
    {
        var properties = await currentPropertyService.GetAccessibleAsync(User, ct);
        var current = await currentPropertyService.ResolveAsync(User, propertyId, ct);
        var isAdmin = User.IsInRole("Admin");

        if (!isAdmin && current is null) return Forbid();

        PageDataJson = JsonSerializer.Serialize(new
        {
            isAdmin,
            propertyId = current?.Id,
            propertyName = current?.Name ?? (isAdmin ? "Toàn hệ thống" : "Cơ sở"),
            listApi = isAdmin
                ? "/api/admin/site/global/media"
                : $"/api/admin/properties/{current!.Id}/media",
            properties = properties.Select(x => new { id = x.Id, code = x.Code, name = x.Name })
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        return Page();
    }
}
