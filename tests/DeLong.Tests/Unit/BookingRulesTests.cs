using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Bookings;
using Xunit;

namespace DeLong.Tests.Unit;

public sealed class BookingRulesTests
{
    [Theory]
    [InlineData(BookingStatus.Held)]
    [InlineData(BookingStatus.Confirmed)]
    [InlineData(BookingStatus.CheckedIn)]
    public void Locking_statuses_block_room(BookingStatus status)
    {
        Assert.True(BookingRules.LocksRoom(status));
    }

    [Theory]
    [InlineData(BookingStatus.Requested)]
    [InlineData(BookingStatus.Completed)]
    [InlineData(BookingStatus.Cancelled)]
    [InlineData(BookingStatus.NoShow)]
    public void Non_locking_statuses_do_not_block_room(BookingStatus status)
    {
        Assert.False(BookingRules.LocksRoom(status));
    }

    [Fact]
    public void Confirmed_can_check_in_but_cannot_complete_directly()
    {
        Assert.True(BookingRules.CanTransition(BookingStatus.Confirmed, BookingStatus.CheckedIn));
        Assert.False(BookingRules.CanTransition(BookingStatus.Confirmed, BookingStatus.Completed));
    }
}
