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
public sealed class BookingLifecycleAutomationIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Due_bookings_follow_property_automation_settings_and_run_completion_side_effects()
    {
        var connectionString = Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options);
        await db.Database.MigrateAsync();

        var suffix = Guid.NewGuid().ToString("N")[..10];
        var automatic = new Property { Code = $"AUTO-{suffix}", Name = "Automatic lifecycle" };
        var manual = new Property
        {
            Code = $"MANUAL-{suffix}",
            Name = "Manual lifecycle",
            AutomaticCheckInEnabled = false,
            AutomaticCheckOutEnabled = false
        };
        var automaticRoom1 = Room(automatic, $"A1-{suffix}");
        var automaticRoom2 = Room(automatic, $"A2-{suffix}");
        var manualRoom1 = Room(manual, $"M1-{suffix}");
        var manualRoom2 = Room(manual, $"M2-{suffix}");
        var automaticCustomer = Customer(automatic, $"0901{suffix[..6]}");
        var manualCustomer = Customer(manual, $"0902{suffix[..6]}");
        var now = DateTime.UtcNow;
        var dueCheckIn = Booking(automatic, automaticRoom1, automaticCustomer, $"AIN-{suffix}",
            now.AddMinutes(-1), now.AddMinutes(30), BookingStatus.Confirmed);
        var dueCheckOut = Booking(automatic, automaticRoom2, automaticCustomer, $"AOUT-{suffix}",
            now.AddHours(-2), now.AddMinutes(-1), BookingStatus.CheckedIn);
        var manualCheckIn = Booking(manual, manualRoom1, manualCustomer, $"MIN-{suffix}",
            now.AddMinutes(-1), now.AddMinutes(30), BookingStatus.Confirmed);
        var manualCheckOut = Booking(manual, manualRoom2, manualCustomer, $"MOUT-{suffix}",
            now.AddHours(-2), now.AddMinutes(-1), BookingStatus.CheckedIn);
        db.AddRange(automatic, manual, automaticRoom1, automaticRoom2, manualRoom1, manualRoom2,
            automaticCustomer, manualCustomer, dueCheckIn, dueCheckOut, manualCheckIn, manualCheckOut);
        await db.SaveChangesAsync();

        var bookingService = new BookingService(db, new CustomerService(db), new AuditService(db));
        var result = await new BookingLifecycleAutomationService(db, bookingService).ProcessDueAsync(now);

        Assert.Equal(1, result.CheckedIn);
        Assert.Equal(1, result.CheckedOut);
        Assert.Equal(BookingStatus.CheckedIn, dueCheckIn.Status);
        Assert.Equal(BookingStatus.Completed, dueCheckOut.Status);
        Assert.Equal(HousekeepingStatus.Dirty, automaticRoom2.HousekeepingStatus);
        Assert.Equal(BookingStatus.Confirmed, manualCheckIn.Status);
        Assert.Equal(BookingStatus.CheckedIn, manualCheckOut.Status);
        Assert.Equal(2, await db.AuditLogs.CountAsync(x =>
            x.EntityType == "Booking" && x.Action == "StatusChanged" &&
            (x.EntityId == dueCheckIn.Id || x.EntityId == dueCheckOut.Id)));
    }

    private static Room Room(Property property, string code) => new()
    {
        Property = property,
        Code = code,
        Name = code,
        Slug = code.ToLowerInvariant(),
        IsActive = true
    };

    private static Customer Customer(Property property, string phone) => new()
    {
        Property = property,
        Name = "Automation guest",
        Phone = phone,
        NormalizedPhone = phone,
        IsActive = true
    };

    private static Booking Booking(
        Property property,
        Room room,
        Customer customer,
        string code,
        DateTime checkInUtc,
        DateTime checkOutUtc,
        BookingStatus status) => new()
    {
        Property = property,
        Room = room,
        Customer = customer,
        Code = code,
        CheckInUtc = checkInUtc,
        CheckOutUtc = checkOutUtc,
        Status = status,
        RoomAmount = 200_000m
    };
}
