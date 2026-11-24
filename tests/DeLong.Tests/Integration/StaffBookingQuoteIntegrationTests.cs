using DeLong.Web.Common.Auditing;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Features.Bookings;
using DeLong.Web.Features.Pricing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeLong.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class StaffBookingQuoteIntegrationTests
{
    [RoomLockPostgresFact]
    public async Task Two_ca_stay_is_priced_per_ca_with_weekend_prices()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION")!).Options);
        await db.Database.MigrateAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var property = new Property { Code = "QT" + suffix, Name = "Quote test", TimeZoneId = "Asia/Ho_Chi_Minh" };
        var room = new Room { Property = property, Name = "Quote", Code = "R" + suffix };
        var ca1 = new RoomRate { Room = room, Name = "Ca 1", StartTime = new(11, 0), EndTime = new(14, 0), Price = 250000, IsActive = true, SortOrder = 1, UseWeekdayPriceOnWeekend = false, WeekendPrice = 280000 };
        var ca2 = new RoomRate { Room = room, Name = "Ca 2", StartTime = new(14, 0), EndTime = new(17, 30), Price = 300000, IsActive = true, SortOrder = 2, UseWeekdayPriceOnWeekend = true };
        db.AddRange(ca1, ca2);
        await db.SaveChangesAsync();
        var service = new StaffBookingQuoteService(db, new PricingService(db, new AuditService(db)));
        DateTime At(int day, int h, int m = 0) => new DateTimeOffset(2031, 1, day, h, m, 0, TimeSpan.FromHours(7)).UtcDateTime;

        // 2031-01-08 is a Wednesday.
        var (weekday, error) = await service.QuoteAsync(property.Id, room.Id, ca1.Id, At(8, 11), At(8, 17, 30), default);
        Assert.Null(error);
        Assert.Equal(["Ca 1", "Ca 2"], weekday!.Slots.Select(x => x.RateName));
        Assert.Equal(550000, weekday.Total);

        var (single, _) = await service.QuoteAsync(property.Id, room.Id, ca1.Id, At(8, 11), At(8, 14), default);
        Assert.Equal(250000, Assert.Single(single!.Slots).AppliedAmount);

        // 2031-01-11 is a Saturday: Ca 1 uses its weekend price.
        var (weekend, _) = await service.QuoteAsync(property.Id, room.Id, ca1.Id, At(11, 11), At(11, 15), default);
        Assert.Equal(580000, weekend!.Total);
    }
}
