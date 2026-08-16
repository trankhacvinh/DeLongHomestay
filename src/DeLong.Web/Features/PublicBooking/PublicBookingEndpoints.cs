using DeLong.Web.Common.Security;
using DeLong.Web.Features.Site;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DeLong.Web.Features.PublicBooking;

public static class PublicBookingEndpoints
{
    public static IEndpointRouteBuilder MapPublicBookingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/public").WithTags("Public booking");
        group.MapGet("/rooms", async ([FromQuery] string? siteSlug, PublicBookingService service, CancellationToken ct) =>
        {
            var catalog = await service.GetCatalogAsync(siteSlug, null, ct);
            return catalog is null ? Results.NotFound() : Results.Ok(catalog);
        });
        group.MapGet("/availability", async ([FromQuery] string date, [FromQuery] string? siteSlug, PublicBookingService service, CancellationToken ct) =>
        {
            if (!DateOnly.TryParse(date, out var stayDate)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["date"] = ["Ngày không hợp lệ."] });
            var availability = await service.GetAvailabilityAsync(siteSlug, stayDate, ct); return availability is null ? Results.NotFound() : Results.Ok(availability);
        });
        group.MapGet("/stay-availability", async ([FromQuery] string checkIn, [FromQuery] string checkOut, [FromQuery] string? siteSlug, PublicBookingService service, CancellationToken ct) =>
        {
            if (!DateOnly.TryParse(checkIn, out var arrival) || !DateOnly.TryParse(checkOut, out var departure)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["dates"] = ["Ngày nhận hoặc ngày trả không hợp lệ."] });
            var (availability, error) = await service.GetStayAvailabilityAsync(siteSlug, arrival, departure, ct);
            return error is not null ? Results.ValidationProblem(new Dictionary<string, string[]> { ["dates"] = [error.Message] }) : availability is null ? Results.NotFound() : Results.Ok(availability);
        });
        group.MapPost("/booking-requests", async (HttpContext http, [FromQuery] string? siteSlug, PublicBookingRequest request, PublicBookingService service, CancellationToken ct) =>
        {
            var idempotencyKey = http.Request.Headers["Idempotency-Key"].FirstOrDefault();
            var (result, error) = await service.CreateRequestAsync(siteSlug, request, idempotencyKey, ct);
            if (result is not null)
            {
                var prefix = PublicPropertyResolver.ScopePrefix(siteSlug);
                return Results.Created($"{prefix}/booking/success?code={Uri.EscapeDataString(result.Code)}", result);
            }
            return error?.Code switch
            {
                "booking_conflict" => Results.Conflict(new ProblemDetails { Status = 409, Title = "Phòng vừa hết chỗ", Detail = error.Message, Type = "booking_conflict" }),
                "spam" => Results.BadRequest(new ProblemDetails { Status = 400, Title = "Không thể gửi yêu cầu", Detail = error.Message, Type = "spam" }),
                _ => Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [error?.Message ?? "Thông tin đặt phòng chưa hợp lệ."] })
            };
        }).AddEndpointFilter<ApiAntiforgeryFilter>().RequireRateLimiting("public-booking");
        return app;
    }
}
