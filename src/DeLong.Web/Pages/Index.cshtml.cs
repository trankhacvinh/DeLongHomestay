using System.Text.Json.Nodes;
using DeLong.Web.Features.PublicBooking;
using DeLong.Web.Features.PublicRooms;
using DeLong.Web.Features.Site;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages;

public sealed record PublicHomeSectionVm(Guid Id, string Type, string Name, string Variant, JsonObject Content);

public sealed class IndexModel(
    PublicBookingService publicBookingService,
    PublicRoomContentService publicRoomContentService,
    PublicPropertyResolver publicPropertyResolver,
    SiteContentService siteContentService) : PageModel
{
    public PublicRoomCatalogDto Catalog { get; private set; } = new([]);
    public string DefaultDate { get; private set; } = string.Empty;
    public SiteSettingsDto? SiteSettings { get; private set; }
    public IReadOnlyList<PublicHomeSectionVm> Sections { get; private set; } = [];
    public string? SiteSlug { get; private set; }
    public string ScopePrefix => PublicPropertyResolver.ScopePrefix(SiteSlug);

    public async Task<IActionResult> OnGetAsync(string? siteSlug, CancellationToken cancellationToken)
    {
        var property = await publicPropertyResolver.ResolveAsync(siteSlug, cancellationToken);
        if (property is null) return NotFound();
        SiteSlug = string.IsNullOrWhiteSpace(siteSlug) ? null : property.SiteSlug;

        Catalog = await publicRoomContentService.GetCatalogAsync(property.Id, cancellationToken);
        var bookingCatalog = await publicBookingService.GetCatalogAsync(SiteSlug, null, cancellationToken);
        if (bookingCatalog is not null)
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(bookingCatalog.TimeZoneId);
            var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
            DefaultDate = DateOnly.FromDateTime(localNow).ToString("yyyy-MM-dd");
        }

        var site = await siteContentService.GetPublicAsync(SiteSlug, cancellationToken);
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
        return Page();
    }
}
