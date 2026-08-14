using DeLong.Web.Common.Security;

namespace DeLong.Web.Features.Rooms;

public static class RoomContentEndpoints
{
    public static IEndpointRouteBuilder MapRoomContentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/properties/{propertyId:guid}/rooms/{roomId:guid}/content")
            .RequireAuthorization("ManageRooms")
            .AddEndpointFilter<PropertyAccessFilter>()
            .WithTags("Room Content");

        group.MapGet("/", async (Guid propertyId, Guid roomId, RoomContentService service, CancellationToken ct) =>
        {
            var room = await service.GetAsync(propertyId, roomId, ct);
            return room is null ? Results.NotFound() : Results.Ok(room);
        });

        group.MapPut("/", async (Guid propertyId, Guid roomId, UpdateRoomContentRequest request, RoomContentService service, CancellationToken ct) =>
        {
            var (room, error) = await service.UpdateAsync(propertyId, roomId, request, ct);
            return error is null ? Results.Ok(room) : ToProblem(error);
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        group.MapGet("/amenity-presets", async (Guid propertyId, RoomContentService service, CancellationToken ct) =>
            Results.Ok(await service.GetAmenityPresetsAsync(propertyId, ct)));

        group.MapPost("/amenity-presets", async (Guid propertyId, CreateAmenityPresetRequest request, RoomContentService service, CancellationToken ct) =>
        {
            var (preset, error) = await service.CreateAmenityPresetAsync(propertyId, request, ct);
            return error is null ? Results.Created($"/api/admin/properties/{propertyId}/rooms/amenity-presets/{preset!.Id}", preset) : ToProblem(error);
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        group.MapDelete("/amenity-presets/{presetId:guid}", async (Guid propertyId, Guid presetId, RoomContentService service, CancellationToken ct) =>
        {
            var error = await service.DeleteAmenityPresetAsync(propertyId, presetId, ct);
            return error is null ? Results.NoContent() : ToProblem(error);
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        group.MapPost("/images", async (Guid propertyId, Guid roomId, HttpRequest request, RoomContentService service, CancellationToken ct) =>
        {
            if (!request.HasFormContentType) return Results.Problem(title: "Ảnh không hợp lệ", detail: "Yêu cầu phải dùng multipart/form-data.", statusCode: 400);
            var form = await request.ReadFormAsync(ct);
            var file = form.Files.GetFile("file");
            if (file is null) return Results.Problem(title: "Thiếu ảnh", detail: "Vui lòng chọn file ảnh.", statusCode: 400);
            var (image, error) = await service.UploadImageAsync(propertyId, roomId, file, ct);
            return error is null ? Results.Created($"/api/admin/properties/{propertyId}/rooms/{roomId}/content/images/{image!.Id}", image) : ToProblem(error);
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        group.MapPut("/images/{imageId:guid}", async (Guid propertyId, Guid roomId, Guid imageId, UpdateRoomImageRequest request, RoomContentService service, CancellationToken ct) =>
        {
            var (image, error) = await service.UpdateImageAsync(propertyId, roomId, imageId, request, ct);
            return error is null ? Results.Ok(image) : ToProblem(error);
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        group.MapPost("/images/reorder", async (Guid propertyId, Guid roomId, ReorderRoomImagesRequest request, RoomContentService service, CancellationToken ct) =>
        {
            var error = await service.ReorderImagesAsync(propertyId, roomId, request, ct);
            return error is null ? Results.NoContent() : ToProblem(error);
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        group.MapDelete("/images/{imageId:guid}", async (Guid propertyId, Guid roomId, Guid imageId, RoomContentService service, CancellationToken ct) =>
        {
            var error = await service.DeleteImageAsync(propertyId, roomId, imageId, ct);
            return error is null ? Results.NoContent() : ToProblem(error);
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        return app;
    }

    private static IResult ToProblem(RoomContentError error)
    {
        var status = error.Code == "not_found" ? 404 : error.Code is "slug_exists" or "preset_exists" ? 409 : 400;
        return Results.Problem(title: "Không thể cập nhật nội dung phòng", detail: error.Message, statusCode: status, extensions: new Dictionary<string, object?> { ["code"] = error.Code });
    }
}
