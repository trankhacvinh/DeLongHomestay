using DeLong.Web.Common.Security;

namespace DeLong.Web.Features.Rooms;

public static class RoomEndpoints
{
    public static IEndpointRouteBuilder MapRoomEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/properties/{propertyId:guid}/rooms")
            .RequireAuthorization("ViewRooms")
            .AddEndpointFilter<PropertyAccessFilter>()
            .WithTags("Rooms");

        group.MapGet("/", async (Guid propertyId, RoomService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetAllAsync(propertyId, cancellationToken)));

        group.MapGet("/{roomId:guid}", async (
            Guid propertyId, Guid roomId, RoomService service, CancellationToken cancellationToken) =>
        {
            var room = await service.GetAsync(propertyId, roomId, cancellationToken);
            return room is null ? Results.NotFound() : Results.Ok(room);
        });

        group.MapPost("/", async (
            Guid propertyId, CreateRoomRequest request, RoomService service, CancellationToken cancellationToken) =>
        {
            var (room, error) = await service.CreateAsync(propertyId, request, cancellationToken);
            return error is not null
                ? Results.Problem(title: "Không thể tạo phòng", detail: error, statusCode: 400)
                : Results.Created($"/api/admin/properties/{propertyId}/rooms/{room!.Id}", room);
        })
        .RequireAuthorization("ManageRooms")
        .AddEndpointFilter<ApiAntiforgeryFilter>();

        group.MapPut("/{roomId:guid}", async (
            Guid propertyId, Guid roomId, UpdateRoomRequest request, RoomService service, CancellationToken cancellationToken) =>
        {
            var existing = await service.GetAsync(propertyId, roomId, cancellationToken);
            if (existing is null) return Results.NotFound();

            var (room, error) = await service.UpdateAsync(propertyId, roomId, request, cancellationToken);
            return error is not null
                ? Results.Problem(title: "Không thể cập nhật phòng", detail: error, statusCode: 400)
                : Results.Ok(room);
        })
        .RequireAuthorization("ManageRooms")
        .AddEndpointFilter<ApiAntiforgeryFilter>();

        group.MapDelete("/{roomId:guid}", async (
            Guid propertyId, Guid roomId, RoomService service, CancellationToken cancellationToken) =>
        {
            var archived = await service.ArchiveAsync(propertyId, roomId, cancellationToken);
            return archived ? Results.NoContent() : Results.NotFound();
        })
        .RequireAuthorization("ManageRooms")
        .AddEndpointFilter<ApiAntiforgeryFilter>();

        return app;
    }
}
