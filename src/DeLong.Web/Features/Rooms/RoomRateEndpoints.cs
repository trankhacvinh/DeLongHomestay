using DeLong.Web.Common.Security;

namespace DeLong.Web.Features.Rooms;

public static class RoomRateEndpoints
{
    public static IEndpointRouteBuilder MapRoomRateEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/properties/{propertyId:guid}/rooms/{roomId:guid}/rates")
            .RequireAuthorization("ManageRooms")
            .AddEndpointFilter<PropertyAccessFilter>()
            .WithTags("Room Rates");

        group.MapPost("/", async (
            Guid propertyId,
            Guid roomId,
            CreateRoomRateRequest request,
            RoomRateService service,
            CancellationToken cancellationToken) =>
        {
            var (rate, error) = await service.CreateAsync(propertyId, roomId, request, cancellationToken);
            if (error is not null) return ToProblem(error);
            return Results.Created($"/api/admin/properties/{propertyId}/rooms/{roomId}/rates/{rate!.Id}", rate);
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        group.MapPut("/{rateId:guid}", async (
            Guid propertyId,
            Guid roomId,
            Guid rateId,
            UpdateRoomRateRequest request,
            RoomRateService service,
            CancellationToken cancellationToken) =>
        {
            var (rate, error) = await service.UpdateAsync(propertyId, roomId, rateId, request, cancellationToken);
            if (error is not null) return ToProblem(error);
            return Results.Ok(rate);
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        group.MapDelete("/{rateId:guid}", async (
            Guid propertyId,
            Guid roomId,
            Guid rateId,
            RoomRateService service,
            CancellationToken cancellationToken) =>
        {
            var archived = await service.ArchiveAsync(propertyId, roomId, rateId, cancellationToken);
            return archived ? Results.NoContent() : Results.NotFound();
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        return app;
    }

    private static IResult ToProblem(RoomRateOperationError error)
    {
        var status = error.Code switch
        {
            "not_found" or "room_not_found" => 404,
            _ => 400
        };
        return Results.Problem(
            type: $"https://delong.local/problems/{error.Code}",
            title: "Không thể xử lý khung giá",
            detail: error.Message,
            statusCode: status,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
    }
}
