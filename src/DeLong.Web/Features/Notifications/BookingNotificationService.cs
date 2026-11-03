using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Cancellations;
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
    private static readonly string[] InAppTypes = [BookingRequestedType, Pay2SLatePaymentType, "data-quality"];

    public async Task NotifyLatePay2SPaymentAsync(Guid propertyId, Guid bookingId, decimal amount, CancellationToken cancellationToken = default, string provider = "Pay2S")
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
                    Title = $"Tiền {provider} đến muộn · {booking.Code}",
                    Message = $"{booking.CustomerName} · {booking.RoomName} · {amount:N0} đ · bắt buộc xử lý",
                    ActionUrl = $"/Admin/Bookings?propertyId={propertyId}&bookingId={bookingId}&paymentIssue=late"
                };
                db.Add(notification);
            }

            await AddTelegramOutboxIfEnabledAsync(propertyId, notification, BuildLatePay2SMessage(booking, amount, provider), cancellationToken);
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
            var telegramEnabled = IsTelegramConfigured(settings) && TelegramEvents.Allows(settings?.TelegramEventTypes, BookingRequestedType);
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
                string.Empty,
                ScheduleAdjustment(booking.Note));
            var notification = new PropertyNotification
            {
                PropertyId = propertyId,
                BookingId = booking.Id,
                Type = inAppEnabled ? BookingRequestedType : BookingEmailOnlyType,
                Title = $"Yêu cầu đặt phòng mới · {booking.Code}",
                Message = $"{booking.CustomerName} · {booking.RoomName} · {localTimes.CheckIn:dd/MM/yyyy HH:mm}",
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
        CancellationToken cancellationToken = default,
        CancellationNotice? cancellation = null)
    {
        if (previousStatus == nextStatus) return;
        var type = nextStatus switch
        {
            BookingStatus.Confirmed => BookingConfirmedType,
            // Mỗi lần hủy có hồ sơ riêng nên booking hủy rồi khôi phục rồi hủy lại vẫn được báo lại.
            BookingStatus.Cancelled => cancellation is null ? BookingCancelledType : $"{BookingCancelledType}:{cancellation.CancellationId:N}",
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
                BuildStatusMessage(booking, nextStatus, reason, occurredAtUtc ?? DateTime.UtcNow, cancellation),
                cancellationToken);
            if (cancellation is not null)
                await AddManagerCancellationEmailIfEnabledAsync(propertyId, notification, booking, cancellation, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            if (nextStatus == BookingStatus.Cancelled && !await db.Set<PropertyNotificationSettings>().AsNoTracking()
                .AnyAsync(x => x.PropertyId == propertyId && !x.InAppBookingEnabled, cancellationToken))
                realtimeBroker.Publish(new NotificationRealtimeEvent(notification.Id, propertyId, notification.Type, notification.CreatedAtUtc));
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
            if (autoConfirmed)
                await AddInternalBookingEmailIfEnabledAsync(propertyId, notification, booking, cancellationToken);
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

    public async Task NotifyRefundRecordedAsync(
        Guid propertyId,
        Guid bookingId,
        Guid paymentId,
        decimal amount,
        PaymentMethod method,
        string? actorName,
        decimal netPaidAfter,
        CancellationToken cancellationToken = default)
    {
        var type = $"booking-refunded:{paymentId:N}";
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
                Title = $"Hoàn tiền · {booking.Code}",
                Message = $"{booking.CustomerName} · {CancellationRecorder.Money(amount)} · {PaymentMethodText(method)}",
                ActionUrl = $"/Admin/Bookings?propertyId={propertyId}&bookingId={bookingId}"
            };
            db.Add(notification);
            await AddTelegramOutboxIfEnabledAsync(propertyId, notification, string.Join(Environment.NewLine,
            [
                $"💸 {booking.PropertyName} · ĐÃ GHI NHẬN HOÀN TIỀN", string.Empty,
                $"Mã: {booking.Code}", $"Khách: {booking.CustomerName}", $"Phòng: {booking.RoomName}",
                $"Số tiền hoàn: {CancellationRecorder.Money(amount)}", $"Phương thức: {PaymentMethodText(method)}",
                $"Người ghi nhận: {(string.IsNullOrWhiteSpace(actorName) ? "Không rõ" : actorName)}",
                $"Khách còn đã thanh toán: {CancellationRecorder.Money(Math.Max(0, netPaidAfter))}"
            ]), cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not enqueue refund notification for payment {PaymentId}, booking {BookingId}.", paymentId, bookingId);
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

    public async Task<NotificationFeedDto> GetFeedAsync(Guid propertyId, Guid userId, int take = 20, CancellationToken cancellationToken = default, bool includeDataQuality = false)
    {
        take = Math.Clamp(take, 1, 50);
        var notifications = InAppNotifications(propertyId).Where(x => includeDataQuality || x.Type != "data-quality");
        var items = await notifications
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
        var unreadCount = await notifications
            .CountAsync(x => !x.Reads.Any(r => r.UserId == userId), cancellationToken);
        return new NotificationFeedDto(items, unreadCount);
    }

    public async Task<bool> MarkReadAsync(Guid propertyId, Guid notificationId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (!await InAppNotifications(propertyId)
            .AnyAsync(x => x.Id == notificationId, cancellationToken)) return false;
        if (await db.Set<PropertyNotificationRead>().AnyAsync(x => x.NotificationId == notificationId && x.UserId == userId, cancellationToken)) return true;
        db.Add(new PropertyNotificationRead { NotificationId = notificationId, UserId = userId, ReadAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> MarkAllReadAsync(Guid propertyId, Guid userId, CancellationToken cancellationToken = default)
    {
        var unreadIds = await InAppNotifications(propertyId)
            .Where(x => !x.Reads.Any(r => r.UserId == userId))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        if (unreadIds.Count == 0) return 0;
        db.AddRange(unreadIds.Select(id => new PropertyNotificationRead { NotificationId = id, UserId = userId, ReadAtUtc = DateTime.UtcNow }));
        await db.SaveChangesAsync(cancellationToken);
        return unreadIds.Count;
    }

    private IQueryable<PropertyNotification> InAppNotifications(Guid propertyId) =>
        db.Set<PropertyNotification>().AsNoTracking()
            .Where(x => x.PropertyId == propertyId &&
                (InAppTypes.Contains(x.Type) ||
                 ((x.Type == BookingCancelledType || x.Type.StartsWith(BookingCancelledType + ":")) &&
                  !db.Set<PropertyNotificationSettings>().AsNoTracking()
                      .Any(settings => settings.PropertyId == propertyId && !settings.InAppBookingEnabled))));

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
                x.Property.TimeZoneId,
                x.Note))
            .SingleOrDefaultAsync(cancellationToken);

    private async Task AddTelegramOutboxIfEnabledAsync(Guid propertyId, PropertyNotification notification, string message, CancellationToken cancellationToken)
    {
        if (await db.Set<NotificationTelegramOutbox>().AsNoTracking().AnyAsync(x => x.NotificationId == notification.Id, cancellationToken)) return;
        var settings = await db.Set<PropertyNotificationSettings>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.PropertyId == propertyId, cancellationToken);
        if (!IsTelegramConfigured(settings) || !TelegramEvents.Allows(settings?.TelegramEventTypes, notification.Type)) return;
        AddTelegramOutbox(propertyId, notification, settings!.TelegramChatIds!, message);
    }

    private async Task AddInternalBookingEmailIfEnabledAsync(
        Guid propertyId,
        PropertyNotification notification,
        BookingTelegramData booking,
        CancellationToken cancellationToken)
    {
        var settings = await db.Set<PropertyNotificationSettings>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.PropertyId == propertyId, cancellationToken);
        if (settings?.EmailBookingEnabled != true || string.IsNullOrWhiteSpace(settings.EmailRecipients)) return;

        var localTimes = GetLocalTimes(booking);
        var templateData = new BookingEmailTemplateData(
            booking.PropertyName,
            booking.Code,
            booking.CustomerName,
            booking.CustomerPhone,
            booking.CustomerEmail ?? string.Empty,
            booking.RoomName,
            localTimes.CheckIn,
            localTimes.CheckOut,
            booking.TotalAmount,
            BookingGuestGuideEmailService.HtmlToText(booking.GuestGuideHtml),
            string.Empty);
        var bodyHtml = NotificationEmailTemplateRenderer.RenderHtml(
            settings.InternalBookingEmailBodyTemplate,
            NotificationEmailTemplateRenderer.DefaultInternalBookingBody,
            templateData);
        db.Add(new NotificationEmailOutbox
        {
            PropertyId = propertyId,
            NotificationId = notification.Id,
            ToRecipients = settings.EmailRecipients,
            Subject = NotificationEmailTemplateRenderer.Render(
                settings.InternalBookingEmailSubjectTemplate,
                NotificationEmailTemplateRenderer.DefaultInternalBookingSubject,
                templateData),
            BodyHtml = bodyHtml,
            BodyText = NotificationEmailTemplateRenderer.ToPlainText(bodyHtml),
            NextAttemptAtUtc = DateTime.UtcNow
        });
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
        var lines = new List<string>
        {
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
        };
        var adjustment = ScheduleAdjustment(booking.Note);
        if (!string.IsNullOrWhiteSpace(adjustment)) lines.Insert(lines.Count - 2, $"Lưu ý: {adjustment}");
        return string.Join(Environment.NewLine, lines);
    }

    private static string ScheduleAdjustment(string? note) =>
        (note ?? string.Empty).Split('\n', StringSplitOptions.TrimEntries)
            .FirstOrDefault(x => x.StartsWith("Điều chỉnh thời gian", StringComparison.Ordinal)) ?? string.Empty;

    private async Task AddManagerCancellationEmailIfEnabledAsync(
        Guid propertyId,
        PropertyNotification notification,
        BookingTelegramData booking,
        CancellationNotice cancellation,
        CancellationToken cancellationToken)
    {
        var settings = await db.Set<PropertyNotificationSettings>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.PropertyId == propertyId, cancellationToken);
        if (settings is null || !settings.EmailBookingEnabled || !settings.ManagerCancellationEmailEnabled ||
            string.IsNullOrWhiteSpace(settings.EmailRecipients)) return;

        var local = GetLocalTimes(booking);
        var lines = new List<(string Label, string Value)>
        {
            ("Mã booking", booking.Code),
            ("Khách", $"{booking.CustomerName} · {booking.CustomerPhone}"),
            ("Phòng", booking.RoomName),
            ("Nhận phòng", local.CheckIn.ToString("dd/MM/yyyy HH:mm")),
            ("Trả phòng", local.CheckOut.ToString("dd/MM/yyyy HH:mm")),
            ("Người hủy", string.IsNullOrWhiteSpace(cancellation.ActorName) ? "Không rõ" : cancellation.ActorName!),
            ("Lý do hủy", string.IsNullOrWhiteSpace(cancellation.Reason) ? "Không có" : cancellation.Reason!),
            ("Tổng tiền booking", CancellationRecorder.Money(booking.TotalAmount)),
            ("Khách đã thanh toán", CancellationRecorder.Money(cancellation.PaidAmount)),
            ("Xử lý tiền", cancellation.RefundSummary)
        };
        var html = "<p><strong>" + System.Net.WebUtility.HtmlEncode(booking.PropertyName) + " · Booking bị hủy</strong></p><table cellpadding=\"6\" style=\"border-collapse:collapse\">" +
            string.Concat(lines.Select(x => "<tr><td style=\"color:#666\">" + System.Net.WebUtility.HtmlEncode(x.Label) + "</td><td><strong>" + System.Net.WebUtility.HtmlEncode(x.Value) + "</strong></td></tr>")) + "</table>";
        var text = $"{booking.PropertyName} · Booking bị hủy{Environment.NewLine}{Environment.NewLine}" + string.Join(Environment.NewLine, lines.Select(x => $"{x.Label}: {x.Value}"));
        var subject = $"[{booking.PropertyName}] Booking {booking.Code} bị hủy";
        db.Add(new NotificationEmailOutbox
        {
            PropertyId = propertyId,
            NotificationId = notification.Id,
            ToRecipients = settings.EmailRecipients,
            Subject = Truncate(subject.Replace('\r', ' ').Replace('\n', ' '), 300),
            BodyHtml = html,
            BodyText = text,
            NextAttemptAtUtc = DateTime.UtcNow
        });
    }

    private static string BuildStatusMessage(BookingTelegramData booking, BookingStatus status, string? reason, DateTime occurredAtUtc, CancellationNotice? cancellation = null)
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
            if (cancellation is not null)
            {
                lines.Add($"Người hủy: {(string.IsNullOrWhiteSpace(cancellation.ActorName) ? "Không rõ" : cancellation.ActorName)}");
                lines.Add($"Tổng tiền: {CancellationRecorder.Money(booking.TotalAmount)}");
                lines.Add($"Đã thanh toán: {CancellationRecorder.Money(cancellation.PaidAmount)}");
                lines.Add($"Xử lý tiền: {cancellation.RefundSummary}");
            }
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

    private static string BuildLatePay2SMessage(BookingTelegramData booking, decimal amount, string provider) =>
        string.Join(Environment.NewLine,
        [
            $"⚠️ {booking.PropertyName} · {provider} ĐẾN MUỘN", string.Empty,
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
        PaymentMethod.SePay => "SePay",
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
        string TimeZoneId,
        string? Note);
}
