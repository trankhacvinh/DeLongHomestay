using DeLong.Web.Common.Security;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
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

        var global = app.MapGroup("/api/admin/site/global")
            .RequireAuthorization("ManageProperties");

        global.MapGet("/", async (SiteContentService service, CancellationToken ct) =>
        {
            var result = await service.GetGlobalAdminAsync(ct);
            return Results.Ok(new
            {
                sections = result.Sections.Where(x => x.Type != GlobalSiteBrandingStore.MetadataSectionType)
            });
        });

        global.MapGet("/branding", async (
            AppDbContext db,
            SiteContentService service,
            PublicPropertyResolver resolver,
            CancellationToken ct) =>
        {
            var properties = await resolver.GetActiveAsync(ct);
            return Results.Ok(await GlobalSiteBrandingStore.ResolveAsync(db, service, properties, ct));
        });

        global.MapPut("/branding", async (
            SaveGlobalSiteBrandingRequest request,
            AppDbContext db,
            SiteContentService service,
            PublicPropertyResolver resolver,
            CancellationToken ct) =>
        {
            // Make sure the normal global homepage blocks exist before the reserved metadata row is created.
            await service.GetGlobalAdminAsync(ct);
            var (success, error) = await GlobalSiteBrandingStore.SaveAsync(db, request, ct);
            if (!success)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["branding"] = [error ?? "Cấu hình thương hiệu không hợp lệ."] });

            var properties = await resolver.GetActiveAsync(ct);
            return Results.Ok(await GlobalSiteBrandingStore.ResolveAsync(db, service, properties, ct));
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        global.MapPost("/assets/{kind}", async (
            string kind,
            IFormFile file,
            ISiteAssetStorage storage,
            CancellationToken ct) =>
        {
            var (asset, error) = await storage.SaveAsync("global", kind, file, ct);
            return error is null ? Results.Ok(asset) : Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = [error] });
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        global.MapPost("/sections", async (
            SaveHomeSectionRequest request,
            SiteContentService service,
            CancellationToken ct) =>
        {
            var (section, error) = await service.CreateGlobalSectionAsync(request, ct);
            return error is null ? Results.Ok(section) : ToProblem(error);
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        global.MapPut("/sections/{sectionId:guid}", async (
            Guid sectionId,
            SaveHomeSectionRequest request,
            SiteContentService service,
            CancellationToken ct) =>
        {
            var (section, error) = await service.UpdateGlobalSectionAsync(sectionId, request, ct);
            return error is null ? Results.Ok(section) : ToProblem(error);
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        global.MapDelete("/sections/{sectionId:guid}", async (
            Guid sectionId,
            SiteContentService service,
            CancellationToken ct) =>
        {
            var error = await service.DeleteGlobalSectionAsync(sectionId, ct);
            return error is null ? Results.NoContent() : ToProblem(error);
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        global.MapPut("/sections/reorder", async (
            ReorderHomeSectionsRequest request,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var sections = await db.Set<HomeSection>()
                .Where(x => x.PropertyId == null && x.Type != GlobalSiteBrandingStore.MetadataSectionType)
                .ToListAsync(ct);
            if (request.Ids.Count != sections.Count || request.Ids.Distinct().Count() != request.Ids.Count || sections.Any(x => !request.Ids.Contains(x.Id)))
                return ToProblem(new SiteContentError("validation", "Danh sách sắp xếp không khớp các khối trang chủ chung hiện tại."));

            var order = request.Ids.Select((id, index) => new { id, index }).ToDictionary(x => x.id, x => x.index);
            foreach (var section in sections) section.SortOrder = order[section.Id];
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        app.MapGet("/api/admin/site/visual-context", async (
            string? siteSlug,
            HttpContext http,
            PublicPropertyResolver resolver,
            CurrentPropertyService currentPropertyService,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(siteSlug))
            {
                if (!http.User.IsInRole("Admin")) return (IResult)Results.Forbid();
                return (IResult)Results.Ok(new
                {
                    canEdit = true,
                    scope = "global",
                    propertyId = (Guid?)null,
                    propertyName = "Trang chủ chung"
                });
            }

            if (!http.User.IsInRole("Admin") && !http.User.IsInRole("Manager")) return (IResult)Results.Forbid();

            var property = await resolver.ResolveAsync(siteSlug, ct);
            if (property is null) return (IResult)Results.NotFound();

            var accessible = await currentPropertyService.GetAccessibleAsync(http.User, ct);
            if (!accessible.Any(x => x.Id == property.Id)) return (IResult)Results.Forbid();

            return (IResult)Results.Ok(new
            {
                canEdit = true,
                scope = "property",
                propertyId = property.Id,
                propertyName = property.Name,
                siteSlug = property.SiteSlug
            });
        }).RequireAuthorization();

        app.MapGet("/api/public/global-branding", async (
            AppDbContext db,
            SiteContentService service,
            PublicPropertyResolver resolver,
            CancellationToken ct) =>
        {
            var properties = await resolver.GetActiveAsync(ct);
            return Results.Ok(await GlobalSiteBrandingStore.ResolveAsync(db, service, properties, ct));
        }).AllowAnonymous();

        MapCustomCss(app, "/site/custom.css", scoped: false);
        MapCustomCss(app, "/h/{siteSlug}/site/custom.css", scoped: true);
        MapCustomJs(app, "/site/custom.js", scoped: false);
        MapCustomJs(app, "/h/{siteSlug}/site/custom.js", scoped: true);

        return app;
    }

    private static void MapCustomCss(IEndpointRouteBuilder app, string pattern, bool scoped)
    {
        app.MapGet(pattern, async (HttpContext http, SiteContentService service, CancellationToken ct) =>
        {
            var siteSlug = scoped ? http.Request.RouteValues["siteSlug"]?.ToString() : null;
            var site = await service.GetPublicAsync(siteSlug, ct);
            return site is null
                ? Results.NotFound()
                : Results.Text(site.Settings.CustomCss, "text/css; charset=utf-8");
        }).AllowAnonymous();
    }

    private static void MapCustomJs(IEndpointRouteBuilder app, string pattern, bool scoped)
    {
        app.MapGet(pattern, async (HttpContext http, SiteContentService service, CancellationToken ct) =>
        {
            var siteSlug = scoped ? http.Request.RouteValues["siteSlug"]?.ToString() : null;
            var site = await service.GetPublicAsync(siteSlug, ct);
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
