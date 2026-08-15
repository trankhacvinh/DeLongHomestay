using DeLong.Web.Common.Security;

namespace DeLong.Web.Features.Properties;

public static class PropertyAdminEndpoints
{
    public static IEndpointRouteBuilder MapPropertyAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/properties-admin")
            .RequireAuthorization("ManageProperties");

        group.MapGet("/", async (PropertyAdminService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(ct)));

        group.MapPost("/", async (
            SavePropertyRequest request,
            HttpContext http,
            PropertyAdminService service,
            CancellationToken ct) =>
        {
            var (property, error) = await service.CreateAsync(request, http.User, ct);
            return error is null
                ? Results.Ok(property)
                : ToProblem(error);
        })
        .AddEndpointFilter<ApiAntiforgeryFilter>();

        group.MapPut("/{propertyId:guid}", async (
            Guid propertyId,
            SavePropertyRequest request,
            PropertyAdminService service,
            CancellationToken ct) =>
        {
            var (property, error) = await service.UpdateAsync(propertyId, request, ct);
            return error is null
                ? Results.Ok(property)
                : ToProblem(error);
        })
        .AddEndpointFilter<ApiAntiforgeryFilter>();

        return app;
    }

    private static IResult ToProblem(PropertyAdminError error)
    {
        var status = error.Code switch
        {
            "not_found" => StatusCodes.Status404NotFound,
            "duplicate_code" => StatusCodes.Status409Conflict,
            "property_in_use" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };
        return Results.Problem(
            statusCode: status,
            title: "Không thể lưu cơ sở",
            detail: error.Message,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
    }
}
