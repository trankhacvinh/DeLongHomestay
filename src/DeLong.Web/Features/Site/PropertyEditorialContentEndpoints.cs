using DeLong.Web.Common.Security;
using DeLong.Web.Data;

namespace DeLong.Web.Features.Site;

public static class PropertyEditorialContentEndpoints
{
    public static IEndpointRouteBuilder MapPropertyEditorialContentEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/properties/{propertyId:guid}/editorial")
            .RequireAuthorization("ManageSiteContent")
            .AddEndpointFilter<PropertyAccessFilter>();

        admin.MapGet("/", async (Guid propertyId, PropertyEditorialContentService service, CancellationToken ct) =>
        {
            var result = await service.GetAdminAsync(propertyId, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        admin.MapGet("/placement", async (Guid propertyId, AppDbContext db, CancellationToken ct) =>
            Results.Ok(await EditorialPlacementStore.GetAsync(db, propertyId, ct)));

        admin.MapPut("/placement", async (
            Guid propertyId,
            SaveEditorialPlacementRequest request,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var (placement, error) = await EditorialPlacementStore.SaveAsync(db, propertyId, request, ct);
            return error is null ? Results.Ok(placement) : ToProblem(error);
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        admin.MapPost("/gallery", async (Guid propertyId, SaveGalleryItemRequest request, PropertyEditorialContentService service, CancellationToken ct) =>
        {
            var (item, error) = await service.CreateGalleryAsync(propertyId, request, ct);
            return error is null ? Results.Ok(item) : ToProblem(error);
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        admin.MapPut("/gallery/{itemId:guid}", async (Guid propertyId, Guid itemId, SaveGalleryItemRequest request, PropertyEditorialContentService service, CancellationToken ct) =>
        {
            var (item, error) = await service.UpdateGalleryAsync(propertyId, itemId, request, ct);
            return error is null ? Results.Ok(item) : ToProblem(error);
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        admin.MapDelete("/gallery/{itemId:guid}", async (Guid propertyId, Guid itemId, PropertyEditorialContentService service, CancellationToken ct) =>
        {
            var error = await service.DeleteGalleryAsync(propertyId, itemId, ct);
            return error is null ? Results.NoContent() : ToProblem(error);
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        admin.MapPut("/gallery/layout", async (Guid propertyId, SaveGalleryLayoutRequest request, PropertyEditorialContentService service, CancellationToken ct) =>
        {
            var error = await service.SaveGalleryLayoutAsync(propertyId, request.Layout, ct);
            return error is null ? Results.NoContent() : ToProblem(error);
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        admin.MapPut("/gallery/reorder", async (Guid propertyId, ReorderGalleryRequest request, PropertyEditorialContentService service, CancellationToken ct) =>
        {
            var error = await service.ReorderGalleryAsync(propertyId, request.Ids, ct);
            return error is null ? Results.NoContent() : ToProblem(error);
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        admin.MapPost("/posts", async (Guid propertyId, SaveBlogPostRequest request, PropertyEditorialContentService service, CancellationToken ct) =>
        {
            var (post, error) = await service.CreatePostAsync(propertyId, request, ct);
            return error is null ? Results.Ok(post) : ToProblem(error);
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        admin.MapPut("/posts/{postId:guid}", async (Guid propertyId, Guid postId, SaveBlogPostRequest request, PropertyEditorialContentService service, CancellationToken ct) =>
        {
            var (post, error) = await service.UpdatePostAsync(propertyId, postId, request, ct);
            return error is null ? Results.Ok(post) : ToProblem(error);
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        admin.MapDelete("/posts/{postId:guid}", async (Guid propertyId, Guid postId, PropertyEditorialContentService service, CancellationToken ct) =>
        {
            var error = await service.DeletePostAsync(propertyId, postId, ct);
            return error is null ? Results.NoContent() : ToProblem(error);
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        var global = app.MapGroup("/api/admin/site/global/editorial")
            .RequireAuthorization("ManageProperties");

        global.MapGet("/", async (
            GlobalEditorialShowcaseService showcase,
            PropertyEditorialContentService editorial,
            CancellationToken ct) => Results.Ok(new
            {
                settings = await showcase.GetAsync(ct),
                gallery = await editorial.GetGlobalPublicGalleryAsync(ct),
                posts = await editorial.GetGlobalPublicPostsAsync(ct)
            }));

        global.MapPut("/", async (
            SaveGlobalEditorialShowcaseRequest request,
            GlobalEditorialShowcaseService showcase,
            CancellationToken ct) =>
        {
            var (settings, error) = await showcase.SaveAsync(request, ct);
            return error is null ? Results.Ok(settings) : ToProblem(error);
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        global.MapGet("/placement", async (AppDbContext db, CancellationToken ct) =>
            Results.Ok(await EditorialPlacementStore.GetAsync(db, null, ct)));

        global.MapPut("/placement", async (
            SaveEditorialPlacementRequest request,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var (placement, error) = await EditorialPlacementStore.SaveAsync(db, null, request, ct);
            return error is null ? Results.Ok(placement) : ToProblem(error);
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        app.MapGet("/api/public/site/editorial-placement", async (
            string? siteSlug,
            AppDbContext db,
            SiteContentService siteContentService,
            PublicPropertyResolver resolver,
            CancellationToken ct) =>
        {
            Guid? propertyId = null;
            IReadOnlyList<HomeSectionDto> sections;

            if (string.IsNullOrWhiteSpace(siteSlug))
            {
                sections = await siteContentService.GetGlobalPublicSectionsAsync(ct);
            }
            else
            {
                var property = await resolver.ResolveAsync(siteSlug, ct);
                if (property is null) return Results.NotFound();
                propertyId = property.Id;
                var site = await siteContentService.GetPublicAsync(property.SiteSlug, ct);
                if (site is null) return Results.NotFound();
                sections = site.Sections;
            }

            var placement = await EditorialPlacementStore.GetAsync(db, propertyId, ct);
            return Results.Ok(new
            {
                placement,
                sections = sections
                    .Where(x => x.IsVisible && x.Type != EditorialPlacementStore.MetadataSectionType && x.Type != GlobalSiteBrandingStore.MetadataSectionType)
                    .OrderBy(x => x.SortOrder)
                    .Select(x => new { x.Id, x.Type })
            });
        }).AllowAnonymous();

        return app;
    }

    private static IResult ToProblem(SiteContentError error)
    {
        var status = error.Code == "not_found" ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest;
        return Results.Problem(statusCode: status, title: "Không thể cập nhật nội dung", detail: error.Message,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
    }
}
