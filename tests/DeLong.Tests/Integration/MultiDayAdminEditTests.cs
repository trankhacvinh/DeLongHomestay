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
public sealed class MultiDayAdminEditTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Admin_can_extend_multiday_stay_and_conflicts_are_rejected()
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
            Code = $"EDIT-{suffix}",
            Name = $"Multi-day Edit {suffix}",
            TimeZoneId = "Asia/Ho_Chi_Minh"
        };
        var roomA = new Room { Property = property, Code = $"A-{suffix}", Name = "Room A", Capacity = 2, IsActive = true };
        var roomB = new Room { Property = property, Code = $"B-{suffix}", Name = "Room B", Capacity = 2, IsActive = true };
        var nightlyA = new RoomRate
        {
            Room = roomA,
            Name = "Lưu trú theo đêm",
            Type = RoomRateType.Nightly,
            StartTime = new TimeOnly(14, 0),
            EndTime = new TimeOnly(12, 0),
            Price = 500_000m,
            IsActive = true
        };
        var nightlyB = new RoomRate
        {
            Room = roomB,
            Name = "Lưu trú theo đêm",
            Type = RoomRateType.Nightly,
            StartTime = new TimeOnly(14, 0),
            EndTime = new TimeOnly(12, 0),
            Price = 600_000m,
            IsActive = true
        };
        var customer = new Customer
        {
            Property = property,
            Name = "Admin Edit Guest",
            Phone = $"091{Random.Shared.Next(1000000, 9999999)}",
            NormalizedPhone = $"091{Random.Shared.Next(1000000, 9999999)}",
            IsActive = true
        };

        db.AddRange(property, roomA, roomB, nightlyA, nightlyB, customer);
        await db.SaveChangesAsync();

        var service = new BookingService(db, new CustomerService(db), new AuditService(db));
        var checkIn = new DateTimeOffset(2026, 9, 10, 7, 0, 0, TimeSpan.Zero);
        var checkOut3 = new DateTimeOffset(2026, 9, 13, 5, 0, 0, TimeSpan.Zero);

        var (created, createError) = await service.CreateAsync(property.Id, new CreateBookingRequest
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
            CheckIn = checkIn,
            CheckOut = checkOut3,
            Status = BookingStatus.Held,
            RoomAmount = 1_500_000m,
            Source = "Admin"
        });

        Assert.Null(createError);
        Assert.NotNull(created);

        var checkOut4 = new DateTimeOffset(2026, 9, 14, 5, 0, 0, TimeSpan.Zero);
        var (extended, extendError) = await service.UpdateAsync(property.Id, created!.Id, new UpdateBookingRequest
        {
            RoomId = roomA.Id,
            CustomerId = customer.Id,
            CustomerName = customer.Name,
            CustomerPhone = customer.Phone,
            Type = BookingType.MultiDay,
            RoomRateId = nightlyA.Id,
            RateName = nightlyA.Name,
            UnitPrice = 450_000m,
            NightCount = 4,
            CheckIn = checkIn,
            CheckOut = checkOut4,
            RoomAmount = 1_800_000m,
            Source = "Admin",
            Note = "Gia thoa thuan"
        });

        Assert.Null(extendError);
        Assert.NotNull(extended);
        Assert.Equal(BookingType.MultiDay, extended!.Type);
        Assert.Equal(roomA.Id, extended.RoomId);
        Assert.Equal(4, extended.NightCount);
        Assert.Equal(450_000m, extended.UnitPrice);
        Assert.Equal(1_800_000m, extended.RoomAmount);
        Assert.Equal(checkOut4.UtcDateTime, extended.CheckOutUtc);

        db.Bookings.Add(new Booking
        {
            PropertyId = property.Id,
            RoomId = roomB.Id,
            CustomerId = customer.Id,
            Code = $"BK-BLOCK-{suffix}",
            Type = BookingType.MultiDay,
            RoomRateId = nightlyB.Id,
            RateName = nightlyB.Name,
            UnitPrice = 600_000m,
            NightCount = 4,
            CheckInUtc = checkIn.UtcDateTime,
            CheckOutUtc = checkOut4.UtcDateTime,
            Status = BookingStatus.Held,
            RoomAmount = 2_400_000m
        });
        await db.SaveChangesAsync();

        var (moved, moveError) = await service.UpdateAsync(property.Id, created.Id, new UpdateBookingRequest
        {
            RoomId = roomB.Id,
            CustomerId = customer.Id,
            CustomerName = customer.Name,
            CustomerPhone = customer.Phone,
            Type = BookingType.MultiDay,
            RoomRateId = nightlyB.Id,
            RateName = nightlyB.Name,
            UnitPrice = 600_000m,
            NightCount = 4,
            CheckIn = checkIn,
            CheckOut = checkOut4,
            RoomAmount = 2_400_000m,
            Source = "Admin"
        });

        Assert.Null(moved);
        Assert.NotNull(moveError);
        Assert.Equal("booking_conflict", moveError!.Code);

        var persisted = await db.Bookings.AsNoTracking().SingleAsync(x => x.Id == created.Id);
        Assert.Equal(roomA.Id, persisted.RoomId);
        Assert.Equal(4, persisted.NightCount);
        Assert.Equal(1_800_000m, persisted.RoomAmount);
    }
}
