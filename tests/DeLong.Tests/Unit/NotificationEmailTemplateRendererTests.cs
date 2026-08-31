using DeLong.Web.Features.Notifications;
using System.Net;
using Xunit;

namespace DeLong.Tests.Unit;

public sealed class NotificationEmailTemplateRendererTests
{
    [Fact]
    public void Render_replaces_supported_booking_variables()
    {
        var data = new BookingEmailTemplateData(
            "De Long", "BK-001", "Nguyễn Minh Anh", "0901000000", "guest@example.test", "Coco #1",
            new DateTime(2026, 8, 30, 14, 0, 0), new DateTime(2026, 8, 31, 10, 0, 0),
            750_000m, "Nhận khóa tại quầy.", "Khách yêu cầu hủy.");

        var result = NotificationEmailTemplateRenderer.Render(
            "{{CustomerName}} · {{BookingCode}} · {{RoomName}} · {{CancellationReason}} · {{TotalAmount}}",
            "fallback",
            data);

        Assert.Equal("Nguyễn Minh Anh · BK-001 · Coco #1 · Khách yêu cầu hủy. · 750.000", result);
    }

    [Fact]
    public void Render_uses_default_when_custom_template_is_blank()
    {
        var data = new BookingEmailTemplateData(
            "De Long", "BK-002", "Khách", "0902", "", "Phòng 2",
            new DateTime(2026, 9, 1, 10, 30, 0), new DateTime(2026, 9, 1, 13, 30, 0),
            250_000m, "Hướng dẫn", "Đã hủy");

        var result = NotificationEmailTemplateRenderer.Render(null, "Mã {{BookingCode}}", data);

        Assert.Equal("Mã BK-002", result);
    }

    [Fact]
    public void Html_template_is_sanitized_and_dynamic_values_are_encoded()
    {
        var data = new BookingEmailTemplateData(
            "De Long", "BK-003", "<img src=x onerror=alert(1)>", "0903", "guest@example.test", "Phòng 3",
            new DateTime(2026, 9, 2, 10, 0, 0), new DateTime(2026, 9, 2, 13, 0, 0),
            300_000m, "Dòng 1\nDòng 2", "Đã hủy");

        var html = NotificationEmailTemplateRenderer.RenderHtml(
            "<script>alert(1)</script><p onclick=\"alert(2)\">Xin chào {{CustomerName}}</p><p>{{GuestGuide}}</p>",
            "fallback",
            data);

        Assert.DoesNotContain("script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onclick", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;img src=x onerror=alert(1)&gt;", html, StringComparison.Ordinal);
        var decoded = WebUtility.HtmlDecode(html);
        Assert.Contains("Dòng 1", decoded, StringComparison.Ordinal);
        Assert.Contains("<br", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Dòng 2", decoded, StringComparison.Ordinal);
    }
}
