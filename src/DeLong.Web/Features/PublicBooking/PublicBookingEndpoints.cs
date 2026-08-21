using DeLong.Web.Common.Operations;
using DeLong.Web.Common.Security;
using DeLong.Web.Data;
using DeLong.Web.Features.Bookings;
using DeLong.Web.Features.Site;
using DeLong.Web.Features.CustomerAccounts;
using System.Security.Claims;
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
        group.MapGet("/availability", async (
            [FromQuery] string date,
            [FromQuery] string? siteSlug,
            PublicBookingService service,
            BookingService bookingService,
            AppDbContext db,
            PublicPropertyResolver resolver,
            StoragePaths paths,
            IConfiguration configuration,
            CancellationToken ct) =>
        {
            if (!DateOnly.TryParse(date, out var stayDate)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["date"] = ["Ngày không hợp lệ."] });
            await new PublicBookingCoreV2Service(db, resolver, service, bookingService, paths, configuration).ReleaseExpiredHoldsAsync(siteSlug, ct);
            var availability = await service.GetAvailabilityAsync(siteSlug, stayDate, ct);
            return availability is null ? Results.NotFound() : Results.Ok(availability);
        });
        group.MapGet("/stay-availability", async (
            [FromQuery] string checkIn,
            [FromQuery] string checkOut,
            [FromQuery] string? siteSlug,
            PublicBookingService service,
            BookingService bookingService,
            AppDbContext db,
            PublicPropertyResolver resolver,
            StoragePaths paths,
            IConfiguration configuration,
            CancellationToken ct) =>
        {
            if (!DateOnly.TryParse(checkIn, out var arrival) || !DateOnly.TryParse(checkOut, out var departure))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["dates"] = ["Ngày nhận hoặc ngày trả không hợp lệ."] });
            var property = await resolver.ResolveAsync(siteSlug, ct);
            if (property is null) return Results.NotFound();
            var policy = await new BookingPolicyStore(paths, configuration).GetAsync(property.Id, ct);
            var nights = departure.DayNumber - arrival.DayNumber;
            if (nights > policy.PublicMaxNights)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["dates"] = [$"Khách đặt online tối đa {policy.PublicMaxNights} đêm mỗi lượt."] });
            await new PublicBookingCoreV2Service(db, resolver, service, bookingService, paths, configuration).ReleaseExpiredHoldsAsync(siteSlug, ct);
            var (availability, error) = await service.GetStayAvailabilityAsync(siteSlug, arrival, departure, ct);
            return error is not null ? Results.ValidationProblem(new Dictionary<string, string[]> { ["dates"] = [error.Message] }) : availability is null ? Results.NotFound() : Results.Ok(availability);
        });
        group.MapPost("/booking-requests", async (
            HttpContext http,
            [FromQuery] string? siteSlug,
            PublicBookingRequest request,
            PublicBookingService service,
            BookingService bookingService,
            AppDbContext db,
            PublicPropertyResolver resolver,
            StoragePaths paths,
            IConfiguration configuration,
            CustomerAccountService customerAccountService,
            CancellationToken ct) =>
        {
            var idempotencyKey = http.Request.Headers["Idempotency-Key"].FirstOrDefault();
            var core = new PublicBookingCoreV2Service(db, resolver, service, bookingService, paths, configuration);
            var (result, error) = await core.CreateRequestAsync(siteSlug, request, idempotencyKey, ct);
            if (result is not null)
            {
                var userIdValue = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (Guid.TryParse(userIdValue, out var userId) && http.User.IsInRole(CustomerAccountService.CustomerRole))
                {
                    var property = await resolver.ResolveAsync(siteSlug, ct);
                    if (property is not null)
                    {
                        await customerAccountService.LinkBookingCustomerAsync(userId, property.Id, result.BookingId, ct);
                        await customerAccountService.CopySavedIdentityDocumentsToBookingAsync(
                            userId, property.Id, result.BookingId, new IdentityDocumentStorage(paths, configuration), ct);
                    }
                }
                var prefix = PublicPropertyResolver.ScopePrefix(siteSlug);
                return Results.Created($"{prefix}/booking/success?code={Uri.EscapeDataString(result.Code)}", result);
            }
            return error?.Code switch
            {
                "booking_conflict" => Results.Conflict(new ProblemDetails { Status = 409, Title = "Phòng vừa hết chỗ", Detail = error.Message, Type = "booking_conflict" }),
                "policy_changed" => Results.Conflict(new ProblemDetails { Status = 409, Title = "Nội quy vừa được cập nhật", Detail = error.Message, Type = "policy_changed" }),
                "identity_storage_unavailable" => Results.Problem(statusCode: 503, title: "Chưa thể nhận CCCD", detail: error.Message, type: "identity_storage_unavailable"),
                "spam" => Results.BadRequest(new ProblemDetails { Status = 400, Title = "Không thể gửi yêu cầu", Detail = error.Message, Type = "spam" }),
                _ => Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [error?.Message ?? "Thông tin đặt phòng chưa hợp lệ."] })
            };
        }).AddEndpointFilter<ApiAntiforgeryFilter>().RequireRateLimiting("public-booking");

        app.MapBookingCoreV2Endpoints();
        return app;
    }
}
