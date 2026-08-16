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

    public async Task NotifyBookingCreatedAsync(Guid propertyId, Guid bookingId, CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = await db.Set<PropertyNotificationSettings>().AsNoTracking()
                .SingleOrDefaultAsync(x => x.PropertyId == propertyId, cancellationToken);
            var inAppEnabled = settings?.InAppBookingEnabled ?? true;
            var emailEnabled = settings?.EmailBookingEnabled ?? false;
            if (!inAppEnabled && !emailEnabled) return;

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
                    RoomName = x.Room.Name,
                    PropertyName = x.Property.Name,
                    TimeZoneId = x.Property.TimeZoneId
                })
                .SingleOrDefaultAsync(cancellationToken);
            if (booking is null) return;

            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(booking.TimeZoneId);
            var checkInLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(booking.CheckInUtc, DateTimeKind.Utc), timeZone);
            var checkOutLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(booking.CheckOutUtc, DateTimeKind.Utc), timeZone);
            var total = booking.RoomAmount + booking.ExtraAmount - booking.DiscountAmount;
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
                    Subject = $"[{booking.PropertyName}] Yêu cầu đặt phòng mới {booking.Code}",
                    BodyText = string.Join(Environment.NewLine,
                    [
                        $"Cơ sở: {booking.PropertyName}",
                        $"Mã booking: {booking.Code}",
                        $"Khách: {booking.CustomerName}",
                        $"Điện thoại: {booking.CustomerPhone}",
                        $"Phòng: {booking.RoomName}",
                        $"Nhận: {checkInLocal:dd/MM/yyyy HH:mm}",
                        $"Trả: {checkOutLocal:dd/MM/yyyy HH:mm}",
                        $"Tổng tiền: {total:N0} VND",
                        string.Empty,
                        "Vui lòng mở trang quản trị De Long Homestay để xử lý yêu cầu."
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
            foreach (var entry in db.ChangeTracker.Entries().Where(x => x.Entity is PropertyNotification or NotificationEmailOutbox && x.State == EntityState.Added))
                entry.State = EntityState.Detached;
        }
    }

    public async Task<NotificationFeedDto> GetFeedAsync(Guid propertyId, Guid userId, int take = 20, CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 50);
        var items = await db.Set<PropertyNotification>().AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.Type == BookingRequestedType)
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
            .CountAsync(x => x.PropertyId == propertyId && x.Type == BookingRequestedType && !x.Reads.Any(r => r.UserId == userId), cancellationToken);
        return new NotificationFeedDto(items, unreadCount);
    }

    public async Task<bool> MarkReadAsync(Guid propertyId, Guid notificationId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (!await db.Set<PropertyNotification>().AsNoTracking()
            .AnyAsync(x => x.Id == notificationId && x.PropertyId == propertyId && x.Type == BookingRequestedType, cancellationToken)) return false;
        if (await db.Set<PropertyNotificationRead>().AnyAsync(x => x.NotificationId == notificationId && x.UserId == userId, cancellationToken)) return true;
        db.Add(new PropertyNotificationRead { NotificationId = notificationId, UserId = userId, ReadAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> MarkAllReadAsync(Guid propertyId, Guid userId, CancellationToken cancellationToken = default)
    {
        var unreadIds = await db.Set<PropertyNotification>().AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.Type == BookingRequestedType && !x.Reads.Any(r => r.UserId == userId))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        if (unreadIds.Count == 0) return 0;
        db.AddRange(unreadIds.Select(id => new PropertyNotificationRead { NotificationId = id, UserId = userId, ReadAtUtc = DateTime.UtcNow }));
        await db.SaveChangesAsync(cancellationToken);
        return unreadIds.Count;
    }
}
