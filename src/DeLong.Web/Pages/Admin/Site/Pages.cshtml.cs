using System.Text.Json;
using DeLong.Web.Common.Security;
using DeLong.Web.Features.Site;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Admin.Site;

[Authorize(Policy = "ManageSiteContent")]
public sealed class PagesModel(CurrentPropertyService currentPropertyService, PublicPropertyResolver propertyResolver) : PageModel
{
    public string PageDataJson { get; private set; } = "{}";

    public async Task<IActionResult> OnGetAsync(Guid? propertyId, string? scope, CancellationToken ct)
    {
        var isAdmin = User.IsInRole("Admin");
        var globalScope = isAdmin && string.Equals(scope, "global", StringComparison.OrdinalIgnoreCase);
        var properties = await currentPropertyService.GetAccessibleAsync(User, ct);
        var current = globalScope ? null : await currentPropertyService.ResolveAsync(User, propertyId, ct);
        if (!globalScope && current is null) return isAdmin ? PageWithData(isAdmin, globalScope, current, properties, null) : Forbid();

        var publicBasePath = "/";
        if (current is not null)
        {
            var property = await propertyResolver.ResolveByIdAsync(current.Id, ct);
            publicBasePath = property is null ? "/" : PublicPropertyResolver.ScopePrefix(property.SiteSlug);
        }

        return PageWithData(isAdmin, globalScope, current, properties, publicBasePath);
    }

    private IActionResult PageWithData(bool isAdmin, bool globalScope, CurrentPropertyDto? current, IReadOnlyList<CurrentPropertyDto> properties, string? publicBasePath)
    {
        PageDataJson = JsonSerializer.Serialize(new
        {
            isAdmin,
            scope = globalScope ? "global" : "property",
            propertyId = current?.Id,
            propertyName = globalScope ? "Trang chung" : current?.Name ?? "Chưa chọn cơ sở",
            listApi = globalScope ? "/api/admin/site/global/pages" : current is null ? "" : $"/api/admin/properties/{current.Id}/site/pages",
            siteApi = globalScope ? "/api/admin/site/global" : current is null ? "" : $"/api/admin/properties/{current.Id}/site",
            publicBasePath = publicBasePath ?? "/",
            properties = properties.Select(x => new { id = x.Id, code = x.Code, name = x.Name })
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return Page();
    }
}
