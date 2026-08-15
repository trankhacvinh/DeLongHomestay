
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
    public bool IsGlobalHome { get; private set; }
    public PublicGlobalRoomCatalogDto GlobalCatalog { get; private set; } = new([], []);
    public PublicRoomCatalogDto Catalog { get; private set; } = new([]);
    public string DefaultDate { get; private set; } = string.Empty;
    public SiteSettingsDto? SiteSettings { get; private set; }
    public IReadOnlyList<PublicHomeSectionVm> Sections { get; private set; } = [];
    public string? SiteSlug { get; private set; }
    public string ScopePrefix => PublicPropertyResolver.ScopePrefix(SiteSlug);

    public async Task<IActionResult> OnGetAsync(string? siteSlug, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(siteSlug))
        {
            IsGlobalHome = true;
            GlobalCatalog = await publicRoomContentService.GetGlobalCatalogAsync(cancellationToken);
            var globalSections = await siteContentService.GetGlobalPublicSectionsAsync(cancellationToken);
            Sections = ToViewModels(globalSections);
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
            var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
            DefaultDate = DateOnly.FromDateTime(localNow).ToString("yyyy-MM-dd");
            return Page();
        }

        var property = await publicPropertyResolver.ResolveAsync(siteSlug, cancellationToken);
        if (property is null) return NotFound();
        SiteSlug = property.SiteSlug;

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
        Sections = ToViewModels(site?.Sections ?? []);
        return Page();
    }

    public IReadOnlyList<PublicPropertyCardDto> SelectGlobalProperties(JsonObject content)
    {
        var ids = ReadGuidArray(content["propertyIds"]);
        if (ids.Count == 0) return GlobalCatalog.Properties;
        var order = ids.Select((id, index) => new { id, index }).ToDictionary(x => x.id, x => x.index);
        return GlobalCatalog.Properties.Where(x => order.ContainsKey(x.Id)).OrderBy(x => order[x.Id]).ToList();
    }

    public IReadOnlyList<PublicGlobalRoomCardDto> SelectGlobalRooms(JsonObject content)
    {
        var limit = Math.Clamp(ReadInt(content["limit"], 6), 1, 24);
        var mode = ReadString(content["mode"], "all");
        if (string.Equals(mode, "manual", StringComparison.OrdinalIgnoreCase))
        {
            var ids = ReadGuidArray(content["roomIds"]);
            if (ids.Count == 0) return [];
            var byId = GlobalCatalog.Rooms.ToDictionary(x => x.Room.Id);
            return ids.Where(byId.ContainsKey).Select(id => byId[id]).Take(limit).ToList();
        }

        if (string.Equals(mode, "byProperty", StringComparison.OrdinalIgnoreCase) && content["propertyQuotas"] is JsonObject quotas)
        {
            var selected = new List<PublicGlobalRoomCardDto>();
            foreach (var property in GlobalCatalog.Properties)
            {
                var quota = ReadInt(quotas[property.Id.ToString()], 0);
                if (quota <= 0) continue;
                selected.AddRange(GlobalCatalog.Rooms.Where(x => x.PropertyId == property.Id).Take(Math.Clamp(quota, 0, 24)));
                if (selected.Count >= limit) break;
            }
            return selected.Take(limit).ToList();
        }

        return GlobalCatalog.Rooms.Take(limit).ToList();
    }

    private static IReadOnlyList<PublicHomeSectionVm> ToViewModels(IEnumerable<HomeSectionDto> sections) =>
        sections.Where(x => x.IsVisible)
            .OrderBy(x => x.SortOrder)
            .Select(x => new PublicHomeSectionVm(
                x.Id,
                x.Type,
                x.Name,
                x.Variant,
                JsonNode.Parse(string.IsNullOrWhiteSpace(x.ContentJson) ? "{}" : x.ContentJson) as JsonObject ?? new JsonObject()))
            .ToList();

    private static IReadOnlyList<Guid> ReadGuidArray(JsonNode? node)
    {
        if (node is not JsonArray array) return [];
        var result = new List<Guid>();
        foreach (var item in array)
        {
            if (Guid.TryParse(item?.ToString(), out var id)) result.Add(id);
        }
        return result;
    }

    private static int ReadInt(JsonNode? node, int fallback)
    {
        if (node is JsonValue value && value.TryGetValue<int>(out var number)) return number;
        return int.TryParse(node?.ToString(), out var parsed) ? parsed : fallback;
    }

    private static string ReadString(JsonNode? node, string fallback) =>
        string.IsNullOrWhiteSpace(node?.ToString()) ? fallback : node!.ToString();
}
