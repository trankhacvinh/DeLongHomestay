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
    public async Task Condition_templates_support_property_scoped_create_update_soft_delete_and_restore()
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
            Code = $"TAG-{suffix}", Name = "Template Test", SiteSlug = $"tag-{suffix}",
            TimeZoneId = "Asia/Ho_Chi_Minh", IsActive = true
        };
        var otherProperty = new Property
        {
            Code = $"TAG2-{suffix}", Name = "Other Template Test", SiteSlug = $"tag2-{suffix}",
            TimeZoneId = "Asia/Ho_Chi_Minh", IsActive = true
        };
        db.AddRange(property, otherProperty);
        await db.SaveChangesAsync();

        var service = new HousekeepingService(db);
        var (created, createError) = await service.CreateConditionTagAsync(
            property.Id, new("Đã kiểm tra máy lạnh", "Trang thiết bị"));
        Assert.Null(createError);
        Assert.NotNull(created);

        var (crossProperty, crossPropertyError) = await service.UpdateConditionTagAsync(
            otherProperty.Id, created!.Id, new("Không được sửa", "Khác"));
        Assert.Null(crossProperty);
        Assert.Equal("Không tìm thấy nội dung mẫu.", crossPropertyError);

        var (updated, updateError) = await service.UpdateConditionTagAsync(
            property.Id, created.Id, new("Máy lạnh hoạt động tốt", "Thiết bị"));
        Assert.Null(updateError);
        Assert.Equal("Máy lạnh hoạt động tốt", updated!.Name);
        Assert.Equal("Thiết bị", updated.Category);

        Assert.True(await service.DeleteConditionTagAsync(property.Id, created.Id));
        Assert.DoesNotContain(await service.GetConditionTagsAsync(property.Id), x => x.Id == created.Id);

        var (restored, restoreError) = await service.CreateConditionTagAsync(
            property.Id, new("Máy lạnh hoạt động tốt", "Tiện nghi"));
        Assert.Null(restoreError);
        Assert.Equal(created.Id, restored!.Id);
        Assert.Equal("Tiện nghi", restored.Category);
    }

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
        Assert.Equal(3, day.Tasks.Count);
        var turnover = Assert.Single(day.Tasks, task => task.Kind == "turnover" && task.BookingId == completed.Id);
        Assert.Equal("Giữ mở đèn", turnover.Action);
        Assert.Equal(ToUtc(targetDate, new TimeOnly(9, 15), timeZone), turnover.AtUtc);
        Assert.Contains("giữ mở đèn", turnover.Text, StringComparison.Ordinal);
        var prepare = Assert.Single(day.Tasks, task => task.Kind == "prepare");
        Assert.Equal(arriving.Id, prepare.BookingId);
        Assert.Equal("Mở đèn", prepare.Action);
        Assert.Equal(ToUtc(targetDate, new TimeOnly(10, 30), timeZone), prepare.AtUtc);
        var arrivingTurnover = Assert.Single(day.Tasks, task => task.Kind == "turnover" && task.BookingId == arriving.Id);
        Assert.Equal("Tắt đèn", arrivingTurnover.Action);
        Assert.Equal(ToUtc(targetDate, new TimeOnly(13, 15), timeZone), arrivingTurnover.AtUtc);
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
