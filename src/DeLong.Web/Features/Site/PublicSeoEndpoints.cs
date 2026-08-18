using System.Security;
using DeLong.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Site;

public static class PublicSeoEndpoints
{
    private sealed record SitemapEntry(string Path, DateTime? LastModifiedUtc = null);

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
                var body = $"User-agent: *\nAllow: /\nDisallow: /Admin\nDisallow: /Account\nDisallow: /booking/lookup\nDisallow: /booking/success\nSitemap: {baseUrl}/sitemap.xml\n";
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
                : $"User-agent: *\nAllow: {prefix}/\nDisallow: {prefix}/booking/lookup\nDisallow: {prefix}/booking/success\nDisallow: /Admin\nDisallow: /Account\nSitemap: {scopedBaseUrl}{prefix}/sitemap.xml\n";
            http.Response.Headers.CacheControl = "public,max-age=300";
            return Results.Text(scopedBody, "text/plain; charset=utf-8");
        }).AllowAnonymous();
    }

    private static void MapSitemap(IEndpointRouteBuilder app, string pattern, bool scoped)
    {
        app.MapGet(pattern, async (HttpContext http, AppDbContext db, PublicPropertyResolver resolver, SiteContentService service, CustomPageStore customPageStore, CancellationToken ct) =>
        {
            if (!scoped)
            {
                var properties = await db.Properties.AsNoTracking().Where(x => x.IsActive)
                    .OrderBy(x => x.Name)
                    .Select(x => new { x.Id, x.Code, x.SiteSlug, x.UpdatedAtUtc })
                    .ToListAsync(ct);
                var baseUrl = BaseUrl(http, null);
                var urls = new List<SitemapEntry>
                {
                    new("/"),
                    new("/rooms"),
                    new("/blog")
                };

                var globalPages = await customPageStore.ListAsync(null, true, ct);
                urls.AddRange(globalPages.Where(page => !page.NoIndex).Select(page => new SitemapEntry(page.Url, page.UpdatedAtUtc)));

                foreach (var property in properties)
                {
                    var siteSlug = PublicPropertyResolver.EffectiveSiteSlug(property.SiteSlug, property.Code);
                    urls.Add(new(PublicUrlBuilder.PropertyHome(siteSlug), property.UpdatedAtUtc));
                    urls.Add(new(PublicUrlBuilder.Rooms(siteSlug), property.UpdatedAtUtc));

                    var propertyRooms = await db.Rooms.AsNoTracking()
                        .Where(x => x.PropertyId == property.Id && x.IsActive && x.IsPublished && x.Slug != null && x.Slug != "")
                        .OrderBy(x => x.SortOrder)
                        .Select(x => new { Slug = x.Slug!, x.UpdatedAtUtc })
                        .ToListAsync(ct);
                    urls.AddRange(propertyRooms.Select(x => new SitemapEntry(PublicUrlBuilder.Room(siteSlug, x.Slug), x.UpdatedAtUtc)));

                    var propertyPosts = await db.BlogPosts.AsNoTracking()
                        .Where(x => x.PropertyId == property.Id && x.IsPublished)
                        .OrderByDescending(x => x.PublishedAtUtc)
                        .Select(x => new { x.Slug, x.PublishedAtUtc, x.UpdatedAtUtc })
                        .ToListAsync(ct);
                    if (propertyPosts.Count > 0) urls.Add(new($"/h/{Uri.EscapeDataString(siteSlug)}/blog", propertyPosts.Max(x => x.UpdatedAtUtc)));
                    urls.AddRange(propertyPosts.Select(x => new SitemapEntry($"/h/{Uri.EscapeDataString(siteSlug)}/blog/{Uri.EscapeDataString(x.Slug)}", x.UpdatedAtUtc)));

                    var propertyPages = await customPageStore.ListAsync(property.Id, true, ct);
                    urls.AddRange(propertyPages.Where(page => !page.NoIndex).Select(page => new SitemapEntry(page.Url, page.UpdatedAtUtc)));
                }

                return Sitemap(http, baseUrl, urls);
            }

            var effectiveSlug = http.Request.RouteValues["siteSlug"]?.ToString();
            var propertyScope = await resolver.ResolveAsync(effectiveSlug, ct);
            var site = await service.GetPublicAsync(effectiveSlug, ct);
            if (propertyScope is null || site is null) return Results.NotFound();
            if (site.Settings.RobotsIndex == false) return Sitemap(http, BaseUrl(http, site.Settings.CanonicalBaseUrl), []);

            var scopedBaseUrl = BaseUrl(http, site.Settings.CanonicalBaseUrl);
            var rooms = await db.Rooms.AsNoTracking()
                .Where(x => x.PropertyId == propertyScope.Id && x.Property.IsActive && x.IsActive && x.IsPublished && x.Slug != null && x.Slug != "")
                .OrderBy(x => x.SortOrder)
                .Select(x => new { Slug = x.Slug!, x.UpdatedAtUtc })
                .ToListAsync(ct);
            var posts = await db.BlogPosts.AsNoTracking()
                .Where(x => x.PropertyId == propertyScope.Id && x.IsPublished)
                .OrderByDescending(x => x.PublishedAtUtc)
                .Select(x => new { x.Slug, x.UpdatedAtUtc })
                .ToListAsync(ct);
            var pages = await customPageStore.ListAsync(propertyScope.Id, true, ct);

            var scopedUrls = new List<SitemapEntry>
            {
                new(PublicUrlBuilder.PropertyHome(propertyScope.SiteSlug)),
                new(PublicUrlBuilder.Rooms(propertyScope.SiteSlug))
            };
            scopedUrls.AddRange(rooms.Select(x => new SitemapEntry(PublicUrlBuilder.Room(propertyScope.SiteSlug, x.Slug), x.UpdatedAtUtc)));
            if (posts.Count > 0) scopedUrls.Add(new($"/h/{Uri.EscapeDataString(propertyScope.SiteSlug)}/blog", posts.Max(x => x.UpdatedAtUtc)));
            scopedUrls.AddRange(posts.Select(x => new SitemapEntry($"/h/{Uri.EscapeDataString(propertyScope.SiteSlug)}/blog/{Uri.EscapeDataString(x.Slug)}", x.UpdatedAtUtc)));
            scopedUrls.AddRange(pages.Where(page => !page.NoIndex).Select(page => new SitemapEntry(page.Url, page.UpdatedAtUtc)));
            return Sitemap(http, scopedBaseUrl, scopedUrls);
        }).AllowAnonymous();
    }

    private static IResult Sitemap(HttpContext http, string baseUrl, IEnumerable<SitemapEntry> entries)
    {
        var unique = entries.GroupBy(x => x.Path, StringComparer.OrdinalIgnoreCase).Select(x => x.First());
        var xml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
                  "<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n" +
                  string.Join("\n", unique.Select(x =>
                  {
                      var loc = SecurityElement.Escape(baseUrl + x.Path);
                      var lastmod = x.LastModifiedUtc.HasValue ? $"<lastmod>{x.LastModifiedUtc.Value.ToUniversalTime():yyyy-MM-dd}</lastmod>" : string.Empty;
                      return $"  <url><loc>{loc}</loc>{lastmod}</url>";
                  })) +
                  "\n</urlset>";
        http.Response.Headers.CacheControl = "public,max-age=300";
        return Results.Text(xml, "application/xml; charset=utf-8");
    }

    private static string BaseUrl(HttpContext http, string? configured) =>
        string.IsNullOrWhiteSpace(configured) ? $"{http.Request.Scheme}://{http.Request.Host}" : configured.Trim().TrimEnd('/');
}
