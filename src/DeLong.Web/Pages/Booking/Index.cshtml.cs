
using System.Text.Json;
using DeLong.Web.Features.PublicBooking;
using DeLong.Web.Features.PublicRooms;
using DeLong.Web.Features.Site;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Booking;

public sealed class IndexModel(
    PublicBookingService publicBookingService,
    PublicPropertyResolver publicPropertyResolver,
    PublicRoomContentService publicRoomContentService) : PageModel
{
    public string PageDataJson { get; private set; } = "{}";
    public bool RequiresPropertySelection { get; private set; }
    public IReadOnlyList<PublicPropertyCardDto> Properties { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(string? siteSlug, string? site, string? date, string? room, Guid? rate, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(siteSlug))
        {
            if (!string.IsNullOrWhiteSpace(site))
            {
                var selectedProperty = await publicPropertyResolver.ResolveAsync(site, cancellationToken);
                return selectedProperty is null
                    ? NotFound()
                    : Redirect(PublicUrlBuilder.Booking(selectedProperty.SiteSlug, date, room, rate));
            }

            var globalCatalog = await publicRoomContentService.GetGlobalCatalogAsync(cancellationToken);
            if (globalCatalog.Properties.Count == 0) return NotFound();
            if (globalCatalog.Properties.Count == 1)
                return Redirect(PublicUrlBuilder.Booking(globalCatalog.Properties[0].SiteSlug, date, room, rate));
            RequiresPropertySelection = true;
            Properties = globalCatalog.Properties;
            return Page();
        }

        var property = await publicPropertyResolver.ResolveAsync(siteSlug, cancellationToken);
        if (property is null) return NotFound();
        var effectiveSlug = property.SiteSlug;
        var catalog = await publicBookingService.GetCatalogAsync(effectiveSlug, null, cancellationToken);
        if (catalog is null) return NotFound();

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(catalog.TimeZoneId);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone));
        var selectedDate = DateOnly.TryParse(date, out var parsedDate) && parsedDate >= today ? parsedDate : today;
        var selectedRoom = catalog.Rooms.FirstOrDefault(x => string.Equals(x.Code, room, StringComparison.OrdinalIgnoreCase));
        PublicRateDto? selectedRate = null;
        if (rate.HasValue)
        {
            var rateRoom = catalog.Rooms.FirstOrDefault(x => x.Rates.Any(r => r.Id == rate.Value));
            if (rateRoom is not null && (selectedRoom is null || selectedRoom.Id == rateRoom.Id))
            {
                selectedRoom ??= rateRoom;
                selectedRate = rateRoom.Rates.First(r => r.Id == rate.Value);
            }
        }

        PageDataJson = JsonSerializer.Serialize(new
        {
            propertyName = catalog.PropertyName,
            timeZoneId = catalog.TimeZoneId,
            siteSlug = effectiveSlug,
            scopePrefix = PublicPropertyResolver.ScopePrefix(effectiveSlug),
            today = today.ToString("yyyy-MM-dd"),
            date = selectedDate.ToString("yyyy-MM-dd"),
            rooms = catalog.Rooms,
            initialRoomId = selectedRoom?.Id,
            initialRateId = selectedRate?.Id
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return Page();
    }
}
