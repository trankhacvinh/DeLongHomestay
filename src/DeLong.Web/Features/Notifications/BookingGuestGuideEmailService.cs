using System.Net;
using System.Text.RegularExpressions;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Notifications;

public sealed partial class BookingGuestGuideEmailService(
    AppDbContext db,
    ILogger<BookingGuestGuideEmailService> logger)
{
    private const int MaximumAttempts = 8;

    public async Task QueueAutomaticAsync(Guid propertyId, Guid bookingId, CancellationToken cancellationToken = default)
    {
        try
        {
            var enabled = await db.PropertyNotificationSettings.AsNoTracking()
                .Where(x => x.PropertyId == propertyId)
                .Select(x => (bool?)x.GuestCheckInEmailEnabled)
                .SingleOrDefaultAsync(cancellationToken) ?? false;
            if (!enabled) return;
            if (await db.BookingGuestGuideEmails.AsNoTracking().AnyAsync(x => x.PropertyId == propertyId && x.BookingId == bookingId && x.TemplateKey == "CheckInGuide", cancellationToken)) return;
            await QueueCoreAsync(propertyId, bookingId, "CheckInGuide", "AutomaticConfirmation", null, null, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not queue automatic guest guide email for booking {BookingId}.", bookingId);
        }
    }

    public async Task<(GuestGuideEmailStatusDto? Status, NotificationOperationError? Error)> ResendAsync(
        Guid propertyId,
        Guid bookingId,
        Guid? requestedByUserId,
        CancellationToken cancellationToken = default)
    {
        var error = await QueueCoreAsync(propertyId, bookingId, "CheckInGuide", "ManualResend", requestedByUserId, null, cancellationToken);
        return error is null
            ? (await GetStatusAsync(propertyId, bookingId, cancellationToken), null)
            : (null, error);
    }

    public async Task<GuestGuideEmailStatusDto> GetStatusAsync(
        Guid propertyId,
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        var booking = await db.Bookings.AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.Id == bookingId)
            .Select(x => new { x.Customer.Email, x.Status })
            .SingleOrDefaultAsync(cancellationToken);
        if (booking is null) return new("NotFound", null, null, null, 0, null, false, "Không tìm thấy booking.");
        if (string.IsNullOrWhiteSpace(booking.Email)) return new("NoEmail", null, null, null, 0, null, false, "Khách chưa cung cấp email.");

        var latest = await db.BookingGuestGuideEmails.AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.BookingId == bookingId)
            .Where(x => x.TemplateKey == "CheckInGuide")
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new { x.RecipientEmail, x.CreatedAtUtc, x.SentAtUtc, x.AttemptCount, x.LastError })
            .FirstOrDefaultAsync(cancellationToken);
        if (latest is null)
        {
            var confirmed = booking.Status is BookingStatus.Confirmed or BookingStatus.CheckedIn;
            return new("NotSent", booking.Email, null, null, 0, null, true,
                confirmed ? "Chưa gửi hướng dẫn check-in." : "Booking chưa xác nhận; có thể gửi thủ công nếu cần.");
        }
        if (latest.SentAtUtc.HasValue)
            return new("Sent", latest.RecipientEmail, latest.CreatedAtUtc, latest.SentAtUtc, latest.AttemptCount, null, true, "Đã gửi hướng dẫn check-in.");
        if (latest.AttemptCount >= MaximumAttempts)
            return new("Failed", latest.RecipientEmail, latest.CreatedAtUtc, null, latest.AttemptCount, latest.LastError, true, "Gửi thất bại sau nhiều lần thử.");
        return new("Queued", latest.RecipientEmail, latest.CreatedAtUtc, null, latest.AttemptCount, latest.LastError, true,
            latest.AttemptCount > 0 ? "Đang chờ thử gửi lại." : "Đã xếp hàng chờ gửi.");
    }

    public async Task QueueCancellationAsync(
        Guid propertyId,
        Guid bookingId,
        Guid? requestedByUserId,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var enabled = await db.PropertyNotificationSettings.AsNoTracking()
                .Where(x => x.PropertyId == propertyId)
                .Select(x => (bool?)x.GuestCancellationEmailEnabled)
                .SingleOrDefaultAsync(cancellationToken) ?? true;
            if (!enabled) return;
            await QueueCoreAsync(propertyId, bookingId, "Cancellation", "BookingCancelled", requestedByUserId, reason, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not queue cancellation email for booking {BookingId}.", bookingId);
        }
    }

    private async Task<NotificationOperationError?> QueueCoreAsync(
        Guid propertyId,
        Guid bookingId,
        string templateKey,
        string trigger,
        Guid? requestedByUserId,
        string? cancellationReason,
        CancellationToken cancellationToken)
    {
        var booking = await db.Bookings.AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.Id == bookingId)
            .Select(x => new
            {
                x.Code,
                x.CheckInUtc,
                x.CheckOutUtc,
                x.RoomAmount,
                x.SpecialSurchargeAmount,
                x.ExtraAmount,
                x.DiscountAmount,
                CustomerName = x.Customer.Name,
                CustomerPhone = x.Customer.Phone,
                CustomerEmail = x.Customer.Email,
                RoomName = x.Room.Name,
                GuestGuideHtml = x.Room.GuestGuideHtml,
                PropertyName = x.Property.Name,
                x.Property.TimeZoneId
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (booking is null) return new("booking_not_found", "Không tìm thấy booking.");
        if (string.IsNullOrWhiteSpace(booking.CustomerEmail)) return new("customer_email_missing", "Khách chưa cung cấp email nên không thể gửi hướng dẫn check-in.");

        var tz = TimeZoneInfo.FindSystemTimeZoneById(booking.TimeZoneId);
        var checkIn = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(booking.CheckInUtc, DateTimeKind.Utc), tz);
        var checkOut = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(booking.CheckOutUtc, DateTimeKind.Utc), tz);
        var guide = HtmlToText(booking.GuestGuideHtml);
        if (string.IsNullOrWhiteSpace(guide)) guide = "Phòng chưa có nội dung hướng dẫn riêng. Vui lòng liên hệ cơ sở để được hỗ trợ nhận phòng.";
        var settings = await db.PropertyNotificationSettings.AsNoTracking()
            .SingleOrDefaultAsync(x => x.PropertyId == propertyId, cancellationToken);
        var data = new BookingEmailTemplateData(
            booking.PropertyName,
            booking.Code,
            booking.CustomerName,
            booking.CustomerPhone,
            booking.CustomerEmail,
            booking.RoomName,
            checkIn,
            checkOut,
            booking.RoomAmount + booking.SpecialSurchargeAmount + booking.ExtraAmount - booking.DiscountAmount,
            guide,
            string.IsNullOrWhiteSpace(cancellationReason) ? "Booking đã được hủy trên hệ thống." : cancellationReason.Trim());
        var cancellation = string.Equals(templateKey, "Cancellation", StringComparison.Ordinal);
        var subject = NotificationEmailTemplateRenderer.Render(
            cancellation ? settings?.GuestCancellationEmailSubjectTemplate : settings?.GuestCheckInEmailSubjectTemplate,
            cancellation ? NotificationEmailTemplateRenderer.DefaultGuestCancellationSubject : NotificationEmailTemplateRenderer.DefaultGuestCheckInSubject,
            data);
        var bodyHtml = NotificationEmailTemplateRenderer.RenderHtml(
            cancellation ? settings?.GuestCancellationEmailBodyTemplate : settings?.GuestCheckInEmailBodyTemplate,
            cancellation ? NotificationEmailTemplateRenderer.DefaultGuestCancellationBody : NotificationEmailTemplateRenderer.DefaultGuestCheckInBody,
            data);

        db.Add(new BookingGuestGuideEmail
        {
            PropertyId = propertyId,
            BookingId = bookingId,
            RecipientEmail = booking.CustomerEmail.Trim(),
            Trigger = trigger,
            TemplateKey = templateKey,
            RequestedByUserId = requestedByUserId,
            Subject = subject,
            BodyHtml = bodyHtml,
            BodyText = NotificationEmailTemplateRenderer.ToPlainText(bodyHtml),
            NextAttemptAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
        return null;
    }

    internal static string HtmlToText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var value = BreakRegex().Replace(html, Environment.NewLine);
        value = BlockRegex().Replace(value, Environment.NewLine);
        value = TagRegex().Replace(value, string.Empty);
        value = WebUtility.HtmlDecode(value).Replace('\u00A0', ' ');
        return string.Join(Environment.NewLine, value.Split(['\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
    }

    [GeneratedRegex("<br\\s*/?>", RegexOptions.IgnoreCase)] private static partial Regex BreakRegex();
    [GeneratedRegex("</(?:p|div|li|h[1-6]|blockquote)>", RegexOptions.IgnoreCase)] private static partial Regex BlockRegex();
    [GeneratedRegex("<[^>]+>")] private static partial Regex TagRegex();
}
