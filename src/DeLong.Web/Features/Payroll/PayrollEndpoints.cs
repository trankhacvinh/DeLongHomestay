using System.Security.Claims;
using DeLong.Web.Common.Security;

namespace DeLong.Web.Features.Payroll;

public static class PayrollEndpoints
{
    public const string ManageAttendancePolicy = "ManageAttendance";
    public const string ManagePayrollPolicy = "ManagePayroll";

    public static IEndpointRouteBuilder MapPayrollEndpoints(this IEndpointRouteBuilder app)
    {
        // Manager được chấm công; mọi thứ liên quan tới tiền chỉ Admin.
        var group = app.MapGroup("/api/admin/properties/{propertyId:guid}/payroll")
            .RequireAuthorization(ManageAttendancePolicy)
            .AddEndpointFilter<PropertyAccessFilter>()
            .WithTags("Payroll");

        group.MapGet("/", async (Guid propertyId, string? month, ClaimsPrincipal user, PayrollService service, CancellationToken ct) =>
        {
            var (sheet, error) = await service.GetSheetAsync(propertyId, month, user.IsInRole("Admin"), ct);
            return error is not null ? ToProblem(error) : Results.Ok(sheet);
        });

        group.MapPut("/attendance", async (Guid propertyId, SaveAttendanceRequest request, ClaimsPrincipal user, PayrollService service, CancellationToken ct) =>
        {
            var error = await service.SaveAttendanceAsync(propertyId, request, UserId(user), ct);
            return error is not null ? ToProblem(error) : Results.NoContent();
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        var admin = group.MapGroup("").RequireAuthorization(ManagePayrollPolicy);

        admin.MapPost("/employees", async (Guid propertyId, SaveEmployeeRequest request, ClaimsPrincipal user, PayrollService service, CancellationToken ct) =>
        {
            var (id, error) = await service.SaveEmployeeAsync(propertyId, null, request, UserId(user), ct);
            return error is not null ? ToProblem(error) : Results.Ok(new { id });
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        admin.MapPut("/employees/{employeeId:guid}", async (Guid propertyId, Guid employeeId, SaveEmployeeRequest request, ClaimsPrincipal user, PayrollService service, CancellationToken ct) =>
        {
            var (id, error) = await service.SaveEmployeeAsync(propertyId, employeeId, request, UserId(user), ct);
            return error is not null ? ToProblem(error) : Results.Ok(new { id });
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        admin.MapPost("/adjustments", async (Guid propertyId, SaveAdjustmentRequest request, ClaimsPrincipal user, PayrollService service, CancellationToken ct) =>
        {
            var (id, error) = await service.SaveAdjustmentAsync(propertyId, null, request, UserId(user), ct);
            return error is not null ? ToProblem(error) : Results.Ok(new { id });
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        admin.MapPut("/adjustments/{adjustmentId:guid}", async (Guid propertyId, Guid adjustmentId, SaveAdjustmentRequest request, ClaimsPrincipal user, PayrollService service, CancellationToken ct) =>
        {
            var (id, error) = await service.SaveAdjustmentAsync(propertyId, adjustmentId, request, UserId(user), ct);
            return error is not null ? ToProblem(error) : Results.Ok(new { id });
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        admin.MapDelete("/adjustments/{adjustmentId:guid}", async (Guid propertyId, Guid adjustmentId, ClaimsPrincipal user, PayrollService service, CancellationToken ct) =>
        {
            var error = await service.DeleteAdjustmentAsync(propertyId, adjustmentId, UserId(user), ct);
            return error is not null ? ToProblem(error) : Results.NoContent();
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        admin.MapPut("/policy", async (Guid propertyId, PayrollPolicyDto request, ClaimsPrincipal user, PayrollService service, CancellationToken ct) =>
        {
            var error = await service.SavePolicyAsync(propertyId, request, UserId(user), ct);
            return error is not null ? ToProblem(error) : Results.NoContent();
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        admin.MapPost("/holidays", async (Guid propertyId, SaveHolidayRequest request, ClaimsPrincipal user, PayrollService service, CancellationToken ct) =>
        {
            var (id, error) = await service.AddHolidayAsync(propertyId, request, UserId(user), ct);
            return error is not null ? ToProblem(error) : Results.Ok(new { id });
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        admin.MapDelete("/holidays/{holidayId:guid}", async (Guid propertyId, Guid holidayId, ClaimsPrincipal user, PayrollService service, CancellationToken ct) =>
        {
            var error = await service.DeleteHolidayAsync(propertyId, holidayId, UserId(user), ct);
            return error is not null ? ToProblem(error) : Results.NoContent();
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        admin.MapPost("/lock", async (Guid propertyId, PayrollMonthRequest request, ClaimsPrincipal user, PayrollService service, CancellationToken ct) =>
        {
            var error = await service.LockAsync(propertyId, request.Month, UserId(user), ct);
            return error is not null ? ToProblem(error) : Results.NoContent();
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        admin.MapPost("/unlock", async (Guid propertyId, UnlockPayrollRequest request, ClaimsPrincipal user, PayrollService service, CancellationToken ct) =>
        {
            var error = await service.UnlockAsync(propertyId, request, UserId(user), ct);
            return error is not null ? ToProblem(error) : Results.NoContent();
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        admin.MapPost("/transactions", async (Guid propertyId, CreatePayrollTransactionRequest request, ClaimsPrincipal user, PayrollService service, CancellationToken ct) =>
        {
            var (result, error) = await service.CreateTransactionAsync(propertyId, request, UserId(user), ct);
            return error is not null ? ToProblem(error) : Results.Ok(result);
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        admin.MapPost("/transactions/{transactionId:guid}/void", async (Guid propertyId, Guid transactionId, VoidPayrollTransactionRequest request, ClaimsPrincipal user, PayrollService service, CancellationToken ct) =>
        {
            var error = await service.VoidTransactionAsync(propertyId, transactionId, request.Reason, UserId(user), ct);
            return error is not null ? ToProblem(error) : Results.NoContent();
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        return app;
    }

    private static Guid? UserId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private static IResult ToProblem(PayrollError error)
    {
        var status = error.Code switch
        {
            "not_found" => StatusCodes.Status404NotFound,
            "locked" or "not_locked" or "has_payments" or "duplicate" or "retry_conflict" or "incomplete" or "overpay" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };
        return Results.Problem(
            type: $"https://delong.local/problems/{error.Code}",
            title: "Không thể xử lý chấm công / lương",
            detail: error.Message,
            statusCode: status,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
    }
}
