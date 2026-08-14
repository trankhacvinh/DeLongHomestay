using DeLong.Web.Features.PublicRooms;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Rooms;

public sealed class IndexModel(PublicRoomContentService publicRoomContentService) : PageModel
{
    public PublicRoomCatalogDto Catalog { get; private set; } = new([]);

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Catalog = await publicRoomContentService.GetCatalogAsync(cancellationToken);
    }
}
