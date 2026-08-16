using System.Net.Mail;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Notifications;

public sealed class NotificationSettingsService(AppDbContext db, SmtpCredentialProtector credentialProtector)
{
    public async Task<NotificationSettingsDto> GetAsync(Guid propertyId, CancellationToken cancellationToken = default)
    {
        var settings = await db.Set<PropertyNotificationSettings>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.PropertyId == propertyId, cancellationToken);
        return ToDto(settings);
    }

    public async Task<(NotificationSettingsDto? Settings, NotificationOperationError? Error)> SaveAsync(
        Guid propertyId,
        UpdateNotificationSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await db.Properties.AnyAsync(x => x.Id == propertyId, cancellationToken))
            return (null, new("property_not_found", "Không tìm thấy cơ sở."));

        var recipientsResult = NormalizeRecipients(request.EmailRecipients);
        if (recipientsResult.Error is not null) return (null, recipientsResult.Error);
        var host = Clean(request.SmtpHost);
        var username = Clean(request.SmtpUsername);
        var fromEmail = Clean(request.SmtpFromEmail);
        var fromName = Clean(request.SmtpFromName) ?? "De Long Homestay";

        if (request.SmtpPort is < 1 or > 65535)
            return (null, new("smtp_port_invalid", "Cổng SMTP phải nằm trong khoảng 1–65535."));
        if (fromEmail is not null && !IsEmail(fromEmail))
            return (null, new("smtp_from_invalid", "Email người gửi không hợp lệ."));

        var settings = await db.Set<PropertyNotificationSettings>()
            .SingleOrDefaultAsync(x => x.PropertyId == propertyId, cancellationToken);
        if (settings is null)
        {
            settings = new PropertyNotificationSettings { PropertyId = propertyId };
            db.Add(settings);
        }

        if (request.ClearSmtpPassword) settings.SmtpPasswordProtected = null;
        if (!string.IsNullOrWhiteSpace(request.SmtpPassword))
            settings.SmtpPasswordProtected = credentialProtector.Protect(request.SmtpPassword);

        if (request.EmailBookingEnabled)
        {
            if (string.IsNullOrWhiteSpace(recipientsResult.Normalized))
                return (null, new("email_recipients_required", "Vui lòng nhập ít nhất một email nhận thông báo."));
            if (string.IsNullOrWhiteSpace(host))
                return (null, new("smtp_host_required", "Vui lòng nhập máy chủ SMTP."));
            if (string.IsNullOrWhiteSpace(fromEmail))
                return (null, new("smtp_from_required", "Vui lòng nhập email người gửi."));
            if (!string.IsNullOrWhiteSpace(username) && string.IsNullOrWhiteSpace(settings.SmtpPasswordProtected))
                return (null, new("smtp_password_required", "SMTP username đã được nhập nhưng chưa có password."));
        }

        settings.InAppBookingEnabled = request.InAppBookingEnabled;
        settings.EmailBookingEnabled = request.EmailBookingEnabled;
        settings.EmailRecipients = recipientsResult.Normalized;
        settings.SmtpHost = host;
        settings.SmtpPort = request.SmtpPort;
        settings.SmtpUseSsl = request.SmtpUseSsl;
        settings.SmtpUsername = username;
        settings.SmtpFromEmail = fromEmail;
        settings.SmtpFromName = fromName;
        await db.SaveChangesAsync(cancellationToken);
        return (ToDto(settings), null);
    }

    public async Task<(SmtpDeliveryProfile? Profile, IReadOnlyList<string> Recipients, NotificationOperationError? Error)> GetDeliveryProfileAsync(
        Guid propertyId,
        bool requireEmailEnabled,
        CancellationToken cancellationToken = default)
    {
        var settings = await db.Set<PropertyNotificationSettings>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.PropertyId == propertyId, cancellationToken);
        if (settings is null)
            return (null, [], new("smtp_not_configured", "Cơ sở chưa cấu hình SMTP."));
        if (requireEmailEnabled && !settings.EmailBookingEnabled)
            return (null, [], new("email_disabled", "Gửi email thông báo đang tắt."));

        var recipientsResult = NormalizeRecipients(settings.EmailRecipients);
        if (recipientsResult.Error is not null || string.IsNullOrWhiteSpace(recipientsResult.Normalized))
            return (null, [], new("email_recipients_required", "Danh sách email nhận thông báo chưa hợp lệ."));
        if (string.IsNullOrWhiteSpace(settings.SmtpHost) || string.IsNullOrWhiteSpace(settings.SmtpFromEmail))
            return (null, [], new("smtp_not_configured", "SMTP host hoặc email người gửi chưa được cấu hình."));

        string? password = null;
        if (!string.IsNullOrWhiteSpace(settings.SmtpPasswordProtected))
        {
            if (!credentialProtector.TryUnprotect(settings.SmtpPasswordProtected, out password))
                return (null, [], new("smtp_password_unreadable", "Không thể giải mã SMTP password. Hãy nhập lại password và lưu cấu hình."));
        }
        if (!string.IsNullOrWhiteSpace(settings.SmtpUsername) && string.IsNullOrWhiteSpace(password))
            return (null, [], new("smtp_password_required", "SMTP username đã được cấu hình nhưng password đang thiếu."));

        var recipients = recipientsResult.Normalized.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return (new SmtpDeliveryProfile(
                settings.SmtpHost,
                settings.SmtpPort,
                settings.SmtpUseSsl,
                settings.SmtpUsername,
                password,
                settings.SmtpFromEmail,
                string.IsNullOrWhiteSpace(settings.SmtpFromName) ? "De Long Homestay" : settings.SmtpFromName),
            recipients,
            null);
    }

    private static NotificationSettingsDto ToDto(PropertyNotificationSettings? settings) => new(
        settings?.InAppBookingEnabled ?? true,
        settings?.EmailBookingEnabled ?? false,
        settings?.EmailRecipients ?? string.Empty,
        settings?.SmtpHost ?? string.Empty,
        settings?.SmtpPort ?? 587,
        settings?.SmtpUseSsl ?? true,
        settings?.SmtpUsername ?? string.Empty,
        !string.IsNullOrWhiteSpace(settings?.SmtpPasswordProtected),
        settings?.SmtpFromEmail ?? string.Empty,
        settings?.SmtpFromName ?? "De Long Homestay",
        settings?.LastEmailSentAtUtc,
        settings?.LastEmailError,
        settings?.LastEmailErrorAtUtc);

    private static (string? Normalized, NotificationOperationError? Error) NormalizeRecipients(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return (null, null);
        var values = raw.Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var normalized = new List<string>();
        foreach (var value in values)
        {
            if (!IsEmail(value)) return (null, new("email_recipient_invalid", $"Email nhận thông báo không hợp lệ: {value}"));
            var address = new MailAddress(value).Address;
            if (!normalized.Contains(address, StringComparer.OrdinalIgnoreCase)) normalized.Add(address);
        }
        return (string.Join(';', normalized), null);
    }

    private static bool IsEmail(string value)
    {
        try { return string.Equals(new MailAddress(value).Address, value.Trim(), StringComparison.OrdinalIgnoreCase); }
        catch { return false; }
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
