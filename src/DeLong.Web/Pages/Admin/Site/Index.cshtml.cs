using System.Text.Json;
using DeLong.Web.Common.Security;
using DeLong.Web.Features.Site;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Admin.Site;

[Authorize(Policy = "ManageSiteContent")]
public sealed class IndexModel(
    CurrentPropertyService currentPropertyService,
    SiteContentService siteContentService,
    PublicPropertyResolver publicPropertyResolver) : PageModel
{
    public string PageDataJson { get; private set; } = "{}";

    public async Task<IActionResult> OnGetAsync(Guid? propertyId, CancellationToken ct)
    {
        var property = await currentPropertyService.ResolveAsync(User, propertyId, ct);
        if (property is null) return Forbid();
        var site = await siteContentService.GetAdminAsync(property.Id, ct);
        if (site is null) return NotFound();
        var publicProperty = await publicPropertyResolver.ResolveByIdAsync(property.Id, ct);
        var siteSlug = publicProperty?.SiteSlug ?? PublicPropertyResolver.ToSiteSlug(property.Code);
        PageDataJson = JsonSerializer.Serialize(new
        {
            propertyId = property.Id,
            propertyName = property.Name,
            siteSlug,
            publicBasePath = $"/h/{Uri.EscapeDataString(siteSlug)}",
            canEditCode = User.IsInRole("Admin"),
            site
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return Page();
    }
}
