using System.Text.Json.Nodes;
using DeLong.Web.Features.PublicRooms;
using DeLong.Web.Features.Site;
using DeLong.Web.Features.Site.Themes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages;

public sealed record PublicHomeSectionVm(Guid Id, string Type, string Name, string Variant, JsonObject Content);

public sealed class IndexModel(
    PublicRoomContentService publicRoomContentService,
    PublicPropertyResolver publicPropertyResolver,
    SiteContentService siteContentService,
    PropertyEditorialContentService editorialContentService,
    GlobalEditorialShowcaseService globalEditorialShowcaseService) : PageModel
{
    public bool IsGlobalHome { get; private set; }
    /// <summary>Standard theme: visual-editor style classes and layout variants are not rendered.</summary>
    public bool ThemeStandard { get; private set; }
    public PublicThemeDto Theme { get; private set; } = new(PublicThemeStore.StandardMode);
    /// <summary>Theme drawing this page: the saved one, or the one an admin is previewing with <c>?theme=</c>.</summary>
    public ThemeDefinition ActiveTheme { get; private set; } = ThemeCatalog.Theme1;
    public PublicGlobalRoomCatalogDto GlobalCatalog { get; private set; } = new([], []);
    public PublicRoomCatalogDto Catalog { get; private set; } = new([]);
    public string DefaultDate { get; private set; } = string.Empty;
    public SiteSettingsDto? SiteSettings { get; private set; }
    public IReadOnlyList<PublicHomeSectionVm> Sections { get; private set; } = [];
    public string? SiteSlug { get; private set; }
    public string ScopePrefix => PublicPropertyResolver.ScopePrefix(SiteSlug);
    public GlobalEditorialPublicDto? GlobalEditorial { get; private set; }
    public IReadOnlyList<GalleryItemDto> PropertyGallery { get; private set; } = [];
    public string PropertyGalleryLayout { get; private set; } = "mosaic";
    public IReadOnlyList<BlogPostSummaryDto> PropertyPosts { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(string? siteSlug, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(siteSlug))
            return RedirectPermanent(PublicUrlBuilder.Home());
        Theme = await siteContentService.GetPublicThemeAsync(cancellationToken);
        var selection = PublicThemeSelection.Resolve(Theme, HttpContext);
        ActiveTheme = selection.Theme;
        ThemeStandard = selection.Standard;

        if (string.IsNullOrWhiteSpace(siteSlug))
        {
            IsGlobalHome = true;
            GlobalCatalog = await publicRoomContentService.GetGlobalCatalogAsync(cancellationToken);
            var globalSections = await siteContentService.GetGlobalPublicSectionsAsync(cancellationToken);
            Sections = NormalizeGlobalSections(ThemeSections(ActiveTheme, selection.Layout, globalSections));
            GlobalEditorial = await globalEditorialShowcaseService.GetPublicAsync(cancellationToken);
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
            var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
            DefaultDate = DateOnly.FromDateTime(localNow).ToString("yyyy-MM-dd");
            return Page();
        }

        var property = await publicPropertyResolver.ResolveAsync(siteSlug, cancellationToken);
        if (property is null) return NotFound();
        SiteSlug = property.SiteSlug;

        Catalog = await publicRoomContentService.GetCatalogAsync(property.Id, cancellationToken);
        var propertyTimeZone = TimeZoneInfo.FindSystemTimeZoneById(property.TimeZoneId);
        var propertyLocalNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, propertyTimeZone);
        DefaultDate = DateOnly.FromDateTime(propertyLocalNow).ToString("yyyy-MM-dd");

        var site = await siteContentService.GetPublicAsync(SiteSlug, cancellationToken);
        SiteSettings = site?.Settings;
        Sections = ToViewModels(site?.Sections ?? []);
        PropertyGallery = await editorialContentService.GetPublicGalleryAsync(property.Id, cancellationToken);
        PropertyGalleryLayout = await editorialContentService.GetGalleryLayoutAsync(property.Id, cancellationToken);
        PropertyPosts = await editorialContentService.GetPublicPostsAsync(property.Id, cancellationToken);
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

    /// <summary>
    /// Blocks of the homepage in the theme's order. A theme with a saved layout shows its switched-on blocks, each filled
    /// with the content of the first stored block of that type (hidden or not, so switching themes never loses text or
    /// photos); a block with no stored content renders its built-in defaults. Theme 1 without a saved layout shows the
    /// stored blocks exactly as before themes existed; any other theme starts from its default blocks.
    /// </summary>
    public static IReadOnlyList<PublicHomeSectionVm> ThemeSections(
        ThemeDefinition theme,
        IReadOnlyList<ThemeLayoutItem>? layout,
        IEnumerable<HomeSectionDto> stored)
    {
        var rows = stored.ToList();
        if (layout is null && theme.Key == ThemeCatalog.DefaultKey) return ToViewModels(rows);
        var items = layout ?? theme.Slots.Select(x => new ThemeLayoutItem(x.Type, x.DefaultVisible)).ToList();
        var byType = ToViewModels(rows.Select(x => x with { IsVisible = true }))
            .GroupBy(x => x.Type, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        var visibleFirst = ToViewModels(rows).GroupBy(x => x.Type, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        var result = new List<PublicHomeSectionVm>();
        foreach (var item in items.Where(x => x.Visible))
        {
            var slot = theme.Slot(item.Type);
            if (slot is null) continue;
            if (visibleFirst.TryGetValue(slot.Type, out var shown)) result.Add(shown);
            else if (byType.TryGetValue(slot.Type, out var hidden)) result.Add(hidden);
            else result.Add(new PublicHomeSectionVm(Guid.Empty, slot.Type, slot.Label, "default", new JsonObject()));
        }
        return result;
    }

    private static IReadOnlyList<PublicHomeSectionVm> NormalizeGlobalSections(
        IReadOnlyList<PublicHomeSectionVm> sections)
    {
        var normalized = new List<PublicHomeSectionVm>();
        foreach (var section in sections)
        {
            if (string.Equals(section.Type, "BranchGrid", StringComparison.OrdinalIgnoreCase)) continue;

            if (string.Equals(section.Type, "Hero", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(section.Content["secondaryUrl"]?.ToString(), "/#co-so", StringComparison.OrdinalIgnoreCase))
            {
                section.Content["secondaryText"] = string.Empty;
                section.Content["secondaryUrl"] = string.Empty;
            }

            normalized.Add(section);
        }

        return normalized;
    }

    private static IReadOnlyList<PublicHomeSectionVm> ToViewModels(IEnumerable<HomeSectionDto> sections) =>
        sections.Where(x => x.IsVisible && x.Type != GlobalSiteBrandingStore.MetadataSectionType)
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
