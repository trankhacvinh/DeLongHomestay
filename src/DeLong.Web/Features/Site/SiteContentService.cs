using System.Text.Json;
using System.Text.Json.Nodes;
using DeLong.Web.Common.Caching;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using Ganss.Xss;
using Microsoft.EntityFrameworkCore;
using ZiggyCreatures.Caching.Fusion;

namespace DeLong.Web.Features.Site;

public sealed record SiteSettingsDto(
    Guid PropertyId,
    string PropertyCode,
    string PropertyName,
    string TimeZoneId,
    string SiteName,
    string Tagline,
    string Address,
    string Phone,
    string Email,
    string FacebookUrl,
    string ZaloUrl,
    string GoogleMapsUrl,
    string CoverImageUrl,
    string LogoUrl,
    string FaviconUrl,
    string OgImageUrl,
    string MetaTitle,
    string MetaDescription,
    string CanonicalBaseUrl,
    string OgTitle,
    string OgDescription,
    string GoogleSiteVerification,
    bool RobotsIndex,
    string CustomCss,
    string CustomJs);

public sealed record HomeSectionDto(
    Guid Id,
    string Type,
    string Name,
    string Variant,
    string ContentJson,
    int SortOrder,
    bool IsVisible);

public sealed record SiteAdminDto(SiteSettingsDto Settings, IReadOnlyList<HomeSectionDto> Sections);
public sealed record GlobalSiteAdminDto(IReadOnlyList<HomeSectionDto> Sections);

public sealed class SaveSiteSettingsRequest
{
    public string? SiteName { get; init; }
    public string? Tagline { get; init; }
    public string? Address { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? FacebookUrl { get; init; }
    public string? ZaloUrl { get; init; }
    public string? GoogleMapsUrl { get; init; }
    public string? CoverImageUrl { get; init; }
    public string? LogoUrl { get; init; }
    public string? FaviconUrl { get; init; }
    public string? OgImageUrl { get; init; }
    public string? MetaTitle { get; init; }
    public string? MetaDescription { get; init; }
    public string? CanonicalBaseUrl { get; init; }
    public string? OgTitle { get; init; }
    public string? OgDescription { get; init; }
    public string? GoogleSiteVerification { get; init; }
    public bool RobotsIndex { get; init; } = true;
    public string? CustomCss { get; init; }
    public string? CustomJs { get; init; }
}

public sealed class SaveHomeSectionRequest
{
    public string Type { get; init; } = "RichText";
    public string Name { get; init; } = string.Empty;
    public string Variant { get; init; } = "default";
    public string ContentJson { get; init; } = "{}";
    public bool IsVisible { get; init; } = true;
}

public sealed record ReorderHomeSectionsRequest(IReadOnlyList<Guid> Ids);
public sealed record SiteContentError(string Code, string Message);

public sealed class SiteContentService(AppDbContext db, PublicPropertyResolver? resolver = null, IFusionCache? fusionCache = null)
{
    private readonly PublicPropertyResolver publicPropertyResolver = resolver ?? new PublicPropertyResolver(db);
    private readonly IFusionCache? cache = fusionCache;

    // Kept for compatibility with older tests/callers. New public code resolves by route scope.
    public const string PublicPropertyCode = PublicPropertyResolver.LegacyPropertyCode;
    private static readonly HashSet<string> AllowedSectionTypes =
        ["Hero", "AvailabilitySearch", "BranchGrid", "RoomGrid", "FeatureGrid", "Faq", "Location", "PolicyGrid", "RichText", "Cta"];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<GlobalSiteAdminDto> GetGlobalAdminAsync(CancellationToken ct = default)
    {
        await EnsureGlobalDefaultSectionsAsync(ct);
        var sections = await db.Set<HomeSection>().AsNoTracking()
            .Where(x => x.PropertyId == null)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.CreatedAtUtc)
            .ToListAsync(ct);
        return new(sections.Select(ToDto).ToList());
    }

