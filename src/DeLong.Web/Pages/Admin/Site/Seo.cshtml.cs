using DeLong.Web.Common.Security;
using DeLong.Web.Data;
using DeLong.Web.Features.Site;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Pages.Admin.Site;

[Authorize(Policy = "ManageSiteContent")]
public sealed class SeoModel(
    CurrentPropertyService currentPropertyService,
    PublicPropertyResolver propertyResolver,
    SiteContentService siteContentService,
    CustomPageStore customPageStore,
    AppDbContext db) : PageModel
{
    public bool IsGlobal { get; private set; }
    public bool IsAdmin { get; private set; }
    public Guid? PropertyId { get; private set; }
    public string ScopeName { get; private set; } = string.Empty;
    public string SiteSlug { get; private set; } = string.Empty;
    public string HomeUrl { get; private set; } = "/";
    public string SitemapUrl { get; private set; } = "/sitemap.xml";
    public string RobotsUrl { get; private set; } = "/robots.txt";
    public string SettingsUrl { get; private set; } = "/Admin/Site/Global";
    public string PagesUrl { get; private set; } = "/Admin/Site/Pages?scope=global";
    public string BlogAdminUrl { get; private set; } = "/Admin/Site/Editorial?tab=blog";
    public SeoScopeAuditVm ScopeAudit { get; private set; } = new();
    public IReadOnlyList<SeoPageAuditVm> Pages { get; private set; } = [];
    public IReadOnlyList<SeoRoomAuditVm> Rooms { get; private set; } = [];
    public IReadOnlyList<SeoBlogAuditVm> BlogPosts { get; private set; } = [];
    public IReadOnlyList<SeoPageAuditVm> VisiblePages { get; private set; } = [];
    public IReadOnlyList<SeoRoomAuditVm> VisibleRooms { get; private set; } = [];
    public IReadOnlyList<SeoBlogAuditVm> VisibleBlogPosts { get; private set; } = [];
    public IReadOnlyList<SeoRedirectVm> Redirects { get; private set; } = [];
    public IReadOnlyList<CurrentPropertyDto> Properties { get; private set; } = [];
    public string ContentFilter { get; private set; } = "all";
    public string SearchQuery { get; private set; } = string.Empty;
    public int PageNumber { get; private set; } = 1;
    public int PageSize { get; private set; } = 20;
    public int TotalItems { get; private set; }
    public int TotalPages { get; private set; } = 1;
    public int FirstItemNumber => TotalItems == 0 ? 0 : ((PageNumber - 1) * PageSize) + 1;
    public int LastItemNumber => TotalItems == 0 ? 0 : Math.Min(PageNumber * PageSize, TotalItems);
    public string CurrentListUrl { get; private set; } = "/Admin/Site/Seo";

    [TempData] public string? StatusMessage { get; set; }
    [TempData] public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(
        Guid? propertyId,
        string? scope,
        string? type,
        string? q,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        if (!await LoadAsync(propertyId, scope, type, q, page, pageSize, ct)) return IsAdmin ? Page() : Forbid();
        return Page();
    }

    public async Task<IActionResult> OnPostUpdateScopeAsync(
        Guid? propertyId,
        string? scope,
        string? metaTitle,
        string? metaDescription,
        string? ogImageUrl,
        string? returnUrl,
        CancellationToken ct)
    {
        var resolved = await ResolveScopeAsync(propertyId, scope, ct);
        if (!resolved.Allowed) return Forbid();
        metaTitle = Clean(metaTitle);
        metaDescription = Clean(metaDescription);
        ogImageUrl = Clean(ogImageUrl);
        if (metaTitle.Length > 200 || metaDescription.Length > 500 || ogImageUrl.Length > 1200)
        {
            ErrorMessage = "Thông tin SEO vượt quá độ dài cho phép.";
            return ReturnToSeo(returnUrl, resolved.IsGlobal, resolved.PropertyId);
        }
        if (!IsSafeImageUrl(ogImageUrl))
        {
            ErrorMessage = "Ảnh chia sẻ không hợp lệ.";
            return ReturnToSeo(returnUrl, resolved.IsGlobal, resolved.PropertyId);
        }

        if (resolved.IsGlobal)
        {
            var activeProperties = await propertyResolver.GetActiveAsync(ct);
            var current = await GlobalSiteBrandingStore.ResolveAsync(db, siteContentService, activeProperties, ct);
            var result = await GlobalSiteBrandingStore.SaveAsync(db, new SaveGlobalSiteBrandingRequest
            {
                SiteName = current.OverrideSiteName,
                Tagline = current.OverrideTagline,
                LogoUrl = current.OverrideLogoUrl,
                FaviconUrl = current.OverrideFaviconUrl,
                OgImageUrl = ogImageUrl,
                MetaTitle = metaTitle,
                MetaDescription = metaDescription
            }, ct);
            if (!result.Success) ErrorMessage = result.Error;
            else StatusMessage = "Đã lưu thông tin trang chủ.";
        }
        else
        {
            var site = await siteContentService.GetAdminAsync(resolved.PropertyId!.Value, ct);
            if (site is null) return NotFound();
            var s = site.Settings;
            var (settings, error) = await siteContentService.SaveSettingsAsync(resolved.PropertyId.Value, new SaveSiteSettingsRequest
            {
                SiteName = s.SiteName,
                Tagline = s.Tagline,
                Address = s.Address,
                Phone = s.Phone,
                Email = s.Email,
                FacebookUrl = s.FacebookUrl,
                ZaloUrl = s.ZaloUrl,
                GoogleMapsUrl = s.GoogleMapsUrl,
                CoverImageUrl = s.CoverImageUrl,
                LogoUrl = s.LogoUrl,
                FaviconUrl = s.FaviconUrl,
                OgImageUrl = ogImageUrl,
                MetaTitle = metaTitle,
                MetaDescription = metaDescription,
                CanonicalBaseUrl = s.CanonicalBaseUrl,
                OgTitle = s.OgTitle,
                OgDescription = s.OgDescription,
                GoogleSiteVerification = s.GoogleSiteVerification,
                RobotsIndex = s.RobotsIndex
            }, allowCustomCode: false, ct);
            _ = settings;
            if (error is not null) ErrorMessage = error.Message;
            else StatusMessage = "Đã lưu thông tin trang chủ.";
        }
        return ReturnToSeo(returnUrl, resolved.IsGlobal, resolved.PropertyId);
    }

    public async Task<IActionResult> OnPostUpdatePageBasicAsync(
        Guid? propertyId,
        string? scope,
        Guid pageId,
        string? title,
        string? seoTitle,
        string? seoDescription,
        string? ogImageUrl,
        string? returnUrl,
        CancellationToken ct)
    {
        var resolved = await ResolveScopeAsync(propertyId, scope, ct);
        if (!resolved.Allowed) return Forbid();
        var current = await customPageStore.GetAsync(resolved.PropertyId, pageId, ct);
        if (current is null) return NotFound();
        var result = await customPageStore.UpdateAsync(resolved.PropertyId, pageId, new SaveCustomPageRequest
        {
            Title = title,
            Slug = current.Slug,
            IsPublished = current.IsPublished,
            HideFromNavigation = current.HideFromNavigation,
            SeoTitle = seoTitle,
            SeoDescription = seoDescription,
            OgImageUrl = ogImageUrl
        }, ct);
        if (result.Error is not null) ErrorMessage = result.Error.Message;
        else StatusMessage = $"Đã lưu {result.Page?.Title ?? current.Title}.";
        return ReturnToSeo(returnUrl, resolved.IsGlobal, resolved.PropertyId);
    }

    public async Task<IActionResult> OnPostUpdatePageAsync(
        Guid? propertyId,
        string? scope,
        Guid pageId,
        bool noIndex,
        string? canonicalUrl,
        string? returnUrl,
        CancellationToken ct)
    {
        var resolved = await ResolveScopeAsync(propertyId, scope, ct);
        if (!resolved.Allowed) return Forbid();
        var error = await customPageStore.UpdateSeoAsync(resolved.PropertyId, pageId, noIndex, canonicalUrl, ct);
        if (error is null) StatusMessage = "Đã lưu cài đặt index của trang.";
        else ErrorMessage = error.Message;
        return ReturnToSeo(returnUrl, resolved.IsGlobal, resolved.PropertyId);
    }

    public async Task<IActionResult> OnPostUpdateRoomAsync(
        Guid? propertyId,
        string? scope,
        Guid roomId,
        string? name,
        string? shortDescription,
        string? returnUrl,
        CancellationToken ct)
    {
        var resolved = await ResolveScopeAsync(propertyId, scope, ct);
        if (!resolved.Allowed || resolved.IsGlobal || resolved.PropertyId is null) return Forbid();
        name = Clean(name);
        shortDescription = Clean(shortDescription);
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200 || shortDescription.Length > 1200)
        {
            ErrorMessage = "Tên phòng hoặc mô tả không hợp lệ.";
            return ReturnToSeo(returnUrl, false, resolved.PropertyId);
        }
        var room = await db.Rooms.SingleOrDefaultAsync(x => x.Id == roomId && x.PropertyId == resolved.PropertyId.Value, ct);
        if (room is null) return NotFound();
        room.Name = name;
        room.ShortDescription = string.IsNullOrWhiteSpace(shortDescription) ? null : shortDescription;
        await db.SaveChangesAsync(ct);
        StatusMessage = $"Đã lưu {room.Name}.";
        return ReturnToSeo(returnUrl, false, resolved.PropertyId);
    }

    public async Task<IActionResult> OnPostUpdateBlogAsync(
        Guid? propertyId,
        string? scope,
        Guid postId,
        string? title,
        string? excerpt,
        string? coverImageUrl,
        string? returnUrl,
        CancellationToken ct)
    {
        var resolved = await ResolveScopeAsync(propertyId, scope, ct);
        if (!resolved.Allowed || resolved.IsGlobal || resolved.PropertyId is null) return Forbid();
        title = Clean(title);
        excerpt = Clean(excerpt);
        coverImageUrl = Clean(coverImageUrl);
        if (string.IsNullOrWhiteSpace(title) || title.Length > 240 || excerpt.Length > 800 || coverImageUrl.Length > 1000)
        {
            ErrorMessage = "Tiêu đề, mô tả hoặc ảnh bài viết không hợp lệ.";
            return ReturnToSeo(returnUrl, false, resolved.PropertyId);
        }
        if (!IsSafeImageUrl(coverImageUrl))
        {
            ErrorMessage = "Ảnh bài viết không hợp lệ.";
            return ReturnToSeo(returnUrl, false, resolved.PropertyId);
        }
        var post = await db.BlogPosts.SingleOrDefaultAsync(x => x.Id == postId && x.PropertyId == resolved.PropertyId.Value, ct);
        if (post is null) return NotFound();
        post.Title = title;
        post.Excerpt = excerpt;
        post.CoverImageUrl = string.IsNullOrWhiteSpace(coverImageUrl) ? null : coverImageUrl;
        await db.SaveChangesAsync(ct);
        StatusMessage = $"Đã lưu {post.Title}.";
        return ReturnToSeo(returnUrl, false, resolved.PropertyId);
    }

    public async Task<IActionResult> OnPostRemoveRedirectAsync(
        Guid? propertyId,
        string? scope,
        Guid pageId,
        string legacySlug,
        string? returnUrl,
        CancellationToken ct)
    {
        var resolved = await ResolveScopeAsync(propertyId, scope, ct);
        if (!resolved.Allowed) return Forbid();
        var error = await customPageStore.RemoveLegacySlugAsync(resolved.PropertyId, pageId, legacySlug, ct);
        if (error is null) StatusMessage = "Đã gỡ đường dẫn cũ.";
        else ErrorMessage = error.Message;
        return ReturnToSeo(returnUrl, resolved.IsGlobal, resolved.PropertyId);
    }

    private async Task<bool> LoadAsync(
        Guid? propertyId,
        string? scope,
        string? type,
        string? q,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        var resolved = await ResolveScopeAsync(propertyId, scope, ct);
        IsAdmin = User.IsInRole("Admin");
        Properties = await currentPropertyService.GetAccessibleAsync(User, ct);
        if (!resolved.Allowed) return false;

        IsGlobal = resolved.IsGlobal;
        PropertyId = resolved.PropertyId;
        ScopeName = resolved.ScopeName;
        SiteSlug = resolved.SiteSlug;
        HomeUrl = IsGlobal ? "/" : PublicUrlBuilder.PropertyHome(SiteSlug);
        SitemapUrl = IsGlobal ? "/sitemap.xml" : $"{HomeUrl.TrimEnd('/')}/sitemap.xml";
        RobotsUrl = IsGlobal ? "/robots.txt" : $"{HomeUrl.TrimEnd('/')}/robots.txt";
        SettingsUrl = IsGlobal ? "/Admin/Site/Global" : $"/Admin/Site?propertyId={PropertyId}";
        PagesUrl = IsGlobal ? "/Admin/Site/Pages?scope=global" : $"/Admin/Site/Pages?propertyId={PropertyId}";
        BlogAdminUrl = IsGlobal ? "/Admin/Site/Editorial?tab=blog" : $"/Admin/Site/Editorial?propertyId={PropertyId}&tab=blog";

        ContentFilter = NormalizeFilter(type);
        SearchQuery = Clean(q);
        if (SearchQuery.Length > 120) SearchQuery = SearchQuery[..120];
        PageSize = pageSize is 50 or 100 ? pageSize : 20;
        PageNumber = Math.Max(1, page);

        string metaTitle;
        string metaDescription;
        string ogImage;
        bool indexed;
        if (IsGlobal)
        {
            var activeProperties = await propertyResolver.GetActiveAsync(ct);
            var branding = await GlobalSiteBrandingStore.ResolveAsync(db, siteContentService, activeProperties, ct);
            metaTitle = branding.MetaTitle;
            metaDescription = branding.MetaDescription;
            ogImage = branding.OgImageUrl;
            indexed = true;
        }
        else
        {
            var site = await siteContentService.GetAdminAsync(PropertyId!.Value, ct);
            metaTitle = site?.Settings.MetaTitle ?? string.Empty;
            metaDescription = site?.Settings.MetaDescription ?? string.Empty;
            ogImage = site?.Settings.OgImageUrl ?? string.Empty;
            indexed = site?.Settings.RobotsIndex ?? true;
        }

        ScopeAudit = new SeoScopeAuditVm
        {
            TitleOk = !string.IsNullOrWhiteSpace(metaTitle),
            DescriptionOk = !string.IsNullOrWhiteSpace(metaDescription),
            OgImageOk = !string.IsNullOrWhiteSpace(ogImage),
            Indexed = indexed,
            MetaTitle = metaTitle,
            MetaDescription = metaDescription,
            OgImageUrl = ogImage
        };

        var pages = await customPageStore.ListAsync(PropertyId, false, ct);
        Pages = pages.Select(pageItem => new SeoPageAuditVm
        {
            Id = pageItem.Id,
            Title = pageItem.Title,
            Url = pageItem.Url,
            Slug = pageItem.Slug,
            IsPublished = pageItem.IsPublished,
            NoIndex = pageItem.NoIndex,
            CanonicalUrl = pageItem.CanonicalUrl,
            HasTitle = !string.IsNullOrWhiteSpace(pageItem.SeoTitle) || !string.IsNullOrWhiteSpace(pageItem.Title),
            HasDescription = !string.IsNullOrWhiteSpace(pageItem.SeoDescription),
            HasOgImage = !string.IsNullOrWhiteSpace(pageItem.OgImageUrl),
            SeoTitle = pageItem.SeoTitle,
            SeoDescription = pageItem.SeoDescription,
            OgImageUrl = pageItem.OgImageUrl,
            RedirectCount = pageItem.LegacySlugs.Count
        }).OrderByDescending(x => x.IsPublished).ThenBy(x => x.Title, StringComparer.CurrentCultureIgnoreCase).ToList();

        Redirects = pages.SelectMany(pageItem => pageItem.LegacySlugs.Select(slug => new SeoRedirectVm
        {
            PageId = pageItem.Id,
            LegacySlug = slug,
            FromUrl = CustomPageStore.Url(IsGlobal ? null : SiteSlug, slug),
            ToUrl = pageItem.Url,
            PageTitle = pageItem.Title
        })).OrderBy(x => x.FromUrl, StringComparer.OrdinalIgnoreCase).ToList();

        if (!IsGlobal && PropertyId.HasValue)
        {
            var propertyIdValue = PropertyId.Value;
            var roomRows = await db.Rooms.AsNoTracking()
                .Where(x => x.PropertyId == propertyIdValue && x.IsActive)
                .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
                .Select(x => new
                {
                    x.Id, x.Name, x.Code, x.Slug, x.ShortDescription, x.IsPublished,
                    HasImage = x.Images.Any(),
                    HasCover = x.Images.Any(i => i.IsCover)
                }).ToListAsync(ct);
            Rooms = roomRows.Select(room => new SeoRoomAuditVm
            {
                Id = room.Id,
                Name = room.Name,
                Code = room.Code,
                Url = PublicUrlBuilder.Room(SiteSlug, string.IsNullOrWhiteSpace(room.Slug) ? room.Code : room.Slug),
                EditUrl = $"/Admin/Rooms/{room.Id}/Content?propertyId={propertyIdValue}",
                IsPublished = room.IsPublished,
                ShortDescription = room.ShortDescription ?? string.Empty,
                HasDescription = !string.IsNullOrWhiteSpace(room.ShortDescription),
                HasImage = room.HasImage,
                HasCover = room.HasCover
            }).ToList();

            var postRows = await db.BlogPosts.AsNoTracking()
                .Where(x => x.PropertyId == propertyIdValue)
                .OrderByDescending(x => x.IsPublished).ThenByDescending(x => x.PublishedAtUtc).ThenBy(x => x.Title)
                .Select(x => new { x.Id, x.Title, x.Slug, x.Excerpt, x.CoverImageUrl, x.IsPublished })
                .ToListAsync(ct);
            BlogPosts = postRows.Select(post => new SeoBlogAuditVm
            {
                Id = post.Id,
                Title = post.Title,
                Url = $"/h/{Uri.EscapeDataString(SiteSlug)}/blog/{Uri.EscapeDataString(post.Slug)}",
                IsPublished = post.IsPublished,
                Excerpt = post.Excerpt,
                CoverImageUrl = post.CoverImageUrl ?? string.Empty,
                HasDescription = !string.IsNullOrWhiteSpace(post.Excerpt),
                HasImage = !string.IsNullOrWhiteSpace(post.CoverImageUrl)
            }).ToList();
        }

        ApplyContentPaging();
        CurrentListUrl = BuildListUrl(ContentFilter, PageNumber, SearchQuery, PageSize);
        return true;
    }

    private void ApplyContentPaging()
    {
        var includePages = ContentFilter is "all" or "page" or "issue";
        var includeRooms = ContentFilter is "all" or "room" or "issue";
        var includeBlogs = ContentFilter is "all" or "blog" or "issue";
        var issuesOnly = ContentFilter == "issue";

        var filteredPages = includePages
            ? Pages.Where(x => (!issuesOnly || x.IssueCount > 0) && MatchesQuery(SearchQuery, x.Title, x.Url, x.SeoTitle, x.SeoDescription)).ToList()
            : [];
        var filteredRooms = includeRooms
            ? Rooms.Where(x => (!issuesOnly || x.IssueCount > 0) && MatchesQuery(SearchQuery, x.Name, x.Code, x.Url, x.ShortDescription)).ToList()
            : [];
        var filteredBlogs = includeBlogs
            ? BlogPosts.Where(x => (!issuesOnly || x.IssueCount > 0) && MatchesQuery(SearchQuery, x.Title, x.Url, x.Excerpt)).ToList()
            : [];

        TotalItems = filteredPages.Count + filteredRooms.Count + filteredBlogs.Count;
        TotalPages = Math.Max(1, (int)Math.Ceiling(TotalItems / (double)PageSize));
        PageNumber = Math.Min(PageNumber, TotalPages);

        var skip = (PageNumber - 1) * PageSize;
        var take = PageSize;
        VisiblePages = TakeSlice(filteredPages, ref skip, ref take);
        VisibleRooms = TakeSlice(filteredRooms, ref skip, ref take);
        VisibleBlogPosts = TakeSlice(filteredBlogs, ref skip, ref take);
    }

    public string FilterUrl(string type) => BuildListUrl(NormalizeFilter(type), 1, SearchQuery, PageSize);
    public string PageUrl(int page) => BuildListUrl(ContentFilter, page, SearchQuery, PageSize);

    private string BuildListUrl(string type, int page, string? query, int pageSize)
    {
        var parts = new List<string>();
        if (IsGlobal) parts.Add("scope=global");
        else if (PropertyId.HasValue) parts.Add($"propertyId={Uri.EscapeDataString(PropertyId.Value.ToString())}");
        if (!string.Equals(type, "all", StringComparison.Ordinal)) parts.Add($"type={Uri.EscapeDataString(type)}");
        if (!string.IsNullOrWhiteSpace(query)) parts.Add($"q={Uri.EscapeDataString(query.Trim())}");
        if (page > 1) parts.Add($"page={page}");
        if (pageSize != 20) parts.Add($"pageSize={pageSize}");
        return parts.Count == 0 ? "/Admin/Site/Seo" : $"/Admin/Site/Seo?{string.Join("&", parts)}";
    }

    private IActionResult ReturnToSeo(string? returnUrl, bool global, Guid? propertyId)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)) return LocalRedirect(returnUrl);
        return RedirectToPage("/Admin/Site/Seo", ScopeRouteValues(global, propertyId));
    }

    private static IReadOnlyList<T> TakeSlice<T>(IReadOnlyList<T> source, ref int skip, ref int take)
    {
        if (take <= 0 || source.Count == 0) return [];
        if (skip >= source.Count)
        {
            skip -= source.Count;
            return [];
        }
        var result = source.Skip(skip).Take(take).ToList();
        take -= result.Count;
        skip = 0;
        return result;
    }

    private static bool MatchesQuery(string query, params string?[] values)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;
        return values.Any(value => !string.IsNullOrWhiteSpace(value) && value.Contains(query, StringComparison.CurrentCultureIgnoreCase));
    }

    private static string NormalizeFilter(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "page" => "page",
        "room" => "room",
        "blog" => "blog",
        "issue" => "issue",
        _ => "all"
    };

    private async Task<ScopeResolution> ResolveScopeAsync(Guid? propertyId, string? scope, CancellationToken ct)
    {
        var isAdmin = User.IsInRole("Admin");
        var global = isAdmin && string.Equals(scope, "global", StringComparison.OrdinalIgnoreCase);
        if (global) return new(true, true, null, "Trang chung", string.Empty);

        var current = await currentPropertyService.ResolveAsync(User, propertyId, ct);
        if (current is null) return new(false, false, null, "Chưa chọn cơ sở", string.Empty);
        var property = await propertyResolver.ResolveByIdAsync(current.Id, ct);
        if (property is null) return new(false, false, null, current.Name, string.Empty);
        return new(true, false, current.Id, current.Name, property.SiteSlug);
    }

    private static object ScopeRouteValues(bool global, Guid? propertyId) => global
        ? new { scope = "global" }
        : new { propertyId };

    private static string Clean(string? value) => (value ?? string.Empty).Trim();
    private static bool IsSafeImageUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        var url = value.Trim();
        if (url.StartsWith("/", StringComparison.Ordinal) && !url.StartsWith("//", StringComparison.Ordinal)) return true;
        return Uri.TryCreate(url, UriKind.Absolute, out var absolute) && absolute.Scheme is "http" or "https";
    }

    private sealed record ScopeResolution(bool Allowed, bool IsGlobal, Guid? PropertyId, string ScopeName, string SiteSlug);
}

