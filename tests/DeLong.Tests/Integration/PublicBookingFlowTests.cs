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
    public async Task Public_request_uses_server_rate_and_locked_booking_marks_slot_unavailable()
    {
        var connectionString = Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();

        Assert.False(await db.Properties.AnyAsync(x => x.Code == "DELONG"));

        var property = new Property
        {
            Id = Guid.CreateVersion7(),
            Code = "DELONG",
            Name = "De Long Integration",
            TimeZoneId = "Asia/Ho_Chi_Minh"
        };
        var room = new Room
        {
            Id = Guid.CreateVersion7(),
            PropertyId = property.Id,
            Code = "PUBLIC-01",
            Name = "Public Test Room",
            Capacity = 2,
            IsActive = true
        };
        var rate = new RoomRate
        {
            Id = Guid.CreateVersion7(),
            RoomId = room.Id,
            Name = "Khung chiều",
            StartTime = new TimeOnly(14, 0),
            EndTime = new TimeOnly(17, 0),
            Price = 123_000m,
            SortOrder = 1,
            IsActive = true
        };

        db.Properties.Add(property);
        db.Rooms.Add(room);
        db.RoomRates.Add(rate);
        await db.SaveChangesAsync();

        var customerService = new CustomerService(db);
        var bookingService = new BookingService(db, customerService, new AuditService(db));
        var publicService = new PublicBookingService(db, bookingService);

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(property.TimeZoneId);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone));
        var stayDate = today.AddDays(5);

        var (result, error) = await publicService.CreateRequestAsync(new PublicBookingRequest
        {
            RoomId = room.Id,
            RateId = rate.Id,
            StayDate = stayDate.ToString("yyyy-MM-dd"),
            CustomerName = "Public Guest",
            CustomerPhone = "0901234567",
            Note = "Integration test"
        });

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal(123_000m, result!.TotalAmount);

        var booking = await db.Bookings.SingleAsync(x => x.Id == result.BookingId);
        Assert.Equal(BookingStatus.Requested, booking.Status);
        Assert.Equal(123_000m, booking.RoomAmount);
        Assert.Equal("Website", booking.Source);

        // Requested does not lock the room, so another guest may submit a request for the
        // same slot. The two rows must still receive distinct human-facing booking codes.
        var (secondResult, secondError) = await publicService.CreateRequestAsync(new PublicBookingRequest
        {
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
        Assert.Equal(2, await db.Bookings.CountAsync());

        booking.Status = BookingStatus.Held;
        await db.SaveChangesAsync();

        var availability = await publicService.GetAvailabilityAsync(stayDate);
        var availabilityRate = Assert.Single(Assert.Single(availability!.Rooms).Rates);
        Assert.False(availabilityRate.Available);
    }
}
