using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Operations;
using Xunit;

namespace DeLong.Tests;

public sealed class AvailabilityIntervalProjectorTests
{
    private static readonly DateTime SlotStart = new(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SlotEnd = new(2026, 8, 20, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Empty_slot_is_available_with_the_full_free_range()
    {
        var result = AvailabilityIntervalProjector.Project(SlotStart, SlotEnd, []);

        Assert.Equal("available", result.State);
        Assert.Equal(0d, result.OccupiedRatio);
        var free = Assert.Single(result.Free);
        Assert.Equal(SlotStart, free.StartUtc);
        Assert.Equal(SlotEnd, free.EndUtc);
        Assert.Empty(result.Occupied);
    }

    [Fact]
    public void Full_slot_is_occupied()
    {
        var result = AvailabilityIntervalProjector.Project(
            SlotStart,
            SlotEnd,
            [Occupancy(SlotStart, SlotEnd)]);

        Assert.Equal("occupied", result.State);
        Assert.Equal(1d, result.OccupiedRatio);
        Assert.Empty(result.Free);
    }

    [Fact]
    public void Booking_until_14_in_a_12_to_15_slot_leaves_the_last_hour_free()
    {
        var occupiedUntil = SlotStart.AddHours(2);
        var result = AvailabilityIntervalProjector.Project(
            SlotStart,
            SlotEnd,
            [Occupancy(SlotStart, occupiedUntil)]);

        Assert.Equal("partial", result.State);
        Assert.Equal(0.6667d, result.OccupiedRatio, 4);
        var free = Assert.Single(result.Free);
        Assert.Equal(occupiedUntil, free.StartUtc);
        Assert.Equal(SlotEnd, free.EndUtc);
    }

    [Fact]
    public void Booking_in_the_middle_leaves_two_free_ranges()
    {
        var result = AvailabilityIntervalProjector.Project(
            SlotStart,
            SlotEnd,
            [Occupancy(SlotStart.AddHours(1), SlotStart.AddHours(2))]);

        Assert.Equal("partial", result.State);
        Assert.Equal(0.3333d, result.OccupiedRatio, 4);
        Assert.Collection(
            result.Free,
            first =>
            {
                Assert.Equal(SlotStart, first.StartUtc);
                Assert.Equal(SlotStart.AddHours(1), first.EndUtc);
            },
            second =>
            {
                Assert.Equal(SlotStart.AddHours(2), second.StartUtc);
                Assert.Equal(SlotEnd, second.EndUtc);
            });
    }

    [Fact]
    public void Occupancy_is_clipped_to_the_rate_slot()
    {
        var result = AvailabilityIntervalProjector.Project(
            SlotStart,
            SlotEnd,
            [Occupancy(SlotStart.AddHours(-1), SlotStart.AddHours(1))]);

        Assert.Equal("partial", result.State);
        Assert.Equal(0.3333d, result.OccupiedRatio, 4);
        var occupied = Assert.Single(result.Occupied);
        Assert.Equal(SlotStart, occupied.StartUtc);
        Assert.Equal(SlotStart.AddHours(1), occupied.EndUtc);
    }

    [Fact]
    public void Overlapping_occupancies_are_not_double_counted()
    {
        var result = AvailabilityIntervalProjector.Project(
            SlotStart,
            SlotEnd,
            [
                Occupancy(SlotStart, SlotStart.AddHours(2)),
                Occupancy(SlotStart.AddHours(1), SlotEnd)
            ]);

        Assert.Equal("occupied", result.State);
        Assert.Equal(1d, result.OccupiedRatio);
        Assert.Empty(result.Free);
    }

    private static AvailabilityOccupancyInput Occupancy(DateTime start, DateTime end) =>
        new(Guid.NewGuid(), BookingStatus.Confirmed, start, end);
}
