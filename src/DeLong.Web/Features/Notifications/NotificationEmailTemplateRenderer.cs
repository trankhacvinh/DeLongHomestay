using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Ganss.Xss;

namespace DeLong.Web.Features.Notifications;

public sealed record BookingEmailTemplateData(
    string PropertyName,
    string BookingCode,
    string CustomerName,
    string CustomerPhone,
    string CustomerEmail,
    string RoomName,
    DateTime CheckInLocal,
    DateTime CheckOutLocal,
    decimal TotalAmount,
    string GuestGuide,
    string CancellationReason);

public static partial class NotificationEmailTemplateRenderer
{
    public const string DefaultInternalBookingSubject = "[{{PropertyName}}] Yêu cầu đặt phòng mới {{BookingCode}}";
    public const string DefaultInternalBookingBody = """
        <h2>Yêu cầu đặt phòng mới</h2>
        <p><strong>Cơ sở:</strong> {{PropertyName}}<br><strong>Mã booking:</strong> {{BookingCode}}<br><strong>Khách:</strong> {{CustomerName}}<br><strong>Điện thoại:</strong> {{CustomerPhone}}<br><strong>Email:</strong> {{CustomerEmail}}</p>
        <p><strong>Phòng:</strong> {{RoomName}}<br><strong>Nhận:</strong> {{CheckIn}}<br><strong>Trả:</strong> {{CheckOut}}<br><strong>Tổng tiền:</strong> {{TotalAmount}} VND</p>
        <p>Vui lòng mở trang quản trị De Long Homestay để xử lý yêu cầu.</p>
        """;
    public const string DefaultGuestCheckInSubject = "[{{PropertyName}}] Hướng dẫn check-in · {{BookingCode}}";
    public const string DefaultGuestCheckInBody = """
        <p>Xin chào <strong>{{CustomerName}}</strong>,</p>
        <p><strong>Booking:</strong> {{BookingCode}}<br><strong>Phòng:</strong> {{RoomName}}<br><strong>Nhận phòng:</strong> {{CheckIn}}<br><strong>Trả phòng:</strong> {{CheckOut}}</p>
        <h2>Hướng dẫn check-in</h2>
        <p>{{GuestGuide}}</p>
        <p>Nếu cần hỗ trợ, vui lòng liên hệ trực tiếp cơ sở.</p>
        """;
    public const string DefaultGuestCancellationSubject = "[{{PropertyName}}] Booking {{BookingCode}} đã được hủy";
    public const string DefaultGuestCancellationBody = """
        <p>Xin chào <strong>{{CustomerName}}</strong>,</p>
        <p>Booking <strong>{{BookingCode}}</strong> cho phòng <strong>{{RoomName}}</strong> đã được hủy.</p>
        <p><strong>Thời gian dự kiến:</strong> {{CheckIn}} - {{CheckOut}}<br><strong>Lý do:</strong> {{CancellationReason}}</p>
        <p>Nếu bạn cần hỗ trợ hoặc muốn đặt thời gian khác, vui lòng liên hệ trực tiếp cơ sở.</p>
        """;

    private static readonly HtmlSanitizer Sanitizer = CreateSanitizer();

    public static string Render(string? template, string fallback, BookingEmailTemplateData data)
    {
        var result = string.IsNullOrWhiteSpace(template) ? fallback : template.Trim();
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PropertyName"] = data.PropertyName,
            ["BookingCode"] = data.BookingCode,
            ["CustomerName"] = data.CustomerName,
            ["CustomerPhone"] = data.CustomerPhone,
            ["CustomerEmail"] = data.CustomerEmail,
            ["RoomName"] = data.RoomName,
            ["CheckIn"] = data.CheckInLocal.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
            ["CheckOut"] = data.CheckOutLocal.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
            ["TotalAmount"] = data.TotalAmount.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")),
            ["GuestGuide"] = data.GuestGuide,
            ["CancellationReason"] = data.CancellationReason
        };
        foreach (var (key, value) in values)
            result = result.Replace("{{" + key + "}}", value ?? string.Empty, StringComparison.Ordinal);
        return result;
    }

    public static string RenderHtml(string? template, string fallback, BookingEmailTemplateData data)
    {
        var safe = data with
        {
            PropertyName = WebUtility.HtmlEncode(data.PropertyName),
            BookingCode = WebUtility.HtmlEncode(data.BookingCode),
            CustomerName = WebUtility.HtmlEncode(data.CustomerName),
            CustomerPhone = WebUtility.HtmlEncode(data.CustomerPhone),
            CustomerEmail = WebUtility.HtmlEncode(data.CustomerEmail),
            RoomName = WebUtility.HtmlEncode(data.RoomName),
            GuestGuide = WebUtility.HtmlEncode(data.GuestGuide).Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "<br>", StringComparison.Ordinal),
            CancellationReason = WebUtility.HtmlEncode(data.CancellationReason)
        };
        return Render(SanitizeTemplate(template), SanitizeTemplate(fallback), safe);
    }

    public static string SanitizeTemplate(string? template)
    {
        if (string.IsNullOrWhiteSpace(template)) return string.Empty;
        var source = HasHtmlTagRegex().IsMatch(template)
            ? template
            : WebUtility.HtmlEncode(template).Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "<br>", StringComparison.Ordinal);
        return Sanitizer.Sanitize(source).Trim();
    }

    public static string ToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var value = BreakRegex().Replace(html, Environment.NewLine);
        value = BlockRegex().Replace(value, Environment.NewLine);
        value = TagRegex().Replace(value, string.Empty);
        value = WebUtility.HtmlDecode(value).Replace('\u00A0', ' ');
        return string.Join(Environment.NewLine, value.Split(['\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
    }

    private static HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        foreach (var tag in new[] { "p", "br", "strong", "em", "u", "s", "ul", "ol", "li", "h1", "h2", "h3", "blockquote", "a", "table", "thead", "tbody", "tr", "th", "td", "hr" })
            sanitizer.AllowedTags.Add(tag);
        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedAttributes.Add("href");
        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.Add("https");
        sanitizer.AllowedSchemes.Add("http");
        return sanitizer;
    }

    [GeneratedRegex("<[^>]+>")] private static partial Regex HasHtmlTagRegex();
    [GeneratedRegex("<br\\s*/?>", RegexOptions.IgnoreCase)] private static partial Regex BreakRegex();
    [GeneratedRegex("</(?:p|div|li|h[1-6]|blockquote|tr)>", RegexOptions.IgnoreCase)] private static partial Regex BlockRegex();
    [GeneratedRegex("<[^>]+>")] private static partial Regex TagRegex();
}
