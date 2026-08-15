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
            CancellationToken cancellationToken) =>
        {
            var history = await service.GetEntityHistoryAsync(
                propertyId, entityType.Trim(), entityId, cancellationToken);
            return Results.Ok(history);
        });

        return app;
    }
}
