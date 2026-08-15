using System.Text.Json;
using DeLong.Web.Features.PublicRooms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Admin.Site;

[Authorize(Roles = "Admin")]
public sealed class EditorialGlobalModel(PublicRoomContentService publicRoomContentService) : PageModel
{
    public string PageDataJson { get; private set; } = "{}";

    public async Task OnGetAsync(string? tab, CancellationToken ct)
    {
        var catalog = await publicRoomContentService.GetGlobalCatalogAsync(ct);
        var selectedTab = string.Equals(tab, "blog", StringComparison.OrdinalIgnoreCase) ? "blog" : "gallery";
        PageDataJson = JsonSerializer.Serialize(new { properties = catalog.Properties, tab = selectedTab }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }
}
