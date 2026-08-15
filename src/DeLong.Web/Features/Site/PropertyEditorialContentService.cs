using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using Ganss.Xss;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Site;

public sealed record GalleryItemDto(
    Guid Id,
    Guid PropertyId,
    string PropertyName,
    string PropertySiteSlug,
    string ImageUrl,
    string AltText,
    string Caption,
    int SortOrder,
    bool IsPublished);

public sealed record BlogPostDto(
    Guid Id,
    Guid PropertyId,
    string PropertyName,
    string PropertySiteSlug,
    string Slug,
    string Title,
    string Excerpt,
    string BodyHtml,
    string CoverImageUrl,
    bool IsPublished,
    DateTime? PublishedAtUtc);

public sealed record PropertyEditorialAdminDto(
    IReadOnlyList<GalleryItemDto> Gallery,
    IReadOnlyList<BlogPostDto> Posts);

public sealed class SaveGalleryItemRequest
{
    public string ImageUrl { get; init; } = string.Empty;
    public string? AltText { get; init; }
    public string? Caption { get; init; }
    public bool IsPublished { get; init; } = true;
}

public sealed record ReorderGalleryRequest(IReadOnlyList<Guid> Ids);

public sealed class SaveBlogPostRequest
{
    public string? Slug { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Excerpt { get; init; }
    public string? BodyHtml { get; init; }
    public string? CoverImageUrl { get; init; }
    public bool IsPublished { get; init; }
}

public sealed class PropertyEditorialContentService(AppDbContext db)
{
    private static readonly Regex InvalidSlugCharacters = new("[^a-z0-9]+", RegexOptions.Compiled);

    public async Task<PropertyEditorialAdminDto?> GetAdminAsync(Guid propertyId, CancellationToken ct = default)
    {
        if (!await db.Properties.AnyAsync(x => x.Id == propertyId, ct)) return null;
        return new(
            await GetGalleryQuery(propertyId, includeUnpublished: true).ToListAsync(ct),
            await GetBlogQuery(propertyId, includeUnpublished: true).ToListAsync(ct));
    }

    public Task<List<GalleryItemDto>> GetPublicGalleryAsync(Guid propertyId, CancellationToken ct = default) =>
        GetGalleryQuery(propertyId, includeUnpublished: false).ToListAsync(ct);

    public Task<List<BlogPostDto>> GetPublicPostsAsync(Guid propertyId, CancellationToken ct = default) =>
        GetBlogQuery(propertyId, includeUnpublished: false).ToListAsync(ct);

    public Task<List<GalleryItemDto>> GetGlobalPublicGalleryAsync(CancellationToken ct = default) =>
        GetGalleryQuery(null, includeUnpublished: false).ToListAsync(ct);

    public Task<List<BlogPostDto>> GetGlobalPublicPostsAsync(CancellationToken ct = default) =>
        GetBlogQuery(null, includeUnpublished: false).ToListAsync(ct);

    public async Task<BlogPostDto?> GetPublicPostAsync(Guid propertyId, string slug, CancellationToken ct = default)
    {
        var normalized = NormalizeSlug(slug);
        var posts = await GetBlogQuery(propertyId, includeUnpublished: false).ToListAsync(ct);
        return posts.SingleOrDefault(x => x.Slug == normalized);
    }

    public async Task<(GalleryItemDto? Item, SiteContentError? Error)> CreateGalleryAsync(
        Guid propertyId,
        SaveGalleryItemRequest request,
        CancellationToken ct = default)
    {
        if (!await db.Properties.AnyAsync(x => x.Id == propertyId, ct))
            return (null, new("not_found", "Không tìm thấy cơ sở."));
        var imageUrl = Clean(request.ImageUrl);
        if (imageUrl is null || imageUrl.Length > 1000)
            return (null, new("validation", "Ảnh gallery không hợp lệ."));
        var nextOrder = (await db.PropertyGalleryItems.Where(x => x.PropertyId == propertyId)
            .MaxAsync(x => (int?)x.SortOrder, ct) ?? -1) + 1;
        var entity = new PropertyGalleryItem
        {
            PropertyId = propertyId,
            ImageUrl = imageUrl,
            AltText = Limit(Clean(request.AltText) ?? "Không gian homestay", 300)!,
            Caption = Limit(Clean(request.Caption), 500),
            SortOrder = nextOrder,
            IsPublished = request.IsPublished
        };
        db.PropertyGalleryItems.Add(entity);
        await db.SaveChangesAsync(ct);
        var items = await GetGalleryQuery(propertyId, true).ToListAsync(ct);
        return (items.Single(x => x.Id == entity.Id), null);
    }

