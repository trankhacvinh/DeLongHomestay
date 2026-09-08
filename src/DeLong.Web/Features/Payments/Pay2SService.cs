using System.Globalization;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Bookings;
using DeLong.Web.Features.Notifications;
using DeLong.Web.Features.Vouchers;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Payments;

public sealed class Pay2SService(
    AppDbContext db,
    Pay2SSettingsService settingsService,
    Pay2SClient client,
    BookingService bookingService,
    BookingNotificationService notificationService,
    BookingGuestGuideEmailService guestGuideEmailService,
    VoucherService voucherService)
{
    public async Task<(Pay2SIntentDto? Intent, Pay2SOperationError? Error)> CreateIntentAsync(
        Guid propertyId,
        Guid bookingId,
        bool cancelBookingOnExpiry,
        CancellationToken ct = default,
        string? siteSlug = null,
        bool useSettlementGrace = true)
    {
        await ExpirePendingAsync(ct);
        var now = DateTime.UtcNow;
        var existing = await db.Pay2SPaymentIntents.AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.BookingId == bookingId && x.Status == Pay2SPaymentIntentStatus.Pending && x.ExpiresAtUtc > now)
            .OrderByDescending(x => x.CreatedAtUtc).FirstOrDefaultAsync(ct);
        if (existing is not null) return (ToDto(existing), null);
        if (await db.Pay2SPaymentIntents.AsNoTracking().AnyAsync(x =>
                x.PropertyId == propertyId && x.BookingId == bookingId &&
                (x.Status == Pay2SPaymentIntentStatus.Pending || x.Status == Pay2SPaymentIntentStatus.Failed) &&
                x.ReleaseAtUtc > now, ct))
            return (null, new("payment_settlement_pending", "Phiên thanh toán đã đóng và đang trong thời gian chờ ngân hàng xác nhận. Không thể tạo QR mới."));

        var booking = await db.Bookings.Include(x => x.Customer)
            .SingleOrDefaultAsync(x => x.PropertyId == propertyId && x.Id == bookingId, ct);
        if (booking is null) return (null, new("booking_not_found", "Không tìm thấy booking."));
        if (booking.Status is BookingStatus.Cancelled or BookingStatus.Completed or BookingStatus.NoShow)
            return (null, new("booking_not_payable", "Booking không còn ở trạng thái có thể thanh toán."));
        var paid = await db.Payments.Where(x => x.BookingId == bookingId && !x.IsVoided)
            .SumAsync(x => x.Type == PaymentType.Receipt ? x.Amount : -x.Amount, ct);
        var amount = booking.TotalAmount - paid;
        if (amount <= 0) return (null, new("booking_paid", "Booking đã được thanh toán đủ."));

        var (profile, profileError) = await settingsService.GetProfileAsync(propertyId, ct);
        if (profile is null) return (null, profileError);
        var suffix = Guid.CreateVersion7().ToString("N")[..20].ToUpperInvariant();
        var orderId = $"DL{suffix}";
        var requestId = $"RQ{suffix}";
        var orderInfo = $"BOOK{booking.Code.Replace("-", string.Empty, StringComparison.Ordinal)[^Math.Min(20, booking.Code.Replace("-", string.Empty, StringComparison.Ordinal).Length)..]}";
        if (orderInfo.Length > 32) orderInfo = orderInfo[..32];
        var customer = new Pay2SCustomerData(booking.Customer.Name, booking.Customer.Phone, booking.Customer.Email);
        var (created, createError) = await client.CreateAsync(profile, requestId, orderId, orderInfo, amount, customer, ct);
        if (created is null) return (null, createError);

        var paymentExpiresAtUtc = now.AddMinutes(profile.HoldMinutes);
        var intent = new Pay2SPaymentIntent
        {
            PropertyId = propertyId,
            BookingId = bookingId,
            OrderId = orderId,
            RequestId = string.IsNullOrWhiteSpace(created.RequestId) ? requestId : created.RequestId.Trim(),
            OrderInfo = string.IsNullOrWhiteSpace(created.OrderInfo) ? orderInfo : created.OrderInfo,
            SiteSlug = string.IsNullOrWhiteSpace(siteSlug) ? null : siteSlug.Trim(),
            Amount = amount,
            PayUrl = created.PayUrl,
            ExpiresAtUtc = paymentExpiresAtUtc,
            ReleaseAtUtc = Pay2SPaymentLifecycle.CalculateReleaseAtUtc(
                paymentExpiresAtUtc,
                profile.SettlementGraceMinutes,
                useSettlementGrace),
            CancelBookingOnExpiry = cancelBookingOnExpiry
        };
        if (cancelBookingOnExpiry && booking.Status is BookingStatus.Requested or BookingStatus.Held)
            booking.Status = BookingStatus.Held;
        db.Add(intent);
        await db.SaveChangesAsync(ct);
        return (ToDto(intent), null);
    }

    public Task<(Pay2SIntentDto? Intent, Pay2SOperationError? Error)> HandleIpnAsync(
        Pay2SIpnRequest request,
        CancellationToken ct = default) =>
        HandleNotificationAsync(
            request,
            (accessKey, secretKey) => Pay2SClient.VerifyIpn(request, accessKey, secretKey),
            "Pay2S xác nhận qua IPN.",
            ct);

    public Task<(Pay2SIntentDto? Intent, Pay2SOperationError? Error)> HandleRedirectAsync(
        Pay2SRedirectRequest request,
        CancellationToken ct = default)
    {
        if (!long.TryParse(request.Amount, NumberStyles.None, CultureInfo.InvariantCulture, out var amount) ||
            !int.TryParse(request.ResultCode, NumberStyles.Integer, CultureInfo.InvariantCulture, out var resultCode))
            return Task.FromResult<(Pay2SIntentDto?, Pay2SOperationError?)>(
                (null, new("redirect_invalid", "Dữ liệu Pay2S trả về không hợp lệ.")));

        var transId = 0L;
        if (!string.IsNullOrWhiteSpace(request.TransId) &&
            !long.TryParse(request.TransId, NumberStyles.Integer, CultureInfo.InvariantCulture, out transId))
            return Task.FromResult<(Pay2SIntentDto?, Pay2SOperationError?)>(
                (null, new("redirect_transaction_invalid", "Mã giao dịch Pay2S trả về không hợp lệ.")));

        var normalized = new Pay2SIpnRequest(
            request.PartnerCode,
            request.OrderId,
            request.RequestId,
            amount,
            request.OrderInfo,
            request.OrderType,
            transId,
            resultCode,
            request.Message,
            request.PayType,
            request.ResponseTime,
            string.Empty,
            request.Signature);
        return HandleNotificationAsync(
            normalized,
            (accessKey, secretKey) => Pay2SClient.VerifyRedirect(request, accessKey, secretKey),
            "Pay2S xác nhận qua redirect có chữ ký; IPN có thể bổ sung lại cùng giao dịch.",
            ct);
    }

    private async Task<(Pay2SIntentDto? Intent, Pay2SOperationError? Error)> HandleNotificationAsync(
        Pay2SIpnRequest request,
        Func<string, string, bool> verifySignature,
        string paymentNote,
        CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var intent = await db.Pay2SPaymentIntents
            .FromSqlInterpolated($"""
                SELECT *
                FROM pay2_s_payment_intents
                WHERE order_id = {request.OrderId}
                FOR UPDATE
                """)
            .Include(x => x.Booking)
            .SingleOrDefaultAsync(ct);
        if (intent is null) return (null, new("intent_not_found", "Không tìm thấy phiên thanh toán."));
        intent.LastCallbackAttemptAtUtc = DateTime.UtcNow;
        var (profile, profileError) = await settingsService.GetProfileAsync(intent.PropertyId, ct);
        if (profile is null)
        {
            intent.LastCallbackErrorCode = profileError?.Code;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return (null, profileError);
        }
        if (!string.Equals(profile.PartnerCode, request.PartnerCode, StringComparison.Ordinal) ||
            !verifySignature(profile.AccessKey, profile.SecretKey))
        {
            intent.LastCallbackErrorCode = "signature_invalid";
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return (null, new("signature_invalid", "Chữ ký IPN Pay2S không hợp lệ."));
        }
        if (!string.Equals(intent.RequestId, request.RequestId, StringComparison.Ordinal))
        {
            // Older intents stored the merchant-generated RQ... value. Pay2S Collection Link V2
            // returns its own numeric requestId in the signed redirect/IPN. Adopt it only after
            // the provider signature and partner code have been verified.
            if (!intent.RequestId.StartsWith("RQ", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(request.RequestId))
            {
                intent.LastCallbackErrorCode = "request_mismatch";
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return (null, new("request_mismatch", "Mã yêu cầu Pay2S không khớp phiên thanh toán."));
            }

            intent.RequestId = request.RequestId.Trim();
        }
        if (!string.Equals(intent.OrderInfo, request.OrderInfo, StringComparison.Ordinal))
        {
            intent.LastCallbackErrorCode = "order_mismatch";
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return (null, new("order_mismatch", "Nội dung đơn hàng IPN không khớp phiên thanh toán."));
        }
        if (request.Amount != decimal.ToInt64(intent.Amount))
        {
            intent.LastCallbackErrorCode = "amount_mismatch";
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return (null, new("amount_mismatch", "Số tiền IPN không khớp phiên thanh toán."));
        }
        intent.LastCallbackErrorCode = null;

        if (request.TransId > 0 && intent.TransactionId == request.TransId &&
            intent.Status is Pay2SPaymentIntentStatus.Succeeded or Pay2SPaymentIntentStatus.PaidAfterExpiry)
        {
            await transaction.CommitAsync(ct);
            return (ToDto(intent), null);
        }
        if (intent.Status is Pay2SPaymentIntentStatus.Succeeded or Pay2SPaymentIntentStatus.PaidAfterExpiry)
        {
            if (!intent.TransactionId.HasValue && request.TransId > 0)
            {
                intent.TransactionId = request.TransId;
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return (ToDto(intent), null);
            }
            intent.LastCallbackErrorCode = "transaction_conflict";
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return (null, new("transaction_conflict", "Phiên thanh toán đã được ghi nhận bằng một giao dịch Pay2S khác."));
        }
        if (request.TransId > 0 && await db.Pay2SPaymentIntents.AsNoTracking().AnyAsync(x =>
                x.Id != intent.Id && x.TransactionId == request.TransId, ct))
        {
            intent.LastCallbackErrorCode = "transaction_reused";
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return (null, new("transaction_reused", "Mã giao dịch Pay2S đã được dùng cho một phiên thanh toán khác."));
        }
        intent.TransactionId = request.TransId > 0 ? request.TransId : null;
        intent.PayType = request.PayType;
        intent.ResultCode = request.ResultCode;
        intent.ProviderMessage = request.Message;
        intent.CallbackReceivedAtUtc = DateTime.UtcNow;
        if (request.ResultCode != 0)
        {
            intent.Status = Pay2SPaymentIntentStatus.Failed;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return (ToDto(intent), null);
        }

        var now = DateTime.UtcNow;
        var late = Pay2SPaymentLifecycle.IsSuccessfulPaymentLate(intent.Status, intent.Booking.Status, now, intent.ReleaseAtUtc);
        intent.Status = late ? Pay2SPaymentIntentStatus.PaidAfterExpiry : Pay2SPaymentIntentStatus.Succeeded;
        var payment = new Payment
        {
            PropertyId = intent.PropertyId,
            BookingId = intent.BookingId,
            Type = PaymentType.Receipt,
            Method = PaymentMethod.Pay2S,
            Amount = intent.Amount,
            OccurredAtUtc = DateTime.UtcNow,
            Reference = request.TransId > 0
                ? request.TransId.ToString(CultureInfo.InvariantCulture)
                : request.OrderId,
            Note = late ? "Pay2S thanh toán sau khi phiên giữ phòng hết hạn; cần quản trị viên xử lý." : paymentNote
        };
        db.Add(payment);
        intent.Payment = payment;
        if (!late && intent.Booking.Status is BookingStatus.Requested or BookingStatus.Held)
            intent.Booking.Status = BookingStatus.Confirmed;
        if (!late) await voucherService.MarkRedeemedAsync(intent.BookingId, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        if (late) await notificationService.NotifyLatePay2SPaymentAsync(intent.PropertyId, intent.BookingId, intent.Amount, ct);
        else await guestGuideEmailService.QueueAutomaticAsync(intent.PropertyId, intent.BookingId, ct);
        return (ToDto(intent), null);
    }

    public async Task<int> ExpirePendingAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var intents = await db.Pay2SPaymentIntents
            .FromSqlInterpolated($"""
                SELECT *
                FROM pay2_s_payment_intents
                WHERE status IN ('Pending', 'Failed') AND release_at_utc <= {now}
                FOR UPDATE SKIP LOCKED
                """)
            .Include(x => x.Booking)
            .ToListAsync(ct);
        var cancelledBookingIds = new List<(Guid PropertyId, Guid BookingId)>();
        foreach (var intent in intents)
        {
            intent.Status = Pay2SPaymentIntentStatus.Expired;
            if (intent.CancelBookingOnExpiry && intent.Booking.Status is BookingStatus.Held or BookingStatus.Requested)
            {
                intent.Booking.Status = BookingStatus.Cancelled;
                await voucherService.ReleaseReservedAsync(intent.BookingId, "Booking bị hủy do hết thời gian thanh toán Pay2S.", ct);
                cancelledBookingIds.Add((intent.PropertyId, intent.BookingId));
            }
        }
        if (intents.Count > 0) await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        foreach (var cancelled in cancelledBookingIds)
            await guestGuideEmailService.QueueCancellationAsync(cancelled.PropertyId, cancelled.BookingId, null, "Booking đã bị hủy do hết thời gian thanh toán.", ct);
        return intents.Count;
    }

    public async Task<(Pay2SIntentDto? Intent, Pay2SOperationError? Error)> ResolveLatePaymentAsync(
        Guid propertyId, Guid intentId, ResolveLatePay2SRequest request, Guid? actorUserId, CancellationToken ct = default)
    {
        var intent = await db.Pay2SPaymentIntents.Include(x => x.Booking)
            .SingleOrDefaultAsync(x => x.PropertyId == propertyId && x.Id == intentId, ct);
        if (intent is null) return (null, new("intent_not_found", "Không tìm thấy phiên thanh toán."));
        if (intent.Status != Pay2SPaymentIntentStatus.PaidAfterExpiry)
            return (null, new("intent_not_late_paid", "Phiên này không phải khoản thanh toán đến muộn."));
        if (!string.IsNullOrWhiteSpace(intent.LatePaymentResolution))
            return (null, new("intent_already_resolved", "Khoản thanh toán đến muộn đã được xử lý."));

        var action = request.Action.Trim().ToLowerInvariant();
        if (action == "refund")
        {
            db.Add(new Payment
            {
                PropertyId = propertyId,
                BookingId = intent.BookingId,
                Type = PaymentType.Refund,
                Method = PaymentMethod.Pay2S,
                Amount = intent.Amount,
                OccurredAtUtc = DateTime.UtcNow,
                Reference = intent.TransactionId?.ToString(),
                Note = $"Hoàn khoản Pay2S đến muộn. {request.Note}".Trim()
            });
            intent.LatePaymentResolution = "Refunded";
        }
        else if (action is "confirm" or "move")
        {
            var booking = intent.Booking;
            var roomId = action == "move" ? request.RoomId : booking.RoomId;
            var checkInUtc = action == "move" ? request.CheckInUtc : booking.CheckInUtc;
            var checkOutUtc = action == "move" ? request.CheckOutUtc : booking.CheckOutUtc;
            if (!roomId.HasValue || !checkInUtc.HasValue || !checkOutUtc.HasValue || checkOutUtc <= checkInUtc)
                return (null, new("replacement_schedule_invalid", "Vui lòng chọn phòng và thời gian thay thế hợp lệ."));
            if (!await db.Rooms.AsNoTracking().AnyAsync(x => x.PropertyId == propertyId && x.Id == roomId && x.IsActive, ct))
                return (null, new("room_not_found", "Không tìm thấy phòng đang hoạt động."));
            if (await bookingService.HasConflictAsync(propertyId, roomId.Value, checkInUtc.Value, checkOutUtc.Value, booking.Id, ct))
                return (null, new("booking_conflict", "Phòng đã có booking khác trong khoảng thời gian này."));
            booking.RoomId = roomId.Value;
            booking.CheckInUtc = DateTime.SpecifyKind(checkInUtc.Value, DateTimeKind.Utc);
            booking.CheckOutUtc = DateTime.SpecifyKind(checkOutUtc.Value, DateTimeKind.Utc);
            booking.Status = BookingStatus.Confirmed;
            intent.LatePaymentResolution = action == "move" ? "MovedAndConfirmed" : "ManuallyConfirmed";
        }
        else return (null, new("resolution_invalid", "Cách xử lý phải là hoàn tiền, chuyển phòng/thời gian hoặc xác nhận thủ công."));

        intent.LatePaymentResolutionNote = request.Note?.Trim();
        intent.LatePaymentResolvedAtUtc = DateTime.UtcNow;
        intent.LatePaymentResolvedByUserId = actorUserId;
        await db.SaveChangesAsync(ct);
        if (action is "confirm" or "move")
            await guestGuideEmailService.QueueAutomaticAsync(propertyId, intent.BookingId, ct);
        return (ToDto(intent), null);
    }

    private static Pay2SIntentDto ToDto(Pay2SPaymentIntent x) => new(x.Id, x.BookingId, x.OrderId, x.Amount, x.Status.ToString(), x.PayUrl, x.ExpiresAtUtc, x.ReleaseAtUtc);
}
