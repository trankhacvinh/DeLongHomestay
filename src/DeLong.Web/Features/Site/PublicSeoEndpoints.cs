using System.Security;
using DeLong.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Site;

public static class PublicSeoEndpoints
{
    public static IEndpointRouteBuilder MapPublicSeoEndpoints(this IEndpointRouteBuilder app)
    {
        MapRobots(app, "/robots.txt", scoped: false);
        MapRobots(app, "/h/{siteSlug}/robots.txt", scoped: true);
        MapSitemap(app, "/sitemap.xml", scoped: false);
        MapSitemap(app, "/h/{siteSlug}/sitemap.xml", scoped: true);
        return app;
    }

    private static void MapRobots(IEndpointRouteBuilder app, string pattern, bool scoped)
    {
        app.MapGet(pattern, async (HttpContext http, SiteContentService service, CancellationToken ct) =>
        {
            var effectiveSlug = scoped ? http.Request.RouteValues["siteSlug"]?.ToString() : null;
            var site = await service.GetPublicAsync(effectiveSlug, ct);
            if (site is null) return Results.NotFound();

            var prefix = PublicPropertyResolver.ScopePrefix(effectiveSlug);
            var baseUrl = BaseUrl(http, site.Settings.CanonicalBaseUrl);
            var body = site.Settings.RobotsIndex == false
                ? "User-agent: *\nDisallow: /\n"
                : $"User-agent: *\nAllow: {prefix}/\nDisallow: /Admin\nDisallow: /Account\nSitemap: {baseUrl}{prefix}/sitemap.xml\n";
            http.Response.Headers.CacheControl = "public,max-age=300";
            return Results.Text(body, "text/plain; charset=utf-8");
        }).AllowAnonymous();
    }

    private static void MapSitemap(IEndpointRouteBuilder app, string pattern, bool scoped)
    {
        app.MapGet(pattern, async (HttpContext http, AppDbContext db, PublicPropertyResolver resolver, SiteContentService service, CancellationToken ct) =>
        {
            var effectiveSlug = scoped ? http.Request.RouteValues["siteSlug"]?.ToString() : null;
            var property = await resolver.ResolveAsync(effectiveSlug, ct);
            var site = await service.GetPublicAsync(effectiveSlug, ct);
            if (property is null || site is null) return Results.NotFound();

            var baseUrl = BaseUrl(http, site.Settings.CanonicalBaseUrl);
            var prefix = PublicPropertyResolver.ScopePrefix(effectiveSlug);
            var slugs = await db.Rooms.AsNoTracking()
                .Where(x => x.PropertyId == property.Id && x.Property.IsActive && x.IsActive && x.IsPublished && x.Slug != null && x.Slug != "")
                .OrderBy(x => x.SortOrder)
                .Select(x => x.Slug!)
                .ToListAsync(ct);

            var urls = new List<string> { $"{prefix}/", $"{prefix}/rooms", $"{prefix}/booking" };
            urls.AddRange(slugs.Select(x => $"{prefix}/rooms/{Uri.EscapeDataString(x)}"));
            var xml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
                      "<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n" +
                      string.Join("\n", urls.Select(x => $"  <url><loc>{SecurityElement.Escape(baseUrl + x)}</loc></url>")) +
                      "\n</urlset>";
            http.Response.Headers.CacheControl = "public,max-age=300";
            return Results.Text(xml, "application/xml; charset=utf-8");
        }).AllowAnonymous();
    }

    private static string BaseUrl(HttpContext http, string? configured) =>
        string.IsNullOrWhiteSpace(configured)
            ? $"{http.Request.Scheme}://{http.Request.Host}"
            : configured.Trim().TrimEnd('/');
}
