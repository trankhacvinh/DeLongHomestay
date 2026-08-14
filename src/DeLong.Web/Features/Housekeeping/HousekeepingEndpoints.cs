using System.Security.Claims;
using DeLong.Web.Common.Security;

namespace DeLong.Web.Features.Housekeeping;

public static class HousekeepingEndpoints
{
    public static IEndpointRouteBuilder MapHousekeepingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/properties/{propertyId:guid}/housekeeping")
            .RequireAuthorization("ViewHousekeeping")
            .AddEndpointFilter<PropertyAccessFilter>()
            .WithTags("Housekeeping");

        group.MapGet("/", async (
            Guid propertyId,
            HousekeepingService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.GetAllAsync(propertyId, cancellationToken)));

        group.MapPost("/rooms/{roomId:guid}/status", async (
            Guid propertyId,
            Guid roomId,
            ChangeHousekeepingStatusRequest request,
            ClaimsPrincipal user,
            HousekeepingService service,
            CancellationToken cancellationToken) =>
        {
            var actorUserId = GetUserId(user);
            var room = await service.ChangeStatusAsync(propertyId, roomId, request.Status, actorUserId, cancellationToken);
            return room is null ? Results.NotFound() : Results.Ok(room);
        })
        .RequireAuthorization("ManageHousekeeping")
        .AddEndpointFilter<ApiAntiforgeryFilter>();

        return app;
    }

    private static Guid? GetUserId(ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : null;
    }
}
