using Xunit;

namespace DeLong.Tests;

public sealed class AdminBookingAndCustomerSourceContractTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Booking_rows_open_details_and_modal_loads_guest_information_natively()
    {
        var page = ReadRepositoryFile("src/DeLong.Web/Pages/Admin/Bookings/Index.cshtml");
        var script = ReadRepositoryFile("src/DeLong.Web/wwwroot/js/pages/admin-bookings.js");

        Assert.Contains("v-on:click=\"openBooking(booking)\"", page, StringComparison.Ordinal);
        Assert.Contains("v-on:click.stop=\"openBooking(booking)\"", page, StringComparison.Ordinal);
        Assert.Contains("data-native-booking-guest-details", page, StringComparison.Ordinal);
        Assert.Contains("guestDetails.customerEmail", page, StringComparison.Ordinal);
        Assert.Contains("identityUrl(selectedBooking.id, side)", page, StringComparison.Ordinal);
        Assert.Contains("await Promise.all([this.loadPayments(), this.loadGuestDetails(booking.id)])", script, StringComparison.Ordinal);
        Assert.Contains("/guest-details", script, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Customer_rows_open_property_scoped_profile_and_booking_history()
    {
        var page = ReadRepositoryFile("src/DeLong.Web/Pages/Admin/Customers/Index.cshtml");
        var script = ReadRepositoryFile("src/DeLong.Web/wwwroot/js/pages/admin-customers.js");

        Assert.Contains("v-on:click=\"openDetail(customer)\"", page, StringComparison.Ordinal);
        Assert.Contains("Lịch sử đặt phòng", page, StringComparison.Ordinal);
        Assert.Contains("detail.bookings", page, StringComparison.Ordinal);
        Assert.Contains("/customers/${customer.id}/profile", script, StringComparison.Ordinal);
    }

    private static string ReadRepositoryFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DeLongHomestay.sln")))
            directory = directory.Parent;

        if (directory is null)
            throw new InvalidOperationException("Could not locate repository root from the test output directory.");

        return File.ReadAllText(Path.Combine(directory.FullName, relativePath));
    }
}
