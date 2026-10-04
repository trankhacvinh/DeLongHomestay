using DeLong.Web.Features.Bookings;
using DeLong.Web.Features.PublicBooking;
using Xunit;

namespace DeLong.Tests;

public sealed class PublicBookingEmailLookupTests
{
    [Theory]
    [InlineData("guest@example.com", true)]
    [InlineData(" Guest+room@example.com ", true)]
    [InlineData("Name <guest@example.com>", false)]
    [InlineData("guest@example.com,other@example.com", false)]
    [InlineData("not-an-email", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Email_lookup_accepts_one_email_address_only(string? email, bool expected)
    {
        Assert.Equal(expected, PublicBookingEmailLookupService.IsValidEmail(email));
    }

    [Fact]
    public void New_booking_codes_are_short_and_avoid_ambiguous_characters()
    {
        var codes = Enumerable.Range(0, 1000).Select(_ => BookingCodeGenerator.Create()).ToList();
        Assert.All(codes, code => Assert.Matches("^BK[23456789ABCDEFGHJKLMNPQRSTUVWXYZ]{8}$", code));
        Assert.Equal(codes.Count, codes.Distinct().Count());
    }
}
