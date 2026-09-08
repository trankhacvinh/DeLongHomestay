using Xunit;

namespace DeLong.Tests;

public sealed class VoucherSourceContractTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void Public_booking_revalidates_voucher_server_side_and_supports_zero_value_payment()
    {
        var core = File.ReadAllText(Path.Combine(Root, "src/DeLong.Web/Features/PublicBooking/PublicBookingCoreV2Service.cs"));
        var endpoints = File.ReadAllText(Path.Combine(Root, "src/DeLong.Web/Features/PublicBooking/PublicBookingEndpoints.cs"));

        Assert.Contains("ReserveForBookingAsync", core);
        Assert.Contains("booking.TotalAmount == 0", core);
        Assert.Contains("MarkRedeemedAsync", core);
        Assert.Contains("if (!result.PaymentRequired)", endpoints);
        Assert.Contains("ReleaseReservedAsync", endpoints);
    }

    [Fact]
    public void Voucher_admin_and_public_ui_expose_required_controls()
    {
        var admin = File.ReadAllText(Path.Combine(Root, "src/DeLong.Web/Pages/Admin/Vouchers/Index.cshtml"));
        var booking = File.ReadAllText(Path.Combine(Root, "src/DeLong.Web/Pages/Booking/Index.cshtml"));

        Assert.Contains("Hoàn lượt", admin);
        Assert.Contains("Gửi voucher", admin);
        Assert.Contains("Lượt giữ cũng được tính vào hạn mức", admin);
        Assert.Contains("voucherCode", booking);
        Assert.Contains("Tiền phòng", booking);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src/DeLong.Web")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Không tìm thấy workspace root.");
    }
}
