using DeLong.Web.Common.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DeLong.Web.Features.PublicBooking;

public static class PublicBookingEndpoints
{
    public static IEndpointRouteBuilder MapPublicBookingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/public").WithTags("Public booking");

        group.MapGet("/rooms", async (PublicBookingService service, CancellationToken cancellationToken) =>
        {
            var catalog = await service.GetCatalogAsync(null, cancellationToken);
            return catalog is null ? Results.NotFound() : Results.Ok(catalog);
        });

        group.MapGet("/availability", async (
            [FromQuery] string date,
            PublicBookingService service,
            CancellationToken cancellationToken) =>
        {
            if (!DateOnly.TryParse(date, out var stayDate))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["date"] = ["Ngày không hợp lệ."] });

            var availability = await service.GetAvailabilityAsync(stayDate, cancellationToken);
            return availability is null ? Results.NotFound() : Results.Ok(availability);
        });

        group.MapPost("/booking-requests", async (
            PublicBookingRequest request,
            PublicBookingService service,
            CancellationToken cancellationToken) =>
        {
            var (result, error) = await service.CreateRequestAsync(request, cancellationToken);
            if (result is not null)
                return Results.Created($"/booking/success?code={Uri.EscapeDataString(result.Code)}", result);

            return error?.Code switch
            {
                "booking_conflict" => Results.Conflict(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Khung giờ vừa hết phòng",
                    Detail = error.Message,
                    Type = "booking_conflict"
                }),
                "spam" => Results.BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Không thể gửi yêu cầu",
                    Detail = error.Message,
                    Type = "spam"
                }),
                _ => Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["request"] = [error?.Message ?? "Thông tin đặt phòng chưa hợp lệ."]
                })
            };
        })
        .AddEndpointFilter<ApiAntiforgeryFilter>()
        .RequireRateLimiting("public-booking");

        return app;
    }
}
