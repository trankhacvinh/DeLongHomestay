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

        Assert.True(bytes.Length > 10000, "PDF must contain embedded fonts and rendered text, not an empty page.");
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        var previewPath = Environment.GetEnvironmentVariable("DELONG_PDF_PREVIEW_PATH");
        if (!string.IsNullOrWhiteSpace(previewPath)) File.WriteAllBytes(previewPath, bytes);
    }

    [Theory]
    [InlineData("NotoSans-Regular.ttf")]
    [InlineData("NotoSans-Bold.ttf")]
    public void Embedded_fonts_cover_Vietnamese_without_system_fonts(string filename)
    {
        using var stream = typeof(BookingGuestGuidePdf).Assembly.GetManifestResourceStream($"DeLong.Web.Assets.Fonts.{filename}");
        Assert.NotNull(stream);
        using var data = SkiaSharp.SKData.Create(stream);
        using var typeface = SkiaSharp.SKTypeface.FromData(data);
        Assert.NotNull(typeface);
        using var font = new SkiaSharp.SKFont(typeface, 12);
        Assert.All(font.GetGlyphs("Hướng dẫn sử dụng phòng · Nhận khóa tại quầy lễ tân"), glyph => Assert.NotEqual((ushort)0, glyph));
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
