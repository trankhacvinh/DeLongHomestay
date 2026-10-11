using System.Security.Claims;
using DeLong.Web.Common.Auditing;
using DeLong.Web.Common.Security;

namespace DeLong.Web.Features.DataQuality;

public static class DataQualityEndpoints
{
    public static IEndpointRouteBuilder MapDataQualityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/properties/{propertyId:guid}/data-quality")
            .RequireAuthorization("ManageDataQuality").AddEndpointFilter<PropertyAccessFilter>();
        group.MapGet("/", async (Guid propertyId, int? page, string? filter, string? search, DataQualityService service, CancellationToken ct) => Results.Ok(await service.ListAsync(propertyId, ct, page ?? 1, filter ?? "pending", search)));
        group.MapPost("/scan", async (Guid propertyId, DataQualityService service, CancellationToken ct) =>
        { var count = await service.ScanAsync(propertyId, ct); return Results.Ok(new { count }); })
            .AddEndpointFilter<ApiAntiforgeryFilter>().RequireRateLimiting("admin-ai");
        group.MapPut("/{id:guid}", async (Guid propertyId, Guid id, ReviewQualityRequest request,
            ClaimsPrincipal user, DataQualityService service, CancellationToken ct) =>
        {
            if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Results.Forbid();
            var error = await service.ReviewAsync(propertyId, id, request, actor, ct);
            return error is null ? Results.Ok(new { success = true }) : Results.Problem(detail: error, statusCode: 400);
        }).AddEndpointFilter<ApiAntiforgeryFilter>();
        group.MapGet("/{id:guid}/history", async (Guid propertyId, Guid id, AuditService audit, CancellationToken ct) =>
            Results.Ok(await audit.GetEntityHistoryAsync(propertyId, "DataQualityIssue", id, ct)));
        return app;
    }
}
