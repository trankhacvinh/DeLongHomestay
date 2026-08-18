using DeLong.Web.Common.Security;

namespace DeLong.Web.Features.Site;

public static class CustomPageEndpoints
{
    public static IEndpointRouteBuilder MapCustomPageEndpoints(this IEndpointRouteBuilder app)
    {
        var property = app.MapGroup("/api/admin/properties/{propertyId:guid}/site/pages")
            .RequireAuthorization("ManageSiteContent")
            .AddEndpointFilter<PropertyAccessFilter>();

        property.MapGet("/", async (Guid propertyId, CustomPageStore store, CancellationToken ct) =>
            Results.Ok(new { pages = await store.ListAsync(propertyId, false, ct) }));
        property.MapPost("/", async (Guid propertyId, SaveCustomPageRequest request, CustomPageStore store, CancellationToken ct) =>
            ToResult(await store.CreateAsync(propertyId, request, ct))).AddEndpointFilter<ApiAntiforgeryFilter>();
        property.MapGet("/{pageId:guid}", async (Guid propertyId, Guid pageId, CustomPageStore store, CancellationToken ct) =>
        {
            var page = await store.GetAsync(propertyId, pageId, ct);
            return page is null ? Results.NotFound() : Results.Ok(new { page, sections = page.Sections });
        });
        property.MapPut("/{pageId:guid}", async (Guid propertyId, Guid pageId, SaveCustomPageRequest request, CustomPageStore store, CancellationToken ct) =>
            ToResult(await store.UpdateAsync(propertyId, pageId, request, ct))).AddEndpointFilter<ApiAntiforgeryFilter>();
        property.MapPost("/{pageId:guid}/duplicate", async (Guid propertyId, Guid pageId, CustomPageStore store, CancellationToken ct) =>
            ToResult(await store.DuplicateAsync(propertyId, pageId, ct))).AddEndpointFilter<ApiAntiforgeryFilter>();
        property.MapDelete("/{pageId:guid}", async (Guid propertyId, Guid pageId, CustomPageStore store, CancellationToken ct) =>
        {
            var error = await store.DeleteAsync(propertyId, pageId, ct);
            return error is null ? Results.NoContent() : ToProblem(error);
        }).AddEndpointFilter<ApiAntiforgeryFilter>();
        MapSectionEndpoints(property, true);

        var global = app.MapGroup("/api/admin/site/global/pages")
            .RequireAuthorization("ManageProperties");
        global.MapGet("/", async (CustomPageStore store, CancellationToken ct) =>
            Results.Ok(new { pages = await store.ListAsync(null, false, ct) }));
        global.MapPost("/", async (SaveCustomPageRequest request, CustomPageStore store, CancellationToken ct) =>
            ToResult(await store.CreateAsync(null, request, ct))).AddEndpointFilter<ApiAntiforgeryFilter>();
        global.MapGet("/{pageId:guid}", async (Guid pageId, CustomPageStore store, CancellationToken ct) =>
        {
            var page = await store.GetAsync(null, pageId, ct);
            return page is null ? Results.NotFound() : Results.Ok(new { page, sections = page.Sections });
        });
        global.MapPut("/{pageId:guid}", async (Guid pageId, SaveCustomPageRequest request, CustomPageStore store, CancellationToken ct) =>
            ToResult(await store.UpdateAsync(null, pageId, request, ct))).AddEndpointFilter<ApiAntiforgeryFilter>();
        global.MapPost("/{pageId:guid}/duplicate", async (Guid pageId, CustomPageStore store, CancellationToken ct) =>
            ToResult(await store.DuplicateAsync(null, pageId, ct))).AddEndpointFilter<ApiAntiforgeryFilter>();
        global.MapDelete("/{pageId:guid}", async (Guid pageId, CustomPageStore store, CancellationToken ct) =>
        {
            var error = await store.DeleteAsync(null, pageId, ct);
            return error is null ? Results.NoContent() : ToProblem(error);
        }).AddEndpointFilter<ApiAntiforgeryFilter>();
        MapSectionEndpoints(global, false);

        app.MapGet("/api/public/site-pages", async (
            string? siteSlug,
            CustomPageStore store,
            PublicPropertyResolver resolver,
            CancellationToken ct) =>
        {
            Guid? propertyId = null;
            if (!string.IsNullOrWhiteSpace(siteSlug))
            {
                var resolved = await resolver.ResolveAsync(siteSlug, ct);
                if (resolved is null) return Results.NotFound();
                propertyId = resolved.Id;
            }
            var pages = await store.ListAsync(propertyId, true, ct);
            return Results.Ok(new { pages = pages.Where(x => !x.HideFromNavigation) });
        }).AllowAnonymous();

        return app;
    }

