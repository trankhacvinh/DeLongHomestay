using DeLong.Web.Features.Bookings;
using Xunit;

namespace DeLong.Tests;

public sealed class BookingTimeSuggestionTests
{
    private static DateTime At(int hour,int minute=0)=>new(2031,1,10,hour,minute,0,DateTimeKind.Utc);

    [Theory]
    [InlineData(17,30,18,0)]
    [InlineData(17,50,18,20)]
    [InlineData(18,10,18,40)]
    [InlineData(17,10,18,0)]
    public void Previous_actual_checkout_plus_cleaning_moves_only_the_start(int hour,int minute,int expectedHour,int expectedMinute)
    {
        var start=BookingTimeSuggestionService.SuggestStart(At(18),At(21),[new(At(14,30),At(hour,minute),true)],30);
        Assert.Equal(At(expectedHour,expectedMinute),start);
    }

    [Fact]
    public void Cleaning_uses_configuration_and_room_blocks_do_not_add_cleaning()
    {
        Assert.Equal(At(18,5),BookingTimeSuggestionService.SuggestStart(At(18),At(21),[new(At(14,30),At(17,50),true)],15));
        Assert.Equal(At(18,10),BookingTimeSuggestionService.SuggestStart(At(18),At(21),[new(At(17),At(18,10),false)],30));
        Assert.Equal(At(18),BookingTimeSuggestionService.SuggestStart(At(18),At(21),[],30));
    }

    [Fact]
    public void No_invalid_or_overlapping_continuous_window_is_suggested()
    {
        Assert.Null(BookingTimeSuggestionService.SuggestStart(At(18),At(21),[new(At(14),At(20,30),true)],30));
        Assert.Equal(At(20),BookingTimeSuggestionService.SuggestStart(At(18),At(21),[new(At(14),At(18,10),true),new(At(19),At(19,30),true)],30));
        Assert.Equal(At(18),BookingTimeSuggestionService.SuggestStart(At(18),At(21),[new(At(21),At(23),true)],30));
        Assert.Null(BookingTimeSuggestionService.SuggestStart(At(21),At(18),[],30));
    }

    [Fact]
    public void Overnight_keeps_the_next_morning_deadline()
    {
        var end=At(10).AddDays(1);
        Assert.Equal(At(22,40),BookingTimeSuggestionService.SuggestStart(At(21,30),end,[new(At(18),At(22,10),true)],30));
    }
}
