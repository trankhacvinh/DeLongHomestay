using System.Security.Claims;
using DeLong.Web.Common.Security;

namespace DeLong.Web.Features.Expenses;

public static class ExpenseEndpoints
{
    public static IEndpointRouteBuilder MapExpenseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/properties/{propertyId:guid}/expenses")
            .RequireAuthorization("AdminArea")
            .AddEndpointFilter<PropertyAccessFilter>()
            .WithTags("Expenses");

        group.MapGet("/", async (
            Guid propertyId,
            DateTimeOffset? from,
            DateTimeOffset? to,
            ExpenseService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.GetAllAsync(propertyId, from, to, cancellationToken)));

        group.MapPost("/", async (
            Guid propertyId,
            CreateExpenseRequest request,
            ClaimsPrincipal user,
            ExpenseService service,
            CancellationToken cancellationToken) =>
        {
            var (expense, error) = await service.AddAsync(propertyId, request, GetUserId(user), cancellationToken);
            if (error is not null) return ToProblem(error);
            return Results.Created($"/api/admin/properties/{propertyId}/expenses/{expense!.Id}", expense);
        })
        .RequireAuthorization("ManageFinance")
        .AddEndpointFilter<ApiAntiforgeryFilter>();

        group.MapPost("/{expenseId:guid}/void", async (
            Guid propertyId,
            Guid expenseId,
            VoidExpenseRequest request,
            ClaimsPrincipal user,
            ExpenseService service,
            CancellationToken cancellationToken) =>
        {
            var (expense, error) = await service.VoidAsync(propertyId, expenseId, request.Reason, GetUserId(user), cancellationToken);
            if (error is not null) return ToProblem(error);
            return Results.Ok(expense);
        })
        .RequireAuthorization("ManageFinance")
        .AddEndpointFilter<ApiAntiforgeryFilter>();

        return app;
    }

    private static Guid? GetUserId(ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : null;
    }

    private static IResult ToProblem(ExpenseOperationError error)
    {
        var status = error.Code switch
        {
            "not_found" => 404,
            "already_voided" => 409,
            _ => 400
        };
        return Results.Problem(
            type: $"https://delong.local/problems/{error.Code}",
            title: "Không thể xử lý chi phí",
            detail: error.Message,
            statusCode: status,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
    }
}
