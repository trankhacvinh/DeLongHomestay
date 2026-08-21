using DeLong.Web.Common.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DeLong.Web.Features.PublicBooking;

public static class PublicBookingLookupEndpoints
{
    public static IEndpointRouteBuilder MapPublicBookingLookupEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/public/booking-lookup", async (
            [FromQuery] string? siteSlug,
            PublicBookingLookupRequest request,
            PublicBookingLookupService service,
            CancellationToken ct) =>
        {
            var result = await service.LookupAsync(siteSlug, request.Code, request.Phone, ct);
            return result is null
                ? Results.NotFound(new { message = "Không tìm thấy lượt đặt phù hợp với mã và số điện thoại đã nhập." })
                : Results.Ok(result);
        })
        .AllowAnonymous()
        .AddEndpointFilter<ApiAntiforgeryFilter>()
        .RequireRateLimiting("public-lookup");

        app.MapPost("/api/public/booking-guide-pdf", async (
            [FromQuery] string? siteSlug,
            PublicBookingLookupRequest request,
            PublicBookingLookupService service,
            CancellationToken ct) =>
        {
            var booking = await service.LookupAsync(siteSlug, request.Code, request.Phone, ct);
            if (booking is null) return Results.NotFound(new { message = "Không tìm thấy lượt đặt còn hiệu lực." });
            var guide = new PublicBookingGuideDto(booking.Code, booking.RoomName, booking.GuestGuideHtml);
            return Results.File(BookingGuestGuidePdf.Create(guide), "application/pdf", $"huong-dan-{booking.Code}.pdf");
        })
        .AllowAnonymous()
        .AddEndpointFilter<ApiAntiforgeryFilter>()
        .RequireRateLimiting("public-lookup");

        app.MapGet("/api/public/booking-guide-pdf", async (
            [FromQuery] string? siteSlug,
            [FromQuery] string code,
            PublicBookingLookupService service,
            CancellationToken ct) =>
        {
            var guide = await service.GetSuccessGuideAsync(siteSlug, code, ct);
            return guide is null
                ? Results.NotFound()
                : Results.File(BookingGuestGuidePdf.Create(guide), "application/pdf", $"huong-dan-{guide.Code}.pdf");
        })
        .AllowAnonymous()
        .RequireRateLimiting("public-lookup");

        return app;
    }
}
