using DeLong.Web.Common.Security;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DeLong.Web.Features.Payments;

public static class Pay2SEndpoints
{
    public static IEndpointRouteBuilder MapPay2SEndpoints(this IEndpointRouteBuilder app)
    {
        var settings = app.MapGroup("/api/admin/properties/{propertyId:guid}/pay2s")
            .RequireAuthorization("ManageRooms")
            .AddEndpointFilter<PropertyAccessFilter>()
            .WithTags("Pay2S");
        settings.MapGet("/settings", async (Guid propertyId, Pay2SSettingsService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(propertyId, ct)));
        settings.MapPut("/settings", async (Guid propertyId, UpdatePay2SSettingsRequest request, Pay2SSettingsService service, CancellationToken ct) =>
        {
            var (result, error) = await service.SaveAsync(propertyId, request, ct);
            return error is null ? Results.Ok(result) : Problem(error, "Không thể lưu cấu hình Pay2S");
        }).AddEndpointFilter<ApiAntiforgeryFilter>();
        settings.MapPost("/intents", async (Guid propertyId, CreatePay2SIntentRequest request, Pay2SService service, CancellationToken ct) =>
        {
            var (result, error) = await service.CreateIntentAsync(
                propertyId,
                request.BookingId,
                cancelBookingOnExpiry: true,
                ct,
                useSettlementGrace: false);
            return error is null ? Results.Ok(result) : Problem(error, "Không thể tạo mã thanh toán Pay2S");
        }).AddEndpointFilter<ApiAntiforgeryFilter>();
        settings.MapPost("/intents/{intentId:guid}/resolve-late", async (
            Guid propertyId, Guid intentId, ResolveLatePay2SRequest request, ClaimsPrincipal user, Pay2SService service, CancellationToken ct) =>
        {
            var actorUserId = Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed) ? parsed : (Guid?)null;
            var (result, error) = await service.ResolveLatePaymentAsync(propertyId, intentId, request, actorUserId, ct);
            return error is null ? Results.Ok(result) : Problem(error, "Không thể xử lý khoản thanh toán đến muộn");
        }).AddEndpointFilter<ApiAntiforgeryFilter>();
        settings.MapGet("/bookings/{bookingId:guid}/latest-intent", async (
            Guid propertyId, Guid bookingId, DeLong.Web.Data.AppDbContext db, CancellationToken ct) =>
        {
            var result = await db.Pay2SPaymentIntents.AsNoTracking()
                .Where(x => x.PropertyId == propertyId && x.BookingId == bookingId)
                .OrderByDescending(x => x.CreatedAtUtc)
                .Select(x => new
                {
                    x.Id, x.OrderId, x.Amount, Status = x.Status.ToString(), x.ExpiresAtUtc, x.ReleaseAtUtc,
                    x.TransactionId, x.CallbackReceivedAtUtc, x.LastCallbackAttemptAtUtc, x.LastCallbackErrorCode,
                    x.LatePaymentResolution, x.LatePaymentResolutionNote, x.LatePaymentResolvedAtUtc
                }).FirstOrDefaultAsync(ct);
            return result is null ? Results.NoContent() : Results.Ok(result);
        });