    public async Task<(GalleryItemDto? Item, SiteContentError? Error)> UpdateGalleryAsync(
        Guid propertyId,
        Guid itemId,
        SaveGalleryItemRequest request,
        CancellationToken ct = default)
    {
        var entity = await db.PropertyGalleryItems.SingleOrDefaultAsync(x => x.Id == itemId && x.PropertyId == propertyId, ct);
        if (entity is null) return (null, new("not_found", "Không tìm thấy ảnh gallery."));
        var imageUrl = Clean(request.ImageUrl);
        if (imageUrl is null || imageUrl.Length > 1000)
            return (null, new("validation", "Ảnh gallery không hợp lệ."));
        entity.ImageUrl = imageUrl;
        entity.AltText = Limit(Clean(request.AltText) ?? "Không gian homestay", 300)!;
        entity.Caption = Limit(Clean(request.Caption), 500);
        entity.IsPublished = request.IsPublished;
        await db.SaveChangesAsync(ct);
        var items = await GetGalleryQuery(propertyId, true).ToListAsync(ct);
        return (items.Single(x => x.Id == itemId), null);
    }

    public async Task<SiteContentError?> DeleteGalleryAsync(Guid propertyId, Guid itemId, CancellationToken ct = default)
    {
        var entity = await db.PropertyGalleryItems.SingleOrDefaultAsync(x => x.Id == itemId && x.PropertyId == propertyId, ct);
        if (entity is null) return new("not_found", "Không tìm thấy ảnh gallery.");
        db.PropertyGalleryItems.Remove(entity);
        await db.SaveChangesAsync(ct);
        return null;
    }

    public async Task<SiteContentError?> ReorderGalleryAsync(Guid propertyId, IReadOnlyList<Guid> ids, CancellationToken ct = default)
    {
        var items = await db.PropertyGalleryItems.Where(x => x.PropertyId == propertyId).ToListAsync(ct);
        if (items.Count != ids.Count || ids.Distinct().Count() != ids.Count || items.Any(x => !ids.Contains(x.Id)))
            return new("validation", "Danh sách sắp xếp gallery không hợp lệ.");
        var order = ids.Select((id, index) => new { id, index }).ToDictionary(x => x.id, x => x.index);
        foreach (var item in items) item.SortOrder = order[item.Id];
        await db.SaveChangesAsync(ct);
        return null;
    }

    public async Task<(BlogPostDto? Post, SiteContentError? Error)> CreatePostAsync(
        Guid propertyId,
        SaveBlogPostRequest request,
        CancellationToken ct = default)
    {
        if (!await db.Properties.AnyAsync(x => x.Id == propertyId, ct))
            return (null, new("not_found", "Không tìm thấy cơ sở."));
        var validation = ValidatePost(request);
        if (validation.Error is not null) return (null, validation.Error);
        var slug = await UniqueSlugAsync(propertyId, validation.Slug!, null, ct);
        var entity = new BlogPost
        {
            PropertyId = propertyId,
            Slug = slug,
            Title = validation.Title!,
            Excerpt = validation.Excerpt!,
            BodyHtml = validation.BodyHtml!,
            CoverImageUrl = validation.CoverImageUrl,
            IsPublished = request.IsPublished,
            PublishedAtUtc = request.IsPublished ? DateTime.UtcNow : null
        };
        db.BlogPosts.Add(entity);
        await db.SaveChangesAsync(ct);
        var posts = await GetBlogQuery(propertyId, true).ToListAsync(ct);
        return (posts.Single(x => x.Id == entity.Id), null);
    }

    public async Task<(BlogPostDto? Post, SiteContentError? Error)> UpdatePostAsync(
        Guid propertyId,
        Guid postId,
        SaveBlogPostRequest request,
        CancellationToken ct = default)
    {
        var entity = await db.BlogPosts.SingleOrDefaultAsync(x => x.Id == postId && x.PropertyId == propertyId, ct);
        if (entity is null) return (null, new("not_found", "Không tìm thấy bài viết."));
        var validation = ValidatePost(request);
        if (validation.Error is not null) return (null, validation.Error);
        entity.Slug = await UniqueSlugAsync(propertyId, validation.Slug!, postId, ct);
        entity.Title = validation.Title!;
        entity.Excerpt = validation.Excerpt!;
        entity.BodyHtml = validation.BodyHtml!;
        entity.CoverImageUrl = validation.CoverImageUrl;
        if (request.IsPublished && !entity.IsPublished) entity.PublishedAtUtc = DateTime.UtcNow;
        if (!request.IsPublished) entity.PublishedAtUtc = null;
        entity.IsPublished = request.IsPublished;
        await db.SaveChangesAsync(ct);
        var posts = await GetBlogQuery(propertyId, true).ToListAsync(ct);
        return (posts.Single(x => x.Id == postId), null);
    }

