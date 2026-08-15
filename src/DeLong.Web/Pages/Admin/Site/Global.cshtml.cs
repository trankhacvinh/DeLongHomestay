
using System.Text.Json;
using DeLong.Web.Features.PublicRooms;
using DeLong.Web.Features.Site;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Admin.Site;

[Authorize(Roles = "Admin")]
public sealed class GlobalModel(
    SiteContentService siteContentService,
    PublicRoomContentService publicRoomContentService) : PageModel
{
    public string PageDataJson { get; private set; } = "{}";

    public async Task OnGetAsync(CancellationToken ct)
    {
        var site = await siteContentService.GetGlobalAdminAsync(ct);
        var catalog = await publicRoomContentService.GetGlobalCatalogAsync(ct);
        PageDataJson = JsonSerializer.Serialize(new
        {
            sections = site.Sections,
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
