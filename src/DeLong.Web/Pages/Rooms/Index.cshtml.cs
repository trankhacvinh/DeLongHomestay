
using DeLong.Web.Features.PublicRooms;
using DeLong.Web.Features.Site;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Rooms;

public sealed class IndexModel : PageModel
{
    public bool IsGlobal { get; private set; }
    public PublicRoomCatalogDto Catalog { get; private set; } = new([]);
    public PublicGlobalRoomCatalogDto GlobalCatalog { get; private set; } = new([], []);
    public IReadOnlyList<PublicGlobalRoomCardDto> DisplayRooms { get; private set; } = [];
    public string? SiteSlug { get; private set; }
    public string PropertyName { get; private set; } = string.Empty;
    public string? PropertyFilter { get; private set; }
    public string Search { get; private set; } = string.Empty;

    public Task<IActionResult> OnGetAsync(
        string? siteSlug,
        string? property,
        string? q,
        CancellationToken cancellationToken) =>
        Task.FromResult<IActionResult>(RedirectPermanent(PublicUrlBuilder.BookingHome()));
}
