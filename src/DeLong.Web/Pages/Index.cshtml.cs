using DeLong.Web.Features.PublicBooking;
using DeLong.Web.Features.PublicRooms;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages;

public sealed class IndexModel(
    PublicBookingService publicBookingService,
    PublicRoomContentService publicRoomContentService) : PageModel
{
    public PublicRoomCatalogDto Catalog { get; private set; } = new([]);
    public string DefaultDate { get; private set; } = string.Empty;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Catalog = await publicRoomContentService.GetCatalogAsync(cancellationToken);
        var bookingCatalog = await publicBookingService.GetCatalogAsync(null, cancellationToken);
        if (bookingCatalog is null) return;

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(bookingCatalog.TimeZoneId);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
        DefaultDate = DateOnly.FromDateTime(localNow).ToString("yyyy-MM-dd");
    }
}
