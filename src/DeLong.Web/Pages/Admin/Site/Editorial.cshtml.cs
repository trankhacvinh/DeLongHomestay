using System.Text.Json;
using DeLong.Web.Common.Security;
using DeLong.Web.Features.Site;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Admin.Site;

[Authorize(Policy = "ManageSiteContent")]
public sealed class EditorialModel(
    CurrentPropertyService currentPropertyService,
    PublicPropertyResolver publicPropertyResolver) : PageModel
{
    public string PageDataJson { get; private set; } = "{}";

    public async Task<IActionResult> OnGetAsync(Guid? propertyId, string? tab, CancellationToken ct)
    {
        var property = await currentPropertyService.ResolveAsync(User, propertyId, ct);
        if (property is null) return Forbid();
        var publicProperty = await publicPropertyResolver.ResolveByIdAsync(property.Id, ct);
        var siteSlug = publicProperty?.SiteSlug ?? PublicPropertyResolver.ToSiteSlug(property.Code);
        var selectedTab = string.Equals(tab, "blog", StringComparison.OrdinalIgnoreCase) ? "blog" : "gallery";
        PageDataJson = JsonSerializer.Serialize(new
        {
            propertyId = property.Id,
            propertyName = property.Name,
            siteSlug,
            tab = selectedTab,
            isAdmin = User.IsInRole("Admin")
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return Page();
    }
}
