using DeLong.Web.Features.Rooms;
using Xunit;
namespace DeLong.Tests.Unit;

public sealed class RoomBookingScheduleExpansionTests
{
    private static SaveRoomBlockRequest Request => new([Guid.NewGuid()],"Maintenance",DateTimeOffset.UtcNow,DateTimeOffset.UtcNow.AddHours(1),true,new(2031,1,10),new(2031,1,12),[new(new(21,0),new(9,30))]);
    [Fact] public void Overnight_repeats_include_last_day_and_cross_midnight_in_property_zone()
    {
        var ranges=RoomBookingBlockService.Expand(Request,TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"));
        Assert.Equal(3,ranges.Count);Assert.Equal(new DateTime(2031,1,10,14,0,0,DateTimeKind.Utc),ranges[0].Start);
        Assert.Equal(new DateTime(2031,1,13,2,30,0,DateTimeKind.Utc),ranges[2].End);
    }
    [Fact] public void Equal_midnight_times_mean_full_day_and_duplicate_windows_are_removed()
    {
        var request=Request with {Windows=[new(new(0,0),new(0,0)),new(new(0,0),new(0,0))]};
        var ranges=RoomBookingBlockService.Expand(request,TimeZoneInfo.Utc);Assert.Equal(3,ranges.Count);
        Assert.All(ranges,x=>Assert.Equal(TimeSpan.FromDays(1),x.End-x.Start));
    }
    [Fact] public void Reversed_dates_and_empty_windows_are_rejected()
    {
        Assert.Throws<ArgumentException>(()=>RoomBookingBlockService.Expand(Request with {ToDate=new(2031,1,9)},TimeZoneInfo.Utc));
        Assert.Throws<ArgumentException>(()=>RoomBookingBlockService.Expand(Request with {Windows=[]},TimeZoneInfo.Utc));
    }
}
