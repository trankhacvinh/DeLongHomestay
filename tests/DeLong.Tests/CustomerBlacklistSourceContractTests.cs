using Xunit;

namespace DeLong.Tests;

public sealed class CustomerBlacklistSourceContractTests
{
    [Fact]
    public void Booking_and_account_paths_enforce_blocked_customer_server_side()
    {
        var booking = Read("src/DeLong.Web/Features/Bookings/BookingService.cs");
        var publicBooking = Read("src/DeLong.Web/Features/PublicBooking/PublicBookingCoreV2Service.cs");
        var accounts = Read("src/DeLong.Web/Features/CustomerAccounts/CustomerAccountService.cs");

        Assert.Contains("if (customer.IsBlocked)", booking, StringComparison.Ordinal);
        Assert.Contains("x.PropertyId == propertyId && x.IsBlocked", publicBooking, StringComparison.Ordinal);
        Assert.Contains("x.UserId == user.Id && x.Customer.IsBlocked", accounts, StringComparison.Ordinal);
        Assert.Contains("EF.Functions.ILike(x.Email", publicBooking, StringComparison.Ordinal);
    }

    [Fact]
    public void Customer_admin_surfaces_note_blacklist_filter_and_booking_editor()
    {
        var customerPage = Read("src/DeLong.Web/Pages/Admin/Customers/Index.cshtml");
        var bookingPage = Read("src/DeLong.Web/Pages/Admin/Bookings/Index.cshtml");
        var customerScript = Read("src/DeLong.Web/wwwroot/js/pages/admin-customers.js");

        Assert.Contains("blacklistOnly", customerPage, StringComparison.Ordinal);
        Assert.Contains("customer-note-icon", customerPage, StringComparison.Ordinal);
        Assert.Contains("customer-blacklisted-row", customerPage, StringComparison.Ordinal);
        Assert.Contains("saveCustomerRisk", bookingPage, StringComparison.Ordinal);
        Assert.Contains("blacklistReason", customerScript, StringComparison.Ordinal);
    }

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(FindRepositoryRoot(), relativePath));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
