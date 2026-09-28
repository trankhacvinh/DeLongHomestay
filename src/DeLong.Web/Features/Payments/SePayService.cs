using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Bookings;
using DeLong.Web.Features.Notifications;
using DeLong.Web.Features.Vouchers;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DeLong.Web.Features.Payments;

public sealed record SePayWebhook(long Id, string? AccountNumber, string? SubAccount, string? Code,
    string? Content, string? TransferType, decimal TransferAmount);

public sealed class SePayService(AppDbContext db, SePaySettingsService settings, HttpClient httpClient,
    VoucherService vouchers, BookingNotificationService notifications,
    BookingGuestGuideEmailService emails, PaymentService payments)
{
    public const string PaymentCodePrefix = "DH";

    public static string? PaymentCode(SePayWebhook request)
    {
        var code = request.Code?.Trim().ToUpperInvariant();
        if (!string.IsNullOrEmpty(code) && Regex.IsMatch(code, $"^{PaymentCodePrefix}[0-9]{{10}}$"))
            return code;
        var matches = Regex.Matches(
            request.Content?.ToUpperInvariant() ?? "",
            $@"(?<![A-Z0-9]){PaymentCodePrefix}[0-9]{{10}}(?![A-Z0-9])");
        return matches.Count == 1 ? matches[0].Value : null;
    }

    public Task<(int Status, string Outcome)> ReceiveAsync(Guid propertyId, SePayWebhook request,
        string? authorization, CancellationToken ct) => ReceiveCoreAsync(propertyId, request, authorization, false, ct);

    public async Task<(int Status, string Outcome)> ReconcileAsync(Guid propertyId, long transactionId, CancellationToken ct)
    {
        if (transactionId <= 0) return (400, "invalid_transaction_id");
        var profile = await db.PropertySePaySettings.AsNoTracking().SingleOrDefaultAsync(x => x.PropertyId == propertyId, ct);
        if (string.IsNullOrEmpty(profile?.ApiTokenProtected)) return (400, "api_token_missing");
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                "https://my.sepay.vn/userapi/transactions/details/" + transactionId.ToString(CultureInfo.InvariantCulture));
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", settings.Unprotect(profile.ApiTokenProtected));
            using var response = await httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return (502, "sepay_api_unavailable");
            using var document = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root = document.RootElement;
            if (!root.TryGetProperty("transaction", out var row) || row.ValueKind != System.Text.Json.JsonValueKind.Object)
                return (502, "sepay_api_invalid_response");
            string? Text(string name) => row.TryGetProperty(name, out var value) && value.ValueKind != System.Text.Json.JsonValueKind.Null ? value.ToString() : null;
            if (!long.TryParse(Text("id"), out var id) || id != transactionId ||
                !decimal.TryParse(Text("amount_in"), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) || amount <= 0)
                return (400, "not_incoming_transaction");
            return await ReceiveCoreAsync(propertyId,
                new SePayWebhook(id, Text("account_number"), Text("sub_account"), Text("code"), Text("transaction_content"), "in", amount),
                null, true, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException or System.Security.Cryptography.CryptographicException ||
            ex is OperationCanceledException && !ct.IsCancellationRequested)
        {
            return (502, "sepay_api_unavailable");
        }
    }

    private async Task<(int Status, string Outcome)> ReceiveCoreAsync(Guid propertyId, SePayWebhook request,
        string? authorization, bool verifiedByProviderApi, CancellationToken ct)
    {
        if (request.Id <= 0 || request.TransferAmount <= 0 || request.TransferAmount != decimal.Truncate(request.TransferAmount) ||
            request.TransferAmount > 9999999999999999m || (request.Content?.Length ?? 0) > 4000 ||
            (request.AccountNumber?.Length ?? 0) > 100 || (request.Code?.Length ?? 0) > 100)
            return (400, "invalid_payload");
        var code = PaymentCode(request);
        var snapshot = code is null ? null : await db.Pay2SPaymentIntents.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Provider == PaymentMethod.SePay && x.PropertyId == propertyId && x.OrderId == code, ct);
        var profile = await db.PropertySePaySettings.AsNoTracking().SingleOrDefaultAsync(x => x.PropertyId == propertyId, ct);
        // Old sessions keep their verification key and destination after settings change.
        var key = snapshot?.SePayWebhookKeyProtected ?? profile?.WebhookKeyProtected;
        if (!verifiedByProviderApi && !settings.Verify(authorization, key) && !settings.Verify(authorization, profile?.WebhookKeyProtected))
            return (401, "unauthorized");
        if (snapshot is null && code is not null && await db.Pay2SPaymentIntents.AsNoTracking()
                .AnyAsync(x => x.Provider == PaymentMethod.SePay && x.OrderId == code && x.PropertyId != propertyId, ct))
            return (200, "other_property_ignored");
        if (request.TransferType == "out") return (200, "outgoing_ignored");
        if (request.TransferType != "in") return (400, "invalid_direction");
        // Keep old QR sessions valid after a destination rotation, while allowing an
        // administrator to correct the current bank/VA configuration for an open session.
        var matchesAccount = MatchesDestination(snapshot?.SePayBankAccount, snapshot?.SePaySubAccount, request) ||
            MatchesDestination(profile?.BankAccountNumber, profile?.SubAccount, request);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Serialize duplicate deliveries even when they arrive at different property endpoints.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({request.Id})", ct);
        var existing = await db.SePayTransactions.AsNoTracking().SingleOrDefaultAsync(x => x.TransactionId == request.Id, ct);
        if (existing is not null)
        {
            await transaction.CommitAsync(ct);
            return existing.PropertyId == propertyId ? (200, existing.Outcome) : (409, "transaction_owned_by_other_property");
        }
        if (!matchesAccount) return (400, "account_mismatch");
        Pay2SPaymentIntent? intent = null;
        if (snapshot is not null)
        {
            intent = await db.Pay2SPaymentIntents.FromSqlInterpolated($"SELECT * FROM pay2_s_payment_intents WHERE id = {snapshot.Id} FOR UPDATE")
                .SingleAsync(ct);
            intent.Booking = await db.Bookings.FromSqlInterpolated($"SELECT * FROM bookings WHERE id = {intent.BookingId} FOR UPDATE")
                .SingleAsync(ct);
        }
        var received = new SePayTransaction
        {
            PropertyId = propertyId, TransactionId = request.Id, IntentId = intent?.Id,
            Code = code ?? request.Code ?? "", AccountNumber = request.AccountNumber ?? "",
            Content = request.Content ?? "", Amount = request.TransferAmount,
            Outcome = intent is null ? "unmatched_code" : request.TransferAmount != intent.Amount ? "amount_mismatch" :
                intent.PaymentId.HasValue ? "additional_transfer" : "matched"
        };
        db.Add(received);
        if (received.Outcome != "matched")
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return (200, received.Outcome);
        }
        var now = DateTime.UtcNow;
        var late = Pay2SPaymentLifecycle.IsSuccessfulPaymentLate(intent!.Status, intent.Booking.Status, now, intent.ReleaseAtUtc) ||
            intent.Booking.Status is BookingStatus.Completed or BookingStatus.NoShow;
        var payment = new Payment
        {
            PropertyId = propertyId, BookingId = intent.BookingId, Type = PaymentType.Receipt,
            Method = PaymentMethod.SePay, Amount = request.TransferAmount, OccurredAtUtc = now,
            Reference = request.Id.ToString(CultureInfo.InvariantCulture),
            Note = late ? "SePay: tiền đến sau khi phiên đóng; cần xử lý." : "SePay xác nhận qua webhook đã xác thực."
        };
        db.Add(payment);
        intent.Payment = payment;
        intent.TransactionId = request.Id;
        intent.Status = late ? Pay2SPaymentIntentStatus.PaidAfterExpiry : Pay2SPaymentIntentStatus.Succeeded;
        intent.CallbackReceivedAtUtc = now;
        intent.LastCallbackAttemptAtUtc = now;
        var confirmed = !late && intent.Booking.Status is BookingStatus.Requested or BookingStatus.Held;
        if (confirmed) intent.Booking.Status = BookingStatus.Confirmed;
        if (!late) await vouchers.MarkRedeemedAsync(intent.BookingId, ct);
        received.Outcome = late ? "paid_after_expiry" : "succeeded";
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        if (late) await notifications.NotifyLatePay2SPaymentAsync(propertyId, intent.BookingId, payment.Amount, ct, "SePay");
        else
        {
            var paid = await payments.GetNetPaidAsync(propertyId, intent.BookingId, ct);
            await notifications.NotifyPaymentSucceededAsync(propertyId, intent.BookingId, payment.Id, payment.Amount,
                PaymentMethod.SePay, paid, Math.Max(0, intent.Booking.TotalAmount - paid), confirmed, ct);
            await emails.QueueAutomaticAsync(propertyId, intent.BookingId, ct);
        }
        return (200, received.Outcome);
    }

    private static bool MatchesDestination(string? accountNumber, string? subAccount, SePayWebhook request) =>
        !string.IsNullOrWhiteSpace(accountNumber) &&
        string.Equals(accountNumber.Trim(), request.AccountNumber?.Trim(), StringComparison.Ordinal) &&
        string.Equals(subAccount?.Trim() ?? "", request.SubAccount?.Trim() ?? "", StringComparison.OrdinalIgnoreCase);
}
