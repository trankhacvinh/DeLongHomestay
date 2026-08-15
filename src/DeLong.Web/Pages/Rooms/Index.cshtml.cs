
using DeLong.Web.Features.PublicRooms;
using DeLong.Web.Features.Site;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Rooms;

public sealed class IndexModel(
    PublicRoomContentService publicRoomContentService,
    PublicPropertyResolver publicPropertyResolver) : PageModel
{
    public bool IsGlobal { get; private set; }
    public PublicRoomCatalogDto Catalog { get; private set; } = new([]);
    public PublicGlobalRoomCatalogDto GlobalCatalog { get; private set; } = new([], []);
    public IReadOnlyList<PublicGlobalRoomCardDto> DisplayRooms { get; private set; } = [];
    public string? SiteSlug { get; private set; }
    public string PropertyName { get; private set; } = string.Empty;
    public string? PropertyFilter { get; private set; }
    public string Search { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(string? siteSlug, string? property, string? q, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(siteSlug))
        {
            IsGlobal = true;
            GlobalCatalog = await publicRoomContentService.GetGlobalCatalogAsync(cancellationToken);
            PropertyFilter = string.IsNullOrWhiteSpace(property) ? null : property.Trim();
            Search = q?.Trim() ?? string.Empty;
            IEnumerable<PublicGlobalRoomCardDto> rooms = GlobalCatalog.Rooms;
            if (!string.IsNullOrWhiteSpace(PropertyFilter))
                rooms = rooms.Where(x => string.Equals(x.PropertySiteSlug, PropertyFilter, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(Search))
            {
                rooms = rooms.Where(x =>
                    x.Room.Name.Contains(Search, StringComparison.OrdinalIgnoreCase) ||
                    x.Room.Code.Contains(Search, StringComparison.OrdinalIgnoreCase) ||
                    x.PropertyName.Contains(Search, StringComparison.OrdinalIgnoreCase) ||
                    x.Room.Tags.Any(tag => tag.Contains(Search, StringComparison.OrdinalIgnoreCase)) ||
                    x.Room.Amenities.Any(amenity => amenity.Contains(Search, StringComparison.OrdinalIgnoreCase)));
            }
            DisplayRooms = rooms.ToList();
            return Page();
        }

        var scopedProperty = await publicPropertyResolver.ResolveAsync(siteSlug, cancellationToken);
        if (scopedProperty is null) return NotFound();
        SiteSlug = scopedProperty.SiteSlug;
        PropertyName = scopedProperty.Name;
        Catalog = await publicRoomContentService.GetCatalogAsync(scopedProperty.Id, cancellationToken);
        return Page();
    }
}
