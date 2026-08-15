
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
        var globalCatalog = await publicRoomContentService.GetGlobalCatalogAsync(cancellationToken);
        Properties = globalCatalog.Properties;

        if (string.IsNullOrWhiteSpace(siteSlug))
        {
            if (!string.IsNullOrWhiteSpace(site))
            {
                var selectedProperty = await publicPropertyResolver.ResolveAsync(site, cancellationToken);
                return selectedProperty is null
                    ? NotFound()
                    : Redirect(PublicUrlBuilder.Booking(selectedProperty.SiteSlug, date, room, rate));
            }

            if (Properties.Count == 0) return NotFound();

            // A plain /booking request should start the booking flow immediately. Keep DELONG
            // as the canonical/default public property when it is active, and only fall back to
            // the first active public property when the legacy/default property is unavailable.
            var defaultProperty = await publicPropertyResolver.ResolveAsync(null, cancellationToken);
            var defaultSlug = defaultProperty?.SiteSlug ?? Properties[0].SiteSlug;
            return Redirect(PublicUrlBuilder.Booking(defaultSlug, date, room, rate));
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
        if (selectedRoom is null && !string.IsNullOrWhiteSpace(room))
        {
            // Room cards can link with either the internal room code or the public room slug.
            // Resolving both keeps the public CTA independent from how the card itself is rendered.
            var roomContent = await publicRoomContentService.GetRoomAsync(property.Id, room, cancellationToken);
            if (roomContent is not null)
                selectedRoom = catalog.Rooms.FirstOrDefault(x => x.Id == roomContent.Id);
        }

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
            initialRateId = selectedRate?.Id,
            properties = Properties.Select(x => new
            {
                x.Id,
                x.SiteName,
                x.SiteSlug,
                x.RoomCount,
                x.CoverCardUrl
            })
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return Page();
    }
}
