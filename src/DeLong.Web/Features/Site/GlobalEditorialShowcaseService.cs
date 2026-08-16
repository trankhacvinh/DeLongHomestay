using System.Text.Json;
using DeLong.Web.Common.Caching;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using ZiggyCreatures.Caching.Fusion;

namespace DeLong.Web.Features.Site;

public sealed record GlobalEditorialShowcaseDto(
    Guid Id,
    bool GalleryEnabled,
    string GalleryMode,
    IReadOnlyList<Guid> GalleryPropertyIds,
    IReadOnlyList<Guid> GalleryItemIds,
    int GalleryLimit,
    string GalleryTitle,
    string GalleryLayout,
    bool BlogEnabled,
    string BlogMode,
    IReadOnlyList<Guid> BlogPropertyIds,
    IReadOnlyList<Guid> BlogPostIds,
    int BlogLimit,
    string BlogTitle);

public sealed class SaveGlobalEditorialShowcaseRequest
{
    public bool GalleryEnabled { get; init; } = true;
    public string GalleryMode { get; init; } = "all";
    public IReadOnlyList<Guid> GalleryPropertyIds { get; init; } = [];
    public IReadOnlyList<Guid> GalleryItemIds { get; init; } = [];
    public int GalleryLimit { get; init; } = 8;
    public string? GalleryTitle { get; init; }
    public string GalleryLayout { get; init; } = "mosaic";
    public bool BlogEnabled { get; init; } = true;
    public string BlogMode { get; init; } = "all";
    public IReadOnlyList<Guid> BlogPropertyIds { get; init; } = [];
    public IReadOnlyList<Guid> BlogPostIds { get; init; } = [];
    public int BlogLimit { get; init; } = 3;
    public string? BlogTitle { get; init; }
}

public sealed record GlobalEditorialPublicDto(
    GlobalEditorialShowcaseDto Settings,
    IReadOnlyList<GalleryItemDto> Gallery,
    IReadOnlyList<BlogPostDto> Posts);

