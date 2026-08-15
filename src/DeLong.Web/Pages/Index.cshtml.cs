using System.Text.Json.Nodes;
using DeLong.Web.Features.PublicBooking;
using DeLong.Web.Features.PublicRooms;
using DeLong.Web.Features.Site;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages;

public sealed record PublicHomeSectionVm(Guid Id, string Type, string Name, string Variant, JsonObject Content);

public sealed class IndexModel(
    PublicBookingService publicBookingService,
    PublicRoomContentService publicRoomContentService,
    SiteContentService siteContentService) : PageModel
{
    public PublicRoomCatalogDto Catalog { get; private set; } = new([]);
    public string DefaultDate { get; private set; } = string.Empty;
    public SiteSettingsDto? SiteSettings { get; private set; }
    public IReadOnlyList<PublicHomeSectionVm> Sections { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Catalog = await publicRoomContentService.GetCatalogAsync(cancellationToken);
        var bookingCatalog = await publicBookingService.GetCatalogAsync(null, cancellationToken);
        if (bookingCatalog is not null)
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(bookingCatalog.TimeZoneId);
            var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
            DefaultDate = DateOnly.FromDateTime(localNow).ToString("yyyy-MM-dd");
        }

        var site = await siteContentService.GetPublicAsync(cancellationToken);
        SiteSettings = site?.Settings;
        Sections = site?.Sections
            .Where(x => x.IsVisible)
            .OrderBy(x => x.SortOrder)
            .Select(x => new PublicHomeSectionVm(
                x.Id,
                x.Type,
                x.Name,
                x.Variant,
                JsonNode.Parse(string.IsNullOrWhiteSpace(x.ContentJson) ? "{}" : x.ContentJson) as JsonObject ?? new JsonObject()))
            .ToList() ?? [];
    }
}
