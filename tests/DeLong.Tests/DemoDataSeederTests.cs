using DeLong.Web.Data;
using DeLong.Web.Data.Seed;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeLong.Tests;

public sealed class DemoDataSeederTests
{
    [Theory]
    [InlineData(2026, 10, 12)]
    [InlineData(2027, 1, 1)]
    public void Demo_transactions_follow_setup_clock_and_preserve_money_and_availability(int year, int month, int day)
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused").Options);
        var now = new DateTime(year, month, day, 18, 0, 0, DateTimeKind.Utc);
        var property = new Property { Code = "DEMO", Name = "Test" };
        for (var i = 0; i < 6; i++)
            property.Rooms.Add(new Room { PropertyId = property.Id, Code = $"R{i}", Name = $"Room {i}", SortOrder = i, FullDayPrice = 650_000m });
        db.Add(property);
        DemoDataSeeder.AddOperations(db, property, Guid.NewGuid(), now);
        var bookings = db.ChangeTracker.Entries<Booking>().Select(x => x.Entity).ToArray();
        var payments = db.ChangeTracker.Entries<Payment>().Select(x => x.Entity).ToArray();
        Assert.Equal(30, bookings.Length);
        Assert.Equal(6, db.ChangeTracker.Entries<Customer>().Count());
        Assert.Equal(6, db.ChangeTracker.Entries<Expense>().Count());
        Assert.All(bookings, b =>
        {
            Assert.True(b.CheckOutUtc > b.CheckInUtc);
            Assert.Equal(DateTimeKind.Utc, b.CheckInUtc.Kind);
            Assert.InRange(b.CheckInUtc, now.AddDays(-30), now.AddDays(40));
            Assert.InRange(payments.Where(p => p.BookingId == b.Id).Sum(p => p.SignedAmount), 0, b.TotalAmount);
        });
        var occupied = Assert.Single(bookings, b => b.Status == BookingStatus.CheckedIn);
        Assert.True(occupied.CheckInUtc < now && occupied.CheckOutUtc > now);
        foreach (var room in property.Rooms)
        {
            var active = bookings.Where(b => b.RoomId == room.Id && b.Status != BookingStatus.Cancelled)
                .OrderBy(b => b.CheckInUtc).ToArray();
            for (var i = 1; i < active.Length; i++) Assert.True(active[i - 1].CheckOutUtc <= active[i].CheckInUtc);
        }
        Assert.All(db.ChangeTracker.Entries<Expense>(), e => Assert.True(e.Entity.OccurredAtUtc <= now));
        Assert.Contains(payments, p => p.Type == PaymentType.Refund);
        Assert.All(payments, p => Assert.True(p.OccurredAtUtc <= now));
        var voucher = Assert.Single(db.ChangeTracker.Entries<Voucher>()).Entity;
        Assert.True(voucher.StartsAtUtc <= now && voucher.EndsAtUtc > now);
    }
}
