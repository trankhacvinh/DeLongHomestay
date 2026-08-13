using DeLong.Web.Features.PublicBooking;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Rooms;

public sealed class IndexModel(PublicBookingService publicBookingService) : PageModel
{
    public PublicCatalogDto? Catalog { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Catalog = await publicBookingService.GetCatalogAsync(null, cancellationToken);
    }
}
