using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Notifications;

public sealed class BookingNotificationService(
    AppDbContext db,
    NotificationRealtimeBroker realtimeBroker,
    ILogger<BookingNotificationService> logger)
{
    private const string BookingRequestedType = "booking-requested";
    private const string BookingEmailOnlyType = "booking-email-only";
    private const string Pay2SLatePaymentType = "pay2s-paid-after-expiry";
    private static readonly string[] InAppTypes = [BookingRequestedType, Pay2SLatePaymentType];

    public async Task NotifyLatePay2SPaymentAsync(Guid propertyId, Guid bookingId, decimal amount, CancellationToken cancellationToken = default)
    {
        try
        {
            if (await db.Set<PropertyNotification>().AsNoTracking().AnyAsync(
                    x => x.PropertyId == propertyId && x.BookingId == bookingId && x.Type == Pay2SLatePaymentType,
                    cancellationToken)) return;
            var booking = await db.Bookings.AsNoTracking()
                .Where(x => x.PropertyId == propertyId && x.Id == bookingId)
                .Select(x => new { x.Code, CustomerName = x.Customer.Name, RoomName = x.Room.Name })
                .SingleOrDefaultAsync(cancellationToken);
            if (booking is null) return;
            var notification = new PropertyNotification
            {
                PropertyId = propertyId,
                BookingId = bookingId,
                Type = Pay2SLatePaymentType,
                Title = $"Tiền Pay2S đến muộn · {booking.Code}",
                Message = $"{booking.CustomerName} · {booking.RoomName} · {amount:N0} đ · bắt buộc xử lý",
                ActionUrl = $"/Admin/Bookings?propertyId={propertyId}&bookingId={bookingId}&paymentIssue=late"
            };
            db.Add(notification);
            await db.SaveChangesAsync(cancellationToken);
            realtimeBroker.Publish(new NotificationRealtimeEvent(notification.Id, propertyId, notification.Type, notification.CreatedAtUtc));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not create late Pay2S notification for booking {BookingId}.", bookingId);
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
            var telegramEnabled = settings?.TelegramBookingEnabled ?? false;
            if (!inAppEnabled && !emailEnabled && !telegramEnabled) return;

            if (await db.Set<PropertyNotification>().AsNoTracking()
                .AnyAsync(x => x.PropertyId == propertyId && x.BookingId == bookingId &&
                    (x.Type == BookingRequestedType || x.Type == BookingEmailOnlyType), cancellationToken))
                return;

            var booking = await db.Bookings.AsNoTracking()
                .Where(x => x.PropertyId == propertyId && x.Id == bookingId)
                .Select(x => new
                {
                    x.Id,
                    x.Code,
                    x.CheckInUtc,
                    x.CheckOutUtc,
                    x.RoomAmount,
                    x.ExtraAmount,
                    x.DiscountAmount,
                    CustomerName = x.Customer.Name,
                    CustomerPhone = x.Customer.Phone,
                    CustomerEmail = x.Customer.Email,
                    RoomName = x.Room.Name,
                    GuestGuideHtml = x.Room.GuestGuideHtml,
                    PropertyName = x.Property.Name,
                    TimeZoneId = x.Property.TimeZoneId
                })
                .SingleOrDefaultAsync(cancellationToken);
            if (booking is null) return;

            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(booking.TimeZoneId);
            var checkInLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(booking.CheckInUtc, DateTimeKind.Utc), timeZone);
            var checkOutLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(booking.CheckOutUtc, DateTimeKind.Utc), timeZone);
            var total = booking.RoomAmount + booking.ExtraAmount - booking.DiscountAmount;
            var templateData = new BookingEmailTemplateData(
                booking.PropertyName,
                booking.Code,
                booking.CustomerName,
                booking.CustomerPhone,
                booking.CustomerEmail ?? string.Empty,
                booking.RoomName,
                checkInLocal,
                checkOutLocal,
                total,
                BookingGuestGuideEmailService.HtmlToText(booking.GuestGuideHtml),
                string.Empty);
            var notification = new PropertyNotification
            {
                PropertyId = propertyId,
                BookingId = booking.Id,
                Type = inAppEnabled ? BookingRequestedType : BookingEmailOnlyType,
                Title = $"Yêu cầu đặt phòng mới · {booking.Code}",
                Message = $"{booking.CustomerName} · {booking.RoomName} · {checkInLocal:dd/MM HH:mm}",
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

            if (telegramEnabled && !string.IsNullOrWhiteSpace(settings?.TelegramChatIds) &&
                !string.IsNullOrWhiteSpace(settings.TelegramBotTokenProtected))
            {
                db.Add(new NotificationTelegramOutbox
                {
                    PropertyId = propertyId,
                    NotificationId = notification.Id,
                    ChatIds = settings.TelegramChatIds,
                    MessageText = string.Join(Environment.NewLine,
                    [
                        $"🏡 {booking.PropertyName} · BOOKING MỚI",
                        $"Mã: {booking.Code}",
                        $"Khách: {booking.CustomerName}",
                        $"SĐT: {booking.CustomerPhone}",
                        $"Phòng: {booking.RoomName}",
                        $"Nhận: {checkInLocal:dd/MM/yyyy HH:mm}",
                        $"Trả: {checkOutLocal:dd/MM/yyyy HH:mm}",
                        $"Tổng tiền: {total:N0} VND",
                        "Vui lòng mở trang quản trị để xử lý."
                    ]),
                    NextAttemptAtUtc = DateTime.UtcNow
                });
            }

            await db.SaveChangesAsync(cancellationToken);
            if (inAppEnabled)
                realtimeBroker.Publish(new NotificationRealtimeEvent(notification.Id, propertyId, notification.Type, notification.CreatedAtUtc));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not create booking notification for booking {BookingId} in property {PropertyId}.", bookingId, propertyId);
            foreach (var entry in db.ChangeTracker.Entries().Where(x => x.Entity is PropertyNotification or NotificationEmailOutbox or NotificationTelegramOutbox && x.State == EntityState.Added))
                entry.State = EntityState.Detached;
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
}
