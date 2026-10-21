using DeLong.Web.Common.Security;

namespace DeLong.Web.Common.Auditing;

public static class AuditEndpoints
{
    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/properties/{propertyId:guid}/audit")
            .RequireAuthorization("ViewOperations")
            .AddEndpointFilter<PropertyAccessFilter>()
            .WithTags("Audit");

        group.MapGet("/{entityType}/{entityId:guid}", async (
            Guid propertyId,
            string entityType,
            Guid entityId,
            AuditService service,
            int? skip,
            CancellationToken cancellationToken) =>
        {
            var history = await service.GetEntityHistoryAsync(
                propertyId, entityType.Trim(), entityId, cancellationToken, skip ?? 0);
            return Results.Ok(history);
        });

        group.MapGet("/Booking/{bookingId:guid}/activity", async (Guid propertyId, Guid bookingId, AuditService service, CancellationToken ct) =>
            Results.Ok(await service.GetBookingActivityAsync(propertyId, bookingId, ct)));

        return app;
    }
}
