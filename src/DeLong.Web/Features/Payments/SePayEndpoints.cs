using DeLong.Web.Common.Security;
using DeLong.Web.Data;
using DeLong.Web.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DeLong.Web.Features.Payments;

public static class SePayEndpoints
{
    public static IEndpointRouteBuilder MapSePayEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/properties/{propertyId:guid}/sepay")
            .RequireAuthorization("ManageRooms").AddEndpointFilter<PropertyAccessFilter>();
        admin.MapGet("/settings", async (Guid propertyId, SePaySettingsService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(propertyId, ct)));
        admin.MapPut("/settings", async (Guid propertyId, SePaySettingsRequest request, SePaySettingsService service, CancellationToken ct) =>
        {
            var error = await service.SaveAsync(propertyId, request, ct);
            return error is null ? Results.Ok(await service.GetAsync(propertyId, ct)) : Results.Problem(error, statusCode: 400);
        }).AddEndpointFilter<ApiAntiforgeryFilter>();
        admin.MapPost("/reconcile/{transactionId:long}", async (Guid propertyId, long transactionId, SePayService service, CancellationToken ct) =>
        {
            var result = await service.ReconcileAsync(propertyId, transactionId, ct);
            return result.Status == 200 ? Results.Ok(new { outcome = result.Outcome }) :
                Results.Problem("Không đối soát được giao dịch SePay: " + result.Outcome, statusCode: result.Status);
        }).AddEndpointFilter<ApiAntiforgeryFilter>();
        admin.MapGet("/transactions", async (Guid propertyId, AppDbContext db, CancellationToken ct) =>
            Results.Ok(await db.SePayTransactions.AsNoTracking().Where(x => x.PropertyId == propertyId)
                .OrderByDescending(x => x.CreatedAtUtc).Take(200)
                .Select(x => new { x.Id, x.TransactionId, x.Code, x.Amount, x.Outcome, x.Content, x.CreatedAtUtc,
                    x.ResolutionNote, x.ResolvedAtUtc }).ToListAsync(ct)));
        admin.MapPost("/transactions/{id:guid}/resolve", async (Guid propertyId, Guid id, SePayResolution request,
            AppDbContext db, ClaimsPrincipal user, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Note) || request.Note.Length > 2000)
                return Results.Problem("Nhập ghi chú xử lý tối đa 2000 ký tự.", statusCode: 400);
            var row = await db.SePayTransactions.SingleOrDefaultAsync(x => x.PropertyId == propertyId && x.Id == id, ct);
            if (row is null) return Results.NotFound();
            if (row.ResolvedAtUtc.HasValue) return Results.Conflict();
            row.ResolutionNote = request.Note.Trim();
            row.ResolvedAtUtc = DateTime.UtcNow;
            row.ResolvedByUserId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await db.SaveChangesAsync(ct);
            return Results.Ok();
        }).AddEndpointFilter<ApiAntiforgeryFilter>();
        app.MapPost("/api/payments/sepay/{propertyId:guid}/webhook", async (Guid propertyId, SePayWebhook request,
            HttpContext http, SePayService service, CancellationToken ct) =>
        {
            var result = await service.ReceiveAsync(propertyId, request, http.Request.Headers.Authorization, ct);
            return Results.Json(new { success = result.Status == 200, code = result.Outcome }, statusCode: result.Status);
        }).AllowAnonymous().RequireRateLimiting("pay2s-ipn");
        app.MapGet("/api/public/payments/sepay/{orderId}", async (string orderId, AppDbContext db, Pay2SService lifecycle,
            HttpContext http, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            await lifecycle.ExpirePendingAsync(ct);
            var intent = await db.Pay2SPaymentIntents.AsNoTracking().Include(x => x.Booking)
                .SingleOrDefaultAsync(x => x.Provider == PaymentMethod.SePay && x.OrderId == orderId, ct);
            if (intent is null) return Results.NotFound();
            var canPay = intent.Status == Pay2SPaymentIntentStatus.Pending && intent.ExpiresAtUtc > DateTime.UtcNow &&
                intent.Booking.Status is not (BookingStatus.Cancelled or BookingStatus.Completed or BookingStatus.NoShow);
            var qrParameters = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(
                Uri.TryCreate(intent.SePayQrUrl, UriKind.Absolute, out var qrUri) ? qrUri.Query : "");
            return Results.Ok(new { intent.OrderId, intent.Amount,
                TransferContent = canPay ? qrParameters["des"].ToString() : null,
                AccountNumber = canPay ? qrParameters["acc"].ToString() : null,
                Bank = canPay ? qrParameters["bank"].ToString() : null, Status = intent.Status.ToString(), intent.ExpiresAtUtc,
                intent.ReleaseAtUtc, QrUrl = canPay ? intent.SePayQrUrl : null,
                SuccessUrl = (string.IsNullOrEmpty(intent.SiteSlug) ? "" : "/h/" + Uri.EscapeDataString(intent.SiteSlug)) +
                    "/booking/success?code=" + Uri.EscapeDataString(intent.Booking.Code) });
        }).AllowAnonymous().RequireRateLimiting("pay2s-status");
        return app;
    }
}
public sealed record SePayResolution(string Note);
