using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Site;

public sealed class SaveCustomPageRequest
{
    public string? Title { get; init; }
    public string? Slug { get; init; }
    public bool IsPublished { get; init; }
    public bool HideFromNavigation { get; init; }
    public string? SeoTitle { get; init; }
    public string? SeoDescription { get; init; }
    public string? OgImageUrl { get; init; }
    public string? Template { get; init; }
}

public sealed record CustomPageSummaryDto(
    Guid Id,
    Guid? PropertyId,
    string Title,
    string Slug,
    string Url,
    bool IsPublished,
    bool HideFromNavigation,
    string SeoTitle,
    string SeoDescription,
    string OgImageUrl,
    bool NoIndex,
    string CanonicalUrl,
    IReadOnlyList<string> LegacySlugs,
    int SectionCount,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record CustomPageDto(
    Guid Id,
    Guid? PropertyId,
    string Title,
    string Slug,
    string Url,
    bool IsPublished,
    bool HideFromNavigation,
    string SeoTitle,
    string SeoDescription,
    string OgImageUrl,
    bool NoIndex,
    string CanonicalUrl,
    IReadOnlyList<string> LegacySlugs,
    IReadOnlyList<HomeSectionDto> Sections,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record CustomPageMutationResult(CustomPageDto? Page, SiteContentError? Error);
public sealed record CustomPageSectionMutationResult(HomeSectionDto? Section, SiteContentError? Error);

public sealed class CustomPageStore(AppDbContext db)
{
    public const string MetadataSectionType = "__CustomPage";
    private const int MaxPagesPerScope = 100;
    private const int MaxSectionsPerPage = 30;
    private const int MaxPayloadLength = 700_000;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Regex InvalidSlugCharacters = new("[^a-z0-9]+", RegexOptions.Compiled);
    private static readonly Regex ValidSlug = new("^[a-z0-9](?:[a-z0-9-]{0,118}[a-z0-9])?$", RegexOptions.Compiled);
    private static readonly HashSet<string> ReservedSlugs = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin", "account", "api", "health", "h", "rooms", "booking", "blog", "site",
        "uploads", "not-found", "error", "robots.txt", "sitemap.xml", "favicon.ico"
    };

    private sealed class Payload
    {
        public int Version { get; init; } = 1;
        public string Title { get; init; } = string.Empty;
        public string Slug { get; init; } = string.Empty;
        public bool IsPublished { get; init; }
        public bool HideFromNavigation { get; init; }
        public string SeoTitle { get; init; } = string.Empty;
        public string SeoDescription { get; init; } = string.Empty;
        public string OgImageUrl { get; init; } = string.Empty;
        public bool NoIndex { get; init; }
        public string CanonicalUrl { get; init; } = string.Empty;
        public IReadOnlyList<string> LegacySlugs { get; init; } = [];
        public IReadOnlyList<PageSectionPayload> Sections { get; init; } = [];
    }

    private sealed class PageSectionPayload
    {
        public Guid Id { get; init; }
        public string Type { get; init; } = "RichText";
        public string Name { get; init; } = string.Empty;
        public string Variant { get; init; } = "wide";
        public string ContentJson { get; init; } = "{}";
        public int SortOrder { get; init; }
        public bool IsVisible { get; init; } = true;
    }

    public async Task<IReadOnlyList<CustomPageSummaryDto>> ListAsync(Guid? propertyId, bool publishedOnly = false, CancellationToken ct = default)
    {
        var rows = await db.Set<HomeSection>().AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.Type == MetadataSectionType)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .ToListAsync(ct);
        var siteSlug = await ResolveSiteSlugAsync(propertyId, ct);
        return rows.Select(row => Read(row, siteSlug))
            .Where(page => !publishedOnly || page.IsPublished)
            .Select(ToSummary)
            .OrderBy(x => x.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task<CustomPageDto?> GetAsync(Guid? propertyId, Guid pageId, CancellationToken ct = default)
    {
        var row = await db.Set<HomeSection>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == pageId && x.PropertyId == propertyId && x.Type == MetadataSectionType, ct);
        if (row is null) return null;
        return Read(row, await ResolveSiteSlugAsync(propertyId, ct));
    }

    public async Task<CustomPageDto?> GetBySlugAsync(Guid? propertyId, string slug, bool publishedOnly, CancellationToken ct = default)
    {
        var normalized = NormalizeSlug(slug);
        if (string.IsNullOrWhiteSpace(normalized)) return null;
        var rows = await db.Set<HomeSection>().AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.Type == MetadataSectionType)
            .ToListAsync(ct);
        var siteSlug = await ResolveSiteSlugAsync(propertyId, ct);
        return rows.Select(row => Read(row, siteSlug))
            .FirstOrDefault(page => string.Equals(page.Slug, normalized, StringComparison.OrdinalIgnoreCase) && (!publishedOnly || page.IsPublished));
    }

    public async Task<CustomPageMutationResult> CreateAsync(Guid? propertyId, SaveCustomPageRequest request, CancellationToken ct = default)
    {
        if (propertyId is not null && !await db.Properties.AnyAsync(x => x.Id == propertyId, ct))
            return new(null, new("not_found", "Không tìm thấy cơ sở."));
        if (await db.Set<HomeSection>().CountAsync(x => x.PropertyId == propertyId && x.Type == MetadataSectionType, ct) >= MaxPagesPerScope)
            return new(null, new("validation", $"Mỗi phạm vi tối đa {MaxPagesPerScope} trang nội dung."));

        var title = Clean(request.Title);
        if (string.IsNullOrWhiteSpace(title) || title.Length > 200)
            return new(null, new("validation", "Tên trang là bắt buộc và tối đa 200 ký tự."));
        var slug = string.IsNullOrWhiteSpace(request.Slug) ? Slugify(title) : NormalizeSlug(request.Slug);
        var error = await ValidatePageFieldsAsync(propertyId, null, slug, request, ct);
        if (error is not null) return new(null, error);

        var payload = new Payload
        {
            Title = title,
            Slug = slug,
            IsPublished = request.IsPublished,
            HideFromNavigation = request.HideFromNavigation,
            SeoTitle = Limit(Clean(request.SeoTitle), 200),
            SeoDescription = Limit(Clean(request.SeoDescription), 500),
            OgImageUrl = Limit(Clean(request.OgImageUrl), 1200),
            NoIndex = false,
            CanonicalUrl = string.Empty,
            LegacySlugs = [],
            Sections = TemplateSections(request.Template, title)
        };
        var row = new HomeSection
        {
            PropertyId = propertyId,
            Type = MetadataSectionType,
            Name = title,
            Variant = "custom-page",
            ContentJson = Serialize(payload),
            SortOrder = int.MinValue + 20,
            IsVisible = false
        };
        db.Set<HomeSection>().Add(row);
        await db.SaveChangesAsync(ct);
        return new(Read(row, await ResolveSiteSlugAsync(propertyId, ct)), null);
    }

    public async Task<CustomPageMutationResult> UpdateAsync(Guid? propertyId, Guid pageId, SaveCustomPageRequest request, CancellationToken ct = default)
    {
        var row = await db.Set<HomeSection>()
            .SingleOrDefaultAsync(x => x.Id == pageId && x.PropertyId == propertyId && x.Type == MetadataSectionType, ct);
        if (row is null) return new(null, new("not_found", "Không tìm thấy trang nội dung."));
        var current = ReadPayload(row);
        var title = Clean(request.Title);
        if (string.IsNullOrWhiteSpace(title) || title.Length > 200)
            return new(null, new("validation", "Tên trang là bắt buộc và tối đa 200 ký tự."));
        var slug = string.IsNullOrWhiteSpace(request.Slug) ? current.Slug : NormalizeSlug(request.Slug);
        var error = await ValidatePageFieldsAsync(propertyId, pageId, slug, request, ct);
        if (error is not null) return new(null, error);
        var payload = new Payload
        {
            Title = title,
            Slug = slug,
            IsPublished = request.IsPublished,
            HideFromNavigation = request.HideFromNavigation,
            SeoTitle = Limit(Clean(request.SeoTitle), 200),
            SeoDescription = Limit(Clean(request.SeoDescription), 500),
            OgImageUrl = Limit(Clean(request.OgImageUrl), 1200),
            NoIndex = current.NoIndex,
            CanonicalUrl = current.CanonicalUrl,
            LegacySlugs = NextLegacySlugs(current, slug),
            Sections = current.Sections
        };
        row.Name = title;
        row.ContentJson = Serialize(payload);
        row.IsVisible = false;
        row.SortOrder = int.MinValue + 20;
        await db.SaveChangesAsync(ct);
        return new(Read(row, await ResolveSiteSlugAsync(propertyId, ct)), null);
    }

    public async Task<CustomPageMutationResult> DuplicateAsync(Guid? propertyId, Guid pageId, CancellationToken ct = default)
    {
        var source = await db.Set<HomeSection>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == pageId && x.PropertyId == propertyId && x.Type == MetadataSectionType, ct);
        if (source is null) return new(null, new("not_found", "Không tìm thấy trang cần nhân bản."));
        if (await db.Set<HomeSection>().CountAsync(x => x.PropertyId == propertyId && x.Type == MetadataSectionType, ct) >= MaxPagesPerScope)
            return new(null, new("validation", $"Mỗi phạm vi tối đa {MaxPagesPerScope} trang nội dung."));

        var sourcePayload = ReadPayload(source);
        var baseSlug = NormalizeSlug(sourcePayload.Slug + "-copy");
        var slug = baseSlug;
        for (var i = 2; await SlugExistsAsync(propertyId, null, slug, ct); i++) slug = NormalizeSlug($"{baseSlug}-{i}");
        var sections = sourcePayload.Sections.Select((section, index) => new PageSectionPayload
        {
            Id = Guid.NewGuid(), Type = section.Type, Name = section.Name, Variant = section.Variant,
            ContentJson = section.ContentJson, SortOrder = index, IsVisible = section.IsVisible
        }).ToList();
        var payload = new Payload
        {
            Title = $"{sourcePayload.Title} (bản sao)",
            Slug = slug,
            IsPublished = false,
            HideFromNavigation = true,
            SeoTitle = sourcePayload.SeoTitle,
            SeoDescription = sourcePayload.SeoDescription,
            OgImageUrl = sourcePayload.OgImageUrl,
            NoIndex = false,
            CanonicalUrl = string.Empty,
            LegacySlugs = [],
            Sections = sections
        };
        var row = new HomeSection
        {
            PropertyId = propertyId, Type = MetadataSectionType, Name = payload.Title, Variant = "custom-page",
            ContentJson = Serialize(payload), SortOrder = int.MinValue + 20, IsVisible = false
        };
        db.Set<HomeSection>().Add(row);
        await db.SaveChangesAsync(ct);
        return new(Read(row, await ResolveSiteSlugAsync(propertyId, ct)), null);
    }

    public async Task<SiteContentError?> DeleteAsync(Guid? propertyId, Guid pageId, CancellationToken ct = default)
    {
        var row = await db.Set<HomeSection>()
            .SingleOrDefaultAsync(x => x.Id == pageId && x.PropertyId == propertyId && x.Type == MetadataSectionType, ct);
        if (row is null) return new("not_found", "Không tìm thấy trang nội dung.");
        db.Set<HomeSection>().Remove(row);
        await db.SaveChangesAsync(ct);
        return null;
    }

    public async Task<CustomPageDto?> GetByLegacySlugAsync(Guid? propertyId, string slug, bool publishedOnly, CancellationToken ct = default)
    {
        var normalized = NormalizeSlug(slug);
        if (string.IsNullOrWhiteSpace(normalized)) return null;
        var rows = await db.Set<HomeSection>().AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.Type == MetadataSectionType)
            .ToListAsync(ct);
        var siteSlug = await ResolveSiteSlugAsync(propertyId, ct);
        return rows.Select(row => Read(row, siteSlug))
            .FirstOrDefault(page => page.LegacySlugs.Any(old => string.Equals(old, normalized, StringComparison.OrdinalIgnoreCase)) && (!publishedOnly || page.IsPublished));
    }

    public async Task<SiteContentError?> UpdateSeoAsync(Guid? propertyId, Guid pageId, bool noIndex, string? canonicalUrl, CancellationToken ct = default)
    {
        var row = await FindTrackedRowAsync(propertyId, pageId, ct);
        if (row is null) return new("not_found", "Không tìm thấy trang nội dung.");
        var canonical = Clean(canonicalUrl);
        if (!IsSafeCanonicalUrl(canonical)) return new("validation", "Canonical phải là URL http/https hoặc đường dẫn nội bộ bắt đầu bằng /. ");
        var payload = ReadPayload(row);
        var next = new Payload
        {
            Title = payload.Title, Slug = payload.Slug, IsPublished = payload.IsPublished,
            HideFromNavigation = payload.HideFromNavigation, SeoTitle = payload.SeoTitle,
            SeoDescription = payload.SeoDescription, OgImageUrl = payload.OgImageUrl,
            NoIndex = noIndex, CanonicalUrl = Limit(canonical, 1200), LegacySlugs = payload.LegacySlugs,
            Sections = payload.Sections
        };
        row.ContentJson = Serialize(next);
        row.Name = payload.Title;
        row.IsVisible = false;
        row.SortOrder = int.MinValue + 20;
        await db.SaveChangesAsync(ct);
        return null;
    }

    public async Task<SiteContentError?> RemoveLegacySlugAsync(Guid? propertyId, Guid pageId, string legacySlug, CancellationToken ct = default)
    {
        var row = await FindTrackedRowAsync(propertyId, pageId, ct);
        if (row is null) return new("not_found", "Không tìm thấy trang nội dung.");
        var normalized = NormalizeSlug(legacySlug);
        var payload = ReadPayload(row);
        if (!payload.LegacySlugs.Any(x => string.Equals(x, normalized, StringComparison.OrdinalIgnoreCase)))
            return new("not_found", "Không tìm thấy redirect cũ.");
        var next = new Payload
        {
            Title = payload.Title, Slug = payload.Slug, IsPublished = payload.IsPublished,
            HideFromNavigation = payload.HideFromNavigation, SeoTitle = payload.SeoTitle,
            SeoDescription = payload.SeoDescription, OgImageUrl = payload.OgImageUrl,
            NoIndex = payload.NoIndex, CanonicalUrl = payload.CanonicalUrl,
            LegacySlugs = payload.LegacySlugs.Where(x => !string.Equals(x, normalized, StringComparison.OrdinalIgnoreCase)).ToList(),
            Sections = payload.Sections
        };
        row.ContentJson = Serialize(next);
        await db.SaveChangesAsync(ct);
        return null;
    }

    public async Task<CustomPageSectionMutationResult> CreateSectionAsync(Guid? propertyId, Guid pageId, SaveHomeSectionRequest request, CancellationToken ct = default)
    {
        var row = await FindTrackedRowAsync(propertyId, pageId, ct);
        if (row is null) return new(null, new("not_found", "Không tìm thấy trang nội dung."));
        var payload = ReadPayload(row);
        if (payload.Sections.Count >= MaxSectionsPerPage)
            return new(null, new("validation", $"Mỗi trang tối đa {MaxSectionsPerPage} khối."));
        var (content, error) = SiteContentService.ValidateSectionForReuse(request);
        if (error is not null) return new(null, error);
        var section = new PageSectionPayload
        {
            Id = Guid.NewGuid(), Type = request.Type, Name = Clean(request.Name) ?? SectionLabel(request.Type),
            Variant = Clean(request.Variant) ?? "wide", ContentJson = content!, SortOrder = payload.Sections.Count,
            IsVisible = request.IsVisible
        };
        var sections = payload.Sections.Append(section).ToList();
        var saveError = await SaveSectionsAsync(row, payload, sections, ct);
        return saveError is null ? new(ToDto(section), null) : new(null, saveError);
    }

    public async Task<CustomPageSectionMutationResult> UpdateSectionAsync(Guid? propertyId, Guid pageId, Guid sectionId, SaveHomeSectionRequest request, CancellationToken ct = default)
    {
        var row = await FindTrackedRowAsync(propertyId, pageId, ct);
        if (row is null) return new(null, new("not_found", "Không tìm thấy trang nội dung."));
        var payload = ReadPayload(row);
        var existing = payload.Sections.FirstOrDefault(x => x.Id == sectionId);
        if (existing is null) return new(null, new("not_found", "Không tìm thấy khối trên trang."));
        var (content, error) = SiteContentService.ValidateSectionForReuse(request);
        if (error is not null) return new(null, error);
        var updated = new PageSectionPayload
        {
            Id = existing.Id, Type = request.Type, Name = Clean(request.Name) ?? SectionLabel(request.Type),
            Variant = Clean(request.Variant) ?? "wide", ContentJson = content!, SortOrder = existing.SortOrder,
            IsVisible = request.IsVisible
        };
        var sections = payload.Sections.Select(x => x.Id == sectionId ? updated : x).ToList();
        var saveError = await SaveSectionsAsync(row, payload, sections, ct);
        return saveError is null ? new(ToDto(updated), null) : new(null, saveError);
    }

    public async Task<SiteContentError?> DeleteSectionAsync(Guid? propertyId, Guid pageId, Guid sectionId, CancellationToken ct = default)
    {
        var row = await FindTrackedRowAsync(propertyId, pageId, ct);
        if (row is null) return new("not_found", "Không tìm thấy trang nội dung.");
        var payload = ReadPayload(row);
        if (!payload.Sections.Any(x => x.Id == sectionId)) return new("not_found", "Không tìm thấy khối trên trang.");
        var sections = payload.Sections.Where(x => x.Id != sectionId).Select((x, index) => CopySection(x, index)).ToList();
        return await SaveSectionsAsync(row, payload, sections, ct);
    }

    public async Task<SiteContentError?> ReorderSectionsAsync(Guid? propertyId, Guid pageId, IReadOnlyList<Guid> ids, CancellationToken ct = default)
    {
        var row = await FindTrackedRowAsync(propertyId, pageId, ct);
        if (row is null) return new("not_found", "Không tìm thấy trang nội dung.");
        var payload = ReadPayload(row);
        if (ids.Count != payload.Sections.Count || ids.Distinct().Count() != ids.Count || payload.Sections.Any(x => !ids.Contains(x.Id)))
            return new("validation", "Danh sách sắp xếp không khớp các khối của trang hiện tại.");
        var byId = payload.Sections.ToDictionary(x => x.Id);
        var sections = ids.Select((id, index) => CopySection(byId[id], index)).ToList();
        return await SaveSectionsAsync(row, payload, sections, ct);
    }

    public static string NormalizeSlug(string? value) => InvalidSlugCharacters
        .Replace(RemoveDiacritics(value ?? string.Empty).Trim().ToLowerInvariant().Replace('_', '-'), "-")
        .Trim('-');

    public static string Url(string? siteSlug, string slug) => string.IsNullOrWhiteSpace(siteSlug)
        ? $"/{Uri.EscapeDataString(slug)}"
        : $"/h/{Uri.EscapeDataString(siteSlug)}/{Uri.EscapeDataString(slug)}";

    private async Task<SiteContentError?> ValidatePageFieldsAsync(Guid? propertyId, Guid? pageId, string slug, SaveCustomPageRequest request, CancellationToken ct)
    {
        if (!ValidSlug.IsMatch(slug) || ReservedSlugs.Contains(slug))
            return new("validation", "Slug không hợp lệ hoặc trùng đường dẫn hệ thống. Chỉ dùng chữ thường, số và dấu gạch ngang.");
        if (await SlugExistsAsync(propertyId, pageId, slug, ct))
            return new("validation", "Slug này đã được một trang khác sử dụng.");
        if (Clean(request.SeoTitle).Length > 200 || Clean(request.SeoDescription).Length > 500)
            return new("validation", "SEO title hoặc meta description vượt quá giới hạn cho phép.");
        if (!IsSafeImageUrl(request.OgImageUrl))
            return new("validation", "Ảnh Open Graph phải là URL http/https hoặc đường dẫn nội bộ hợp lệ.");
        return null;
    }

    private async Task<bool> SlugExistsAsync(Guid? propertyId, Guid? pageId, string slug, CancellationToken ct)
    {
        var rows = await db.Set<HomeSection>().AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.Type == MetadataSectionType && (pageId == null || x.Id != pageId))
            .Select(x => x.ContentJson)
            .ToListAsync(ct);
        return rows.Select(ReadPayload).Any(x =>
            string.Equals(x.Slug, slug, StringComparison.OrdinalIgnoreCase) ||
            x.LegacySlugs.Any(old => string.Equals(old, slug, StringComparison.OrdinalIgnoreCase)));
    }

    private async Task<HomeSection?> FindTrackedRowAsync(Guid? propertyId, Guid pageId, CancellationToken ct) =>
        await db.Set<HomeSection>().SingleOrDefaultAsync(x => x.Id == pageId && x.PropertyId == propertyId && x.Type == MetadataSectionType, ct);

    private async Task<SiteContentError?> SaveSectionsAsync(HomeSection row, Payload payload, IReadOnlyList<PageSectionPayload> sections, CancellationToken ct)
    {
        var next = new Payload
        {
            Title = payload.Title, Slug = payload.Slug, IsPublished = payload.IsPublished,
            HideFromNavigation = payload.HideFromNavigation, SeoTitle = payload.SeoTitle,
            SeoDescription = payload.SeoDescription, OgImageUrl = payload.OgImageUrl,
            NoIndex = payload.NoIndex, CanonicalUrl = payload.CanonicalUrl, LegacySlugs = payload.LegacySlugs, Sections = sections
        };
        var json = Serialize(next);
        if (json.Length > MaxPayloadLength) return new("validation", "Trang quá lớn. Hãy tách nội dung thành nhiều trang nhỏ hơn.");
        row.ContentJson = json;
        row.Name = payload.Title;
        row.IsVisible = false;
        row.SortOrder = int.MinValue + 20;
        await db.SaveChangesAsync(ct);
        return null;
    }

    private async Task<string?> ResolveSiteSlugAsync(Guid? propertyId, CancellationToken ct)
    {
        if (propertyId is null) return null;
        var property = await db.Properties.AsNoTracking()
            .Where(x => x.Id == propertyId)
            .Select(x => new { x.Code, x.SiteSlug })
            .SingleOrDefaultAsync(ct);
        return property is null ? null : PublicPropertyResolver.EffectiveSiteSlug(property.SiteSlug, property.Code);
    }

    private static CustomPageDto Read(HomeSection row, string? siteSlug)
    {
        var payload = ReadPayload(row);
        return new CustomPageDto(
            row.Id, row.PropertyId, payload.Title, payload.Slug, Url(siteSlug, payload.Slug), payload.IsPublished,
            payload.HideFromNavigation, payload.SeoTitle, payload.SeoDescription, payload.OgImageUrl,
            payload.NoIndex, payload.CanonicalUrl, payload.LegacySlugs,
            payload.Sections.OrderBy(x => x.SortOrder).Select(ToDto).ToList(), row.CreatedAtUtc, row.UpdatedAtUtc);
    }

    private static CustomPageSummaryDto ToSummary(CustomPageDto page) => new(
        page.Id, page.PropertyId, page.Title, page.Slug, page.Url, page.IsPublished, page.HideFromNavigation,
        page.SeoTitle, page.SeoDescription, page.OgImageUrl, page.NoIndex, page.CanonicalUrl, page.LegacySlugs,
        page.Sections.Count, page.CreatedAtUtc, page.UpdatedAtUtc);

    private static Payload ReadPayload(HomeSection row) => ReadPayload(row.ContentJson);
    private static Payload ReadPayload(string? json)
    {
        try { return JsonSerializer.Deserialize<Payload>(json ?? "{}", JsonOptions) ?? new Payload(); }
        catch (JsonException) { return new Payload(); }
    }

    private static HomeSectionDto ToDto(PageSectionPayload section) => new(
        section.Id, section.Type, section.Name, section.Variant, section.ContentJson, section.SortOrder, section.IsVisible);

    private static PageSectionPayload CopySection(PageSectionPayload source, int sortOrder) => new()
    {
        Id = source.Id, Type = source.Type, Name = source.Name, Variant = source.Variant,
        ContentJson = source.ContentJson, SortOrder = sortOrder, IsVisible = source.IsVisible
    };

    private static IReadOnlyList<PageSectionPayload> TemplateSections(string? template, string title)
    {
        var key = (template ?? string.Empty).Trim().ToLowerInvariant();
        if (key == "blank" || string.IsNullOrWhiteSpace(key)) return [];
        if (key == "contact") return Reindex([
            Section("Hero", "Mở đầu", "centered", new { eyebrow = "LIÊN HỆ", title, body = "Thông tin liên hệ, vị trí và cách kết nối với chúng tôi.", primaryText = "Đặt phòng", primaryUrl = "/booking", secondaryText = "", secondaryUrl = "", imageUrl = "" }),
            Section("Location", "Vị trí", "split", new { eyebrow = "VỊ TRÍ", title = "Tìm đường đến chúng tôi", body = "", address = "", mapUrl = "", embedUrl = "", nearby = Array.Empty<string>() }),
            Section("Cta", "Liên hệ nhanh", "card", new { title = "Bạn cần hỗ trợ?", body = "Gửi yêu cầu đặt phòng hoặc liên hệ trực tiếp để được hỗ trợ.", buttonText = "Đặt phòng", buttonUrl = "/booking" })
        ]);
        if (key == "promo") return Reindex([
            Section("Hero", "Mở đầu ưu đãi", "editorial", new { eyebrow = "ƯU ĐÃI", title, body = "Một landing page để giới thiệu gói lưu trú, chương trình theo mùa hoặc ưu đãi riêng.", primaryText = "Đặt ngay", primaryUrl = "/booking", secondaryText = "Xem phòng", secondaryUrl = "/rooms", imageUrl = "" }),
            Section("FeatureGrid", "Điểm nổi bật", "dark-band", new { eyebrow = "GÓI LƯU TRÚ", title = "Điểm nổi bật của ưu đãi", body = "Chỉnh trực tiếp nội dung, hình ảnh và điều kiện áp dụng.", items = new[] { "Giá rõ ràng", "Thời gian linh hoạt", "Xác nhận trực tiếp" }, imageUrl = "" }),
            Section("Cta", "Đặt ưu đãi", "offer", new { title = "Sẵn sàng đặt phòng?", body = "Chọn ngày phù hợp để kiểm tra tình trạng phòng.", buttonText = "Kiểm tra phòng", buttonUrl = "/booking" })
        ]);
        return Reindex([
            Section("Hero", "Giới thiệu", "centered", new { eyebrow = "GIỚI THIỆU", title, body = "Kể câu chuyện, phong cách và điều làm nơi lưu trú này khác biệt.", primaryText = "Xem phòng", primaryUrl = "/rooms", secondaryText = "Đặt phòng", secondaryUrl = "/booking", imageUrl = "" }),
            Section("FeatureGrid", "Câu chuyện", "split", new { eyebrow = "CÂU CHUYỆN", title = "Một không gian được tạo ra để nghỉ chậm lại", body = "Thay nội dung này bằng câu chuyện thương hiệu, trải nghiệm hoặc triết lý phục vụ.", items = new[] { "Không gian riêng tư", "Trải nghiệm rõ ràng", "Hỗ trợ trực tiếp" }, imageUrl = "" }),
            Section("Cta", "Kêu gọi hành động", "card", new { title = "Khám phá không gian phù hợp với bạn", body = "Xem phòng hoặc gửi yêu cầu đặt chỗ trực tiếp.", buttonText = "Xem phòng", buttonUrl = "/rooms" })
        ]);
    }

    private static PageSectionPayload Section(string type, string name, string variant, object content) => new()
    {
        Id = Guid.NewGuid(), Type = type, Name = name, Variant = variant,
        ContentJson = JsonSerializer.Serialize(content, JsonOptions), IsVisible = true
    };

    private static IReadOnlyList<PageSectionPayload> Reindex(IEnumerable<PageSectionPayload> sections) =>
        sections.Select((section, index) => CopySection(section, index)).ToList();

    private static string Serialize(Payload payload) => JsonSerializer.Serialize(payload, JsonOptions);
    private static string Slugify(string value) => NormalizeSlug(value);
    private static string Clean(string? value) => (value ?? string.Empty).Trim();
    private static string Limit(string value, int max) => value.Length <= max ? value : value[..max];
    private static IReadOnlyList<string> NextLegacySlugs(Payload current, string nextSlug)
    {
        if (string.Equals(current.Slug, nextSlug, StringComparison.OrdinalIgnoreCase)) return current.LegacySlugs;
        return current.LegacySlugs
            .Append(current.Slug)
            .Select(NormalizeSlug)
            .Where(x => !string.IsNullOrWhiteSpace(x) && !string.Equals(x, nextSlug, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .TakeLast(20)
            .ToList();
    }

    private static bool IsSafeCanonicalUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        var url = value.Trim();
        if (url.StartsWith("/", StringComparison.Ordinal) && !url.StartsWith("//", StringComparison.Ordinal)) return true;
        return Uri.TryCreate(url, UriKind.Absolute, out var absolute) && absolute.Scheme is "http" or "https";
    }

    private static bool IsSafeImageUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        var url = value.Trim();
        if (url.StartsWith("/", StringComparison.Ordinal) && !url.StartsWith("//", StringComparison.Ordinal)) return true;
        return Uri.TryCreate(url, UriKind.Absolute, out var absolute) && absolute.Scheme is "http" or "https";
    }
    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) builder.Append(ch == 'đ' ? 'd' : ch == 'Đ' ? 'D' : ch);
        }
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
    private static string SectionLabel(string type) => type switch
    {
        "Hero" => "Mở đầu", "AvailabilitySearch" => "Kiểm tra phòng nhanh", "BranchGrid" => "Danh sách cơ sở",
        "RoomGrid" => "Danh sách phòng", "FeatureGrid" => "Điểm nổi bật", "Faq" => "Câu hỏi thường gặp",
        "Location" => "Vị trí & chỉ đường", "PolicyGrid" => "Quy định lưu trú", "Cta" => "Kêu gọi hành động", _ => "Nội dung"
    };
}
