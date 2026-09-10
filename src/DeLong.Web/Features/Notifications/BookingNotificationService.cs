using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Notifications;

public sealed class BookingNotificationService(
    AppDbContext db,
    NotificationRealtimeBroker realtimeBroker,
    ILogger<BookingNotificationService> logger)
{
    private const string BookingRequestedType = "booking-requested";
    private const string BookingEmailOnlyType = "booking-email-only";
    private const string BookingConfirmedType = "booking-confirmed";
    private const string BookingCancelledType = "booking-cancelled";
    private const string BookingCheckedInType = "booking-checked-in";
    private const string BookingCompletedType = "booking-completed";
    private const string Pay2SLatePaymentType = "pay2s-paid-after-expiry";
    private static readonly string[] InAppTypes = [BookingRequestedType, Pay2SLatePaymentType];

    public async Task NotifyLatePay2SPaymentAsync(Guid propertyId, Guid bookingId, decimal amount, CancellationToken cancellationToken = default)
    {
        try
        {
            var existing = await db.Set<PropertyNotification>()
                .SingleOrDefaultAsync(x => x.PropertyId == propertyId && x.BookingId == bookingId && x.Type == Pay2SLatePaymentType, cancellationToken);
            var booking = await LoadBookingAsync(propertyId, bookingId, cancellationToken);
            if (booking is null) return;

            var notification = existing;
            if (notification is null)
            {
                notification = new PropertyNotification
                {
                    PropertyId = propertyId,
                    BookingId = bookingId,
                    Type = Pay2SLatePaymentType,
                    Title = $"Tiền Pay2S đến muộn · {booking.Code}",
                    Message = $"{booking.CustomerName} · {booking.RoomName} · {amount:N0} đ · bắt buộc xử lý",
                    ActionUrl = $"/Admin/Bookings?propertyId={propertyId}&bookingId={bookingId}&paymentIssue=late"
                };
                db.Add(notification);
            }

            await AddTelegramOutboxIfEnabledAsync(propertyId, notification, BuildLatePay2SMessage(booking, amount), cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            if (existing is null)
                realtimeBroker.Publish(new NotificationRealtimeEvent(notification.Id, propertyId, notification.Type, notification.CreatedAtUtc));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not create late Pay2S notification for booking {BookingId} in property {PropertyId}.", bookingId, propertyId);
            DetachPendingNotificationEntries();
        }
    }

    public async Task NotifyBookingCreatedAsync(Guid propertyId, Guid bookingId, CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = await db.Set<PropertyNotificationSettings>().AsNoTracking()
                .SingleOrDefaultAsync(x => x.PropertyId == propertyId, cancellationToken);
            var inAppEnabled = settings?.InAppBookingEnabled ?? true;
            var emailEnabled = settings?.EmailBookingEnabled ?? false;
            var telegramEnabled = IsTelegramConfigured(settings);
            if (!inAppEnabled && !emailEnabled && !telegramEnabled) return;

            if (await db.Set<PropertyNotification>().AsNoTracking()
                .AnyAsync(x => x.PropertyId == propertyId && x.BookingId == bookingId &&
                    (x.Type == BookingRequestedType || x.Type == BookingEmailOnlyType), cancellationToken))
                return;

            var booking = await LoadBookingAsync(propertyId, bookingId, cancellationToken);
            if (booking is null) return;
            var localTimes = GetLocalTimes(booking);
            var total = booking.TotalAmount;
            var templateData = new BookingEmailTemplateData(
                booking.PropertyName,
                booking.Code,
                booking.CustomerName,
                booking.CustomerPhone,
                booking.CustomerEmail ?? string.Empty,
                booking.RoomName,
                localTimes.CheckIn,
                localTimes.CheckOut,
                total,
                BookingGuestGuideEmailService.HtmlToText(booking.GuestGuideHtml),
                string.Empty);
            var notification = new PropertyNotification
            {
                PropertyId = propertyId,
                BookingId = booking.Id,
                Type = inAppEnabled ? BookingRequestedType : BookingEmailOnlyType,
                Title = $"Yêu cầu đặt phòng mới · {booking.Code}",
                Message = $"{booking.CustomerName} · {booking.RoomName} · {localTimes.CheckIn:dd/MM HH:mm}",
                ActionUrl = $"/Admin/Bookings?propertyId={propertyId}&bookingId={booking.Id}"
            };
            db.Add(notification);

            if (emailEnabled && !string.IsNullOrWhiteSpace(settings?.EmailRecipients))
            {
                db.Add(new NotificationEmailOutbox
                {
                    PropertyId = propertyId,
                    NotificationId = notification.Id,
                    ToRecipients = settings.EmailRecipients,
                    Subject = NotificationEmailTemplateRenderer.Render(settings.InternalBookingEmailSubjectTemplate, NotificationEmailTemplateRenderer.DefaultInternalBookingSubject, templateData),
                    BodyHtml = NotificationEmailTemplateRenderer.RenderHtml(settings.InternalBookingEmailBodyTemplate, NotificationEmailTemplateRenderer.DefaultInternalBookingBody, templateData),
                    BodyText = NotificationEmailTemplateRenderer.ToPlainText(NotificationEmailTemplateRenderer.RenderHtml(settings.InternalBookingEmailBodyTemplate, NotificationEmailTemplateRenderer.DefaultInternalBookingBody, templateData)),
                    NextAttemptAtUtc = DateTime.UtcNow
                });
            }

            if (telegramEnabled)
                AddTelegramOutbox(propertyId, notification, settings!.TelegramChatIds!, BuildBookingCreatedMessage(booking));

            await db.SaveChangesAsync(cancellationToken);
            if (inAppEnabled)
                realtimeBroker.Publish(new NotificationRealtimeEvent(notification.Id, propertyId, notification.Type, notification.CreatedAtUtc));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not create booking notification for booking {BookingId} in property {PropertyId}.", bookingId, propertyId);
            DetachPendingNotificationEntries();
        }
    }

    public async Task NotifyBookingStatusChangedAsync(
        Guid propertyId,
        Guid bookingId,
        BookingStatus previousStatus,
        BookingStatus nextStatus,
        string? reason = null,
        DateTime? occurredAtUtc = null,
        CancellationToken cancellationToken = default)
    {
        if (previousStatus == nextStatus) return;
        var type = nextStatus switch
        {
            BookingStatus.Confirmed => BookingConfirmedType,
            BookingStatus.Cancelled => BookingCancelledType,
            BookingStatus.CheckedIn => BookingCheckedInType,
            BookingStatus.Completed => BookingCompletedType,
            _ => null
        };
        if (type is null) return;

        try
        {
            if (await db.Set<PropertyNotification>().AsNoTracking()
                .AnyAsync(x => x.PropertyId == propertyId && x.BookingId == bookingId && x.Type == type, cancellationToken)) return;
            var booking = await LoadBookingAsync(propertyId, bookingId, cancellationToken);
            if (booking is null) return;
            var notification = new PropertyNotification
            {
                PropertyId = propertyId,
                BookingId = bookingId,
                Type = type,
                Title = BuildStatusTitle(nextStatus, booking.Code),
                Message = BuildStatusSummary(nextStatus, booking),
                ActionUrl = $"/Admin/Bookings?propertyId={propertyId}&bookingId={bookingId}"
            };
            db.Add(notification);
            await AddTelegramOutboxIfEnabledAsync(
                propertyId,
                notification,
                BuildStatusMessage(booking, nextStatus, reason, occurredAtUtc ?? DateTime.UtcNow),
                cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not enqueue status Telegram notification for booking {BookingId} in property {PropertyId}.", bookingId, propertyId);
            DetachPendingNotificationEntries();
        }
    }

    public async Task NotifyPaymentSucceededAsync(
        Guid propertyId,
        Guid bookingId,
        Guid paymentId,
        decimal amount,
        PaymentMethod method,
        decimal paid,
        decimal remaining,
        bool autoConfirmed = false,
        CancellationToken cancellationToken = default)
    {
        var type = $"payment-receipt:{paymentId:N}";
        try
        {
            if (await db.Set<PropertyNotification>().AsNoTracking()
                .AnyAsync(x => x.PropertyId == propertyId && x.BookingId == bookingId && x.Type == type, cancellationToken)) return;
            var booking = await LoadBookingAsync(propertyId, bookingId, cancellationToken);
            if (booking is null) return;
            var notification = new PropertyNotification
            {
                PropertyId = propertyId,
                BookingId = bookingId,
                Type = type,
                Title = $"Thanh toán thành công · {booking.Code}",
                Message = $"{booking.CustomerName} · {amount:N0} đ · {PaymentMethodText(method)}",
                ActionUrl = $"/Admin/Bookings?propertyId={propertyId}&bookingId={bookingId}"
            };
            db.Add(notification);
            await AddTelegramOutboxIfEnabledAsync(
                propertyId,
                notification,
                BuildPaymentMessage(booking, amount, method, paid, Math.Max(0, remaining), autoConfirmed),
                cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not enqueue payment Telegram notification for payment {PaymentId}, booking {BookingId}.", paymentId, bookingId);
            DetachPendingNotificationEntries();
        }
    }

    public async Task NotifyPay2SFailedAsync(
        Guid propertyId,
        Guid bookingId,
        Guid intentId,
        decimal amount,
        string? providerInfo,
        CancellationToken cancellationToken = default)
    {
        var type = $"pay2s-failed:{intentId:N}";
        try
        {
            if (await db.Set<PropertyNotification>().AsNoTracking()
                .AnyAsync(x => x.PropertyId == propertyId && x.BookingId == bookingId && x.Type == type, cancellationToken)) return;
            var booking = await LoadBookingAsync(propertyId, bookingId, cancellationToken);
            if (booking is null) return;
            var notification = new PropertyNotification
            {
                PropertyId = propertyId,
                BookingId = bookingId,
                Type = type,
                Title = $"Pay2S bất thường · {booking.Code}",
                Message = $"{booking.CustomerName} · {amount:N0} đ · cần kiểm tra",
                ActionUrl = $"/Admin/Bookings?propertyId={propertyId}&bookingId={bookingId}&paymentIssue=pay2s"
            };
            db.Add(notification);
            await AddTelegramOutboxIfEnabledAsync(propertyId, notification, BuildPay2SFailedMessage(booking, amount, providerInfo), cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not enqueue failed Pay2S Telegram notification for intent {IntentId}, booking {BookingId}.", intentId, bookingId);
            DetachPendingNotificationEntries();
        }
    }

    public async Task<NotificationFeedDto> GetFeedAsync(Guid propertyId, Guid userId, int take = 20, CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 50);
        var items = await db.Set<PropertyNotification>().AsNoTracking()
            .Where(x => x.PropertyId == propertyId && InAppTypes.Contains(x.Type))
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(take)
            .Select(x => new NotificationItemDto(
                x.Id,
                x.Type,
                x.BookingId,
                x.Title,
                x.Message,
                x.ActionUrl,
                x.CreatedAtUtc,
                x.Reads.Any(r => r.UserId == userId)))
            .ToListAsync(cancellationToken);
        var unreadCount = await db.Set<PropertyNotification>().AsNoTracking()
            .CountAsync(x => x.PropertyId == propertyId && InAppTypes.Contains(x.Type) && !x.Reads.Any(r => r.UserId == userId), cancellationToken);
        return new NotificationFeedDto(items, unreadCount);
    }

    public async Task<bool> MarkReadAsync(Guid propertyId, Guid notificationId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (!await db.Set<PropertyNotification>().AsNoTracking()
            .AnyAsync(x => x.Id == notificationId && x.PropertyId == propertyId && InAppTypes.Contains(x.Type), cancellationToken)) return false;
        if (await db.Set<PropertyNotificationRead>().AnyAsync(x => x.NotificationId == notificationId && x.UserId == userId, cancellationToken)) return true;
        db.Add(new PropertyNotificationRead { NotificationId = notificationId, UserId = userId, ReadAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> MarkAllReadAsync(Guid propertyId, Guid userId, CancellationToken cancellationToken = default)
    {
        var unreadIds = await db.Set<PropertyNotification>().AsNoTracking()
            .Where(x => x.PropertyId == propertyId && InAppTypes.Contains(x.Type) && !x.Reads.Any(r => r.UserId == userId))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        if (unreadIds.Count == 0) return 0;
        db.AddRange(unreadIds.Select(id => new PropertyNotificationRead { NotificationId = id, UserId = userId, ReadAtUtc = DateTime.UtcNow }));
        await db.SaveChangesAsync(cancellationToken);
        return unreadIds.Count;
    }

    private async Task<BookingTelegramData?> LoadBookingAsync(Guid propertyId, Guid bookingId, CancellationToken cancellationToken) =>
        await db.Bookings.AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.Id == bookingId)
            .Select(x => new BookingTelegramData(
                x.Id,
                x.Code,
                x.CheckInUtc,
                x.CheckOutUtc,
                x.TotalAmount,
                x.Customer.Name,
                x.Customer.Phone,
                x.Customer.Email,
                x.Room.Name,
                x.Room.GuestGuideHtml,
                x.Property.Name,
                x.Property.TimeZoneId))
            .SingleOrDefaultAsync(cancellationToken);

    private async Task AddTelegramOutboxIfEnabledAsync(Guid propertyId, PropertyNotification notification, string message, CancellationToken cancellationToken)
    {
        if (await db.Set<NotificationTelegramOutbox>().AsNoTracking().AnyAsync(x => x.NotificationId == notification.Id, cancellationToken)) return;
        var settings = await db.Set<PropertyNotificationSettings>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.PropertyId == propertyId, cancellationToken);
        if (!IsTelegramConfigured(settings)) return;
        AddTelegramOutbox(propertyId, notification, settings!.TelegramChatIds!, message);
    }

    private void AddTelegramOutbox(Guid propertyId, PropertyNotification notification, string chatIds, string message) =>
        db.Add(new NotificationTelegramOutbox
        {
            PropertyId = propertyId,
            NotificationId = notification.Id,
            ChatIds = chatIds,
            MessageText = Truncate(message, 4096),
            NextAttemptAtUtc = DateTime.UtcNow
        });

    private static bool IsTelegramConfigured(PropertyNotificationSettings? settings) =>
        settings?.TelegramBookingEnabled == true &&
        !string.IsNullOrWhiteSpace(settings.TelegramBotTokenProtected) &&
        !string.IsNullOrWhiteSpace(settings.TelegramChatIds) &&
        TelegramNotificationSender.ParseChatIds(settings.TelegramChatIds).Count > 0;

    private static (DateTime CheckIn, DateTime CheckOut) GetLocalTimes(BookingTelegramData booking)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(booking.TimeZoneId);
        return (
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(booking.CheckInUtc, DateTimeKind.Utc), timeZone),
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(booking.CheckOutUtc, DateTimeKind.Utc), timeZone));
    }

    private static DateTime ToLocal(BookingTelegramData booking, DateTime utc)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(booking.TimeZoneId);
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), timeZone);
    }

    private static string BuildBookingCreatedMessage(BookingTelegramData booking)
    {
        var local = GetLocalTimes(booking);
        return string.Join(Environment.NewLine,
        [
            $"🏡 {booking.PropertyName} · BOOKING MỚI",
            string.Empty,
            $"Mã: {booking.Code}",
            $"Khách: {booking.CustomerName}",
            $"SĐT: {booking.CustomerPhone}",
            $"Phòng: {booking.RoomName}",
            $"Nhận: {local.CheckIn:dd/MM/yyyy HH:mm}",
            $"Trả: {local.CheckOut:dd/MM/yyyy HH:mm}",
            $"Tổng tiền: {booking.TotalAmount:N0} VND",
            string.Empty,
            "Vui lòng mở trang quản trị để xử lý."
        ]);
    }

    private static string BuildStatusMessage(BookingTelegramData booking, BookingStatus status, string? reason, DateTime occurredAtUtc)
    {
        var local = GetLocalTimes(booking);
        if (status == BookingStatus.Confirmed)
            return string.Join(Environment.NewLine,
            [
                $"✅ {booking.PropertyName} · BOOKING ĐƯỢC XÁC NHẬN", string.Empty,
                $"Mã: {booking.Code}", $"Khách: {booking.CustomerName}", $"SĐT: {booking.CustomerPhone}",
                $"Phòng: {booking.RoomName}", $"Nhận: {local.CheckIn:dd/MM/yyyy HH:mm}", $"Trả: {local.CheckOut:dd/MM/yyyy HH:mm}",
                $"Tổng tiền: {booking.TotalAmount:N0} VND"
            ]);
        if (status == BookingStatus.Cancelled)
        {
            var lines = new List<string>
            {
                $"❌ {booking.PropertyName} · BOOKING BỊ HỦY", string.Empty,
                $"Mã: {booking.Code}", $"Khách: {booking.CustomerName}", $"Phòng: {booking.RoomName}",
                $"Nhận: {local.CheckIn:dd/MM/yyyy HH:mm}", $"Trả: {local.CheckOut:dd/MM/yyyy HH:mm}"
            };
            if (!string.IsNullOrWhiteSpace(reason)) { lines.Add(string.Empty); lines.Add($"Lý do: {Truncate(reason.Trim(), 600)}"); }
            return string.Join(Environment.NewLine, lines);
        }
        var eventLocal = ToLocal(booking, occurredAtUtc);
        return string.Join(Environment.NewLine,
        [
            status == BookingStatus.CheckedIn
                ? $"🏠 {booking.PropertyName} · CHECK-IN"
                : $"🏠 {booking.PropertyName} · CHECK-OUT / HOÀN TẤT",
            string.Empty,
            $"Mã: {booking.Code}", $"Khách: {booking.CustomerName}", $"Phòng: {booking.RoomName}",
            $"Thời gian: {eventLocal:dd/MM/yyyy HH:mm}"
        ]);
    }

    private static string BuildPaymentMessage(BookingTelegramData booking, decimal amount, PaymentMethod method, decimal paid, decimal remaining, bool autoConfirmed)
    {
        var lines = new List<string>
        {
            $"💰 {booking.PropertyName} · THANH TOÁN THÀNH CÔNG", string.Empty,
            $"Mã: {booking.Code}", $"Khách: {booking.CustomerName}", $"Phòng: {booking.RoomName}",
            $"Số tiền: {amount:N0} VND", $"Phương thức: {PaymentMethodText(method)}",
            $"Đã thanh toán: {paid:N0} VND", $"Còn lại: {Math.Max(0, remaining):N0} VND"
        };
        if (autoConfirmed) { lines.Add(string.Empty); lines.Add("✅ Booking đã được tự động xác nhận sau thanh toán."); }
        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildPay2SFailedMessage(BookingTelegramData booking, decimal amount, string? providerInfo) =>
        string.Join(Environment.NewLine,
        [
            $"⚠️ {booking.PropertyName} · THANH TOÁN PAY2S BẤT THƯỜNG", string.Empty,
            $"Mã: {booking.Code}", $"Khách: {booking.CustomerName}", $"Phòng: {booking.RoomName}",
            $"Số tiền: {amount:N0} VND", $"Thông tin Pay2S: {Truncate(providerInfo?.Trim() ?? "Không có thông tin chi tiết.", 800)}",
            string.Empty, "Vui lòng kiểm tra trước khi yêu cầu khách thanh toán lại."
        ]);

    private static string BuildLatePay2SMessage(BookingTelegramData booking, decimal amount) =>
        string.Join(Environment.NewLine,
        [
            $"⚠️ {booking.PropertyName} · PAY2S ĐẾN MUỘN", string.Empty,
            $"Mã: {booking.Code}", $"Khách: {booking.CustomerName}", $"SĐT: {booking.CustomerPhone}", $"Phòng: {booking.RoomName}",
            $"Số tiền: {amount:N0} VND", string.Empty,
            "Trạng thái: Tiền đã về sau khi phiên giữ phòng hết hạn.",
            "⚠️ Cần quản trị viên kiểm tra và xử lý ngay."
        ]);

    private static string BuildStatusTitle(BookingStatus status, string code) => status switch
    {
        BookingStatus.Confirmed => $"Booking được xác nhận · {code}",
        BookingStatus.Cancelled => $"Booking bị hủy · {code}",
        BookingStatus.CheckedIn => $"Check-in · {code}",
        _ => $"Check-out / hoàn tất · {code}"
    };

    private static string BuildStatusSummary(BookingStatus status, BookingTelegramData booking) =>
        $"{booking.CustomerName} · {booking.RoomName} · {status}";

    private static string PaymentMethodText(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "Tiền mặt",
        PaymentMethod.BankTransfer => "Chuyển khoản",
        PaymentMethod.Card => "Thẻ",
        PaymentMethod.Pay2S => "Pay2S",
        _ => "Khác"
    };

    private void DetachPendingNotificationEntries()
    {
        foreach (var entry in db.ChangeTracker.Entries().Where(x =>
                     x.State == EntityState.Added &&
                     x.Entity is PropertyNotification or NotificationEmailOutbox or NotificationTelegramOutbox))
            entry.State = EntityState.Detached;
    }

    private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];

    private sealed record BookingTelegramData(
        Guid Id,
        string Code,
        DateTime CheckInUtc,
        DateTime CheckOutUtc,
        decimal TotalAmount,
        string CustomerName,
        string CustomerPhone,
        string? CustomerEmail,
        string RoomName,
        string? GuestGuideHtml,
        string PropertyName,
        string TimeZoneId);
}