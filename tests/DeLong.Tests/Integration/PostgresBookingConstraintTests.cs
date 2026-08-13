using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace DeLong.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class PostgresBookingConstraintTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Overlapping_locking_bookings_are_rejected_by_postgres()
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
            Code = $"TEST-{suffix}",
            Name = $"Integration Test {suffix}",
            TimeZoneId = "Asia/Ho_Chi_Minh"
        };
        var room = new Room
        {
            Property = property,
            Code = $"R-{suffix}",
            Name = "Test Room"
        };
        var customer = new Customer
        {
            Property = property,
            Name = "Test Customer",
            Phone = $"090{Random.Shared.Next(1000000, 9999999)}",
            NormalizedPhone = $"090{Random.Shared.Next(1000000, 9999999)}"
        };

        db.AddRange(property, room, customer);
        await db.SaveChangesAsync();

        var first = new Booking
        {
            PropertyId = property.Id,
            RoomId = room.Id,
            CustomerId = customer.Id,
            Code = $"BK-TEST-A-{suffix}",
            CheckInUtc = new DateTime(2026, 8, 10, 7, 0, 0, DateTimeKind.Utc),
            CheckOutUtc = new DateTime(2026, 8, 10, 10, 0, 0, DateTimeKind.Utc),
            Status = BookingStatus.Held,
            RoomAmount = 300_000m
        };
        db.Bookings.Add(first);
        await db.SaveChangesAsync();

        var second = new Booking
        {
            PropertyId = property.Id,
            RoomId = room.Id,
            CustomerId = customer.Id,
            Code = $"BK-TEST-B-{suffix}",
            CheckInUtc = new DateTime(2026, 8, 10, 9, 0, 0, DateTimeKind.Utc),
            CheckOutUtc = new DateTime(2026, 8, 10, 11, 0, 0, DateTimeKind.Utc),
            Status = BookingStatus.Confirmed,
            RoomAmount = 300_000m
        };
        db.Bookings.Add(second);

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        var postgresException = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.ExclusionViolation, postgresException.SqlState);
    }
}
