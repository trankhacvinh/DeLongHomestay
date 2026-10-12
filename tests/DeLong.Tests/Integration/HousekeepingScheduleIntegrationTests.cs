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

    [Theory]
    [InlineData(0, true, 0)]
    [InlineData(1, true, 0)]
    [InlineData(2, true, 15)]
    [InlineData(4, true, 15)]
    [InlineData(5, false, 15)]
    [Trait("Category", "Integration")]
    public async Task Schedule_combines_nearby_arrival_and_excludes_cancelled_bookings(int gapHours, bool combined, int afterMinutes)
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
            HousekeepingAfterCheckOutMinutes = afterMinutes,
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
        var arriving = Booking(property, room, customer, $"IN-{suffix}", targetDate, 9 + gapHours, 10 + gapHours, BookingStatus.Confirmed, timeZone);
        completed.CheckOutUtc = completed.CheckOutUtc.AddMinutes(30);
        arriving.CheckInUtc = arriving.CheckInUtc.AddMinutes(30);
        arriving.CheckOutUtc = arriving.CheckOutUtc.AddMinutes(30);
        var cancelled = Booking(property, room, customer, $"CAN-{suffix}", targetDate, 18, 19, BookingStatus.Cancelled, timeZone);

        var checkoutRate = new RoomRate { RoomId = room.Id, Name = "Ca vừa trả", StartTime = new(8, 0), EndTime = new(9, 30), SortOrder = 0 };
        var arrivalRate = new RoomRate { RoomId = room.Id, Name = "Ca sắp nhận", StartTime = new(9, 30), EndTime = new(18, 30), SortOrder = 1 };
        var overnightRate = new RoomRate { RoomId = room.Id, Name = "Qua đêm", StartTime = new(21, 0), EndTime = new(9, 30), Type = RoomRateType.Overnight, SortOrder = 2 };
        completed.RoomRateId = checkoutRate.Id;
        arriving.RoomRateId = arrivalRate.Id;
        db.AddRange(checkoutRate, arrivalRate, overnightRate);
        db.AddRange(property, room, customer, completed, arriving, cancelled);
        await db.SaveChangesAsync();

        var schedule = await new HousekeepingService(db).GetScheduleAsync(property.Id, targetDate, 1);

        Assert.NotNull(schedule);
        Assert.Equal(30, schedule!.Settings.BeforeCheckInMinutes);
        Assert.Equal(afterMinutes, schedule.Settings.AfterCheckOutMinutes);
        var day = Assert.Single(schedule.Calendar);
        Assert.Equal(combined ? 2 : 3, day.Tasks.Count);
        var turnover = Assert.Single(day.Tasks, task => task.Kind == "turnover" && task.BookingId == completed.Id);
        Assert.Equal(combined ? "Mở đèn" : "Tắt đèn", turnover.Action);
        Assert.Equal(combined ? 1 : 0, turnover.ShiftOrder);
        Assert.Equal(ToUtc(targetDate, new TimeOnly(9, 30), timeZone).AddMinutes(afterMinutes), turnover.AtUtc);
        if (combined)
        {
            Assert.Equal($"Dọn phòng {room.Name} + Mở đèn", turnover.Text);
            Assert.DoesNotContain(day.Tasks, task => task.Kind == "prepare");
        }
        else
        {
            var prepare = Assert.Single(day.Tasks, task => task.Kind == "prepare");
            Assert.Equal(arriving.Id, prepare.BookingId);
            Assert.Equal("Mở đèn", prepare.Action);
            Assert.Equal(ToUtc(targetDate, new TimeOnly(9 + gapHours, 0), timeZone), prepare.AtUtc);
        }
        var arrivingTurnover = Assert.Single(day.Tasks, task => task.Kind == "turnover" && task.BookingId == arriving.Id);
        Assert.Equal("Tắt đèn", arrivingTurnover.Action);
        Assert.Equal(1, arrivingTurnover.ShiftOrder);
        Assert.Equal(ToUtc(targetDate, new TimeOnly(10 + gapHours, 30), timeZone).AddMinutes(afterMinutes), arrivingTurnover.AtUtc);
        Assert.DoesNotContain(day.Tasks, task => task.BookingId == cancelled.Id);

        var otherRoom = new Room { PropertyId = property.Id, Code = $"OTHER-{suffix}", Name = "Other room", IsActive = true };
        db.Rooms.Add(otherRoom);
        arriving.RoomId = otherRoom.Id;
        await db.SaveChangesAsync();
        var independent = await new HousekeepingService(db).GetScheduleAsync(property.Id, targetDate, 1);
        Assert.Equal("Tắt đèn", Assert.Single(independent!.Calendar.Single().Tasks, x => x.BookingId == completed.Id).Action);
        arriving.RoomId = room.Id;
        arriving.Status = BookingStatus.Completed;
        await db.SaveChangesAsync();
        var historical = await new HousekeepingService(db).GetScheduleAsync(property.Id, targetDate, 1);
        var historicalTurnover = Assert.Single(historical!.Calendar.Single().Tasks, x => x.BookingId == completed.Id);
        Assert.Equal(combined ? "Mở đèn" : "Tắt đèn", historicalTurnover.Action);
        Assert.DoesNotContain(historical.Calendar.Single().Tasks, x => x.Kind == "prepare");
        arriving.Status = BookingStatus.Confirmed;

        // A partial overnight stay belongs to that room's overnight shift.
        completed.CheckInUtc = ToUtc(targetDate.AddDays(-1), new TimeOnly(23, 0), timeZone);
        completed.CheckOutUtc = ToUtc(targetDate, new TimeOnly(9, 30), timeZone);
        arriving.CheckInUtc = ToUtc(targetDate, new TimeOnly(10, 30), timeZone);
        arriving.CheckOutUtc = ToUtc(targetDate, new TimeOnly(13, 30), timeZone);
        await db.SaveChangesAsync();
        var morning = await new HousekeepingService(db).GetScheduleAsync(property.Id, targetDate, 1);
        Assert.Equal(1, Assert.Single(morning!.Calendar.Single().Tasks, x => x.BookingId == completed.Id).ShiftOrder);
        arriving.CheckInUtc = ToUtc(targetDate, new TimeOnly(18, 45), timeZone);
        arriving.CheckOutUtc = ToUtc(targetDate, new TimeOnly(22, 0), timeZone);
        await db.SaveChangesAsync();
        var custom = await new HousekeepingService(db).GetScheduleAsync(property.Id, targetDate, 1);
        Assert.Null(Assert.Single(custom!.Calendar.Single().Tasks, x => x.BookingId == arriving.Id && x.Kind == "prepare").ShiftOrder);
        Assert.Equal(2, Assert.Single(custom.Calendar.Single().Tasks, x => x.BookingId == completed.Id).ShiftOrder);

        // A next-day arrival must not reappear as a separate preparation task.
        completed.CheckInUtc = ToUtc(targetDate, new TimeOnly(21, 0), timeZone);
        completed.CheckOutUtc = ToUtc(targetDate, new TimeOnly(23, 30), timeZone);
        arriving.CheckInUtc = completed.CheckOutUtc.AddHours(gapHours);
        arriving.CheckOutUtc = arriving.CheckInUtc.AddHours(1);
        await db.SaveChangesAsync();
        var overnight = await new HousekeepingService(db).GetScheduleAsync(property.Id, targetDate, 2);
        var nextDayOnly = await new HousekeepingService(db).GetScheduleAsync(property.Id, targetDate.AddDays(1), 1);
        Assert.Equal(combined ? 0 : 1, overnight!.Calendar.SelectMany(x => x.Tasks).Count(x => x.Kind == "prepare"));
        Assert.Equal(combined ? 0 : 1, nextDayOnly!.Calendar.Single().Tasks.Count(x => x.Kind == "prepare"));
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
