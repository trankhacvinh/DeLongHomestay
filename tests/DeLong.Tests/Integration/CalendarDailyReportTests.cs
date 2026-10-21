using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Reports;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeLong.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class CalendarDailyReportTests
{
    [RoomLockPostgresFact]
    public async Task Daily_totals_and_details_match_local_date_and_keep_receipts_separate()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION")!).Options);
        await db.Database.MigrateAsync();
        var date = new DateOnly(2031, 1, 10);
        // Local midnight in Vietnam is 17:00 UTC on the previous day.
        var start = new DateTime(2031, 1, 9, 17, 0, 0, DateTimeKind.Utc);
        var property = new Property { Code = Guid.NewGuid().ToString("N"), Name = "Synthetic daily report" };
        var room = new Room { Property = property, Code = "R", Name = "Synthetic room" };
        var customer = new Customer { Property = property, Name = "Synthetic", Phone = "0900000000", NormalizedPhone = "0900000000" };
        Booking Booking(DateTime checkIn, BookingStatus status, decimal amount) => new() { Property = property, Room = room, Customer = customer,
            Code = Guid.NewGuid().ToString("N")[..12], CheckInUtc = checkIn, CheckOutUtc = checkIn.AddHours(10), Status = status,
            RoomAmount = amount, ExtraAmount = 20000, SpecialSurchargeAmount = 10000, DiscountAmount = 5000 };
        var overnight = Booking(start, BookingStatus.Confirmed, 200000);
        var second = Booking(start.AddHours(23), BookingStatus.Held, 300000);
        var cancelled = Booking(start.AddHours(5), BookingStatus.Cancelled, 900000);
        var noShow = Booking(start.AddHours(6), BookingStatus.NoShow, 800000);
        var previous = Booking(start.AddDays(-1), BookingStatus.Completed, 400000);
        var next = Booking(start.AddHours(36), BookingStatus.Confirmed, 500000);
        db.AddRange(overnight, second, cancelled, noShow, previous, next);
        db.Payments.AddRange(
            new Payment { Property = property, Booking = overnight, Amount = 100000, OccurredAtUtc = start.AddMinutes(-1) },
            new Payment { Property = property, Booking = previous, Amount = 400000, OccurredAtUtc = start },
            new Payment { Property = property, Booking = cancelled, Type = PaymentType.Refund, Amount = 50000, OccurredAtUtc = start.AddHours(1) },
            new Payment { Property = property, Booking = second, Amount = 900000, OccurredAtUtc = start.AddHours(2), IsVoided = true },
            new Payment { Property = property, Booking = next, Amount = 500000, OccurredAtUtc = start.AddDays(1) });
        await db.SaveChangesAsync();
        var report = (await BookingDailyReport.GetAsync(db, property.Id, date, 2))!;
        var day = report[0];
        Assert.Equal(2, day.Summary.BookingCount);
        Assert.Equal(550000, day.Summary.BookingValue);
        Assert.Equal(350000, day.Summary.NetReceipts);
        Assert.Equal(day.Summary.BookingValue, day.Bookings.Sum(x => x.Amount));
        Assert.Equal(day.Summary.NetReceipts, day.Payments.Sum(x => x.Type == "Receipt" ? x.Amount : -x.Amount));
        Assert.Equal(new[] { overnight.Id, second.Id }, day.Bookings.Select(x => x.Id));
        Assert.Equal(1, report[1].Summary.BookingCount);
        Assert.Equal(500000, report[1].Summary.NetReceipts);
        Assert.Null(await BookingDailyReport.GetAsync(db, Guid.NewGuid(), date, 1));
        Assert.Equal(BookingStatus.Cancelled, (await db.Bookings.AsNoTracking().SingleAsync(x => x.Id == cancelled.Id)).Status);
    }
}