public sealed class SeoScopeAuditVm
{
    public bool TitleOk { get; init; }
    public bool DescriptionOk { get; init; }
    public bool OgImageOk { get; init; }
    public bool Indexed { get; init; }
    public string MetaTitle { get; init; } = string.Empty;
    public string MetaDescription { get; init; } = string.Empty;
    public string OgImageUrl { get; init; } = string.Empty;
    public int IssueCount => (TitleOk ? 0 : 1) + (DescriptionOk ? 0 : 1) + (OgImageOk ? 0 : 1) + (Indexed ? 0 : 1);
}

public sealed class SeoPageAuditVm
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public bool IsPublished { get; init; }
    public bool NoIndex { get; init; }
    public string CanonicalUrl { get; init; } = string.Empty;
    public bool HasTitle { get; init; }
    public bool HasDescription { get; init; }
    public bool HasOgImage { get; init; }
    public string SeoTitle { get; init; } = string.Empty;
    public string SeoDescription { get; init; } = string.Empty;
    public string OgImageUrl { get; init; } = string.Empty;
    public int RedirectCount { get; init; }
    public int IssueCount => (HasTitle ? 0 : 1) + (HasDescription ? 0 : 1) + (HasOgImage ? 0 : 1);
}

public sealed class SeoRoomAuditVm
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string EditUrl { get; init; } = string.Empty;
    public bool IsPublished { get; init; }
    public string ShortDescription { get; init; } = string.Empty;
    public bool HasDescription { get; init; }
    public bool HasImage { get; init; }
    public bool HasCover { get; init; }
    public int IssueCount => (HasDescription ? 0 : 1) + (HasImage ? 0 : 1);
}

public sealed class SeoBlogAuditVm
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public bool IsPublished { get; init; }
    public string Excerpt { get; init; } = string.Empty;
    public string CoverImageUrl { get; init; } = string.Empty;
    public bool HasDescription { get; init; }
    public bool HasImage { get; init; }
    public int IssueCount => (HasDescription ? 0 : 1) + (HasImage ? 0 : 1);
}

public sealed class SeoRedirectVm
{
    public Guid PageId { get; init; }
    public string LegacySlug { get; init; } = string.Empty;
    public string FromUrl { get; init; } = string.Empty;
    public string ToUrl { get; init; } = string.Empty;
    public string PageTitle { get; init; } = string.Empty;
}
