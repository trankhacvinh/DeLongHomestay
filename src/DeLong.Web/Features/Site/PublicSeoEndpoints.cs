using System.Security;
using DeLong.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Site;

public static class PublicSeoEndpoints
{
    public static IEndpointRouteBuilder MapPublicSeoEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/robots.txt", async (HttpContext http, SiteContentService service, CancellationToken ct) =>
        {
            var site = await service.GetPublicAsync(ct);
            var baseUrl = BaseUrl(http, site?.Settings.CanonicalBaseUrl);
            var body = site?.Settings.RobotsIndex == false
                ? "User-agent: *\nDisallow: /\n"
                : $"User-agent: *\nAllow: /\nDisallow: /Admin\nDisallow: /Account\nSitemap: {baseUrl}/sitemap.xml\n";
            http.Response.Headers.CacheControl = "public,max-age=300";
            return Results.Text(body, "text/plain; charset=utf-8");
        }).AllowAnonymous();

        app.MapGet("/sitemap.xml", async (HttpContext http, AppDbContext db, SiteContentService service, CancellationToken ct) =>
        {
            var site = await service.GetPublicAsync(ct);
            var baseUrl = BaseUrl(http, site?.Settings.CanonicalBaseUrl);
            var slugs = await db.Rooms.AsNoTracking()
                .Where(x => x.Property.Code == SiteContentService.PublicPropertyCode && x.Property.IsActive && x.IsActive && x.IsPublished && x.Slug != null && x.Slug != "")
                .OrderBy(x => x.SortOrder)
                .Select(x => x.Slug!)
                .ToListAsync(ct);

            var urls = new List<string> { "/", "/rooms", "/booking" };
            urls.AddRange(slugs.Select(x => $"/rooms/{Uri.EscapeDataString(x)}"));
            var xml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
                      "<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n" +
                      string.Join("\n", urls.Select(x => $"  <url><loc>{SecurityElement.Escape(baseUrl + x)}</loc></url>")) +
                      "\n</urlset>";
            http.Response.Headers.CacheControl = "public,max-age=300";
            return Results.Text(xml, "application/xml; charset=utf-8");
        }).AllowAnonymous();

        return app;
    }

    private static string BaseUrl(HttpContext http, string? configured) =>
        string.IsNullOrWhiteSpace(configured)
            ? $"{http.Request.Scheme}://{http.Request.Host}"
            : configured.Trim().TrimEnd('/');
}