    public async Task<IReadOnlyList<HomeSectionDto>> GetGlobalPublicSectionsAsync(CancellationToken ct = default)
    {
        async Task<IReadOnlyList<HomeSectionDto>> LoadAsync(CancellationToken token)
        {
            var sections = await db.Set<HomeSection>().AsNoTracking()
                .Where(x => x.PropertyId == null)
                .OrderBy(x => x.SortOrder).ThenBy(x => x.CreatedAtUtc)
                .ToListAsync(token);
            return sections.Count > 0 ? sections.Select(ToDto).ToList() : GlobalDefaultSections().Select(ToDto).ToList();
        }
        if (cache is null) return await LoadAsync(ct);
        return await cache.GetOrSetAsync<IReadOnlyList<HomeSectionDto>>(
            PublicCacheKeys.GlobalSections,
            async (_, token) => await LoadAsync(token),
            tags: [PublicCacheKeys.Tag],
            token: ct);
    }

    public async Task<(HomeSectionDto? Section, SiteContentError? Error)> CreateGlobalSectionAsync(
        SaveHomeSectionRequest request,
        CancellationToken ct = default)
    {
        var (content, error) = ValidateSection(request);
        if (error is not null) return (null, error);
        var nextOrder = (await db.Set<HomeSection>().Where(x => x.PropertyId == null)
            .MaxAsync(x => (int?)x.SortOrder, ct) ?? -1) + 1;
        var section = new HomeSection
        {
            PropertyId = null,
            Type = request.Type,
            Name = Clean(request.Name) ?? SectionLabel(request.Type),
            Variant = Clean(request.Variant) ?? "default",
            ContentJson = content!,
            SortOrder = nextOrder,
            IsVisible = request.IsVisible
        };
        db.Set<HomeSection>().Add(section);
        await db.SaveChangesAsync(ct);
        return (ToDto(section), null);
    }

    public async Task<(HomeSectionDto? Section, SiteContentError? Error)> UpdateGlobalSectionAsync(
        Guid sectionId,
        SaveHomeSectionRequest request,
        CancellationToken ct = default)
    {
        var section = await db.Set<HomeSection>()
            .SingleOrDefaultAsync(x => x.Id == sectionId && x.PropertyId == null, ct);
        if (section is null) return (null, new("not_found", "Không tìm thấy khối trang chủ chung."));
        var (content, error) = ValidateSection(request);
        if (error is not null) return (null, error);
        section.Type = request.Type;
        section.Name = Clean(request.Name) ?? SectionLabel(request.Type);
        section.Variant = Clean(request.Variant) ?? "default";
        section.ContentJson = content!;
        section.IsVisible = request.IsVisible;
        await db.SaveChangesAsync(ct);
        return (ToDto(section), null);
    }

    public async Task<SiteContentError?> DeleteGlobalSectionAsync(Guid sectionId, CancellationToken ct = default)
    {
        var section = await db.Set<HomeSection>()
            .SingleOrDefaultAsync(x => x.Id == sectionId && x.PropertyId == null, ct);
        if (section is null) return new("not_found", "Không tìm thấy khối trang chủ chung.");
        db.Set<HomeSection>().Remove(section);
        await db.SaveChangesAsync(ct);
        return null;
    }

