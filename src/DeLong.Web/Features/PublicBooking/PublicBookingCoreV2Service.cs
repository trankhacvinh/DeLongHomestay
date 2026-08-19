using System.Net.Mail;
using DeLong.Web.Common.Operations;
using DeLong.Web.Data;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Bookings;
using DeLong.Web.Features.Site;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.PublicBooking;

public sealed class PublicBookingCoreV2Service(
    AppDbContext db,
    PublicPropertyResolver propertyResolver,
    PublicBookingService publicBookingService,
    BookingService bookingService,
    StoragePaths storagePaths,
    IConfiguration configuration)
{
    public async Task<(PublicBookingResult? Result, PublicBookingError? Error)> CreateRequestAsync(
        string? siteSlug,
        PublicBookingRequest request,
        string? requestKey,
        CancellationToken cancellationToken = default)
    {
        var property = await propertyResolver.ResolveAsync(siteSlug, cancellationToken);
        if (property is null) return (null, new("property_not_found", "Không tìm thấy cơ sở."));

        var policyStore = new BookingPolicyStore(storagePaths, configuration);
        var holdStore = new PublicBookingHoldStore(storagePaths);
        await holdStore.ReleaseExpiredAsync(db, property.Id, cancellationToken);
        var policy = await policyStore.GetAsync(property.Id, cancellationToken);

        var validation = await ValidateAsync(property.Id, request, policy, cancellationToken);
        if (validation.Error is not null) return (null, validation.Error);

        var (result, error) = await publicBookingService.CreateRequestAsync(siteSlug, request, requestKey, cancellationToken);
        if (error is not null || result is null) return (result, error);

        var booking = await db.Bookings.Include(x => x.Customer)
            .SingleOrDefaultAsync(x => x.PropertyId == property.Id && x.Id == result.BookingId, cancellationToken);
        if (booking is null) return (null, new("booking_not_found", "Không tìm thấy lượt đặt vừa tạo."));

        booking.ExtraAmount = validation.Surcharge;
        booking.Note = BuildBookingNote(request, policy, validation.IncludedGuests, validation.Surcharge);
        booking.Customer.Email = request.CustomerEmail.Trim();
        await db.SaveChangesAsync(cancellationToken);
        await RefreshNotificationTotalAsync(property.Id, booking.Id, booking.TotalAmount, request.CustomerEmail, request.GuestCount, cancellationToken);

        DateTime? holdExpiresAtUtc = null;
        if (booking.Status == BookingStatus.Requested)
        {
            // Write the expiry marker before changing the booking to Held. If the process stops
            // between these operations, the booking is still Requested and the stale marker is
            // harmless. If it stops after the status change, the marker still exists so a later
            // availability request can release the hold instead of leaving it locked forever.
            holdExpiresAtUtc = await holdStore.StartAsync(
                property.Id,
                booking.Id,
                TimeSpan.FromMinutes(BookingPolicyStore.HoldMinutes),
                cancellationToken);

            var (_, holdError) = await bookingService.ChangeStatusAsync(property.Id, booking.Id, BookingStatus.Held, null, cancellationToken);
            if (holdError is not null)
            {
                await holdStore.CompleteAsync(property.Id, booking.Id);
                booking = await db.Bookings.SingleAsync(x => x.Id == booking.Id, cancellationToken);
                booking.Status = BookingStatus.Cancelled;
                booking.Note = AppendLine(booking.Note, "Tự hủy vì phòng vừa được giữ bởi một yêu cầu khác.");
                await db.SaveChangesAsync(cancellationToken);
                await RemoveNotificationAsync(property.Id, booking.Id, cancellationToken);
                return (null, new("booking_conflict", "Phòng vừa được khách khác giữ. Vui lòng chọn khung giờ hoặc phòng khác."));
            }
        }

        return (result with
        {
            TotalAmount = booking.TotalAmount,
            HoldExpiresAtUtc = holdExpiresAtUtc
        }, null);
    }

    public async Task ReleaseExpiredHoldsAsync(string? siteSlug, CancellationToken cancellationToken = default)
    {
        var property = await propertyResolver.ResolveAsync(siteSlug, cancellationToken);
        if (property is null) return;
        await new PublicBookingHoldStore(storagePaths).ReleaseExpiredAsync(db, property.Id, cancellationToken);
    }

    private async Task<(PublicBookingError? Error, decimal Surcharge, int IncludedGuests)> ValidateAsync(
        Guid propertyId,
        PublicBookingRequest request,
        BookingPolicyDto policy,
        CancellationToken cancellationToken)
    {
        if (!IsValidEmail(request.CustomerEmail))
            return (new("validation", "Vui lòng nhập email hợp lệ."), 0m, 0);
        if (!request.PolicyAccepted)
            return (new("policy_required", "Bạn cần đọc và đồng ý với Nội quy & Chính sách trước khi đặt phòng."), 0m, 0);
        if (request.PolicyVersion != policy.PolicyVersion)
            return (new("policy_changed", "Nội quy & Chính sách vừa được cập nhật. Vui lòng đọc lại trước khi tiếp tục."), 0m, 0);
        if (policy.RequireIdentityDocuments && (!request.HasIdentityFront || !request.HasIdentityBack))
            return (new("identity_required", "Cơ sở yêu cầu ảnh CCCD mặt trước và mặt sau cho lượt đặt này."), 0m, 0);
        if (policy.RequireIdentityDocuments && !policy.IdentityEncryptionConfigured)
            return (new("identity_storage_unavailable", "Hệ thống lưu CCCD bảo mật chưa sẵn sàng. Vui lòng liên hệ cơ sở."), 0m, 0);

        var room = await db.Rooms.AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.Id == request.RoomId && x.IsActive && x.IsPublished)
            .Select(x => new { x.Capacity })
            .SingleOrDefaultAsync(cancellationToken);
        if (room is null) return (new("room_not_found", "Phòng không còn mở đặt online."), 0m, 0);
        if (request.GuestCount < 1)
            return (new("validation", "Số lượng khách phải ít nhất là 1."), 0m, 0);
        if (request.GuestCount > room.Capacity)
            return (new("guest_limit", $"Phòng này tối đa {room.Capacity} khách."), 0m, 0);

        if (request.Type == BookingType.MultiDay)
        {
            if (!DateOnly.TryParse(request.CheckInDate, out var checkIn) || !DateOnly.TryParse(request.CheckOutDate, out var checkOut))
                return (new("validation", "Ngày nhận/trả phòng không hợp lệ."), 0m, 0);
            var nights = checkOut.DayNumber - checkIn.DayNumber;
            if (nights < 1 || nights > policy.PublicMaxNights)
                return (new("stay_too_long", $"Khách đặt online tối đa {policy.PublicMaxNights} đêm mỗi lượt."), 0m, 0);
        }

        var includedGuests = Math.Min(policy.IncludedGuests, room.Capacity);
        var extraGuests = Math.Max(0, request.GuestCount - includedGuests);
        var surcharge = extraGuests * policy.ExtraGuestFeePerPerson;
        return (null, surcharge, includedGuests);
    }

    private async Task RefreshNotificationTotalAsync(
        Guid propertyId,
        Guid bookingId,
        decimal totalAmount,
        string email,
        int guestCount,
        CancellationToken cancellationToken)
    {
        var notificationIds = await db.PropertyNotifications.AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.BookingId == bookingId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        if (notificationIds.Count == 0) return;

        var outboxRows = await db.NotificationEmailOutbox
            .Where(x => notificationIds.Contains(x.NotificationId) && x.SentAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var row in outboxRows)
        {
            var lines = row.BodyText.Replace("\r\n", "\n").Split('\n').ToList();
            ReplaceOrAppend(lines, "Tổng tiền:", $"Tổng tiền: {totalAmount:N0} VND");
            ReplaceOrAppend(lines, "Email khách:", $"Email khách: {email.Trim()}");
            ReplaceOrAppend(lines, "Số khách:", $"Số khách: {guestCount}");
            row.BodyText = string.Join(Environment.NewLine, lines);
        }
        if (outboxRows.Count > 0) await db.SaveChangesAsync(cancellationToken);
    }

    private async Task RemoveNotificationAsync(Guid propertyId, Guid bookingId, CancellationToken cancellationToken)
    {
        var notifications = await db.PropertyNotifications
            .Where(x => x.PropertyId == propertyId && x.BookingId == bookingId)
            .ToListAsync(cancellationToken);
        if (notifications.Count == 0) return;
        var ids = notifications.Select(x => x.Id).ToArray();
        var outbox = await db.NotificationEmailOutbox.Where(x => ids.Contains(x.NotificationId)).ToListAsync(cancellationToken);
        db.NotificationEmailOutbox.RemoveRange(outbox);
        db.PropertyNotifications.RemoveRange(notifications);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string BuildBookingNote(PublicBookingRequest request, BookingPolicyDto policy, int includedGuests, decimal surcharge)
    {
        var system = $"[Đặt web] {request.GuestCount} khách · giá gồm {includedGuests} khách";
        if (surcharge > 0) system += $" · phụ thu {surcharge:N0}đ";
        system += $" · đồng ý {policy.PolicyTitle} v{policy.PolicyVersion}";
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        return note is null ? system : system + Environment.NewLine + note;
    }

    private static bool IsValidEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 254) return false;
        try
        {
            var address = new MailAddress(value.Trim());
            return string.Equals(address.Address, value.Trim(), StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string AppendLine(string? current, string line) =>
        string.IsNullOrWhiteSpace(current) ? line : current.TrimEnd() + Environment.NewLine + line;

    private static void ReplaceOrAppend(List<string> lines, string prefix, string replacement)
    {
        var index = lines.FindIndex(x => x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        if (index >= 0) lines[index] = replacement;
        else lines.Add(replacement);
    }
}
