
using DeLong.Web.Features.PublicRooms;
using DeLong.Web.Features.Site;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Rooms;

public sealed class DetailsModel : PageModel
{
    public PublicRoomDetailDto Room { get; private set; } = null!;
    public string PropertyName { get; private set; } = string.Empty;
    public string? SiteSlug { get; private set; }
    public string Today { get; private set; } = string.Empty;
    public IReadOnlyList<PublicRoomCardDto> SimilarRooms { get; private set; } = [];

    public Task<IActionResult> OnGetAsync(
        string code,
        string? siteSlug,
        CancellationToken cancellationToken) =>
        Task.FromResult<IActionResult>(RedirectPermanent(PublicUrlBuilder.BookingHome()));
}
