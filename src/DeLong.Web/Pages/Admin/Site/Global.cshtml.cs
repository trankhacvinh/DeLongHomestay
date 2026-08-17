using System.Text.Json;
using DeLong.Web.Data;
using DeLong.Web.Features.PublicRooms;
using DeLong.Web.Features.Site;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Admin.Site;

[Authorize(Roles = "Admin")]
public sealed class GlobalModel(
    SiteContentService siteContentService,
    PublicRoomContentService publicRoomContentService,
    PublicPropertyResolver publicPropertyResolver,
    AppDbContext db) : PageModel
{
    public string PageDataJson { get; private set; } = "{}";

    public async Task OnGetAsync(CancellationToken ct)
    {
        var site = await siteContentService.GetGlobalAdminAsync(ct);
        var catalog = await publicRoomContentService.GetGlobalCatalogAsync(ct);
        var activeProperties = await publicPropertyResolver.GetActiveAsync(ct);
        var branding = await GlobalSiteBrandingStore.ResolveAsync(db, siteContentService, activeProperties, ct);
        PageDataJson = JsonSerializer.Serialize(new
        {
            branding,
            sections = site.Sections.Where(x =>
                x.Type != GlobalSiteBrandingStore.MetadataSectionType &&
                x.Type != EditorialPlacementStore.MetadataSectionType),
            properties = catalog.Properties,
            rooms = catalog.Rooms.Select(x => new
            {
                id = x.Room.Id,
                name = x.Room.Name,
                code = x.Room.Code,
                propertyId = x.PropertyId,
                propertyName = x.PropertyName,
                propertySiteSlug = x.PropertySiteSlug
            })
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }
}
