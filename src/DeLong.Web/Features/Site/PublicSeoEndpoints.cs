
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
            if (!scoped)
            {
                var baseUrl = BaseUrl(http, null);
                var body = $"User-agent: *\nAllow: /\nDisallow: /Admin\nDisallow: /Account\nSitemap: {baseUrl}/sitemap.xml\n";
                http.Response.Headers.CacheControl = "public,max-age=300";
                return Results.Text(body, "text/plain; charset=utf-8");
            }

            var effectiveSlug = http.Request.RouteValues["siteSlug"]?.ToString();
            var site = await service.GetPublicAsync(effectiveSlug, ct);
            if (site is null) return Results.NotFound();
            var prefix = PublicPropertyResolver.ScopePrefix(effectiveSlug);
            var scopedBaseUrl = BaseUrl(http, site.Settings.CanonicalBaseUrl);
            var scopedBody = site.Settings.RobotsIndex == false
                ? "User-agent: *\nDisallow: /\n"
                : $"User-agent: *\nAllow: {prefix}/\nDisallow: /Admin\nDisallow: /Account\nSitemap: {scopedBaseUrl}{prefix}/sitemap.xml\n";
            http.Response.Headers.CacheControl = "public,max-age=300";
            return Results.Text(scopedBody, "text/plain; charset=utf-8");
        }).AllowAnonymous();
    }

    private static void MapSitemap(IEndpointRouteBuilder app, string pattern, bool scoped)
    {
        app.MapGet(pattern, async (HttpContext http, AppDbContext db, PublicPropertyResolver resolver, SiteContentService service, CancellationToken ct) =>
        {
            if (!scoped)
            {
                var properties = await db.Properties.AsNoTracking().Where(x => x.IsActive)
                    .OrderBy(x => x.Name).Select(x => new { x.Id, x.Code, x.SiteSlug }).ToListAsync(ct);
                var baseUrl = BaseUrl(http, null);
                var urls = new List<string> { "/", "/rooms", "/booking" };
                foreach (var property in properties)
                {
                    var siteSlug = PublicPropertyResolver.EffectiveSiteSlug(property.SiteSlug, property.Code);
                    var prefix = PublicUrlBuilder.PropertyHome(siteSlug);
                    urls.Add(prefix);
                    urls.Add(PublicUrlBuilder.Rooms(siteSlug));
                    urls.Add(PublicUrlBuilder.Booking(siteSlug));
                    var roomSlugs = await db.Rooms.AsNoTracking()
                        .Where(x => x.PropertyId == property.Id && x.IsActive && x.IsPublished && x.Slug != null && x.Slug != "")
                        .OrderBy(x => x.SortOrder).Select(x => x.Slug!).ToListAsync(ct);
                    urls.AddRange(roomSlugs.Select(roomSlug => PublicUrlBuilder.Room(siteSlug, roomSlug)));
                }
                return Sitemap(http, baseUrl, urls.Distinct());
            }

            var effectiveSlug = http.Request.RouteValues["siteSlug"]?.ToString();
            var propertyScope = await resolver.ResolveAsync(effectiveSlug, ct);
            var site = await service.GetPublicAsync(effectiveSlug, ct);
            if (propertyScope is null || site is null) return Results.NotFound();
            var scopedBaseUrl = BaseUrl(http, site.Settings.CanonicalBaseUrl);
            var slugs = await db.Rooms.AsNoTracking()
                .Where(x => x.PropertyId == propertyScope.Id && x.Property.IsActive && x.IsActive && x.IsPublished && x.Slug != null && x.Slug != "")
                .OrderBy(x => x.SortOrder).Select(x => x.Slug!).ToListAsync(ct);
            var scopedUrls = new List<string>
            {
                PublicUrlBuilder.PropertyHome(propertyScope.SiteSlug),
                PublicUrlBuilder.Rooms(propertyScope.SiteSlug),
                PublicUrlBuilder.Booking(propertyScope.SiteSlug)
            };
            scopedUrls.AddRange(slugs.Select(x => PublicUrlBuilder.Room(propertyScope.SiteSlug, x)));
            return Sitemap(http, scopedBaseUrl, scopedUrls);
        }).AllowAnonymous();
    }

    private static IResult Sitemap(HttpContext http, string baseUrl, IEnumerable<string> urls)
    {
        var xml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
                  "<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n" +
                  string.Join("\n", urls.Select(x => $"  <url><loc>{SecurityElement.Escape(baseUrl + x)}</loc></url>")) +
                  "\n</urlset>";
        http.Response.Headers.CacheControl = "public,max-age=300";
        return Results.Text(xml, "application/xml; charset=utf-8");
    }

    private static string BaseUrl(HttpContext http, string? configured) =>
        string.IsNullOrWhiteSpace(configured) ? $"{http.Request.Scheme}://{http.Request.Host}" : configured.Trim().TrimEnd('/');
}