public sealed class GlobalEditorialShowcaseService(
    AppDbContext db,
    PropertyEditorialContentService editorialContent,
    IFusionCache? fusionCache = null)
{
    private readonly IFusionCache? cache = fusionCache;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<GlobalEditorialShowcaseDto> GetAsync(CancellationToken ct = default)
    {
        var entity = await db.GlobalEditorialShowcases.AsNoTracking().SingleOrDefaultAsync(ct);
        return entity is null ? DefaultDto() : ToDto(entity);
    }

    public async Task<(GlobalEditorialShowcaseDto? Settings, SiteContentError? Error)> SaveAsync(
        SaveGlobalEditorialShowcaseRequest request,
        CancellationToken ct = default)
    {
        if (request.GalleryMode is not ("all" or "properties" or "manual") || request.BlogMode is not ("all" or "properties" or "manual"))
            return (null, new("validation", "Chế độ chọn nội dung không hợp lệ."));
        if (request.GalleryLayout is not ("mosaic" or "grid" or "slider"))
            return (null, new("validation", "Kiểu hiển thị Gallery không hợp lệ."));
        if (request.GalleryLimit is < 1 or > 24 || request.BlogLimit is < 1 or > 12)
            return (null, new("validation", "Số lượng nội dung hiển thị không hợp lệ."));

        var entity = await EnsureAsync(ct);
        entity.GalleryEnabled = request.GalleryEnabled;
        entity.GalleryMode = request.GalleryMode;
        entity.GalleryPropertyIdsJson = JsonSerializer.Serialize(request.GalleryPropertyIds.Distinct(), JsonOptions);
        entity.GalleryItemIdsJson = JsonSerializer.Serialize(request.GalleryItemIds.Distinct(), JsonOptions);
        entity.GalleryLimit = request.GalleryLimit;
        entity.GalleryTitle = Clean(request.GalleryTitle) ?? "Một vài khoảnh khắc tại De Long";
        entity.GalleryLayout = request.GalleryLayout;
        entity.BlogEnabled = request.BlogEnabled;
        entity.BlogMode = request.BlogMode;
        entity.BlogPropertyIdsJson = JsonSerializer.Serialize(request.BlogPropertyIds.Distinct(), JsonOptions);
        entity.BlogPostIdsJson = JsonSerializer.Serialize(request.BlogPostIds.Distinct(), JsonOptions);
        entity.BlogLimit = request.BlogLimit;
        entity.BlogTitle = Clean(request.BlogTitle) ?? "Gợi ý cho chuyến nghỉ của bạn";
        await db.SaveChangesAsync(ct);
        return (ToDto(entity), null);
    }

    public async Task<GlobalEditorialPublicDto> GetPublicAsync(CancellationToken ct = default)
    {
        async Task<GlobalEditorialPublicDto> LoadAsync(CancellationToken token)
        {
            var settings = await GetAsync(token);
            var gallery = settings.GalleryEnabled ? SelectGallery(await editorialContent.GetGlobalPublicGalleryAsync(token), settings) : [];
            var posts = settings.BlogEnabled ? SelectPosts(await editorialContent.GetGlobalPublicPostsAsync(token), settings) : [];
            return new GlobalEditorialPublicDto(settings, gallery, posts);
        }
        if (cache is null) return await LoadAsync(ct);
        return await cache.GetOrSetAsync<GlobalEditorialPublicDto>(
            PublicCacheKeys.GlobalShowcase,
            async (_, token) => await LoadAsync(token),
            tags: [PublicCacheKeys.Tag],
            token: ct);
    }

    private static IReadOnlyList<GalleryItemDto> SelectGallery(IReadOnlyList<GalleryItemDto> all, GlobalEditorialShowcaseDto settings)
    {
        IEnumerable<GalleryItemDto> query = all;
        if (settings.GalleryMode == "properties" && settings.GalleryPropertyIds.Count > 0)
            query = query.Where(x => settings.GalleryPropertyIds.Contains(x.PropertyId));
        else if (settings.GalleryMode == "manual")
        {
            var byId = all.ToDictionary(x => x.Id);
            query = settings.GalleryItemIds.Where(byId.ContainsKey).Select(id => byId[id]);
        }
        return query.Take(settings.GalleryLimit).ToList();
    }

    private static IReadOnlyList<BlogPostDto> SelectPosts(IReadOnlyList<BlogPostDto> all, GlobalEditorialShowcaseDto settings)
    {
        IEnumerable<BlogPostDto> query = all;
        if (settings.BlogMode == "properties" && settings.BlogPropertyIds.Count > 0)
            query = query.Where(x => settings.BlogPropertyIds.Contains(x.PropertyId));
        else if (settings.BlogMode == "manual")
        {
            var byId = all.ToDictionary(x => x.Id);
            query = settings.BlogPostIds.Where(byId.ContainsKey).Select(id => byId[id]);
        }
        return query.Take(settings.BlogLimit).ToList();
    }

    private async Task<GlobalEditorialShowcase> EnsureAsync(CancellationToken ct)
    {
        var entity = await db.GlobalEditorialShowcases.SingleOrDefaultAsync(ct);
        if (entity is not null) return entity;
        entity = new GlobalEditorialShowcase();
        db.GlobalEditorialShowcases.Add(entity);
        return entity;
    }

    private static GlobalEditorialShowcaseDto DefaultDto() => new(
        Guid.Empty,
        true,
        "all",
        [],
        [],
        8,
        "Một vài khoảnh khắc tại De Long",
        "mosaic",
        true,
        "all",
        [],
        [],
        3,
        "Gợi ý cho chuyến nghỉ của bạn");

    private static GlobalEditorialShowcaseDto ToDto(GlobalEditorialShowcase entity) => new(
        entity.Id,
        entity.GalleryEnabled,
        entity.GalleryMode,
        ParseIds(entity.GalleryPropertyIdsJson),
        ParseIds(entity.GalleryItemIdsJson),
        entity.GalleryLimit,
        entity.GalleryTitle,
        entity.GalleryLayout,
        entity.BlogEnabled,
        entity.BlogMode,
        ParseIds(entity.BlogPropertyIdsJson),
        ParseIds(entity.BlogPostIdsJson),
        entity.BlogLimit,
        entity.BlogTitle);

    private static IReadOnlyList<Guid> ParseIds(string json)
    {
        try { return JsonSerializer.Deserialize<List<Guid>>(json, JsonOptions) ?? []; }
        catch { return []; }
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
