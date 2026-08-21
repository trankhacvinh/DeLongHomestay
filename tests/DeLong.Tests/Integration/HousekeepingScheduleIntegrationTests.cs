using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Housekeeping;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeLong.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class HousekeepingScheduleIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Schedule_uses_real_booking_times_and_excludes_cancelled_bookings()
    {
        var connectionString = Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();

        var suffix = Guid.NewGuid().ToString("N")[..10];
        var property = new Property
        {
            Code = $"HK-{suffix}",
            Name = "Housekeeping Schedule Test",
            SiteSlug = $"hk-{suffix}",
            TimeZoneId = "Asia/Ho_Chi_Minh",
            HousekeepingBeforeCheckInMinutes = 30,
            HousekeepingAfterCheckOutMinutes = 15,
            IsActive = true
        };
        var room = new Room
        {
            PropertyId = property.Id,
            Code = $"R-{suffix}",
            Name = "Phòng số 1",
            Slug = $"room-{suffix}",
            Capacity = 2,
            IsActive = true
        };
        var phone = $"09{Random.Shared.Next(10000000, 99999999)}";
        var customer = new Customer
        {
            PropertyId = property.Id,
            Name = "Housekeeping Guest",
            Phone = phone,
            NormalizedPhone = phone,
            IsActive = true
        };

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(property.TimeZoneId);
        var targetDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone)).AddDays(5);
        var completed = Booking(property, room, customer, $"OUT-{suffix}", targetDate, 8, 9, BookingStatus.Completed, timeZone);
        var arriving = Booking(property, room, customer, $"IN-{suffix}", targetDate, 11, 13, BookingStatus.Confirmed, timeZone);
        var cancelled = Booking(property, room, customer, $"CAN-{suffix}", targetDate, 15, 17, BookingStatus.Cancelled, timeZone);

        db.AddRange(property, room, customer, completed, arriving, cancelled);
        await db.SaveChangesAsync();

        var schedule = await new HousekeepingService(db).GetScheduleAsync(property.Id, targetDate, 1);

        Assert.NotNull(schedule);
        Assert.Equal(30, schedule!.Settings.BeforeCheckInMinutes);
        Assert.Equal(15, schedule.Settings.AfterCheckOutMinutes);
        var day = Assert.Single(schedule.Calendar);
        Assert.Equal(2, day.Tasks.Count);
        var turnover = Assert.Single(day.Tasks, task => task.Kind == "turnover");
        Assert.Equal("Giữ mở đèn", turnover.Action);
        Assert.Equal(ToUtc(targetDate, new TimeOnly(9, 15), timeZone), turnover.AtUtc);
        Assert.Contains("giữ mở đèn", turnover.Text, StringComparison.Ordinal);
        var prepare = Assert.Single(day.Tasks, task => task.Kind == "prepare");
        Assert.Equal(arriving.Id, prepare.BookingId);
        Assert.Equal("Mở đèn", prepare.Action);
        Assert.Equal(ToUtc(targetDate, new TimeOnly(10, 30), timeZone), prepare.AtUtc);
        Assert.DoesNotContain(day.Tasks, task => task.BookingId == cancelled.Id);
    }

    private static Booking Booking(
        Property property,
        Room room,
        Customer customer,
        string code,
        DateOnly date,
        int checkInHour,
        int checkOutHour,
        BookingStatus status,
        TimeZoneInfo timeZone) =>
        new()
        {
            PropertyId = property.Id,
            RoomId = room.Id,
            CustomerId = customer.Id,
            Code = code,
            Type = BookingType.TimeSlot,
            CheckInUtc = ToUtc(date, new TimeOnly(checkInHour, 0), timeZone),
            CheckOutUtc = ToUtc(date, new TimeOnly(checkOutHour, 0), timeZone),
            Status = status,
            RoomAmount = 200_000m,
            Source = "Integration"
        };

    private static DateTime ToUtc(DateOnly date, TimeOnly time, TimeZoneInfo timeZone) =>
        TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Unspecified),
            timeZone);
}
