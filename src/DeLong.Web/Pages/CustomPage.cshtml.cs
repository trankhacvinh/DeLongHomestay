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
            if (PageContent is null || (!PageContent.IsPublished && !allowDraft)) return NotFound();
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
        if (PageContent is null || (!PageContent.IsPublished && !allowPropertyDraft)) return NotFound();
        SiteSettings = (await siteContentService.GetPublicAsync(property.SiteSlug, ct))?.Settings;
        Catalog = await roomContentService.GetCatalogAsync(property.Id, ct);
        var propertyTimeZone = TimeZoneInfo.FindSystemTimeZoneById(property.TimeZoneId);
        DefaultDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, propertyTimeZone)).ToString("yyyy-MM-dd");
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

    private static IReadOnlyList<Guid> ReadGuidArray(JsonNode? node)
    {
        if (node is not JsonArray array) return [];
        var result = new List<Guid>();
        foreach (var item in array) if (Guid.TryParse(item?.ToString(), out var id)) result.Add(id);
        return result;
    }
    private static int ReadInt(JsonNode? node, int fallback) => int.TryParse(node?.ToString(), out var value) ? value : fallback;
    private static string ReadString(JsonNode? node, string fallback) => string.IsNullOrWhiteSpace(node?.ToString()) ? fallback : node!.ToString();
}
