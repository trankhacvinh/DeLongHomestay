using System.Net.Mail;
using DeLong.Web.Common.Operations;
using DeLong.Web.Data;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Bookings;
using DeLong.Web.Features.Customers;
using DeLong.Web.Features.Operations;
using DeLong.Web.Features.Site;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.PublicBooking;

public sealed class PublicBookingCoreV2Service(
    AppDbContext db,
    PublicPropertyResolver propertyResolver,
    PublicBookingService publicBookingService,
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
        var guestDetailsStore = new BookingGuestDetailsStore(storagePaths);
        var policy = await policyStore.GetAsync(property.Id, cancellationToken);

        var validation = await ValidateAsync(property.Id, request, policy, cancellationToken);
        if (validation.Error is not null) return (null, validation.Error);

        var (result, error) = await publicBookingService.CreateRequestAsync(siteSlug, request, requestKey, policy, cancellationToken);
        if (error is not null || result is null) return (result, error);

        var booking = await db.Bookings.Include(x => x.Customer)
            .SingleOrDefaultAsync(x => x.PropertyId == property.Id && x.Id == result.BookingId, cancellationToken);
        if (booking is null) return (null, new("booking_not_found", "Không tìm thấy lượt đặt vừa tạo."));

        booking.ExtraAmount = validation.Surcharge;
        booking.Note = CleanNote(request.Note);
        booking.Customer.Email = request.CustomerEmail.Trim();
        await db.SaveChangesAsync(cancellationToken);
        await guestDetailsStore.SaveAsync(
            property.Id,
            booking.Id,
            new BookingGuestDetailsDto(
                request.GuestCount,
                true,
                policy.PolicyVersion,
                DateTime.UtcNow),
            cancellationToken);
        await RefreshNotificationTotalAsync(property.Id, booking.Id, booking.TotalAmount, request.CustomerEmail, request.GuestCount, cancellationToken);

        OperationsRealtimeBroker.Shared.Publish(OperationsRealtimeEvent.Create(
            property.Id,
            OperationsEventTypes.BookingCreated,
            booking.Id,
            booking.RoomId));

        return (result with { TotalAmount = booking.TotalAmount }, null);
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
        var normalizedPhone = CustomerService.NormalizePhone(request.CustomerPhone);
        var cleanEmail = request.CustomerEmail.Trim();
        if (await db.Customers.AsNoTracking().AnyAsync(x =>
                x.PropertyId == propertyId && x.IsBlocked &&
                (x.NormalizedPhone == normalizedPhone ||
                 (x.Email != null && EF.Functions.ILike(x.Email, cleanEmail))),
                cancellationToken))
            return (new("customer_blocked", "Thông tin liên hệ này không thể gửi yêu cầu đặt phòng. Vui lòng liên hệ trực tiếp cơ sở."), 0m, 0);
        if (!request.PolicyAccepted)
            return (new("policy_required", "Bạn cần đọc và đồng ý với Nội quy & Chính sách trước khi đặt phòng."), 0m, 0);
        if (request.PolicyVersion != policy.PolicyVersion)
            return (new("policy_changed", "Nội quy & Chính sách vừa được cập nhật. Vui lòng đọc lại trước khi tiếp tục."), 0m, 0);
        if (!request.HasIdentityFront || !request.HasIdentityBack)
            return (new("identity_required", "Khách đặt online phải cung cấp ảnh CCCD mặt trước và mặt sau."), 0m, 0);
        if (request.GuestCount >= 3 && (!request.HasSecondIdentityFront || !request.HasSecondIdentityBack))
            return (new("second_identity_required", "Từ 3 khách trở lên cần thêm CCCD mặt trước và mặt sau của người thứ hai."), 0m, 0);
        if (!policy.IdentityEncryptionConfigured)
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

    private static string? CleanNote(string? note) =>
        string.IsNullOrWhiteSpace(note) ? null : note.Trim();

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
