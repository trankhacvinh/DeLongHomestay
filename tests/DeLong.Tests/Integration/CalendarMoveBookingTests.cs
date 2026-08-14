using DeLong.Web.Common.Auditing;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Bookings;
using DeLong.Web.Features.Customers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeLong.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class CalendarMoveBookingTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Calendar_move_preserves_financial_snapshot_and_rejects_conflicts()
    {
        var connectionString = Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var property = new Property
        {
            Code = $"MOVE-{suffix}",
            Name = $"Calendar Move {suffix}",
            TimeZoneId = "Asia/Ho_Chi_Minh"
        };
        var roomA = new Room { Property = property, Code = $"A-{suffix}", Name = "Room A", Capacity = 2, IsActive = true };
        var roomB = new Room { Property = property, Code = $"B-{suffix}", Name = "Room B", Capacity = 2, IsActive = true };
        var slotA = new RoomRate
        {
            Room = roomA, Name = "Khung chiều", Type = RoomRateType.TimeSlot,
            StartTime = new TimeOnly(14, 0), EndTime = new TimeOnly(17, 0), Price = 250_000m, IsActive = true
        };
        var slotB = new RoomRate
        {
            Room = roomB, Name = "Khung chiều B", Type = RoomRateType.TimeSlot,
            StartTime = new TimeOnly(14, 0), EndTime = new TimeOnly(17, 0), Price = 300_000m, IsActive = true
        };
        var nightlyA = new RoomRate
        {
            Room = roomA, Name = "Lưu trú theo đêm", Type = RoomRateType.Nightly,
            StartTime = new TimeOnly(14, 0), EndTime = new TimeOnly(12, 0), Price = 500_000m, IsActive = true
        };
        var nightlyB = new RoomRate
        {
            Room = roomB, Name = "Lưu trú theo đêm", Type = RoomRateType.Nightly,
            StartTime = new TimeOnly(15, 0), EndTime = new TimeOnly(11, 0), Price = 650_000m, IsActive = true
        };
        var customer = new Customer
        {
            Property = property,
            Name = "Calendar Move Guest",
            Phone = $"092{Random.Shared.Next(1000000, 9999999)}",
            NormalizedPhone = $"092{Random.Shared.Next(1000000, 9999999)}",
            IsActive = true
        };
        db.AddRange(property, roomA, roomB, slotA, slotB, nightlyA, nightlyB, customer);
        await db.SaveChangesAsync();

        var bookingService = new BookingService(db, new CustomerService(db), new AuditService(db));
        var moveService = new BookingMoveService(db, new AuditService(db), bookingService);

        var (slotBooking, slotError) = await bookingService.CreateAsync(property.Id, new CreateBookingRequest
        {
            RoomId = roomA.Id,
            CustomerId = customer.Id,
            CustomerName = customer.Name,
            CustomerPhone = customer.Phone,
            Type = BookingType.TimeSlot,
            RoomRateId = slotA.Id,
            RateName = slotA.Name,
            UnitPrice = 250_000m,
            CheckIn = new DateTimeOffset(2026, 9, 10, 7, 0, 0, TimeSpan.Zero),
            CheckOut = new DateTimeOffset(2026, 9, 10, 10, 0, 0, TimeSpan.Zero),
            Status = BookingStatus.Confirmed,
            RoomAmount = 250_000m,
            Source = "Admin"
        });
        Assert.Null(slotError);

        var (movedSlot, movedSlotError) = await moveService.MoveAsync(property.Id, slotBooking!.Id, new MoveBookingRequest
        {
            RoomId = roomB.Id,
            TargetDate = new DateOnly(2026, 9, 12)
        });
        Assert.Null(movedSlotError);
        Assert.NotNull(movedSlot);
        Assert.Equal(roomB.Id, movedSlot!.RoomId);
        Assert.Equal(slotB.Id, movedSlot.RoomRateId);
        Assert.Equal(250_000m, movedSlot.RoomAmount);
        Assert.Equal(new DateTime(2026, 9, 12, 7, 0, 0, DateTimeKind.Utc), movedSlot.CheckInUtc);
        Assert.Equal(new DateTime(2026, 9, 12, 10, 0, 0, DateTimeKind.Utc), movedSlot.CheckOutUtc);

        var (multiDay, multiDayError) = await bookingService.CreateAsync(property.Id, new CreateBookingRequest
        {
            RoomId = roomA.Id,
            CustomerId = customer.Id,
            CustomerName = customer.Name,
            CustomerPhone = customer.Phone,
            Type = BookingType.MultiDay,
            RoomRateId = nightlyA.Id,
            RateName = nightlyA.Name,
            UnitPrice = 500_000m,
            NightCount = 3,
            CheckIn = new DateTimeOffset(2026, 9, 20, 7, 0, 0, TimeSpan.Zero),
            CheckOut = new DateTimeOffset(2026, 9, 23, 5, 0, 0, TimeSpan.Zero),
            Status = BookingStatus.Held,
            RoomAmount = 1_500_000m,
            Source = "Admin"
        });
        Assert.Null(multiDayError);

        var (movedStay, movedStayError) = await moveService.MoveAsync(property.Id, multiDay!.Id, new MoveBookingRequest
        {
            RoomId = roomB.Id,
            TargetDate = new DateOnly(2026, 9, 25)
        });
        Assert.Null(movedStayError);
        Assert.NotNull(movedStay);
        Assert.Equal(roomB.Id, movedStay!.RoomId);
        Assert.Equal(nightlyB.Id, movedStay.RoomRateId);
        Assert.Equal(3, movedStay.NightCount);
        Assert.Equal(500_000m, movedStay.UnitPrice);
        Assert.Equal(1_500_000m, movedStay.RoomAmount);
        Assert.Equal(new DateTime(2026, 9, 25, 8, 0, 0, DateTimeKind.Utc), movedStay.CheckInUtc);
        Assert.Equal(new DateTime(2026, 9, 28, 4, 0, 0, DateTimeKind.Utc), movedStay.CheckOutUtc);

        var (otherBooking, otherError) = await bookingService.CreateAsync(property.Id, new CreateBookingRequest
        {
            RoomId = roomA.Id,
            CustomerId = customer.Id,
            CustomerName = customer.Name,
            CustomerPhone = customer.Phone,
            Type = BookingType.TimeSlot,
            RoomRateId = slotA.Id,
            RateName = slotA.Name,
            UnitPrice = 250_000m,
            CheckIn = new DateTimeOffset(2026, 9, 13, 7, 0, 0, TimeSpan.Zero),
            CheckOut = new DateTimeOffset(2026, 9, 13, 10, 0, 0, TimeSpan.Zero),
            Status = BookingStatus.Held,
            RoomAmount = 250_000m,
            Source = "Admin"
        });
        Assert.Null(otherError);

        var (conflicted, conflictError) = await moveService.MoveAsync(property.Id, otherBooking!.Id, new MoveBookingRequest
        {
            RoomId = roomB.Id,
            TargetDate = new DateOnly(2026, 9, 12)
        });
        Assert.Null(conflicted);
        Assert.NotNull(conflictError);
        Assert.Equal("booking_conflict", conflictError!.Code);

        var persisted = await db.Bookings.AsNoTracking().SingleAsync(x => x.Id == otherBooking.Id);
        Assert.Equal(roomA.Id, persisted.RoomId);
        Assert.Equal(new DateTime(2026, 9, 13, 7, 0, 0, DateTimeKind.Utc), persisted.CheckInUtc);
    }
}
