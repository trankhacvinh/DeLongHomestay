using DeLong.Web.Common.Security;
using Microsoft.AspNetCore.RateLimiting;

namespace DeLong.Web.Features.PublicBooking;

public static class PublicBookingLookupEndpoints
{
    public static IEndpointRouteBuilder MapPublicBookingLookupEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/public/booking-lookup", async (
            PublicBookingLookupRequest request,
            PublicBookingLookupService service,
            CancellationToken ct) =>
        {
            var result = await service.LookupAsync(request.Code, request.Phone, ct);
            return result is null
                ? Results.NotFound(new { message = "Không tìm thấy lượt đặt phù hợp với mã và số điện thoại đã nhập." })
                : Results.Ok(result);
        })
        .AllowAnonymous()
        .AddEndpointFilter<ApiAntiforgeryFilter>()
        .RequireRateLimiting("public-lookup");

        return app;
    }
}
