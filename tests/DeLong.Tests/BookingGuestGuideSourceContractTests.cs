using System.Text;
using DeLong.Web.Features.PublicBooking;
using Xunit;

namespace DeLong.Tests;

public sealed class BookingGuestGuideSourceContractTests
{
    [Fact]
    public void Lookup_blocks_terminal_bookings_and_exposes_the_room_guide()
    {
        var service = ReadRepositoryFile("src/DeLong.Web/Features/PublicBooking/PublicBookingLookupService.cs");
        var lookupPage = ReadRepositoryFile("src/DeLong.Web/Pages/Booking/Lookup.cshtml");
        var successPage = ReadRepositoryFile("src/DeLong.Web/Pages/Booking/Success.cshtml");

        Assert.Contains("x.Status != BookingStatus.Completed", service, StringComparison.Ordinal);
        Assert.Contains("x.Status != BookingStatus.Cancelled", service, StringComparison.Ordinal);
        Assert.Contains("x.Status != BookingStatus.NoShow", service, StringComparison.Ordinal);
        Assert.Contains("x.Room.GuestGuideHtml", service, StringComparison.Ordinal);
        Assert.Contains("v-html=\"result.guestGuideHtml\"", lookupPage, StringComparison.Ordinal);
        Assert.Contains("Tải PDF hướng dẫn", successPage, StringComparison.Ordinal);
    }

    [Fact]
    public void Guide_pdf_is_a_real_pdf_document()
    {
        var bytes = BookingGuestGuidePdf.Create(new PublicBookingGuideDto(
            "BK-TEST-12345678",
            "Coco Blue #1",
            "<h2>Check-in</h2><p>Nhận khóa tại quầy lễ tân.</p><ul><li>Giữ yên tĩnh</li></ul>"));

        Assert.True(bytes.Length > 500);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
    }

    private static string ReadRepositoryFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DeLongHomestay.sln")))
            directory = directory.Parent;
        if (directory is null) throw new InvalidOperationException("Could not locate repository root.");
        return File.ReadAllText(Path.Combine(directory.FullName, relativePath));
    }
}
