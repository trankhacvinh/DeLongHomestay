using System.Security.Claims;
using DeLong.Web.Common.Security;

namespace DeLong.Web.Features.Pricing;

public static class PricingEndpoints
{
    public static IEndpointRouteBuilder MapPricingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/properties/{propertyId:guid}/pricing")
            .RequireAuthorization("ViewRooms")
            .AddEndpointFilter<PropertyAccessFilter>()
            .WithTags("Pricing");

        group.MapGet("/", async (Guid propertyId, PricingService service, CancellationToken ct) => Results.Ok(new
        {
            settings = await service.GetSettingsAsync(propertyId, ct),
            specialDays = await service.GetSpecialDaysAsync(propertyId, ct)
        }));

        group.MapPut("/settings", async (Guid propertyId, SavePricingSettingsRequest request, ClaimsPrincipal user, PricingService service, CancellationToken ct) =>
        {
            var (settings, error) = await service.SaveSettingsAsync(propertyId, request, UserId(user), ct);
            return error is null ? Results.Ok(settings) : Problem(error);
        }).RequireAuthorization("ManageRooms").AddEndpointFilter<ApiAntiforgeryFilter>();

        group.MapPut("/room-rates", async (Guid propertyId, SaveRoomPricingRequest request, ClaimsPrincipal user, PricingService service, CancellationToken ct) =>
        {
            var error = await service.SaveRoomPricingAsync(propertyId, request, UserId(user), ct);
            return error is null ? Results.NoContent() : Problem(error);
        }).RequireAuthorization("ManageRooms").AddEndpointFilter<ApiAntiforgeryFilter>();

        group.MapPost("/special-days", async (Guid propertyId, SaveSpecialPricingDayRequest request, ClaimsPrincipal user, PricingService service, CancellationToken ct) =>
        {
            var (day, error) = await service.SaveSpecialDayAsync(propertyId, null, request, UserId(user), ct);
            return error is null ? Results.Created($"/api/admin/properties/{propertyId}/pricing/special-days/{day!.Id}", day) : Problem(error);
        }).RequireAuthorization("ManageRooms").AddEndpointFilter<ApiAntiforgeryFilter>();

        group.MapPut("/special-days/{id:guid}", async (Guid propertyId, Guid id, SaveSpecialPricingDayRequest request, ClaimsPrincipal user, PricingService service, CancellationToken ct) =>
        {
            var (day, error) = await service.SaveSpecialDayAsync(propertyId, id, request, UserId(user), ct);
            return error is null ? Results.Ok(day) : Problem(error);
        }).RequireAuthorization("ManageRooms").AddEndpointFilter<ApiAntiforgeryFilter>();

        group.MapDelete("/special-days/{id:guid}", async (Guid propertyId, Guid id, ClaimsPrincipal user, PricingService service, CancellationToken ct) =>
            await service.ArchiveSpecialDayAsync(propertyId, id, UserId(user), ct) ? Results.NoContent() : Results.NotFound())
            .RequireAuthorization("ManageRooms").AddEndpointFilter<ApiAntiforgeryFilter>();

        return app;
    }

    private static Guid? UserId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var value) ? value : null;

    private static IResult Problem(PricingOperationError error) => Results.Problem(
        type: $"https://delong.local/problems/{error.Code}",
        title: "Không thể cập nhật cấu hình giá",
        detail: error.Message,
        statusCode: error.Code == "not_found" ? 404 : error.Code == "special_day_overlap" ? 409 : 400,
        extensions: new Dictionary<string, object?> { ["code"] = error.Code });
}
