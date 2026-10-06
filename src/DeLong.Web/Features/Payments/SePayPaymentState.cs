using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Payments;

public sealed record SePayPaymentState(string OrderId, decimal Amount, string? TransferContent,
    string? AccountNumber, string? Bank, string Status, DateTime ExpiresAtUtc, DateTime ReleaseAtUtc,
    string? QrUrl, string SuccessUrl)
{
    public static async Task<SePayPaymentState?> LoadAsync(AppDbContext db, string orderId, CancellationToken ct)
    {
        var intent = await db.Pay2SPaymentIntents.AsNoTracking().Include(x => x.Booking)
            .SingleOrDefaultAsync(x => x.Provider == PaymentMethod.SePay && x.OrderId == orderId, ct);
        return intent is null ? null : FromIntent(intent, DateTime.UtcNow);
    }

    public static SePayPaymentState FromIntent(Pay2SPaymentIntent intent, DateTime now)
    {
        // The worker persists expiry; a status read must enforce the deadline even between worker runs.
        var status = intent.Status is Pay2SPaymentIntentStatus.Pending or Pay2SPaymentIntentStatus.Failed &&
            intent.ReleaseAtUtc <= now ? Pay2SPaymentIntentStatus.Expired : intent.Status;
        var canPay = status == Pay2SPaymentIntentStatus.Pending && intent.ExpiresAtUtc > now &&
            intent.Booking.Status is not (BookingStatus.Cancelled or BookingStatus.Completed or BookingStatus.NoShow);
        var parameters = QueryHelpers.ParseQuery(
            Uri.TryCreate(intent.SePayQrUrl, UriKind.Absolute, out var uri) ? uri.Query : "");
        return new(intent.OrderId, intent.Amount, canPay ? parameters["des"].ToString() : null,
            canPay ? parameters["acc"].ToString() : null, canPay ? parameters["bank"].ToString() : null,
            status.ToString(), intent.ExpiresAtUtc, intent.ReleaseAtUtc, canPay ? intent.SePayQrUrl : null,
            (string.IsNullOrEmpty(intent.SiteSlug) ? "" : "/h/" + Uri.EscapeDataString(intent.SiteSlug)) +
            "/booking/success?code=" + Uri.EscapeDataString(intent.Booking.Code));
    }
}