    public async Task<SiteContentError?> ReorderGlobalAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default)
    {
        var sections = await db.Set<HomeSection>().Where(x => x.PropertyId == null).ToListAsync(ct);
        if (ids.Count != sections.Count || ids.Distinct().Count() != ids.Count || sections.Any(x => !ids.Contains(x.Id)))
            return new("validation", "Danh sách sắp xếp không khớp các khối trang chủ chung hiện tại.");
        var order = ids.Select((id, index) => new { id, index }).ToDictionary(x => x.id, x => x.index);
        foreach (var section in sections) section.SortOrder = order[section.Id];
        await db.SaveChangesAsync(ct);
        return null;
    }

    public async Task<SiteAdminDto?> GetAdminAsync(Guid propertyId, CancellationToken ct = default)
    {
        var property = await db.Properties.SingleOrDefaultAsync(x => x.Id == propertyId, ct);
        if (property is null) return null;
        await EnsureSettingsAndDefaultSectionsAsync(property, ct);
        return await ReadSiteAsync(propertyId, ct);
    }

    public Task<SiteAdminDto?> GetPublicAsync(CancellationToken ct = default) => GetPublicAsync(null, ct);

    public async Task<SiteAdminDto?> GetPublicAsync(string? siteSlug, CancellationToken ct = default)
    {
        var property = await publicPropertyResolver.ResolveAsync(siteSlug, ct);
        if (property is null) return null;
        if (cache is null) return await ReadSiteAsync(property.Id, ct);
        return await cache.GetOrSetAsync<SiteAdminDto?>(
            PublicCacheKeys.Site(property.Id),
            async (_, token) => await ReadSiteAsync(property.Id, token),
            tags: [PublicCacheKeys.Tag],
            token: ct);
    }

    public async Task<(SiteSettingsDto? Settings, SiteContentError? Error)> SaveSettingsAsync(
        Guid propertyId,
        SaveSiteSettingsRequest request,
        bool allowCustomCode,
        CancellationToken ct = default)
    {
        var property = await db.Properties.SingleOrDefaultAsync(x => x.Id == propertyId, ct);
        if (property is null) return (null, new("not_found", "Không tìm thấy cơ sở."));

        if (Length(request.SiteName) > 200 || Length(request.Tagline) > 300 || Length(request.MetaTitle) > 200 || Length(request.MetaDescription) > 500)
            return (null, new("validation", "Một hoặc nhiều trường nội dung vượt quá độ dài cho phép."));
        if (!IsOptionalHttpUrl(request.CanonicalBaseUrl) || !IsOptionalHttpUrl(request.FacebookUrl) ||
            !IsOptionalHttpUrl(request.ZaloUrl) || !IsOptionalHttpUrl(request.GoogleMapsUrl))
            return (null, new("validation", "Các đường dẫn website/social phải là URL http hoặc https hợp lệ."));
        if (Length(request.CustomCss) > 50_000 || Length(request.CustomJs) > 100_000)
            return (null, new("validation", "Custom CSS/JS vượt quá giới hạn an toàn."));

        var settings = await db.Set<PropertySiteSettings>().SingleOrDefaultAsync(x => x.PropertyId == propertyId, ct);
        if (settings is null)
        {
            settings = new PropertySiteSettings { PropertyId = propertyId };
            db.Set<PropertySiteSettings>().Add(settings);
        }

        settings.SiteName = Clean(request.SiteName) ?? property.Name;
        settings.Tagline = Clean(request.Tagline);
        settings.Address = Clean(request.Address);
        settings.Phone = Clean(request.Phone);
        settings.Email = Clean(request.Email);
        settings.FacebookUrl = Clean(request.FacebookUrl);
        settings.ZaloUrl = Clean(request.ZaloUrl);
        settings.GoogleMapsUrl = Clean(request.GoogleMapsUrl);
        settings.CoverImageUrl = Clean(request.CoverImageUrl);
        settings.LogoUrl = Clean(request.LogoUrl);
        settings.FaviconUrl = Clean(request.FaviconUrl);
        settings.OgImageUrl = Clean(request.OgImageUrl);
        settings.MetaTitle = Clean(request.MetaTitle);
        settings.MetaDescription = Clean(request.MetaDescription);
        settings.CanonicalBaseUrl = Clean(request.CanonicalBaseUrl)?.TrimEnd('/');
        settings.OgTitle = Clean(request.OgTitle);
        settings.OgDescription = Clean(request.OgDescription);
        settings.GoogleSiteVerification = Clean(request.GoogleSiteVerification);
        settings.RobotsIndex = request.RobotsIndex;
        if (allowCustomCode)
        {
            settings.CustomCss = request.CustomCss?.Trim();
            settings.CustomJs = request.CustomJs?.Trim();
        }
        await db.SaveChangesAsync(ct);
        return ((await ReadSiteAsync(propertyId, ct))!.Settings, null);
    }

    public async Task<(HomeSectionDto? Section, SiteContentError? Error)> CreateSectionAsync(
        Guid propertyId,
        SaveHomeSectionRequest request,
        CancellationToken ct = default)
    {
        if (!await db.Properties.AnyAsync(x => x.Id == propertyId, ct))
            return (null, new("not_found", "Không tìm thấy cơ sở."));
        var (content, error) = ValidateSection(request);
        if (error is not null) return (null, error);
        var nextOrder = (await db.Set<HomeSection>().Where(x => x.PropertyId == propertyId).MaxAsync(x => (int?)x.SortOrder, ct) ?? -1) + 1;
        var section = new HomeSection
        {
            PropertyId = propertyId,
            Type = request.Type,
            Name = Clean(request.Name) ?? SectionLabel(request.Type),
            Variant = Clean(request.Variant) ?? "default",
            ContentJson = content!,
            SortOrder = nextOrder,
            IsVisible = request.IsVisible
        };
        db.Set<HomeSection>().Add(section);
        await db.SaveChangesAsync(ct);
        return (ToDto(section), null);
    }

    public async Task<(HomeSectionDto? Section, SiteContentError? Error)> UpdateSectionAsync(
        Guid propertyId,
        Guid sectionId,
        SaveHomeSectionRequest request,
        CancellationToken ct = default)
    {
        var section = await db.Set<HomeSection>().SingleOrDefaultAsync(x => x.Id == sectionId && x.PropertyId == propertyId, ct);
        if (section is null) return (null, new("not_found", "Không tìm thấy khối trang chủ."));
        var (content, error) = ValidateSection(request);
        if (error is not null) return (null, error);
        section.Type = request.Type;
        section.Name = Clean(request.Name) ?? SectionLabel(request.Type);
        section.Variant = Clean(request.Variant) ?? "default";
        section.ContentJson = content!;
        section.IsVisible = request.IsVisible;
        await db.SaveChangesAsync(ct);
        return (ToDto(section), null);
    }

    public async Task<SiteContentError?> DeleteSectionAsync(Guid propertyId, Guid sectionId, CancellationToken ct = default)
    {
        var section = await db.Set<HomeSection>().SingleOrDefaultAsync(x => x.Id == sectionId && x.PropertyId == propertyId, ct);
        if (section is null) return new("not_found", "Không tìm thấy khối trang chủ.");
        db.Set<HomeSection>().Remove(section);
        await db.SaveChangesAsync(ct);
        return null;
    }

    public async Task<SiteContentError?> ReorderAsync(Guid propertyId, IReadOnlyList<Guid> ids, CancellationToken ct = default)
    {
        var sections = await db.Set<HomeSection>().Where(x => x.PropertyId == propertyId).ToListAsync(ct);
        if (ids.Count != sections.Count || ids.Distinct().Count() != ids.Count || sections.Any(x => !ids.Contains(x.Id)))
            return new("validation", "Danh sách sắp xếp không khớp các khối hiện tại.");
        var order = ids.Select((id, index) => new { id, index }).ToDictionary(x => x.id, x => x.index);
        foreach (var section in sections) section.SortOrder = order[section.Id];
        await db.SaveChangesAsync(ct);
        return null;
    }

    private async Task EnsureGlobalDefaultSectionsAsync(CancellationToken ct)
    {
        if (await db.Set<HomeSection>().AnyAsync(x => x.PropertyId == null, ct)) return;
        db.Set<HomeSection>().AddRange(GlobalDefaultSections());
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureSettingsAndDefaultSectionsAsync(Property property, CancellationToken ct)
    {
        if (!await db.Set<PropertySiteSettings>().AnyAsync(x => x.PropertyId == property.Id, ct))
        {
            db.Set<PropertySiteSettings>().Add(new PropertySiteSettings
            {
                PropertyId = property.Id,
                SiteName = property.Name,
                MetaTitle = property.Name,
                RobotsIndex = true
            });
        }
        if (!await db.Set<HomeSection>().AnyAsync(x => x.PropertyId == property.Id, ct))
        {
            var defaults = DefaultSections(property);
            db.Set<HomeSection>().AddRange(defaults);
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task<SiteAdminDto?> ReadSiteAsync(Guid propertyId, CancellationToken ct)
    {
        var property = await db.Properties.AsNoTracking().SingleOrDefaultAsync(x => x.Id == propertyId, ct);
        if (property is null) return null;
        var settings = await db.Set<PropertySiteSettings>().AsNoTracking().SingleOrDefaultAsync(x => x.PropertyId == propertyId, ct);
        var sectionEntities = await db.Set<HomeSection>().AsNoTracking().Where(x => x.PropertyId == propertyId)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.CreatedAtUtc).ToListAsync(ct);
        var sections = sectionEntities.Select(ToDto).ToList();
        return new SiteAdminDto(ToDto(property, settings), sections);
    }

    private static SiteSettingsDto ToDto(Property property, PropertySiteSettings? settings) => new(
        property.Id,
        property.Code,
        property.Name,
        property.TimeZoneId,
        Clean(settings?.SiteName) ?? property.Name,
        Clean(settings?.Tagline) ?? string.Empty,
        Clean(settings?.Address) ?? string.Empty,
        Clean(settings?.Phone) ?? string.Empty,
        Clean(settings?.Email) ?? string.Empty,
        Clean(settings?.FacebookUrl) ?? string.Empty,
        Clean(settings?.ZaloUrl) ?? string.Empty,
        Clean(settings?.GoogleMapsUrl) ?? string.Empty,
        Clean(settings?.CoverImageUrl) ?? string.Empty,
        Clean(settings?.LogoUrl) ?? string.Empty,
        Clean(settings?.FaviconUrl) ?? string.Empty,
        Clean(settings?.OgImageUrl) ?? string.Empty,
        Clean(settings?.MetaTitle) ?? property.Name,
        Clean(settings?.MetaDescription) ?? string.Empty,
        Clean(settings?.CanonicalBaseUrl) ?? string.Empty,
        Clean(settings?.OgTitle) ?? string.Empty,
        Clean(settings?.OgDescription) ?? string.Empty,
        Clean(settings?.GoogleSiteVerification) ?? string.Empty,
        settings?.RobotsIndex ?? true,
        settings?.CustomCss ?? string.Empty,
        settings?.CustomJs ?? string.Empty);

    private static HomeSectionDto ToDto(HomeSection x) => new(x.Id, x.Type, x.Name, x.Variant, x.ContentJson, x.SortOrder, x.IsVisible);

    private static (string? Content, SiteContentError? Error) ValidateSection(SaveHomeSectionRequest request)
    {
        if (!AllowedSectionTypes.Contains(request.Type))
            return (null, new("validation", "Loại khối trang chủ không được hỗ trợ."));
        if (Length(request.ContentJson) > 30_000)
            return (null, new("validation", "Nội dung khối quá lớn."));
        JsonObject? json;
        try { json = JsonNode.Parse(string.IsNullOrWhiteSpace(request.ContentJson) ? "{}" : request.ContentJson) as JsonObject; }
        catch { return (null, new("validation", "Nội dung khối không phải JSON hợp lệ.")); }
        if (json is null) return (null, new("validation", "Nội dung khối phải là một object."));
        if (request.Type == "Location")
        {
            var mapUrl = json["mapUrl"]?.GetValue<string>();
            var embedUrl = json["embedUrl"]?.GetValue<string>();
            if (!IsOptionalHttpUrl(mapUrl) || !IsOptionalHttpUrl(embedUrl))
                return (null, new("validation", "Đường dẫn bản đồ phải là URL http hoặc https hợp lệ."));
        }
        if (request.Type == "RichText" && json["html"] is JsonValue value && value.TryGetValue<string>(out var html))
        {
            var sanitizer = new HtmlSanitizer();
            sanitizer.AllowedAttributes.Add("class");
            json["html"] = sanitizer.Sanitize(html);
        }
        return (json.ToJsonString(JsonOptions), null);
    }

    private static IReadOnlyList<HomeSection> GlobalDefaultSections() =>
    [
        New(null, 0, "Hero", "Mở đầu trang chung", "split", new
        {
            eyebrow = "DE LONG HOMESTAY",
            title = "Chọn một không gian phù hợp với nhịp nghỉ của bạn.",
            body = "Khám phá các cơ sở và phòng đang mở trên cùng một website; mỗi cơ sở vẫn có trang riêng với nội dung, phòng và luồng đặt phòng độc lập.",
            primaryText = "Xem tất cả phòng", primaryUrl = "/rooms",
            secondaryText = "Khám phá các cơ sở", secondaryUrl = "/#co-so"
        }),
        New(null, 1, "BranchGrid", "Danh sách cơ sở", "grid-3", new
        {
            eyebrow = "CƠ SỞ",
            title = "Chọn nơi bạn muốn ghé",
            propertyIds = Array.Empty<Guid>()
        }),
        New(null, 2, "RoomGrid", "Phòng nổi bật", "grid-3", new
        {
            eyebrow = "PHÒNG",
            title = "Một vài lựa chọn đang mở",
            mode = "all",
            limit = 6,
            propertyQuotas = new Dictionary<string, int>(),
            roomIds = Array.Empty<Guid>()
        }),
        New(null, 3, "AvailabilitySearch", "Kiểm tra nhanh", "card", new
        {
            title = "Chọn cơ sở và ngày bạn muốn ghé"
        }),
        New(null, 4, "FeatureGrid", "Giới thiệu", "split", new
        {
            eyebrow = "DE LONG HOMESTAY",
            title = "Nhiều cơ sở, một trải nghiệm đặt phòng rõ ràng.",
            body = "Bạn có thể xem tất cả phòng trên một danh sách, lọc theo cơ sở rồi đi sâu vào trang riêng của từng nơi.",
            items = new[] { "Phòng và giá rõ ràng", "Tách dữ liệu theo cơ sở", "Đặt phòng theo đúng chi nhánh", "Tra cứu thuận tiện" }
        })
    ];

    private static IReadOnlyList<HomeSection> DefaultSections(Property property) =>
    [
        New(property.Id, 0, "Hero", "Mở đầu", "split", new
        {
            eyebrow = property.Name.ToUpperInvariant(),
            title = "Một khoảng nghỉ riêng tư, vừa đủ để chậm lại.",
            body = $"Khám phá các không gian nghỉ tại {property.Name}, phù hợp cho nghỉ ngắn, qua đêm hoặc lưu trú nhiều ngày.",
            primaryText = "Kiểm tra phòng trống", primaryUrl = "/booking",
            secondaryText = "Xem phòng", secondaryUrl = "/rooms"
        }),
        New(property.Id, 1, "AvailabilitySearch", "Kiểm tra nhanh", "card", new { title = "Chọn ngày bạn muốn ghé" }),
        New(property.Id, 2, "RoomGrid", "Danh sách phòng", "grid-3", new { eyebrow = "KHÔNG GIAN", title = "Chọn căn phòng hợp với nhịp của bạn", limit = 6 }),
        New(property.Id, 3, "FeatureGrid", "Giới thiệu cuối trang", "split", new
        {
            eyebrow = property.Name.ToUpperInvariant(),
            title = "Một khoảng nghỉ được tổ chức theo cách bạn cần.",
            body = "Website hiển thị phòng, giá và thời gian rõ ràng; đội ngũ cơ sở xác nhận trực tiếp trước khi giữ phòng chính thức.",
            items = new[] { "Nhận phòng linh hoạt", "Không gian riêng tư", "Giá hiển thị rõ ràng", "Nhân viên xác nhận trực tiếp" }
        })
    ];

    private static HomeSection New(Guid? propertyId, int order, string type, string name, string variant, object content) => new()
    {
        PropertyId = propertyId,
        SortOrder = order,
        Type = type,
        Name = name,
        Variant = variant,
        ContentJson = JsonSerializer.Serialize(content, JsonOptions),
        IsVisible = true
    };

    private static string SectionLabel(string type) => type switch
    {
        "Hero" => "Mở đầu",
        "AvailabilitySearch" => "Kiểm tra phòng nhanh",
        "BranchGrid" => "Danh sách cơ sở",
        "RoomGrid" => "Danh sách phòng",
        "FeatureGrid" => "Điểm nổi bật",
        "Faq" => "Câu hỏi thường gặp",
        "Location" => "Vị trí & chỉ đường",
        "PolicyGrid" => "Quy định lưu trú",
        "Cta" => "Kêu gọi hành động",
        _ => "Nội dung"
    };

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static int Length(string? value) => value?.Length ?? 0;
    private static bool IsOptionalHttpUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        return Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
    }
}
