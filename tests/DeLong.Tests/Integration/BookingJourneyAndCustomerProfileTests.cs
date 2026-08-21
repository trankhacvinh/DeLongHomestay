using DeLong.Web.Common.Auditing;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Bookings;
using DeLong.Web.Features.Customers;
using DeLong.Web.Features.Payments;
using DeLong.Web.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeLong.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class BookingJourneyAndCustomerProfileTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Booking_journey_collects_payment_completes_stay_and_keeps_customer_history_property_scoped()
    {
        var connectionString = Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();

        var suffix = Guid.NewGuid().ToString("N")[..10];
        var property = new Property { Code = $"JOURNEY-{suffix}", Name = "Booking Journey", TimeZoneId = "Asia/Ho_Chi_Minh", IsActive = true };
        var otherProperty = new Property { Code = $"OTHER-{suffix}", Name = "Other Property", TimeZoneId = "Asia/Ho_Chi_Minh", IsActive = true };
        var room = new Room { PropertyId = property.Id, Code = $"JR-{suffix}", Name = "Journey Room", Slug = $"journey-{suffix}", Capacity = 2, IsActive = true };
        var otherRoom = new Room { PropertyId = otherProperty.Id, Code = $"OR-{suffix}", Name = "Other Room", Slug = $"other-{suffix}", Capacity = 2, IsActive = true };
        var customer = new Customer { PropertyId = property.Id, Name = "Journey Guest", Phone = "0901234567", NormalizedPhone = "0901234567", Email = "journey@example.test", IsActive = true };
        var otherCustomer = new Customer { PropertyId = otherProperty.Id, Name = "Other Guest", Phone = "0907654321", NormalizedPhone = "0907654321", IsActive = true };
        var booking = new Booking
        {
            PropertyId = property.Id, RoomId = room.Id, CustomerId = customer.Id, Code = $"BK-J-{suffix}",
            CheckInUtc = DateTime.UtcNow.AddDays(2), CheckOutUtc = DateTime.UtcNow.AddDays(2).AddHours(3),
            Status = BookingStatus.Requested, RoomAmount = 300_000m, Source = "Website"
        };
        var otherBooking = new Booking
        {
            PropertyId = otherProperty.Id, RoomId = otherRoom.Id, CustomerId = otherCustomer.Id, Code = $"BK-O-{suffix}",
            CheckInUtc = DateTime.UtcNow.AddDays(3), CheckOutUtc = DateTime.UtcNow.AddDays(3).AddHours(2),
            Status = BookingStatus.Confirmed, RoomAmount = 999_000m, Source = "Integration"
        };

        db.AddRange(property, otherProperty, room, otherRoom, customer, otherCustomer, booking, otherBooking);
        await db.SaveChangesAsync();

        var customerUser = new ApplicationUser
        {
            Id = Guid.CreateVersion7(), UserName = $"customer-{suffix}", NormalizedUserName = $"CUSTOMER-{suffix}".ToUpperInvariant(),
            DisplayName = customer.Name, PhoneNumber = customer.Phone, IsActive = true, IsCustomerAccount = true,
            SecurityStamp = Guid.NewGuid().ToString("N"), ConcurrencyStamp = Guid.NewGuid().ToString("N")
        };
        db.Users.Add(customerUser);
        db.CustomerAccountLinks.Add(new CustomerAccountLink { UserId = customerUser.Id, PropertyId = property.Id, CustomerId = customer.Id });
        db.CustomerAccountSettings.Add(new CustomerAccountSettings { PropertyId = property.Id, LoyaltyEnabled = true, LoyaltySpendPerPoint = 10_000 });
        await db.SaveChangesAsync();

        var bookingService = new BookingService(db, new CustomerService(db), new AuditService(db));
        var paymentService = new PaymentService(db);

        Assert.Null((await bookingService.ChangeStatusAsync(property.Id, booking.Id, BookingStatus.Confirmed)).Error);
        Assert.Null((await paymentService.AddAsync(property.Id, booking.Id, new CreatePaymentRequest
        {
            Type = PaymentType.Receipt,
            Method = PaymentMethod.BankTransfer,
            Amount = 300_000m,
            Reference = $"TEST-{suffix}"
        }, null)).Error);
        Assert.Null((await bookingService.ChangeStatusAsync(property.Id, booking.Id, BookingStatus.CheckedIn)).Error);
        Assert.Null((await bookingService.ChangeStatusAsync(property.Id, booking.Id, BookingStatus.Completed)).Error);

        var completed = await bookingService.GetAsync(property.Id, booking.Id);
        Assert.NotNull(completed);
        Assert.Equal(BookingStatus.Completed, completed!.Status);
        Assert.Equal(300_000m, completed.PaidAmount);
        Assert.Equal(0m, completed.BalanceAmount);
        Assert.Equal(HousekeepingStatus.Dirty, await db.Rooms.Where(x => x.Id == room.Id).Select(x => x.HousekeepingStatus).SingleAsync());
        var points = await db.LoyaltyLedgerEntries.SingleAsync(x => x.BookingId == booking.Id);
        Assert.Equal(30, points.Points);
        Assert.Equal(customerUser.Id, points.UserId);

        var profile = await new CustomerService(db).GetProfileAsync(property.Id, customer.Id);
        Assert.NotNull(profile);
        var history = Assert.Single(profile!.Bookings);
        Assert.Equal(booking.Id, history.Id);
        Assert.Equal(300_000m, history.PaidAmount);
        Assert.Equal(0m, history.BalanceAmount);
        Assert.DoesNotContain(profile.Bookings, item => item.Id == otherBooking.Id);
        Assert.Null(await new CustomerService(db).GetProfileAsync(property.Id, otherCustomer.Id));
    }
}
