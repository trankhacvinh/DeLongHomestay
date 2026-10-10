using System.Security.Claims;
using DeLong.Web.Common.Security;

namespace DeLong.Web.Features.Rooms;

public static class RoomBookingBlockEndpoints
{
    public static IEndpointRouteBuilder MapRoomBookingBlockEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/properties/{propertyId:guid}/room-blocks")
            .RequireAuthorization("ManageRooms").AddEndpointFilter<PropertyAccessFilter>();
        group.MapGet("/", async (Guid propertyId, RoomBookingBlockService service, CancellationToken ct) =>
            Results.Ok(await service.GetAllAsync(propertyId, ct)));
        group.MapPost("/", async (Guid propertyId, SaveRoomBlockRequest request, ClaimsPrincipal user,
            RoomBookingBlockService service, CancellationToken ct) =>
        {
            if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Results.Forbid();
            return Result(await service.SaveAsync(propertyId, null, request, actor, ct));
        }).AddEndpointFilter<ApiAntiforgeryFilter>();
        group.MapPut("/{batchId:guid}", async (Guid propertyId, Guid batchId, SaveRoomBlockRequest request,
            ClaimsPrincipal user, RoomBookingBlockService service, CancellationToken ct) =>
        {
            if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Results.Forbid();
            return Result(await service.SaveAsync(propertyId, batchId, request, actor, ct));
        }).AddEndpointFilter<ApiAntiforgeryFilter>();
        group.MapPost("/{batchId:guid}/end", async (Guid propertyId, Guid batchId, ClaimsPrincipal user,
            RoomBookingBlockService service, CancellationToken ct) =>
        {
            if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Results.Forbid();
            return Result(await service.CancelAsync(propertyId, batchId, actor, ct));
        }).AddEndpointFilter<ApiAntiforgeryFilter>();
        return app;
    }

    private static IResult Result(RoomBlockError? error) => error is null ? Results.Ok(new { success = true }) :
        Results.Problem(title: "Không thể lưu lịch khóa phòng", detail: error.Message,
            statusCode: error.Code == "not_found" ? 404 : error.Code is "existing_bookings" or "conflict" ? 409 : 400,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code, ["conflicts"] = error.Conflicts });
}
