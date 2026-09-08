using Xunit;

namespace DeLong.Tests;

public sealed class PricingSourceContractTests
{
    [Fact]
    public void Admin_pricing_page_exposes_rate_special_day_and_combo_tabs()
    {
        var root = FindRoot();
        var page = File.ReadAllText(Path.Combine(root, "src/DeLong.Web/Pages/Admin/Pricing/Index.cshtml"));
        Assert.Contains("Bảng giá phòng", page);
        Assert.Contains("Ngày đặc biệt", page);
        Assert.Contains("Quy tắc combo", page);
        Assert.Contains("Chỉ combo cả ngày", page);
    }

    [Fact]
    public void Admin_pricing_styles_use_current_tokens_and_pad_special_day_editor()
    {
        var root = FindRoot();
        var styles = File.ReadAllText(Path.Combine(root, "src/DeLong.Web/wwwroot/css/admin-pricing.css"));

        Assert.DoesNotContain("var(--primary)", styles, StringComparison.Ordinal);
        Assert.DoesNotContain("var(--border)", styles, StringComparison.Ordinal);
        Assert.Contains("background:var(--brand-800)", styles, StringComparison.Ordinal);
        Assert.Contains(".pricing-editor .pricing-form-grid{padding:20px}", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void Admin_special_day_uses_vietnamese_flatpickr_format()
    {
        var root = FindRoot();
        var page = File.ReadAllText(Path.Combine(root, "src/DeLong.Web/Pages/Admin/Pricing/Index.cshtml"));
        var script = File.ReadAllText(Path.Combine(root, "src/DeLong.Web/wwwroot/js/pages/admin-pricing.js"));

        Assert.Contains("flatpickr@4.6.13", page, StringComparison.Ordinal);
        Assert.Contains("dist/l10n/vn.js", page, StringComparison.Ordinal);
        Assert.Contains("altFormat: 'd/m/Y'", script, StringComparison.Ordinal);
        Assert.Contains("dateFormat: 'Y-m-d'", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Public_calendar_handles_full_day_only_and_configurable_three_slot_discount()
    {
        var root = FindRoot();
        var script = File.ReadAllText(Path.Combine(root, "src/DeLong.Web/wwwroot/js/pages/public-availability-calendar.js"));
        Assert.Contains("Number(day.bookingMode) === 1", script);
        Assert.Contains("threeSlotDiscountPercent", script);
        Assert.Contains("effectiveFullDayPrice", script);
    }

    [Fact]
    public void Public_booking_recalculates_pricing_on_the_server_and_persists_the_snapshot()
    {
        var root = FindRoot();
        var service = File.ReadAllText(Path.Combine(root, "src/DeLong.Web/Features/PublicBooking/PublicBookingService.cs"));

        Assert.Contains("pricingService.CalculateAsync", service);
        Assert.Contains("SpecialSurchargeAmount = specialSurchargeAmount", service);
        Assert.Contains("RateSegments = segments", service);
        Assert.Contains("bookingService.HasConflictAsync", service);
    }

    [Fact]
    public void Embedded_booking_modal_receives_server_calculated_pricing()
    {
        var root = FindRoot();
        var pageModel = File.ReadAllText(Path.Combine(root, "src/DeLong.Web/Pages/Booking/Index.cshtml.cs"));
        var script = File.ReadAllText(Path.Combine(root, "src/DeLong.Web/wwwroot/js/pages/public-booking.js"));

        Assert.Contains("pricingService.CalculateAsync", pageModel);
        Assert.Contains("embeddedPricingTotal = pricing!.TotalBeforeVoucher", pageModel);
        Assert.Contains("string.Equals(embed, \"1\"", pageModel);
        Assert.Contains("initial.embeddedPricingTotal", script);
    }

    [Fact]
    public void Voucher_discount_uses_room_amount_and_does_not_discount_special_surcharge()
    {
        var root = FindRoot();
        var service = File.ReadAllText(Path.Combine(root, "src/DeLong.Web/Features/Vouchers/VoucherService.cs"));

        Assert.Contains("CalculateDiscount(roomAmount, voucher.DiscountPercent)", service);
        Assert.Contains("roomAmount + specialSurchargeAmount - discount", service);
    }

    [Fact]
    public void Booking_total_and_balance_include_special_surcharge()
    {
        var root = FindRoot();
        var booking = File.ReadAllText(Path.Combine(root, "src/DeLong.Web/Domain/Entities/Booking.cs"));

        Assert.Contains("RoomAmount + SpecialSurchargeAmount + ExtraAmount - DiscountAmount", booking);
    }

    [Fact]
    public void Migration_preserves_weekday_behavior_for_existing_rows()
    {
        var root = FindRoot();
        var migration = File.ReadAllText(Path.Combine(root,
            "src/DeLong.Web/Data/Migrations/20260901181758_AddWeekendAndSpecialDayPricing.cs"));

        Assert.Contains("use_weekday_price_on_weekend", migration);
        Assert.Contains("defaultValue: true", migration);
        Assert.Contains("special_surcharge_amount", migration);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DeLongHomestay.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Không tìm thấy thư mục dự án.");
    }
}
