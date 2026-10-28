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
public sealed class BookingDisplayColorTests
{
    [RoomLockPostgresFact]
    public async Task Manual_color_survives_reload_and_omitted_updates_and_can_return_to_automatic()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION")!).Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();
        var key = Guid.NewGuid().ToString("N");
        var property = new Property { Code = key, Name = "Color test", SiteSlug = key, IsActive = true };
        var room = new Room { Property = property, Code = key, Name = "Blue", IsActive = true };
        db.AddRange(property, room);
        await db.SaveChangesAsync();
        var service = new BookingService(db, new CustomerService(db), new AuditService(db));
        var start = DateTimeOffset.UtcNow.AddDays(30);
        var created = await service.CreateAsync(property.Id, new CreateBookingRequest
        {
            RoomId = room.Id, CustomerName = "Guest", CustomerPhone = "0900000000",
            CheckIn = start, CheckOut = start.AddHours(3), RoomAmount = 250000,
            Status = BookingStatus.Confirmed, DisplayColor = "#397967"
        });
        Assert.Null(created.Error);
        var booking = created.Booking!;
        var autoCreated = await service.CreateAsync(property.Id, new CreateBookingRequest
        {
            RoomId = room.Id, CustomerName = "Guest", CustomerPhone = "0900000000",
            CheckIn = start.AddDays(1), CheckOut = start.AddDays(1).AddHours(3), RoomAmount = 250000,
            Status = BookingStatus.Confirmed
        });
        Assert.Null(autoCreated.Error);
        Assert.Null(autoCreated.Booking!.DisplayColor);
        db.ChangeTracker.Clear();
        Assert.Equal("#397967", (await service.GetAsync(property.Id, booking.Id))!.DisplayColor);
        UpdateBookingRequest Update(bool change, string? color) => new()
        {
            RoomId = room.Id, CustomerId = booking.CustomerId, CustomerName = booking.CustomerName,
            CustomerPhone = booking.CustomerPhone, CheckIn = start, CheckOut = start.AddHours(3),
            RoomAmount = 250000, UpdateDisplayColor = change, DisplayColor = color
        };
        var preserved = await service.UpdateAsync(property.Id, booking.Id, Update(false, null));
        Assert.Null(preserved.Error);
        Assert.Equal("#397967", preserved.Booking!.DisplayColor);
        var changed = await service.UpdateAsync(property.Id, booking.Id, Update(true, "#7c5cc4"));
        Assert.Null(changed.Error);
        Assert.Equal("#7C5CC4", changed.Booking!.DisplayColor);
        var configured = await service.UpdateAsync(property.Id, booking.Id, Update(true, "#945600"));
        Assert.Null(configured.Error);
        Assert.Equal("#945600", configured.Booking!.DisplayColor);
        var invalid = await service.UpdateAsync(property.Id, booking.Id, Update(true, "red;background:url(x)"));
        Assert.Equal("display_color_invalid", invalid.Error!.Code);
        var automatic = await service.UpdateAsync(property.Id, booking.Id, Update(true, null));
        Assert.Null(automatic.Error);
        Assert.Null(automatic.Booking!.DisplayColor);
        Assert.Equal(booking.Status, automatic.Booking.Status);
        Assert.Equal(booking.TotalAmount, automatic.Booking.TotalAmount);
        Assert.Equal(booking.PaidAmount, automatic.Booking.PaidAmount);
        Assert.Equal(booking.BalanceAmount, automatic.Booking.BalanceAmount);
    }
}
