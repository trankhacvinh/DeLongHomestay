using DeLong.Web.Common.Operations;
using DeLong.Web.Common.Security;
using DeLong.Web.Data;
using DeLong.Web.Features.Bookings;
using DeLong.Web.Features.Site;
using DeLong.Web.Features.CustomerAccounts;
using DeLong.Web.Features.Payments;
using DeLong.Web.Features.Notifications;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Vouchers;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

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
            CancellationToken ct) =>
        {
            if (!DateOnly.TryParse(date, out var stayDate)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["date"] = ["Ngày không hợp lệ."] });
            var availability = await service.GetAvailabilityAsync(siteSlug, stayDate, ct);
            return availability is null ? Results.NotFound() : Results.Ok(availability);
        });
        group.MapGet("/stay-availability", async (
            [FromQuery] string checkIn,
            [FromQuery] string checkOut,
            [FromQuery] string? siteSlug,
            PublicBookingService service,
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
            var (availability, error) = await service.GetStayAvailabilityAsync(siteSlug, arrival, departure, ct);
            return error is not null ? Results.ValidationProblem(new Dictionary<string, string[]> { ["dates"] = [error.Message] }) : availability is null ? Results.NotFound() : Results.Ok(availability);
        });
        group.MapPost("/booking-requests", async (
            HttpContext http,
            [FromQuery] string? siteSlug,
            PublicBookingRequest request,
            PublicBookingService service,
            AppDbContext db,
            PublicPropertyResolver resolver,
            StoragePaths paths,
            IConfiguration configuration,
            CustomerAccountService customerAccountService,
            Pay2SService pay2SService,
            BookingGuestGuideEmailService guestEmailService,
            VoucherService voucherService,
            CancellationToken ct) =>
        {
            var idempotencyKey = http.Request.Headers["Idempotency-Key"].FirstOrDefault();
            var userIdValue = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var customerUserId = Guid.TryParse(userIdValue, out var parsedUserId) && http.User.IsInRole(CustomerAccountService.CustomerRole)
                ? parsedUserId
                : (Guid?)null;
            var core = new PublicBookingCoreV2Service(db, resolver, service, voucherService, paths, configuration);
            var (result, error) = await core.CreateRequestAsync(siteSlug, request, idempotencyKey, customerUserId, ct);
            if (result is not null)
            {
                if (customerUserId.HasValue)
                {
                    var property = await resolver.ResolveAsync(siteSlug, ct);
                    if (property is not null)
                    {
                        await customerAccountService.LinkBookingCustomerAsync(customerUserId.Value, property.Id, result.BookingId, ct);
                        await customerAccountService.CopySavedIdentityDocumentsToBookingAsync(
                            customerUserId.Value, property.Id, result.BookingId, new IdentityDocumentStorage(paths, configuration), ct);
                    }
                }
                var paymentProperty = await resolver.ResolveAsync(siteSlug, ct);
                if (paymentProperty is null) return Results.NotFound();
                var prefix = PublicPropertyResolver.ScopePrefix(siteSlug);
                if (!result.PaymentRequired)
                {
                    await guestEmailService.QueueAutomaticAsync(paymentProperty.Id, result.BookingId, ct);
                    return Results.Created($"{prefix}/booking/success?code={Uri.EscapeDataString(result.Code)}", result);
                }
                var (intent, paymentError) = await pay2SService.CreateIntentAsync(
                    paymentProperty.Id,
                    result.BookingId,
                    cancelBookingOnExpiry: true,
                    ct,
                    siteSlug,
                    useSettlementGrace: true);
                if (intent is null)
                {
                    var booking = await db.Bookings.SingleAsync(x => x.Id == result.BookingId, ct);
                    booking.Status = BookingStatus.Cancelled;
                    await voucherService.ReleaseReservedAsync(result.BookingId, "Booking bị hủy vì không thể khởi tạo thanh toán Pay2S.", ct);
                    await db.SaveChangesAsync(ct);
                    await guestEmailService.QueueCancellationAsync(paymentProperty.Id, result.BookingId, null, "Booking đã bị hủy vì chưa thể khởi tạo thanh toán.", ct);
                    return Results.Problem(statusCode: 503, title: "Chưa thể tạo thanh toán", detail: paymentError?.Message, type: "pay2s_unavailable");
                }
                result = result with { HoldExpiresAtUtc = intent.ExpiresAtUtc, PaymentOrderId = intent.OrderId, PaymentUrl = intent.PayUrl };
                return Results.Created($"{prefix}/booking/success?code={Uri.EscapeDataString(result.Code)}", result);
            }
            if (error is null)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = ["Thông tin đặt phòng chưa hợp lệ."] });

            return error.Code switch
            {
                "booking_conflict" => Results.Conflict(new ProblemDetails { Status = 409, Title = "Phòng vừa hết chỗ", Detail = error.Message, Type = "booking_conflict" }),
                "policy_changed" => Results.Conflict(new ProblemDetails { Status = 409, Title = "Nội quy vừa được cập nhật", Detail = error.Message, Type = "policy_changed" }),
                "identity_storage_unavailable" => Results.Problem(statusCode: 503, title: "Chưa thể nhận CCCD", detail: error.Message, type: "identity_storage_unavailable"),
                "voucher_usage_exhausted" or "voucher_customer_limit_reached" => Results.Conflict(new ProblemDetails { Status = 409, Title = "Voucher vừa hết lượt", Detail = error.Message, Type = error.Code }),
                var code when code.StartsWith("voucher_", StringComparison.Ordinal) => Results.BadRequest(new ProblemDetails { Status = 400, Title = "Không thể áp dụng voucher", Detail = error.Message, Type = error.Code }),
                "spam" => Results.BadRequest(new ProblemDetails { Status = 400, Title = "Không thể gửi yêu cầu", Detail = error.Message, Type = "spam" }),
                _ => Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [error.Message] })
            };
        }).AddEndpointFilter<ApiAntiforgeryFilter>().RequireRateLimiting("public-booking");

        app.MapBookingCoreV2Endpoints();
        return app;
    }
}
