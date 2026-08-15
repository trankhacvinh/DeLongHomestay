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
    public void Global_and_property_room_urls_are_unambiguous()
    {
        Assert.Equal("/rooms", PublicUrlBuilder.Rooms());
        Assert.Equal("/h/nana/rooms", PublicUrlBuilder.Rooms("nana"));
        Assert.Equal("/h/nana/rooms/nana-1", PublicUrlBuilder.Room("nana", "nana-1"));
        Assert.Equal("/booking?site=nana&date=2026-08-15", PublicUrlBuilder.GlobalBooking("nana", "2026-08-15"));
    }
}
