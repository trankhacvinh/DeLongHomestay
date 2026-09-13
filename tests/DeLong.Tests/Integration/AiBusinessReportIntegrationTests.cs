using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.AdminAi;
using DeLong.Web.Features.Reports;
using DeLong.Web.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeLong.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class AiBusinessReportIntegrationTests
{
    [PricingPostgreSqlFact]
    [Trait("Category", "Integration")]
    public async Task Daily_report_uses_property_timezone_and_actual_payments_instead_of_booking_value()
    {
        var connectionString = Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();

        var suffix = Guid.NewGuid().ToString("N")[..10];
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone));
        var dayStartUtc = TimeZoneInfo.ConvertTimeToUtc(today.ToDateTime(TimeOnly.MinValue), timeZone);
        var property = new Property { Code = $"AI-REPORT-{suffix}", Name = "AI report test", TimeZoneId = timeZone.Id };
        var room = new Room { Property = property, Code = "REPORT-ROOM", Name = "Phòng báo cáo", IsActive = true };
        var customer = new Customer { Property = property, Name = "Khách báo cáo", Phone = $"R-{suffix}", NormalizedPhone = $"R-{suffix}" };
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = $"owner-report-{suffix}", DisplayName = "Owner report tester" };
        var booking = new Booking
        {
            Property = property,
            Room = room,
            Customer = customer,
            Code = $"BK-REPORT-{suffix}",
            Status = BookingStatus.Confirmed,
            CheckInUtc = dayStartUtc.AddHours(1),
            CheckOutUtc = dayStartUtc.AddHours(4),
            RoomAmount = 9_000_000m
        };
        db.AddRange(property, room, customer, user, booking);
        db.Payments.AddRange(
            new Payment
            {
                Property = property, Booking = booking, Type = PaymentType.Receipt,
                Amount = 123_000m, OccurredAtUtc = dayStartUtc.AddMinutes(30)
            },
            new Payment
            {
                Property = property, Booking = booking, Type = PaymentType.Receipt,
                Amount = 777_000m, OccurredAtUtc = dayStartUtc.AddMinutes(-1)
            },
            new Payment
            {
                Property = property, Booking = booking, Type = PaymentType.Refund,
                Amount = 23_000m, OccurredAtUtc = dayStartUtc.AddMinutes(40)
            },
            new Payment
            {
                Property = property, Booking = booking, Type = PaymentType.Receipt,
                Amount = 900_000m, OccurredAtUtc = dayStartUtc.AddMinutes(50), IsVoided = true
            });
        await db.SaveChangesAsync();

        var reportService = new ReportService(db);
        var service = new AiBusinessReportService(db, reportService, new AiResponseCacheService(db));
        var result = await service.GetAsync(property.Id, "day", user.Id, default);

        Assert.NotNull(result);
        Assert.Equal(today, result.From);
        Assert.Equal(today, result.To);
        Assert.Equal(timeZone.Id, result.TimeZone);
        Assert.Equal(100_000m, Assert.Single(result.Metrics, x => x.Name == "Thực thu").Current);
        Assert.Equal(23_000m, Assert.Single(result.Metrics, x => x.Name == "Hoàn tiền").Current);
        Assert.Equal(9_000_000m, Assert.Single(result.ByRoom).BookingValue);
        Assert.DoesNotContain(result.Metrics, x => x.Name == "Thực thu" && x.Current is 123_000m or 777_000m or 900_000m or 1_000_000m or 9_000_000m);
    }
}
