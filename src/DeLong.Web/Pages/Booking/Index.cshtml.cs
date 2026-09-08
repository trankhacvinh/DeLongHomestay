
using System.Text.Json;
using DeLong.Web.Common.Operations;
using DeLong.Web.Data;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Pricing;
using DeLong.Web.Features.PublicBooking;
using DeLong.Web.Features.PublicRooms;
using DeLong.Web.Features.Site;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Pages.Booking;

public sealed class IndexModel(
    PublicBookingService publicBookingService,
    PublicPropertyResolver publicPropertyResolver,
    PublicRoomContentService publicRoomContentService,
    PricingService pricingService,
    AppDbContext db,
    StoragePaths storagePaths,
    IConfiguration configuration,
    ILogger<IndexModel> logger) : PageModel
{
    public string PageDataJson { get; private set; } = "{}";
    public bool RequiresPropertySelection { get; private set; }
    public IReadOnlyList<PublicPropertyCardDto> Properties { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(string? siteSlug, string? site, string? date, string? room, Guid? rate, string? slots, string? embed, CancellationToken cancellationToken)
    {
        var isEmbedded = string.Equals(embed, "1", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(embed, "true", StringComparison.OrdinalIgnoreCase);
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
        var bookingPolicy = await new BookingPolicyStore(storagePaths, configuration).GetAsync(property.Id, cancellationToken);

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

        var initialSlots = new List<object>();
        var initialSlotKeys = new List<(DateOnly StayDate, Guid RateId)>();
        if (selectedRoom is not null && !string.IsNullOrWhiteSpace(slots))
        {
            foreach (var item in slots.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var separator = item.LastIndexOf(':');
                if (separator <= 0 || !DateOnly.TryParse(item[..separator], out var slotDate) || !Guid.TryParse(item[(separator + 1)..], out var slotRateId)) continue;
                var slotRate = selectedRoom.Rates.FirstOrDefault(x => x.Id == slotRateId && x.Type != RoomRateType.Nightly);
                if (slotRate is null) continue;
                initialSlots.Add(new { stayDate = slotDate.ToString("yyyy-MM-dd"), rateId = slotRateId });
                initialSlotKeys.Add((slotDate, slotRateId));
            }
        }

        decimal? embeddedPricingTotal = null;
        if (isEmbedded && selectedRoom is not null && initialSlotKeys.Count > 0)
        {
            var roomPricing = await db.Rooms.AsNoTracking()
                .Where(x => x.Id == selectedRoom.Id && x.PropertyId == property.Id && x.IsActive && x.IsPublished)
                .Select(x => new
                {
                    x.FullDayPricingEnabled,
                    x.FullDayPrice,
                    x.UseWeekdayFullDayPriceOnWeekend,
                    x.WeekendFullDayPrice,
                    Rates = x.Rates.Where(r => r.IsActive && r.Type != RoomRateType.Nightly)
                        .OrderBy(r => r.SortOrder).ThenBy(r => r.StartTime).ThenBy(r => r.Name)
                        .Select(r => new { r.Id, r.Name, r.Price, r.UseWeekdayPriceOnWeekend, r.WeekendPrice }).ToList()
                })
                .SingleOrDefaultAsync(cancellationToken);
            if (roomPricing is not null && roomPricing.Rates.Count > 0)
            {
                var rateIndexes = roomPricing.Rates.Select((rateItem, index) => (rateItem.Id, Index: index))
                    .ToDictionary(x => x.Id, x => x.Index);
                var pricingInputs = initialSlotKeys
                    .Where(x => rateIndexes.ContainsKey(x.RateId))
                    .Select(x =>
                    {
                        var rateItem = roomPricing.Rates[rateIndexes[x.RateId]];
                        return new PricingSelectionInput(x.StayDate, rateIndexes[x.RateId], rateItem.Id, rateItem.Name,
                            rateItem.Price, rateItem.UseWeekdayPriceOnWeekend, rateItem.WeekendPrice);
                    })
                    .ToList();
                if (pricingInputs.Count == initialSlotKeys.Count)
                {
                    var (pricing, pricingError) = await pricingService.CalculateAsync(property.Id, pricingInputs,
                        roomPricing.Rates.Count, roomPricing.FullDayPricingEnabled, roomPricing.FullDayPrice,
                        roomPricing.UseWeekdayFullDayPriceOnWeekend, roomPricing.WeekendFullDayPrice, cancellationToken);
                    if (pricingError is null)
                        embeddedPricingTotal = pricing!.TotalBeforeVoucher;
                    else
                        logger.LogWarning("Embedded booking pricing failed for property {PropertyId}, room {RoomId}: {ErrorCode} {ErrorMessage}",
                            property.Id, selectedRoom.Id, pricingError.Code, pricingError.Message);
                }
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
            initialSlots,
            embeddedSlotSelection = isEmbedded && initialSlots.Count > 0,
            embeddedPricingTotal,
            bookingPolicy,
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
