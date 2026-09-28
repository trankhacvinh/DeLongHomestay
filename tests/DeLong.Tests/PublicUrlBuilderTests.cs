using DeLong.Web.Features.Site;
using Xunit;

namespace DeLong.Tests;

public sealed class PublicUrlBuilderTests
{
    [Fact]
    public void Scoped_booking_url_preserves_property_date_room_and_rate()
    {
        var rateId = Guid.Parse("11111111-2222-3333-4444-555555555555");

        var url = PublicUrlBuilder.Booking("nana", "2026-08-15", "NN-1", rateId);

        Assert.Equal(
            "/h/nana/booking?date=2026-08-15&room=NN-1&rate=11111111-2222-3333-4444-555555555555",
            url);
    }

    [Fact]
    public void Public_property_and_room_pages_collapse_to_the_home_booking_calendar()
    {
        Assert.Equal("/", PublicUrlBuilder.PropertyHome("nana"));
        Assert.Equal("/#lich-phong", PublicUrlBuilder.Rooms());
        Assert.Equal("/#lich-phong", PublicUrlBuilder.Rooms("nana"));
        Assert.Equal("/#lich-phong", PublicUrlBuilder.Room("nana", "nana-1"));
        Assert.Equal("/booking?site=nana&date=2026-08-15", PublicUrlBuilder.GlobalBooking("nana", "2026-08-15"));
    }

    [Theory]
    [InlineData(null, "/#lich-phong")]
    [InlineData("nana", "/#lich-phong")]
    public void Booking_home_points_to_the_vertical_calendar(string? siteSlug, string expected) =>
        Assert.Equal(expected, PublicUrlBuilder.BookingHome(siteSlug));

    [Fact]
    public void Embedded_booking_url_preserves_the_internal_modal_mode() =>
        Assert.Equal("/h/nana/booking?room=NN-1&embed=1",
            PublicUrlBuilder.Booking("nana", room: "NN-1", embedded: true));

    [Fact]
    public void Booking_lookup_uses_the_root_public_page_with_property_context() =>
        Assert.Equal("/booking/lookup?siteSlug=nana", PublicUrlBuilder.BookingLookup("nana"));
}
