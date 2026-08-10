using System.Security.Claims;
using DeLong.Web.Common.Security;

namespace DeLong.Web.Features.Bookings;

public static class BookingEndpoints
{
    public static IEndpointRouteBuilder MapBookingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/properties/{propertyId:guid}/bookings")
            .RequireAuthorization("AdminArea")
            .AddEndpointFilter<PropertyAccessFilter>()
            .WithTags("Bookings");

        group.MapGet("/", async (
            Guid propertyId,
            DateTimeOffset? from,
            DateTimeOffset? to,
            BookingService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.GetAllAsync(propertyId, from, to, cancellationToken)));

        group.MapGet("/{bookingId:guid}", async (
            Guid propertyId,
            Guid bookingId,
            BookingService service,
            CancellationToken cancellationToken) =>
        {
            var booking = await service.GetAsync(propertyId, bookingId, cancellationToken);
            return booking is null ? Results.NotFound() : Results.Ok(booking);
        });

        group.MapPost("/", async (
            Guid propertyId,
            CreateBookingRequest request,
            ClaimsPrincipal user,
            BookingService service,
            CancellationToken cancellationToken) =>
        {
            var (booking, error) = await service.CreateAsync(
                propertyId, request, GetUserId(user), cancellationToken);
            if (error is not null) return ToProblem(error);
            return Results.Created($"/api/admin/properties/{propertyId}/bookings/{booking!.Id}", booking);
        })
        .RequireAuthorization("ManageBookings")
        .AddEndpointFilter<ApiAntiforgeryFilter>();

        group.MapPut("/{bookingId:guid}", async (
            Guid propertyId,
            Guid bookingId,
            UpdateBookingRequest request,
            ClaimsPrincipal user,
            BookingService service,
            CancellationToken cancellationToken) =>
        {
            var (booking, error) = await service.UpdateAsync(
                propertyId, bookingId, request, GetUserId(user), cancellationToken);
            if (error is not null) return ToProblem(error);
            return Results.Ok(booking);
        })
        .RequireAuthorization("ManageBookings")
        .AddEndpointFilter<ApiAntiforgeryFilter>();

        group.MapPost("/{bookingId:guid}/status", async (
            Guid propertyId,
            Guid bookingId,
            ChangeBookingStatusRequest request,
            ClaimsPrincipal user,
            BookingService service,
            CancellationToken cancellationToken) =>
        {
            var (booking, error) = await service.ChangeStatusAsync(
                propertyId, bookingId, request.Status, GetUserId(user), cancellationToken);
            if (error is not null) return ToProblem(error);
            return Results.Ok(booking);
        })
        .RequireAuthorization("ManageBookings")
        .AddEndpointFilter<ApiAntiforgeryFilter>();

        return app;
    }

    private static Guid? GetUserId(ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : null;
    }

    private static IResult ToProblem(BookingOperationError error)
    {
        var status = error.Code switch
        {
            "not_found" => 404,
            "booking_conflict" => 409,
            "invalid_transition" => 409,
            "booking_locked" => 409,
            _ => 400
        };

        return Results.Problem(
            type: $"https://delong.local/problems/{error.Code}",
            title: error.Code == "booking_conflict" ? "Phòng đã được đặt" : "Không thể xử lý booking",
            detail: error.Message,
            statusCode: status,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
    }
}
