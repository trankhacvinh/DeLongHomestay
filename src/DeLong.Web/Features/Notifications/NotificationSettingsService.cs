using System.Net.Mail;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using DeLong.Web.Features.Vouchers;

namespace DeLong.Web.Features.Notifications;

public sealed partial class NotificationSettingsService(
    AppDbContext db,
    SmtpCredentialProtector credentialProtector,
    TelegramCredentialProtector telegramCredentialProtector)
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
        var telegramChatIds = NormalizeTelegramChatIds(request.TelegramChatIds);
        if (telegramChatIds.Error is not null) return (null, telegramChatIds.Error);
        var host = Clean(request.SmtpHost);
        var username = Clean(request.SmtpUsername);
        var fromEmail = Clean(request.SmtpFromEmail);
        var fromName = Clean(request.SmtpFromName) ?? "De Long Homestay";
        var templateError = ValidateTemplates(request);
        if (templateError is not null) return (null, templateError);

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
        if (request.ClearTelegramBotToken) settings.TelegramBotTokenProtected = null;
        if (!string.IsNullOrWhiteSpace(request.TelegramBotToken))
        {
            if (!TelegramTokenRegex().IsMatch(request.TelegramBotToken.Trim()))
                return (null, new("telegram_token_invalid", "Bot token Telegram không đúng định dạng BotFather."));
            settings.TelegramBotTokenProtected = telegramCredentialProtector.Protect(request.TelegramBotToken);
        }

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
        if (request.GuestCheckInEmailEnabled || request.GuestCancellationEmailEnabled)
        {
            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(fromEmail))
                return (null, new("guest_email_smtp_required", "Muốn gửi hướng dẫn check-in tự động, vui lòng cấu hình SMTP host và email người gửi."));
            if (!string.IsNullOrWhiteSpace(username) && string.IsNullOrWhiteSpace(settings.SmtpPasswordProtected))
                return (null, new("smtp_password_required", "SMTP username đã được nhập nhưng chưa có password."));
        }
        if (request.TelegramBookingEnabled)
        {
            if (string.IsNullOrWhiteSpace(settings.TelegramBotTokenProtected))
                return (null, new("telegram_token_required", "Vui lòng nhập bot token Telegram."));
            if (string.IsNullOrWhiteSpace(telegramChatIds.Normalized))
                return (null, new("telegram_chat_required", "Vui lòng nhập ít nhất một chat ID hoặc @username Telegram."));
        }

        settings.InAppBookingEnabled = request.InAppBookingEnabled;
        settings.EmailBookingEnabled = request.EmailBookingEnabled;
        settings.GuestCheckInEmailEnabled = request.GuestCheckInEmailEnabled;
        settings.GuestCancellationEmailEnabled = request.GuestCancellationEmailEnabled;
        settings.InternalBookingEmailSubjectTemplate = Clean(request.InternalBookingEmailSubjectTemplate);
        settings.InternalBookingEmailBodyTemplate = Clean(NotificationEmailTemplateRenderer.SanitizeTemplate(request.InternalBookingEmailBodyTemplate));
        settings.GuestCheckInEmailSubjectTemplate = Clean(request.GuestCheckInEmailSubjectTemplate);
        settings.GuestCheckInEmailBodyTemplate = Clean(NotificationEmailTemplateRenderer.SanitizeTemplate(request.GuestCheckInEmailBodyTemplate));
        settings.GuestCancellationEmailSubjectTemplate = Clean(request.GuestCancellationEmailSubjectTemplate);
        settings.GuestCancellationEmailBodyTemplate = Clean(NotificationEmailTemplateRenderer.SanitizeTemplate(request.GuestCancellationEmailBodyTemplate));
        settings.VoucherEmailSubjectTemplate = Clean(request.VoucherEmailSubjectTemplate);
        settings.VoucherEmailBodyTemplate = Clean(NotificationEmailTemplateRenderer.SanitizeTemplate(request.VoucherEmailBodyTemplate));
        settings.EmailRecipients = recipientsResult.Normalized;
        settings.SmtpHost = host;
        settings.SmtpPort = request.SmtpPort;
        settings.SmtpUseSsl = request.SmtpUseSsl;
        settings.SmtpUsername = username;
        settings.SmtpFromEmail = fromEmail;
        settings.SmtpFromName = fromName;
        settings.TelegramBookingEnabled = request.TelegramBookingEnabled;
        settings.TelegramChatIds = telegramChatIds.Normalized;
        await db.SaveChangesAsync(cancellationToken);
        return (ToDto(settings), null);
    }

    public async Task<(SmtpDeliveryProfile? Profile, NotificationOperationError? Error)> GetSmtpProfileAsync(
        Guid propertyId,
        CancellationToken cancellationToken = default)
    {
        var settings = await db.Set<PropertyNotificationSettings>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.PropertyId == propertyId, cancellationToken);
        if (settings is null || string.IsNullOrWhiteSpace(settings.SmtpHost) || string.IsNullOrWhiteSpace(settings.SmtpFromEmail))
            return (null, new("smtp_not_configured", "SMTP host hoặc email người gửi chưa được cấu hình."));

        string? password = null;
        if (!string.IsNullOrWhiteSpace(settings.SmtpPasswordProtected) &&
            !credentialProtector.TryUnprotect(settings.SmtpPasswordProtected, out password))
            return (null, new("smtp_password_unreadable", "Không thể giải mã SMTP password. Hãy nhập lại password và lưu cấu hình."));
        if (!string.IsNullOrWhiteSpace(settings.SmtpUsername) && string.IsNullOrWhiteSpace(password))
            return (null, new("smtp_password_required", "SMTP username đã được cấu hình nhưng password đang thiếu."));

        return (new SmtpDeliveryProfile(
            settings.SmtpHost,
            settings.SmtpPort,
            settings.SmtpUseSsl,
            settings.SmtpUsername,
            password,
            settings.SmtpFromEmail,
            string.IsNullOrWhiteSpace(settings.SmtpFromName) ? "De Long Homestay" : settings.SmtpFromName), null);
    }

    public async Task<(TelegramDeliveryProfile? Profile, NotificationOperationError? Error)> GetTelegramProfileAsync(
        Guid propertyId,
        bool requireEnabled,
        CancellationToken cancellationToken = default)
    {
        var settings = await db.Set<PropertyNotificationSettings>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.PropertyId == propertyId, cancellationToken);
        if (settings is null) return (null, new("telegram_not_configured", "Cơ sở chưa cấu hình Telegram."));
        if (requireEnabled && !settings.TelegramBookingEnabled)
            return (null, new("telegram_disabled", "Thông báo Telegram đang tắt."));
        if (!telegramCredentialProtector.TryUnprotect(settings.TelegramBotTokenProtected, out var token) || string.IsNullOrWhiteSpace(token))
            return (null, new("telegram_token_unreadable", "Không thể đọc bot token Telegram. Hãy nhập lại token và lưu cấu hình."));
        var chatIds = NormalizeTelegramChatIds(settings.TelegramChatIds);
        if (chatIds.Error is not null || string.IsNullOrWhiteSpace(chatIds.Normalized))
            return (null, new("telegram_chat_required", "Danh sách chat ID Telegram chưa hợp lệ."));
        return (new TelegramDeliveryProfile(token, chatIds.Normalized.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)), null);
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

        var recipients = recipientsResult.Normalized.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var (profile, profileError) = await GetSmtpProfileAsync(propertyId, cancellationToken);
        return (profile, recipients, profileError);
    }

    private static NotificationSettingsDto ToDto(PropertyNotificationSettings? settings) => new(
        settings?.InAppBookingEnabled ?? true,
        settings?.EmailBookingEnabled ?? false,
        settings?.GuestCheckInEmailEnabled ?? false,
        settings?.GuestCancellationEmailEnabled ?? true,
        settings?.InternalBookingEmailSubjectTemplate ?? NotificationEmailTemplateRenderer.DefaultInternalBookingSubject,
        settings?.InternalBookingEmailBodyTemplate ?? NotificationEmailTemplateRenderer.DefaultInternalBookingBody,
        settings?.GuestCheckInEmailSubjectTemplate ?? NotificationEmailTemplateRenderer.DefaultGuestCheckInSubject,
        settings?.GuestCheckInEmailBodyTemplate ?? NotificationEmailTemplateRenderer.DefaultGuestCheckInBody,
        settings?.GuestCancellationEmailSubjectTemplate ?? NotificationEmailTemplateRenderer.DefaultGuestCancellationSubject,
        settings?.GuestCancellationEmailBodyTemplate ?? NotificationEmailTemplateRenderer.DefaultGuestCancellationBody,
        settings?.VoucherEmailSubjectTemplate ?? VoucherEmailTemplateRenderer.DefaultSubject,
        settings?.VoucherEmailBodyTemplate ?? VoucherEmailTemplateRenderer.DefaultBody,
        settings?.EmailRecipients ?? string.Empty,
        settings?.SmtpHost ?? string.Empty,
        settings?.SmtpPort ?? 587,
        settings?.SmtpUseSsl ?? true,
        settings?.SmtpUsername ?? string.Empty,
        !string.IsNullOrWhiteSpace(settings?.SmtpPasswordProtected),
        settings?.SmtpFromEmail ?? string.Empty,
        settings?.SmtpFromName ?? "De Long Homestay",
        settings?.TelegramBookingEnabled ?? false,
        !string.IsNullOrWhiteSpace(settings?.TelegramBotTokenProtected),
        settings?.TelegramChatIds ?? string.Empty,
        settings?.LastEmailSentAtUtc,
        settings?.LastEmailError,
        settings?.LastEmailErrorAtUtc,
        settings?.LastTelegramSentAtUtc,
        settings?.LastTelegramError,
        settings?.LastTelegramErrorAtUtc);

    private static NotificationOperationError? ValidateTemplates(UpdateNotificationSettingsRequest request)
    {
        var subjects = new[]
        {
            request.InternalBookingEmailSubjectTemplate,
            request.GuestCheckInEmailSubjectTemplate,
            request.GuestCancellationEmailSubjectTemplate,
            request.VoucherEmailSubjectTemplate
        };
        if (subjects.Any(x => x?.Length > 300)) return new("email_subject_too_long", "Tiêu đề mẫu email tối đa 300 ký tự.");
        if (subjects.Any(x => x?.IndexOfAny(['\r', '\n']) >= 0)) return new("email_subject_invalid", "Tiêu đề email không được chứa ký tự xuống dòng.");
        var bodies = new[]
        {
            request.InternalBookingEmailBodyTemplate,
            request.GuestCheckInEmailBodyTemplate,
            request.GuestCancellationEmailBodyTemplate,
            request.VoucherEmailBodyTemplate
        };
        return bodies.Any(x => x?.Length > 20000)
            ? new("email_body_too_long", "Nội dung mỗi mẫu email tối đa 20.000 ký tự.")
            : null;
    }

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

    private static (string? Normalized, NotificationOperationError? Error) NormalizeTelegramChatIds(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return (null, null);
        var values = raw.Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var normalized = new List<string>();
        foreach (var value in values)
        {
            var valid = value.StartsWith('@')
                ? value.Length is >= 5 and <= 64 && value[1..].All(ch => char.IsLetterOrDigit(ch) || ch == '_')
                : long.TryParse(value, out _);
            if (!valid) return (null, new("telegram_chat_invalid", $"Chat ID Telegram không hợp lệ: {value}"));
            if (!normalized.Contains(value, StringComparer.OrdinalIgnoreCase)) normalized.Add(value);
        }
        return (string.Join(';', normalized), null);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex("^[0-9]{5,20}:[A-Za-z0-9_-]{20,100}$")]
    private static partial Regex TelegramTokenRegex();
}
