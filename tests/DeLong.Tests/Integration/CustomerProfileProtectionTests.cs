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
public sealed class CustomerProfileProtectionTests
{
    [RoomLockPostgresFact]
    public async Task Website_booking_with_a_known_phone_never_renames_the_existing_guest()
    {
        await using var db = Database(); await db.Database.MigrateAsync();
        var property = new Property { Code = Guid.NewGuid().ToString("N"), Name = "Synthetic profile test" };
        var room = new Room { Property = property, Name = "Synthetic", Code = "R", IsActive = true };
        var customer = new Customer { Property = property, Name = "Khách Quen", Phone = "0900000456", NormalizedPhone = "0900000456", IsActive = true };
        db.AddRange(room, customer); await db.SaveChangesAsync();

        var bookings = new BookingService(db, new CustomerService(db), new AuditService(db));
        var start = new DateTimeOffset(DateTime.UtcNow.AddDays(12));
        var created = await bookings.CreateAsync(property.Id, new CreateBookingRequest
        {
            RoomId = room.Id, CustomerName = "Người Khác", CustomerPhone = customer.Phone, Source = "Website",
            CheckIn = start, CheckOut = start.AddHours(3), RoomAmount = 210000, Status = BookingStatus.Held
        });

        Assert.Null(created.Error);
        Assert.Equal("Khách Quen", (await db.Customers.AsNoTracking().SingleAsync(x => x.Id == customer.Id)).Name);
        var booking = await db.Bookings.AsNoTracking().SingleAsync(x => x.Id == created.Booking!.Id);
        Assert.Equal("Người Khác", booking.ContactName);
        Assert.Contains("Người Khác", booking.Note, StringComparison.Ordinal);
    }

    [RoomLockPostgresFact]
    public async Task Changing_the_phone_on_a_booking_moves_it_to_that_person_instead_of_rewriting_the_profile()
    {
        await using var db = Database(); await db.Database.MigrateAsync();
        var property = new Property { Code = Guid.NewGuid().ToString("N"), Name = "Synthetic profile test" };
        var room = new Room { Property = property, Name = "Synthetic", Code = "R", IsActive = true };
        var customer = new Customer { Property = property, Name = "Khách A", Phone = "0900000777", NormalizedPhone = "0900000777", IsActive = true };
        db.AddRange(room, customer); await db.SaveChangesAsync();

        var bookings = new BookingService(db, new CustomerService(db), new AuditService(db));
        var start = new DateTimeOffset(DateTime.UtcNow.AddDays(14));
        var created = await bookings.CreateAsync(property.Id, new CreateBookingRequest
        {
            CustomerId = customer.Id, RoomId = room.Id, CustomerName = customer.Name, CustomerPhone = customer.Phone,
            CheckIn = start, CheckOut = start.AddHours(3), RoomAmount = 210000, Status = BookingStatus.Confirmed
        }, Guid.NewGuid());
        Assert.Null(created.Error);

        var updated = await bookings.UpdateAsync(property.Id, created.Booking!.Id, new UpdateBookingRequest
        {
            CustomerId = customer.Id, RoomId = room.Id, CustomerName = "Khách B", CustomerPhone = "0900000888",
            CheckIn = start, CheckOut = start.AddHours(3), RoomAmount = 210000
        });

        Assert.Null(updated.Error);
        Assert.NotEqual(customer.Id, updated.Booking!.CustomerId);
        var original = await db.Customers.AsNoTracking().SingleAsync(x => x.Id == customer.Id);
        Assert.Equal("Khách A", original.Name);
        Assert.Equal("0900000777", original.NormalizedPhone);
    }

    private static AppDbContext Database() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION")!).Options);
}
