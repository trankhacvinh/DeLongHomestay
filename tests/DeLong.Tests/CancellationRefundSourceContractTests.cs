using Xunit;

namespace DeLong.Tests;

public sealed class CancellationRefundSourceContractTests
{
    [Theory]
    [InlineData("src/DeLong.Web/Pages/Admin/Bookings/Index.cshtml")]
    [InlineData("src/DeLong.Web/Pages/Admin/Calendar.cshtml")]
    [InlineData("src/DeLong.Web/Pages/Admin/CalendarV2.cshtml")]
    public void Every_booking_screen_uses_the_shared_cancel_dialog(string page)
    {
        var markup = Read(page);
        Assert.Contains("<partial name=\"_BookingCancelDialog\" />", markup, StringComparison.Ordinal);
        Assert.Contains("admin-booking-cancel.js", markup, StringComparison.Ordinal);
        Assert.Contains("admin-booking-cancel.css", markup, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("src/DeLong.Web/wwwroot/js/pages/admin-bookings.js")]
    [InlineData("src/DeLong.Web/wwwroot/js/pages/admin-calendar.js")]
    public void Cancel_actions_open_the_dialog_instead_of_posting_the_status_directly(string script)
    {
        var source = Read(script);
        Assert.Contains("DeLongBookingCancel", source, StringComparison.Ordinal);
        Assert.Contains("openCancelDialog()", source, StringComparison.Ordinal);
        Assert.Contains("extraBody", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Cancel_dialog_requires_a_reason_and_a_refund_choice_for_paid_bookings()
    {
        var script = Read("src/DeLong.Web/wwwroot/js/pages/admin-booking-cancel.js");
        var markup = Read("src/DeLong.Web/Pages/Shared/_BookingCancelDialog.cshtml");
        Assert.Contains("Vui lòng nhập lý do hủy.", script, StringComparison.Ordinal);
        Assert.Contains("hãy chọn cách xử lý tiền", script, StringComparison.Ordinal);
        Assert.Contains("Hoàn tiền ngay", script, StringComparison.Ordinal);
        Assert.Contains("Hoàn sau", script, StringComparison.Ordinal);
        Assert.Contains("giữ cọc", script, StringComparison.Ordinal);
        Assert.Contains("cancelDialog.reason", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Cancellation_page_has_filters_search_totals_and_refund_editing()
    {
        var page = Read("src/DeLong.Web/Pages/Admin/Cancellations/Index.cshtml");
        var script = Read("src/DeLong.Web/wwwroot/js/pages/admin-cancellations.js");
        Assert.Contains("v-model=\"from\"", page, StringComparison.Ordinal);
        Assert.Contains("v-model=\"to\"", page, StringComparison.Ordinal);
        Assert.Contains("v-model.trim=\"search\"", page, StringComparison.Ordinal);
        Assert.Contains("Còn phải hoàn", page, StringComparison.Ordinal);
        Assert.Contains("Nhân viên hủy", page, StringComparison.Ordinal);
        Assert.Contains("Hoàn lúc / nhập lúc", page, StringComparison.Ordinal);
        Assert.Contains("Sửa số tiền", page, StringComparison.Ordinal);
        Assert.Contains("/cancellations/${this.detail.id}/refund-due", script, StringComparison.Ordinal);
        Assert.Contains("DeLongApi.put", script, StringComparison.Ordinal);
        Assert.Contains("/api/admin/cancellations/export", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Cancellation_endpoints_enforce_branch_access_and_payment_permission()
    {
        var endpoints = Read("src/DeLong.Web/Features/Cancellations/CancellationEndpoints.cs");
        var program = Read("src/DeLong.Web/Program.cs");
        Assert.Contains("RequireAuthorization(\"ViewCancellations\")", endpoints, StringComparison.Ordinal);
        Assert.Contains("RequireAuthorization(\"ManagePayments\")", endpoints, StringComparison.Ordinal);
        Assert.Contains("AddEndpointFilter<PropertyAccessFilter>()", endpoints, StringComparison.Ordinal);
        Assert.Contains("AddEndpointFilter<ApiAntiforgeryFilter>()", endpoints, StringComparison.Ordinal);
        Assert.Contains("\"ViewCancellations\", policy => policy.RequireRole(\"Admin\", \"Manager\", \"Staff\", \"Viewer\")", program, StringComparison.Ordinal);
        Assert.Contains("app.MapCancellationEndpoints();", program, StringComparison.Ordinal);
    }

    [Fact]
    public void Settings_expose_the_manager_cancellation_email_switch_and_refund_telegram_event()
    {
        var page = Read("src/DeLong.Web/Pages/Admin/Settings/Index.cshtml");
        var script = Read("src/DeLong.Web/wwwroot/js/pages/admin-settings.js");
        Assert.Contains("managerCancellationEmailEnabled", page, StringComparison.Ordinal);
        Assert.Contains("managerCancellationEmailEnabled", script, StringComparison.Ordinal);
        Assert.Contains("booking-refunded", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Finance_and_reports_show_money_still_owed_to_guests()
    {
        Assert.Contains("refundsOwed", Read("src/DeLong.Web/Pages/Admin/Finance/Index.cshtml"), StringComparison.Ordinal);
        Assert.Contains("refundsOwed", Read("src/DeLong.Web/Pages/Admin/Reports/Index.cshtml"), StringComparison.Ordinal);
    }

    private static string Read(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DeLongHomestay.sln"))) directory = directory.Parent;
        if (directory is null) throw new InvalidOperationException("Could not locate repository root.");
        return File.ReadAllText(Path.Combine(directory.FullName, relativePath));
    }
}