    public async Task<SiteContentError?> DeletePostAsync(Guid propertyId, Guid postId, CancellationToken ct = default)
    {
        var entity = await db.BlogPosts.SingleOrDefaultAsync(x => x.Id == postId && x.PropertyId == propertyId, ct);
        if (entity is null) return new("not_found", "Không tìm thấy bài viết.");
        db.BlogPosts.Remove(entity);
        await db.SaveChangesAsync(ct);
        return null;
    }

    private IQueryable<GalleryItemDto> GetGalleryQuery(Guid? propertyId, bool includeUnpublished)
    {
        var query = db.PropertyGalleryItems.AsNoTracking()
            .Where(x => x.Property.IsActive);
        if (propertyId.HasValue) query = query.Where(x => x.PropertyId == propertyId.Value);
        if (!includeUnpublished) query = query.Where(x => x.IsPublished);
        return query.OrderBy(x => x.Property.CreatedAtUtc).ThenBy(x => x.SortOrder).ThenBy(x => x.CreatedAtUtc)
            .Select(x => new GalleryItemDto(
                x.Id,
                x.PropertyId,
                x.Property.Name,
                string.IsNullOrWhiteSpace(x.Property.SiteSlug) ? x.Property.Code.ToLower() : x.Property.SiteSlug!,
                x.ImageUrl,
                x.AltText,
                x.Caption ?? string.Empty,
                x.SortOrder,
                x.IsPublished));
    }

    private IQueryable<BlogPostDto> GetBlogQuery(Guid? propertyId, bool includeUnpublished)
    {
        var query = db.BlogPosts.AsNoTracking()
            .Where(x => x.Property.IsActive);
        if (propertyId.HasValue) query = query.Where(x => x.PropertyId == propertyId.Value);
        if (!includeUnpublished) query = query.Where(x => x.IsPublished && x.PublishedAtUtc != null);
        return query.OrderByDescending(x => x.PublishedAtUtc ?? x.CreatedAtUtc)
            .Select(x => new BlogPostDto(
                x.Id,
                x.PropertyId,
                x.Property.Name,
                string.IsNullOrWhiteSpace(x.Property.SiteSlug) ? x.Property.Code.ToLower() : x.Property.SiteSlug!,
                x.Slug,
                x.Title,
                x.Excerpt,
                x.BodyHtml,
                x.CoverImageUrl ?? string.Empty,
                x.IsPublished,
                x.PublishedAtUtc));
    }

    private async Task<string> UniqueSlugAsync(Guid propertyId, string requested, Guid? excludingId, CancellationToken ct)
    {
        var root = requested;
        var candidate = root;
        var suffix = 2;
        while (await db.BlogPosts.AnyAsync(x => x.PropertyId == propertyId && x.Slug == candidate && (!excludingId.HasValue || x.Id != excludingId.Value), ct))
            candidate = $"{root}-{suffix++}";
        return candidate;
    }

    private static (string? Slug, string? Title, string? Excerpt, string? BodyHtml, string? CoverImageUrl, SiteContentError? Error) ValidatePost(SaveBlogPostRequest request)
    {
        var title = Clean(request.Title);
        if (title is null || title.Length > 240)
            return (null, null, null, null, null, new("validation", "Tiêu đề bài viết là bắt buộc và tối đa 240 ký tự."));
        var slug = NormalizeSlug(Clean(request.Slug) ?? title);
        if (string.IsNullOrWhiteSpace(slug) || slug.Length > 180)
            return (null, null, null, null, null, new("validation", "Đường dẫn bài viết không hợp lệ."));
        var excerpt = Limit(Clean(request.Excerpt) ?? title, 800) ?? title;
        var rawBody = request.BodyHtml?.Trim() ?? string.Empty;
        if (rawBody.Length > 120_000)
            return (null, null, null, null, null, new("validation", "Nội dung bài viết quá dài."));
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedAttributes.Add("class");
        var body = sanitizer.Sanitize(rawBody);
        var cover = Limit(Clean(request.CoverImageUrl), 1000);
        return (slug, title, excerpt, body, cover, null);
    }

    public static string NormalizeSlug(string value)
    {
        var normalized = (value ?? string.Empty).Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
        }
        var ascii = sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant().Replace('đ', 'd');
        return InvalidSlugCharacters.Replace(ascii, "-").Trim('-');
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? Limit(string? value, int max) => value is null ? null : value[..Math.Min(value.Length, max)];
}
