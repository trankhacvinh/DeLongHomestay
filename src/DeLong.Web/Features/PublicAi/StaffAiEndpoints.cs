using System.Security.Claims;
using DeLong.Web.Common.Security;
using Microsoft.AspNetCore.Authorization;

namespace DeLong.Web.Features.PublicAi;

public static class StaffAiEndpoints
{
    public static IEndpointRouteBuilder MapStaffAiEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/properties/{propertyId:guid}/staff-ai")
            .RequireAuthorization("UseStaffAi").AddEndpointFilter<PropertyAccessFilter>().WithTags("Staff AI");
        group.MapPost("/chat", async (Guid propertyId, StaffAiRequest request, ClaimsPrincipal user,
            IAuthorizationService authorization, StaffAiService service, CancellationToken ct) =>
        {
            var canViewFinance = (await authorization.AuthorizeAsync(user, "ViewFinance")).Succeeded;
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var (value, error) = await service.AskAsync(propertyId, userId, request.Message, request.ContextPeriod, canViewFinance, ct);
            return error is null ? Results.Ok(value) : Results.Problem(title: "Không thể tra cứu", detail: error, statusCode: 400);
        }).RequireRateLimiting("admin-ai").AddEndpointFilter<ApiAntiforgeryFilter>();
        return app;
    }
}