        app.MapPost("/api/payments/pay2s/ipn", async (Pay2SIpnRequest request, Pay2SService service, ILoggerFactory loggerFactory, CancellationToken ct) =>
        {
            var (result, error) = await service.HandleIpnAsync(request, ct);
            if (error is null) return Results.Ok(new { success = true, status = result!.Status });
            loggerFactory.CreateLogger("Pay2S.IPN").LogWarning("Rejected Pay2S IPN {OrderId}: {Code}", request.OrderId, error.Code);
            return Results.Ok(new { success = false, code = error.Code });
        }).AllowAnonymous().RequireRateLimiting("pay2s-ipn");
        app.MapPost("/api/public/payments/pay2s/{orderId}/confirm-return", async (
            string orderId,
            Pay2SRedirectRequest request,
            Pay2SService service,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            if (!string.Equals(orderId, request.OrderId, StringComparison.Ordinal))
                return Results.BadRequest(new { code = "order_mismatch", message = "Mã phiên thanh toán không khớp." });
            var (result, error) = await service.HandleRedirectAsync(request, ct);
            if (error is null) return Results.Ok(new { success = true, status = result!.Status });
            loggerFactory.CreateLogger("Pay2S.Redirect").LogWarning(
                "Rejected signed Pay2S redirect {OrderId}: {Code}", request.OrderId, error.Code);
            return Results.BadRequest(new { success = false, code = error.Code, message = error.Message });
        }).AllowAnonymous().RequireRateLimiting("pay2s-status");
        app.MapGet("/api/public/payments/pay2s/{orderId}/confirm-return", (string orderId) =>
            Results.Redirect($"/payment/pay2s/return?orderId={Uri.EscapeDataString(orderId)}"))
            .AllowAnonymous()
            .RequireRateLimiting("pay2s-status");
        app.MapGet("/api/public/payments/pay2s/{orderId}", async (string orderId, DeLong.Web.Data.AppDbContext db, Pay2SService service, HttpContext httpContext, CancellationToken ct) =>
        {
            httpContext.Response.Headers.CacheControl = "no-store";
            await service.ExpirePendingAsync(ct);
            var result = await db.Pay2SPaymentIntents.AsNoTracking().Where(x => x.OrderId == orderId)
                .Select(x => new
                {
                    x.OrderId,
                    Status = x.Status.ToString(),
                    x.ExpiresAtUtc,
                    x.ReleaseAtUtc,
                    BookingCode = x.Booking.Code,
                    BookingStatus = x.Booking.Status,
                    x.SiteSlug
                })
                .SingleOrDefaultAsync(ct);
            return result is null ? Results.NotFound() : Results.Ok(new
            {
                result.OrderId,
                result.Status,
                result.ExpiresAtUtc,
                result.ReleaseAtUtc,
                result.BookingCode,
                InSettlementGrace = result.Status == "Pending" && result.ExpiresAtUtc <= DateTime.UtcNow && result.ReleaseAtUtc > DateTime.UtcNow,
                CanResume = result.Status == "Pending" &&
                            result.ExpiresAtUtc > DateTime.UtcNow &&
                            result.BookingStatus is not (Domain.Enums.BookingStatus.Cancelled or
                                Domain.Enums.BookingStatus.Completed or Domain.Enums.BookingStatus.NoShow),
                SuccessUrl = string.IsNullOrWhiteSpace(result.SiteSlug)
                    ? $"/booking/success?code={Uri.EscapeDataString(result.BookingCode)}"
                    : $"/h/{Uri.EscapeDataString(result.SiteSlug)}/booking/success?code={Uri.EscapeDataString(result.BookingCode)}"
            });
        }).AllowAnonymous().RequireRateLimiting("pay2s-status");
        app.MapGet("/api/public/payments/pay2s/{orderId}/resume", async (
            string orderId,
            DeLong.Web.Data.AppDbContext db,
            Pay2SService service,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            httpContext.Response.Headers.CacheControl = "no-store";
            await service.ExpirePendingAsync(ct);
            var intent = await db.Pay2SPaymentIntents.AsNoTracking()
                .Where(x => x.OrderId == orderId)
                .Select(x => new { x.Status, x.ExpiresAtUtc, x.PayUrl, BookingStatus = x.Booking.Status })
                .SingleOrDefaultAsync(ct);
            if (intent is null) return Results.NotFound();
            if (intent.Status != Domain.Enums.Pay2SPaymentIntentStatus.Pending ||
                intent.ExpiresAtUtc <= DateTime.UtcNow ||
                intent.BookingStatus is Domain.Enums.BookingStatus.Cancelled or
                    Domain.Enums.BookingStatus.Completed or Domain.Enums.BookingStatus.NoShow ||
                string.IsNullOrWhiteSpace(intent.PayUrl))
                return Results.Conflict(new
                {
                    code = "payment_session_closed",
                    message = "Phiên giữ phòng đã kết thúc hoặc booking không còn hiệu lực. Vui lòng tạo lượt đặt mới."
                });
            return Results.Ok(new { PaymentUrl = intent.PayUrl });
        }).AllowAnonymous().RequireRateLimiting("pay2s-status");
        return app;
    }

    private static IResult Problem(Pay2SOperationError error, string title) => Results.Problem(
        type: $"https://delong.local/problems/{error.Code}", title: title, detail: error.Message,
        statusCode: error.Code.EndsWith("not_found", StringComparison.Ordinal) ? 404 : 400,
        extensions: new Dictionary<string, object?> { ["code"] = error.Code });
}
