using DeLong.Web.Features.PublicRooms;
using DeLong.Web.Features.Site;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Rooms;

public sealed class IndexModel(
    PublicRoomContentService publicRoomContentService,
    PublicPropertyResolver publicPropertyResolver) : PageModel
{
    public PublicRoomCatalogDto Catalog { get; private set; } = new([]);
    public string? SiteSlug { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? siteSlug, CancellationToken cancellationToken)
    {
        var property = await publicPropertyResolver.ResolveAsync(siteSlug, cancellationToken);
        if (property is null) return NotFound();
        SiteSlug = string.IsNullOrWhiteSpace(siteSlug) ? null : property.SiteSlug;
        Catalog = await publicRoomContentService.GetCatalogAsync(property.Id, cancellationToken);
        return Page();
    }
}