    private static void MapSectionEndpoints(RouteGroupBuilder group, bool propertyScoped)
    {
        if (propertyScoped)
        {
            group.MapPost("/{pageId:guid}/sections", async (Guid propertyId, Guid pageId, SaveHomeSectionRequest request, CustomPageStore store, CancellationToken ct) =>
                ToSectionResult(await store.CreateSectionAsync(propertyId, pageId, request, ct))).AddEndpointFilter<ApiAntiforgeryFilter>();
            group.MapPut("/{pageId:guid}/sections/{sectionId:guid}", async (Guid propertyId, Guid pageId, Guid sectionId, SaveHomeSectionRequest request, CustomPageStore store, CancellationToken ct) =>
                ToSectionResult(await store.UpdateSectionAsync(propertyId, pageId, sectionId, request, ct))).AddEndpointFilter<ApiAntiforgeryFilter>();
            group.MapDelete("/{pageId:guid}/sections/{sectionId:guid}", async (Guid propertyId, Guid pageId, Guid sectionId, CustomPageStore store, CancellationToken ct) =>
            {
                var error = await store.DeleteSectionAsync(propertyId, pageId, sectionId, ct);
                return error is null ? Results.NoContent() : ToProblem(error);
            }).AddEndpointFilter<ApiAntiforgeryFilter>();
            group.MapPut("/{pageId:guid}/sections/reorder", async (Guid propertyId, Guid pageId, ReorderHomeSectionsRequest request, CustomPageStore store, CancellationToken ct) =>
            {
                var error = await store.ReorderSectionsAsync(propertyId, pageId, request.Ids, ct);
                return error is null ? Results.NoContent() : ToProblem(error);
            }).AddEndpointFilter<ApiAntiforgeryFilter>();
            return;
        }

        group.MapPost("/{pageId:guid}/sections", async (Guid pageId, SaveHomeSectionRequest request, CustomPageStore store, CancellationToken ct) =>
            ToSectionResult(await store.CreateSectionAsync(null, pageId, request, ct))).AddEndpointFilter<ApiAntiforgeryFilter>();
        group.MapPut("/{pageId:guid}/sections/{sectionId:guid}", async (Guid pageId, Guid sectionId, SaveHomeSectionRequest request, CustomPageStore store, CancellationToken ct) =>
            ToSectionResult(await store.UpdateSectionAsync(null, pageId, sectionId, request, ct))).AddEndpointFilter<ApiAntiforgeryFilter>();
        group.MapDelete("/{pageId:guid}/sections/{sectionId:guid}", async (Guid pageId, Guid sectionId, CustomPageStore store, CancellationToken ct) =>
        {
            var error = await store.DeleteSectionAsync(null, pageId, sectionId, ct);
            return error is null ? Results.NoContent() : ToProblem(error);
        }).AddEndpointFilter<ApiAntiforgeryFilter>();
        group.MapPut("/{pageId:guid}/sections/reorder", async (Guid pageId, ReorderHomeSectionsRequest request, CustomPageStore store, CancellationToken ct) =>
        {
            var error = await store.ReorderSectionsAsync(null, pageId, request.Ids, ct);
            return error is null ? Results.NoContent() : ToProblem(error);
        }).AddEndpointFilter<ApiAntiforgeryFilter>();
    }

    private static IResult ToResult(CustomPageMutationResult result) =>
        result.Error is null && result.Page is not null ? Results.Ok(result.Page) : ToProblem(result.Error ?? new("validation", "Không thể lưu trang."));

    private static IResult ToSectionResult(CustomPageSectionMutationResult result) =>
        result.Error is null && result.Section is not null ? Results.Ok(result.Section) : ToProblem(result.Error ?? new("validation", "Không thể lưu khối."));

    private static IResult ToProblem(SiteContentError error) => Results.Problem(
        statusCode: error.Code == "not_found" ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest,
        title: "Không thể cập nhật trang nội dung",
        detail: error.Message,
        extensions: new Dictionary<string, object?> { ["code"] = error.Code });
}
