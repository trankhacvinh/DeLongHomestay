using DeLong.Web.Common.Auditing;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Bookings;
using DeLong.Web.Features.Customers;
using DeLong.Web.Features.PublicBooking;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeLong.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class PublicBookingFlowTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Public_requests_use_server_rates_and_lock_the_correct_ranges()
    {
        var connectionString = Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();

        var property = await db.Properties.SingleOrDefaultAsync(x => x.Code == "DELONG");
        if (property is null)
        {
            property = new Property
            {
                Id = Guid.CreateVersion7(),
                Code = "DELONG",
                Name = "De Long Integration",
                TimeZoneId = "Asia/Ho_Chi_Minh",
                IsActive = true
            };
            db.Properties.Add(property);
            await db.SaveChangesAsync();
        }

        var suffix = Guid.NewGuid().ToString("N")[..6];
        var room = new Room
        {
            Id = Guid.CreateVersion7(),
            PropertyId = property.Id,
            Code = $"PUBLIC-{suffix}",
            Name = "Public Test Room",
            Slug = $"public-{suffix}",
            Capacity = 2,
            IsActive = true,
            IsPublished = true
        };
        var rate = new RoomRate
        {
            Id = Guid.CreateVersion7(),
            RoomId = room.Id,
            Name = "Khung chiều",
            StartTime = new TimeOnly(14, 0),
            EndTime = new TimeOnly(17, 0),
            Type = RoomRateType.TimeSlot,
            Price = 123_000m,
            SortOrder = 1,
            IsActive = true
        };
        var nightlyRate = new RoomRate
        {
            Id = Guid.CreateVersion7(),
            RoomId = room.Id,
            Name = "Lưu trú theo đêm",
            StartTime = new TimeOnly(14, 0),
            EndTime = new TimeOnly(12, 0),
            Type = RoomRateType.Nightly,
            Price = 500_000m,
            SortOrder = 99,
            IsActive = true
        };

        db.Rooms.Add(room);
        db.RoomRates.AddRange(rate, nightlyRate);
        await db.SaveChangesAsync();

        var customerService = new CustomerService(db);
        var bookingService = new BookingService(db, customerService, new AuditService(db));
        var publicService = new PublicBookingService(db, bookingService);

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(property.TimeZoneId);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone));
        var stayDate = today.AddDays(5);

        var firstRequest = new PublicBookingRequest
        {
            Type = BookingType.TimeSlot,
            RoomId = room.Id,
            RateId = rate.Id,
            StayDate = stayDate.ToString("yyyy-MM-dd"),
            CustomerName = "Public Guest",
            CustomerPhone = "0901234567",
            Note = "Integration test"
        };
        var idempotencyKey = $"integration-{Guid.NewGuid():N}";
        var (result, error) = await publicService.CreateRequestAsync(firstRequest, idempotencyKey);

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal(BookingType.TimeSlot, result!.Type);
        Assert.Equal(123_000m, result.TotalAmount);

        var booking = await db.Bookings.SingleAsync(x => x.Id == result.BookingId);
        Assert.Equal(BookingStatus.Requested, booking.Status);
        Assert.Equal(BookingType.TimeSlot, booking.Type);
        Assert.Equal(rate.Id, booking.RoomRateId);
        Assert.Equal(123_000m, booking.UnitPrice);
        Assert.Equal(123_000m, booking.RoomAmount);
        Assert.Equal("Website", booking.Source);
        Assert.Equal(idempotencyKey, booking.PublicRequestKey);

        var (replayedResult, replayedError) = await publicService.CreateRequestAsync(firstRequest, idempotencyKey);
        Assert.Null(replayedError);
        Assert.NotNull(replayedResult);
        Assert.Equal(result.BookingId, replayedResult!.BookingId);
        Assert.Equal(1, await db.Bookings.CountAsync(x => x.PropertyId == property.Id && x.PublicRequestKey == idempotencyKey));

        var (secondResult, secondError) = await publicService.CreateRequestAsync(new PublicBookingRequest
        {
            Type = BookingType.TimeSlot,
            RoomId = room.Id,
            RateId = rate.Id,
            StayDate = stayDate.ToString("yyyy-MM-dd"),
            CustomerName = "Second Public Guest",
            CustomerPhone = "0907654321",
            Note = "Second integration request"
        });

        Assert.Null(secondError);
        Assert.NotNull(secondResult);
        Assert.NotEqual(result.Code, secondResult!.Code);

        booking.Status = BookingStatus.Held;
        await db.SaveChangesAsync();

        var availability = await publicService.GetAvailabilityAsync(stayDate);
        var availabilityRoom = Assert.Single(availability!.Rooms.Where(x => x.Id == room.Id));
        var availabilityRate = Assert.Single(availabilityRoom.Rates.Where(x => x.Id == rate.Id));
        Assert.False(availabilityRate.Available);

        var checkInDate = today.AddDays(12);
        var checkOutDate = checkInDate.AddDays(3);
        var (stayAvailability, stayAvailabilityError) = await publicService.GetStayAvailabilityAsync(checkInDate, checkOutDate);
        Assert.Null(stayAvailabilityError);
        Assert.NotNull(stayAvailability);
        Assert.Equal(3, stayAvailability!.Nights);
        var stayRoom = Assert.Single(stayAvailability.Rooms.Where(x => x.Id == room.Id));
        Assert.True(stayRoom.Available);
        Assert.Equal(nightlyRate.Id, stayRoom.NightlyRate.Id);
        Assert.Equal(500_000m, stayRoom.NightlyRate.Price);
        Assert.Equal(1_500_000m, stayRoom.TotalAmount);

        var (multiDayResult, multiDayError) = await publicService.CreateRequestAsync(new PublicBookingRequest
        {
            Type = BookingType.MultiDay,
            RoomId = room.Id,
            RateId = nightlyRate.Id,
            CheckInDate = checkInDate.ToString("yyyy-MM-dd"),
            CheckOutDate = checkOutDate.ToString("yyyy-MM-dd"),
            CustomerName = "Multi Day Guest",
            CustomerPhone = "0912345678",
            Note = "3 night integration stay"
        });

        Assert.Null(multiDayError);
        Assert.NotNull(multiDayResult);
        Assert.Equal(BookingType.MultiDay, multiDayResult!.Type);
        Assert.Equal(3, multiDayResult.NightCount);
        Assert.Equal(1_500_000m, multiDayResult.TotalAmount);

        var multiDayBooking = await db.Bookings.SingleAsync(x => x.Id == multiDayResult.BookingId);
        Assert.Equal(BookingStatus.Requested, multiDayBooking.Status);
        Assert.Equal(BookingType.MultiDay, multiDayBooking.Type);
        Assert.Equal(nightlyRate.Id, multiDayBooking.RoomRateId);
        Assert.Equal("Lưu trú theo đêm", multiDayBooking.RateName);
        Assert.Equal(500_000m, multiDayBooking.UnitPrice);
        Assert.Equal(3, multiDayBooking.NightCount);
        Assert.Equal(1_500_000m, multiDayBooking.RoomAmount);

        var (stillAvailable, stillAvailableError) = await publicService.GetStayAvailabilityAsync(checkInDate, checkOutDate);
        Assert.Null(stillAvailableError);
        Assert.True(Assert.Single(stillAvailable!.Rooms.Where(x => x.Id == room.Id)).Available);

        multiDayBooking.Status = BookingStatus.Held;
        await db.SaveChangesAsync();
        var (lockedStay, lockedStayError) = await publicService.GetStayAvailabilityAsync(checkInDate, checkOutDate);
        Assert.Null(lockedStayError);
        Assert.False(Assert.Single(lockedStay!.Rooms.Where(x => x.Id == room.Id)).Available);
    }
}
