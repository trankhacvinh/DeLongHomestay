using System.Text.Json.Nodes;
using DeLong.Web.Common.Security;
using DeLong.Web.Features.PublicRooms;
using DeLong.Web.Features.Site;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages;

public sealed class CustomPageModel(
    CustomPageStore customPageStore,
    PublicPropertyResolver propertyResolver,
    PublicRoomContentService roomContentService,
    SiteContentService siteContentService,
    CurrentPropertyService currentPropertyService) : PageModel
{
    public CustomPageDto PageContent { get; private set; } = null!;
    public bool IsGlobal { get; private set; }
    public string? SiteSlug { get; private set; }
    public string ScopePrefix => PublicPropertyResolver.ScopePrefix(SiteSlug);
    public SiteSettingsDto? SiteSettings { get; private set; }
    public PublicGlobalRoomCatalogDto GlobalCatalog { get; private set; } = new([], []);
    public PublicRoomCatalogDto Catalog { get; private set; } = new([]);
    public string DefaultDate { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(string slug, string? siteSlug, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(slug)) return NotFound();
        IsGlobal = string.IsNullOrWhiteSpace(siteSlug);
        if (IsGlobal)
        {
            var allowDraft = User.Identity?.IsAuthenticated == true && User.IsInRole("Admin");
            PageContent = await customPageStore.GetBySlugAsync(null, slug, !allowDraft, ct) ?? null!;
            if (PageContent is null)
            {
                var legacy = await customPageStore.GetByLegacySlugAsync(null, slug, true, ct);
                if (legacy is not null) return RedirectPermanent(legacy.Url);
                return NotFound();
            }
            if (!PageContent.IsPublished && !allowDraft) return NotFound();
            ApplySeoOverrides(PageContent);
            GlobalCatalog = await roomContentService.GetGlobalCatalogAsync(ct);
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
            DefaultDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone)).ToString("yyyy-MM-dd");
            return Page();
        }

        var property = await propertyResolver.ResolveAsync(siteSlug, ct);
        if (property is null) return NotFound();
        SiteSlug = property.SiteSlug;
        var allowPropertyDraft = false;
        if (User.Identity?.IsAuthenticated == true && (User.IsInRole("Admin") || User.IsInRole("Manager")))
        {
            var accessible = await currentPropertyService.GetAccessibleAsync(User, ct);
            allowPropertyDraft = accessible.Any(x => x.Id == property.Id);
        }
        PageContent = await customPageStore.GetBySlugAsync(property.Id, slug, !allowPropertyDraft, ct) ?? null!;
        if (PageContent is null)
        {
            var legacy = await customPageStore.GetByLegacySlugAsync(property.Id, slug, true, ct);
            if (legacy is not null) return RedirectPermanent(legacy.Url);
            return NotFound();
        }
        if (!PageContent.IsPublished && !allowPropertyDraft) return NotFound();
        ApplySeoOverrides(PageContent);
        SiteSettings = (await siteContentService.GetPublicAsync(property.SiteSlug, ct))?.Settings;
        Catalog = await roomContentService.GetCatalogAsync(property.Id, ct);
        var propertyTimeZone = TimeZoneInfo.FindSystemTimeZoneById(property.TimeZoneId);
        DefaultDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, propertyTimeZone)).ToString("yyyy-MM-dd");
        return Page();
    }

    private void ApplySeoOverrides(CustomPageDto page)
    {
        if (page.NoIndex) ViewData["Robots"] = "noindex,follow";
        if (string.IsNullOrWhiteSpace(page.CanonicalUrl)) return;
        var canonical = page.CanonicalUrl.Trim();
        ViewData["CanonicalUrl"] = canonical.StartsWith('/')
            ? $"{Request.Scheme}://{Request.Host}{canonical}"
            : canonical;
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
            var byId = GlobalCatalog.Rooms.ToDictionary(x => x.Room.Id);
            return ids.Where(byId.ContainsKey).Select(id => byId[id]).Take(limit).ToList();
        }
        if (string.Equals(mode, "byProperty", StringComparison.OrdinalIgnoreCase) && content["propertyQuotas"] is JsonObject quotas)
        {
            var result = new List<PublicGlobalRoomCardDto>();
            foreach (var property in GlobalCatalog.Properties)
            {
                var quota = ReadInt(quotas[property.Id.ToString()], 0);
                if (quota <= 0) continue;
                result.AddRange(GlobalCatalog.Rooms.Where(x => x.PropertyId == property.Id).Take(Math.Clamp(quota, 0, 24)));
                if (result.Count >= limit) break;
            }
            return result.Take(limit).ToList();
        }
        return GlobalCatalog.Rooms.Take(limit).ToList();
    }

    public static JsonObject Content(HomeSectionDto section) =>
        JsonNode.Parse(string.IsNullOrWhiteSpace(section.ContentJson) ? "{}" : section.ContentJson) as JsonObject ?? new JsonObject();

    public static string Text(JsonObject content, string key, string fallback = "") => content[key]?.ToString() ?? fallback;

    public static string VisualClass(JsonObject content, string path, bool image = false)
    {
        var style = ReadVisualStyle(content, path);
        if (style is null) return string.Empty;

        var classes = new List<string>();
        AddVisualClasses(classes, style, image, null);
        if (style["tablet"] is JsonObject tablet) AddVisualClasses(classes, tablet, image, "tablet");
        if (style["mobile"] is JsonObject mobile) AddVisualClasses(classes, mobile, image, "mobile");
        return string.Join(' ', classes);
    }

    private static void AddVisualClasses(List<string> classes, JsonObject style, bool image, string? breakpoint)
    {
        var prefix = string.IsNullOrWhiteSpace(breakpoint) ? "cp-" : $"cp-{breakpoint}-";
        var align = Allowed(ReadString(style["align"], "auto"), ["auto", "left", "center", "right"], "auto");
        if (align != "auto") classes.Add($"{prefix}{(image ? "image-align" : "align")}-{align}");

        if (image)
        {
            var width = Allowed(ReadString(style["width"], "auto"), ["auto", "sm", "md", "lg", "full"], "auto");
            var radius = Allowed(ReadString(style["radius"], "auto"), ["auto", "none", "sm", "md", "lg", "pill"], "auto");
            if (width != "auto") classes.Add($"{prefix}image-width-{width}");
            if (radius != "auto") classes.Add($"{prefix}image-radius-{radius}");
        }
        else
        {
            var size = Allowed(ReadString(style["size"], "auto"), ["auto", "xs", "sm", "md", "lg", "xl", "hero"], "auto");
            var width = Allowed(ReadString(style["width"], "auto"), ["auto", "narrow", "content", "wide", "full"], "auto");
            var buttonSize = Allowed(ReadString(style["buttonSize"], "auto"), ["auto", "sm", "md", "lg"], "auto");
            if (size != "auto") classes.Add($"{prefix}size-{size}");
            if (width != "auto") classes.Add($"{prefix}width-{width}");
            if (buttonSize != "auto") classes.Add($"{prefix}button-size-{buttonSize}");
        }

        var space = Allowed(ReadString(style["space"], "auto"), ["auto", "none", "xs", "sm", "md", "lg", "xl"], "auto");
        if (space != "auto") classes.Add($"{prefix}space-{space}");
    }

    private static JsonObject? ReadVisualStyle(JsonObject content, string path)
    {
        JsonNode? cursor = content["_visual"];
        foreach (var part in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (cursor is not JsonObject obj || obj[part] is null) return null;
            cursor = obj[part];
        }
        return cursor as JsonObject;
    }

    private static IReadOnlyList<Guid> ReadGuidArray(JsonNode? node)
    {
        if (node is not JsonArray array) return [];
        var result = new List<Guid>();
        foreach (var item in array) if (Guid.TryParse(item?.ToString(), out var id)) result.Add(id);
        return result;
    }
    private static int ReadInt(JsonNode? node, int fallback) => int.TryParse(node?.ToString(), out var value) ? value : fallback;
    private static string ReadString(JsonNode? node, string fallback) => string.IsNullOrWhiteSpace(node?.ToString()) ? fallback : node!.ToString();
    private static string Allowed(string value, IReadOnlyCollection<string> allowed, string fallback) => allowed.Contains(value, StringComparer.OrdinalIgnoreCase) ? value.ToLowerInvariant() : fallback;
}
