using DeLong.Web.Common.Security;
using DeLong.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Site;

public static class SiteContentEndpoints
{
    public static IEndpointRouteBuilder MapSiteContentEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/properties/{propertyId:guid}/site")
            .RequireAuthorization("ManageSiteContent")
            .AddEndpointFilter<PropertyAccessFilter>();

        admin.MapGet("/", async (Guid propertyId, SiteContentService service, CancellationToken ct) =>
        {
            var result = await service.GetAdminAsync(propertyId, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        admin.MapPut("/settings", async (
            Guid propertyId,
            SaveSiteSettingsRequest request,
            HttpContext http,
            SiteContentService service,
            CancellationToken ct) =>
        {
            var (settings, error) = await service.SaveSettingsAsync(propertyId, request, http.User.IsInRole("Admin"), ct);
            return error is null ? Results.Ok(settings) : ToProblem(error);
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        admin.MapPost("/assets/{kind}", async (
            Guid propertyId,
            string kind,
            IFormFile file,
            AppDbContext db,
            ISiteAssetStorage storage,
            CancellationToken ct) =>
        {
            var code = await db.Properties.AsNoTracking().Where(x => x.Id == propertyId).Select(x => x.Code).SingleOrDefaultAsync(ct);
            if (string.IsNullOrWhiteSpace(code)) return Results.NotFound();
            var (asset, error) = await storage.SaveAsync(code, kind, file, ct);
            return error is null ? Results.Ok(asset) : Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = [error] });
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        admin.MapPost("/sections", async (
            Guid propertyId,
            SaveHomeSectionRequest request,
            SiteContentService service,
            CancellationToken ct) =>
        {
            var (section, error) = await service.CreateSectionAsync(propertyId, request, ct);
            return error is null ? Results.Ok(section) : ToProblem(error);
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        admin.MapPut("/sections/{sectionId:guid}", async (
            Guid propertyId,
            Guid sectionId,
            SaveHomeSectionRequest request,
            SiteContentService service,
            CancellationToken ct) =>
        {
            var (section, error) = await service.UpdateSectionAsync(propertyId, sectionId, request, ct);
            return error is null ? Results.Ok(section) : ToProblem(error);
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        admin.MapDelete("/sections/{sectionId:guid}", async (
            Guid propertyId,
            Guid sectionId,
            SiteContentService service,
            CancellationToken ct) =>
        {
            var error = await service.DeleteSectionAsync(propertyId, sectionId, ct);
            return error is null ? Results.NoContent() : ToProblem(error);
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        admin.MapPut("/sections/reorder", async (
            Guid propertyId,
            ReorderHomeSectionsRequest request,
            SiteContentService service,
            CancellationToken ct) =>
        {
            var error = await service.ReorderAsync(propertyId, request.Ids, ct);
            return error is null ? Results.NoContent() : ToProblem(error);
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        MapCustomCss(app, "/site/custom.css", scoped: false);
        MapCustomCss(app, "/h/{siteSlug}/site/custom.css", scoped: true);
        MapCustomJs(app, "/site/custom.js", scoped: false);
        MapCustomJs(app, "/h/{siteSlug}/site/custom.js", scoped: true);

        return app;
    }

    private static void MapCustomCss(IEndpointRouteBuilder app, string pattern, bool scoped)
    {
        app.MapGet(pattern, async (string? siteSlug, SiteContentService service, CancellationToken ct) =>
        {
            var site = await service.GetPublicAsync(scoped ? siteSlug : null, ct);
            return site is null
                ? Results.NotFound()
                : Results.Text(site.Settings.CustomCss, "text/css; charset=utf-8");
        }).AllowAnonymous();
    }

    private static void MapCustomJs(IEndpointRouteBuilder app, string pattern, bool scoped)
    {
        app.MapGet(pattern, async (string? siteSlug, SiteContentService service, CancellationToken ct) =>
        {
            var site = await service.GetPublicAsync(scoped ? siteSlug : null, ct);
            return site is null
                ? Results.NotFound()
                : Results.Text(site.Settings.CustomJs, "text/javascript; charset=utf-8");
        }).AllowAnonymous();
    }

    private static IResult ToProblem(SiteContentError error)
    {
        var status = error.Code == "not_found" ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest;
        return Results.Problem(
            statusCode: status,
            title: "Không thể cập nhật website",
            detail: error.Message,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
    }
}
