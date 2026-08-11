using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using Xunit;

namespace DeLong.Tests.Unit;

public sealed class RoomEntityTests
{
    [Fact]
    public void New_room_defaults_to_active_clean_with_capacity_two()
    {
        var room = new Room();
        Assert.True(room.IsActive);
        Assert.Equal(2, room.Capacity);
        Assert.Equal(HousekeepingStatus.Clean, room.HousekeepingStatus);
        Assert.NotEqual(Guid.Empty, room.Id);
    }

    [Fact]
    public void Overnight_rate_can_cross_midnight()
    {
        var rate = new RoomRate
        {
            Name = "Qua đêm",
            StartTime = new TimeOnly(21, 0),
            EndTime = new TimeOnly(9, 30),
            IsOvernight = true,
            Price = 360_000m
        };
        Assert.True(rate.IsOvernight);
        Assert.True(rate.EndTime < rate.StartTime);
        Assert.Equal(360_000m, rate.Price);
    }
}
