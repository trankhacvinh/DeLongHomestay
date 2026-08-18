using DeLong.Web.Common.Security;
using DeLong.Web.Data;
using DeLong.Web.Features.Site;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

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
    public SeoScopeAuditVm ScopeAudit { get; private set; } = new();
    public IReadOnlyList<SeoPageAuditVm> Pages { get; private set; } = [];
    public IReadOnlyList<SeoRedirectVm> Redirects { get; private set; } = [];
    public IReadOnlyList<CurrentPropertyDto> Properties { get; private set; } = [];

    [TempData] public string? StatusMessage { get; set; }
    [TempData] public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid? propertyId, string? scope, CancellationToken ct)
    {
        if (!await LoadAsync(propertyId, scope, ct)) return IsAdmin ? Page() : Forbid();
        return Page();
    }

    public async Task<IActionResult> OnPostUpdatePageAsync(
        Guid? propertyId,
        string? scope,
        Guid pageId,
        bool noIndex,
        string? canonicalUrl,
        CancellationToken ct)
    {
        var resolved = await ResolveScopeAsync(propertyId, scope, ct);
        if (!resolved.Allowed) return Forbid();
        var error = await customPageStore.UpdateSeoAsync(resolved.PropertyId, pageId, noIndex, canonicalUrl, ct);
        if (error is null) StatusMessage = "Đã cập nhật SEO của trang.";
        else ErrorMessage = error.Message;
        return RedirectToPage("/Admin/Site/Seo", ScopeRouteValues(resolved.IsGlobal, resolved.PropertyId));
    }

    public async Task<IActionResult> OnPostRemoveRedirectAsync(
        Guid? propertyId,
        string? scope,
        Guid pageId,
        string legacySlug,
        CancellationToken ct)
    {
        var resolved = await ResolveScopeAsync(propertyId, scope, ct);
        if (!resolved.Allowed) return Forbid();
        var error = await customPageStore.RemoveLegacySlugAsync(resolved.PropertyId, pageId, legacySlug, ct);
        if (error is null) StatusMessage = "Đã gỡ redirect cũ.";
        else ErrorMessage = error.Message;
        return RedirectToPage("/Admin/Site/Seo", ScopeRouteValues(resolved.IsGlobal, resolved.PropertyId));
    }

    private async Task<bool> LoadAsync(Guid? propertyId, string? scope, CancellationToken ct)
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
        Pages = pages.Select(page => new SeoPageAuditVm
        {
            Id = page.Id,
            Title = page.Title,
            Url = page.Url,
            Slug = page.Slug,
            IsPublished = page.IsPublished,
            NoIndex = page.NoIndex,
            CanonicalUrl = page.CanonicalUrl,
            HasTitle = !string.IsNullOrWhiteSpace(page.SeoTitle) || !string.IsNullOrWhiteSpace(page.Title),
            HasDescription = !string.IsNullOrWhiteSpace(page.SeoDescription),
            HasOgImage = !string.IsNullOrWhiteSpace(page.OgImageUrl),
            SeoTitle = page.SeoTitle,
            SeoDescription = page.SeoDescription,
            OgImageUrl = page.OgImageUrl,
            RedirectCount = page.LegacySlugs.Count
        }).OrderByDescending(x => x.IsPublished).ThenBy(x => x.Title, StringComparer.CurrentCultureIgnoreCase).ToList();

        Redirects = pages.SelectMany(page => page.LegacySlugs.Select(slug => new SeoRedirectVm
        {
            PageId = page.Id,
            LegacySlug = slug,
            FromUrl = CustomPageStore.Url(IsGlobal ? null : SiteSlug, slug),
            ToUrl = page.Url,
            PageTitle = page.Title
        })).OrderBy(x => x.FromUrl, StringComparer.OrdinalIgnoreCase).ToList();
        return true;
    }

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

public sealed class SeoRedirectVm
{
    public Guid PageId { get; init; }
    public string LegacySlug { get; init; } = string.Empty;
    public string FromUrl { get; init; } = string.Empty;
    public string ToUrl { get; init; } = string.Empty;
    public string PageTitle { get; init; } = string.Empty;
}
