using DeLong.Web.Features.PublicBooking;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages;

public sealed class IndexModel(PublicBookingService publicBookingService) : PageModel
{
    public PublicCatalogDto? Catalog { get; private set; }
    public string DefaultDate { get; private set; } = string.Empty;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Catalog = await publicBookingService.GetCatalogAsync(null, cancellationToken);
        if (Catalog is null) return;

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(Catalog.TimeZoneId);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
        DefaultDate = DateOnly.FromDateTime(localNow).ToString("yyyy-MM-dd");
    }
}
