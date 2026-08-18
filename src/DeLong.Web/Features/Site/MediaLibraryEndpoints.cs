using DeLong.Web.Common.Security;

namespace DeLong.Web.Features.Site;

public static class MediaLibraryEndpoints
{
    public static IEndpointRouteBuilder MapMediaLibraryEndpoints(this IEndpointRouteBuilder app)
    {
        var property = app.MapGroup("/api/admin/properties/{propertyId:guid}/media")
            .RequireAuthorization("ManageSiteContent")
            .AddEndpointFilter<PropertyAccessFilter>();

        property.MapGet("/", async (Guid propertyId, MediaLibraryService service, CancellationToken ct) =>
            Results.Ok(await service.ListForPropertyAsync(propertyId, includeGlobal: true, ct)));

        property.MapPost("/upload", async (
            Guid propertyId,
            IFormFile file,
            MediaLibraryService service,
            CancellationToken ct) =>
        {
            var (asset, error) = await service.UploadAsync(propertyId, file, ct);
            return error is null ? Results.Ok(asset) : ToProblem(error);
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        property.MapPut("/{assetId:guid}", async (
            Guid propertyId,
            Guid assetId,
            SaveMediaAssetMetadataRequest request,
            MediaLibraryService service,
            CancellationToken ct) =>
        {
            var (asset, error) = await service.UpdateAsync(assetId, request, propertyId, allowAll: false, ct);
            return error is null ? Results.Ok(asset) : ToProblem(error);
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        property.MapDelete("/{assetId:guid}", async (
            Guid propertyId,
            Guid assetId,
            MediaLibraryService service,
            CancellationToken ct) =>
        {
            var error = await service.DeleteAsync(assetId, propertyId, allowAll: false, ct);
            return error is null ? Results.NoContent() : ToProblem(error);
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        var global = app.MapGroup("/api/admin/site/global/media")
            .RequireAuthorization("ManageProperties");

        global.MapGet("/", async (MediaLibraryService service, CancellationToken ct) =>
            Results.Ok(await service.ListAllAsync(ct)));

        global.MapPost("/upload", async (
            IFormFile file,
            MediaLibraryService service,
            CancellationToken ct) =>
        {
            var (asset, error) = await service.UploadAsync(null, file, ct);
            return error is null ? Results.Ok(asset) : ToProblem(error);
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        global.MapPut("/{assetId:guid}", async (
            Guid assetId,
            SaveMediaAssetMetadataRequest request,
            MediaLibraryService service,
            CancellationToken ct) =>
        {
            var (asset, error) = await service.UpdateAsync(assetId, request, null, allowAll: true, ct);
            return error is null ? Results.Ok(asset) : ToProblem(error);
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        global.MapDelete("/{assetId:guid}", async (
            Guid assetId,
            MediaLibraryService service,
            CancellationToken ct) =>
        {
            var error = await service.DeleteAsync(assetId, null, allowAll: true, ct);
            return error is null ? Results.NoContent() : ToProblem(error);
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        return app;
    }

    private static IResult ToProblem(MediaLibraryError error)
    {
        var status = error.Code switch
        {
            "not_found" => StatusCodes.Status404NotFound,
            "forbidden" => StatusCodes.Status403Forbidden,
            "in_use" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };
        return Results.Problem(
            statusCode: status,
            title: "Không thể cập nhật Media Library",
            detail: error.Message,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
    }
}
