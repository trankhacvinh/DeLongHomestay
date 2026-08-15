using DeLong.Web.Common.Security;

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

        return app;
    }

    private static IResult ToProblem(SiteContentError error)
    {
        var status = error.Code == "not_found" ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest;
        return Results.Problem(statusCode: status, title: "Không thể cập nhật nội dung", detail: error.Message,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
    }
}
