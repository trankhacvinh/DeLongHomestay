using Xunit;

namespace DeLong.Tests;

public sealed class BookingNotificationChannelsSourceContractTests
{
    [Fact]
    public void Settings_expose_internal_email_guest_guide_and_telegram_channels()
    {
        var page = Read("src/DeLong.Web/Pages/Admin/Settings/Index.cshtml");
        var script = Read("src/DeLong.Web/wwwroot/js/pages/admin-settings.js");

        Assert.Contains("Danh sách thành viên nhận email booking", page, StringComparison.Ordinal);
        Assert.Contains("guestCheckInEmailEnabled", page, StringComparison.Ordinal);
        Assert.Contains("guestCancellationEmailEnabled", page, StringComparison.Ordinal);
        Assert.Contains("guestCancellationEmailBodyTemplate", script, StringComparison.Ordinal);
        Assert.Contains("{{CancellationReason}}", script, StringComparison.Ordinal);
        Assert.Contains("Mã biến hỗ trợ", page, StringComparison.Ordinal);
        Assert.Contains("copyEmailVariable", script, StringComparison.Ordinal);
        Assert.Contains("previewEmailHtml", script, StringComparison.Ordinal);
        Assert.Contains("data-email-template-editor", page, StringComparison.Ordinal);
        Assert.Contains("role=\"tablist\"", page, StringComparison.Ordinal);
        Assert.Contains("selectEmailTemplateTab", script, StringComparison.Ordinal);
        Assert.DoesNotContain("<details class=\"field full notification-template-editor\"", page, StringComparison.Ordinal);
        Assert.Contains("telegramBookingEnabled", page, StringComparison.Ordinal);
        Assert.Contains("test-telegram", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Booking_details_expose_checkin_delivery_status_and_resend()
    {
        var page = Read("src/DeLong.Web/Pages/Admin/Bookings/Index.cshtml");
        var script = Read("src/DeLong.Web/wwwroot/js/pages/admin-bookings.js");
        var calendarBridge = Read("src/DeLong.Web/wwwroot/js/core/admin-booking-live-v2.js");

        Assert.Contains("Email hướng dẫn check-in", page, StringComparison.Ordinal);
        Assert.Contains("resendCheckInEmail", script, StringComparison.Ordinal);
        Assert.Contains("data-resend-checkin-email", calendarBridge, StringComparison.Ordinal);
    }

    private static string Read(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DeLongHomestay.sln"))) directory = directory.Parent;
        if (directory is null) throw new InvalidOperationException("Could not locate repository root.");
        return File.ReadAllText(Path.Combine(directory.FullName, relativePath));
    }
}
