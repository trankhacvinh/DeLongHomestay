using System.Security.Claims;
using DeLong.Web.Common.Security;

namespace DeLong.Web.Features.Payments;

public static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/properties/{propertyId:guid}")
            .RequireAuthorization("AdminArea")
            .AddEndpointFilter<PropertyAccessFilter>()
            .WithTags("Payments");

        group.MapGet("/bookings/{bookingId:guid}/payments", async (
            Guid propertyId,
            Guid bookingId,
            PaymentService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.GetByBookingAsync(propertyId, bookingId, cancellationToken)));

        group.MapPost("/bookings/{bookingId:guid}/payments", async (
            Guid propertyId,
            Guid bookingId,
            CreatePaymentRequest request,
            ClaimsPrincipal user,
            PaymentService service,
            CancellationToken cancellationToken) =>
        {
            var userId = GetUserId(user);
            var (payment, error) = await service.AddAsync(propertyId, bookingId, request, userId, cancellationToken);
            if (error is not null) return ToProblem(error);
            return Results.Created($"/api/admin/properties/{propertyId}/payments/{payment!.Id}", payment);
        })
        .RequireAuthorization("ManagePayments")
        .AddEndpointFilter<ApiAntiforgeryFilter>();

        group.MapPost("/payments/{paymentId:guid}/void", async (
            Guid propertyId,
            Guid paymentId,
            VoidPaymentRequest request,
            ClaimsPrincipal user,
            PaymentService service,
            CancellationToken cancellationToken) =>
        {
            var userId = GetUserId(user);
            var (payment, error) = await service.VoidAsync(propertyId, paymentId, request.Reason, userId, cancellationToken);
            if (error is not null) return ToProblem(error);
            return Results.Ok(payment);
        })
        .RequireAuthorization("ManagePayments")
        .AddEndpointFilter<ApiAntiforgeryFilter>();

        return app;
    }

    private static Guid? GetUserId(ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : null;
    }

    private static IResult ToProblem(PaymentOperationError error)
    {
        var status = error.Code switch
        {
            "not_found" => 404,
            "already_voided" or "refund_exceeds_paid" or "void_breaks_balance" => 409,
            _ => 400
        };
        return Results.Problem(
            type: $"https://delong.local/problems/{error.Code}",
            title: "Không thể xử lý thanh toán",
            detail: error.Message,
            statusCode: status,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
    }
}
