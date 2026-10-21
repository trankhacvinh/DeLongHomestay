using DeLong.Web.Common.Auditing;
using DeLong.Web.Common.Operations;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Bookings;
using DeLong.Web.Features.Customers;
using DeLong.Web.Features.PublicBooking;
using DeLong.Web.Features.Site;
using DeLong.Web.Features.Vouchers;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DeLong.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class PublicBookingCustomerTests
{
    [Theory]
    [InlineData("new")]
    [InlineData("existing")]
    [InlineData("inactive")]
    [InlineData("blocked")]
    [InlineData("inactive-blocked")]
    [InlineData("badphone")]
    [InlineData("badname")]
    public async Task Anonymous_customer_resolution_preserves_identity_booking_and_retry_rules(string scenario)
    {
        var connection = Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(connection), "An isolated PostgreSQL test database is required.");
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
        await db.Database.MigrateAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var property = new Property { Code = suffix[..8], SiteSlug = suffix, Name = "Synthetic customer test", TimeZoneId = "Asia/Ho_Chi_Minh", IsActive = true };
        var room = new Room { Property = property, Code = "R", Name = "Synthetic room", Capacity = 2, IsActive = true, IsPublished = true };
        var rate = new RoomRate { Room = room, Name = "Day", Type = RoomRateType.TimeSlot, StartTime = new(11, 0), EndTime = new(14, 0), Price = 210000, IsActive = true };
        db.RoomRates.Add(rate);
        const string phone = "0900000123";
        Customer? customer = null;
        if (scenario != "new")
        {
            customer = new Customer { Property = property, Name = "Synthetic customer", Phone = phone, NormalizedPhone = phone, IsActive = !scenario.StartsWith("inactive"), IsBlocked = scenario.EndsWith("blocked"), IsBlacklisted = scenario.EndsWith("blocked"), BlacklistReason = scenario.EndsWith("blocked") ? "Synthetic block" : null };
            db.Customers.Add(customer);
        }
        await db.SaveChangesAsync();
        var root = Path.Combine(Path.GetTempPath(), "delong-customer-test-" + suffix);
        var paths = new StoragePaths(root, Path.Combine(root, "public"), new PathString("/uploads/rooms"), true, true, false);
        var config = new ConfigurationBuilder().Build();
        try
        {
            var audit = new AuditService(db);
            var service = new PublicBookingCoreV2Service(db, new PublicPropertyResolver(db),
                new PublicBookingService(db, new BookingService(db, new CustomerService(db), audit)),
                new VoucherService(db, audit, paths, config), paths, config);
            var policy = await new BookingPolicyStore(paths, config).GetAsync(property.Id);
            var request = new PublicBookingRequest { RoomId = room.Id, RateId = rate.Id, StayDate = DateTime.UtcNow.AddDays(5).ToString("yyyy-MM-dd"), Type = BookingType.TimeSlot, CustomerName = scenario == "badname" ? "A" : "Synthetic guest", CustomerPhone = scenario == "badphone" ? "123456789012345678901" : "+84 900 000 123", CustomerEmail = "guest@example.test", GuestCount = 2, HasIdentityFront = true, HasIdentityBack = true, PolicyAccepted = true, PolicyVersion = policy.PolicyVersion };
            var key = "synthetic-" + suffix;
            var result = await service.CreateRequestAsync(suffix, request, key, null);
            if (scenario is "inactive-blocked" or "blocked" or "badphone" or "badname")
            {
                Assert.Equal(scenario switch { "inactive-blocked" or "blocked" => "customer_blocked", "badphone" => "customer_phone_invalid", _ => "customer_name_invalid" }, result.Error?.Code);
                Assert.Null(result.Result);
                Assert.Equal(0, await db.Bookings.CountAsync(x => x.PropertyId == property.Id));
                Assert.Equal(!scenario.StartsWith("inactive"), (await db.Customers.AsNoTracking().SingleAsync(x => x.Id == customer!.Id)).IsActive);
                return;
            }
            Assert.Null(result.Error);
            Assert.NotNull(result.Result);
            var retry = await service.CreateRequestAsync(suffix, request, key, null);
            Assert.Null(retry.Error);
            Assert.Equal(result.Result.BookingId, retry.Result!.BookingId);
            Assert.Equal(1, await db.Bookings.CountAsync(x => x.PropertyId == property.Id));
            Assert.Equal(1, await db.Customers.CountAsync(x => x.PropertyId == property.Id));
            var booking = await db.Bookings.AsNoTracking().SingleAsync(x => x.PropertyId == property.Id);
            Assert.Equal(BookingStatus.Held, booking.Status);
            Assert.Equal(210000, booking.RoomAmount);
            if (customer is not null)
            {
                Assert.Equal(customer.Id, booking.CustomerId);
                Assert.Equal(scenario != "inactive", (await db.Customers.AsNoTracking().SingleAsync(x => x.Id == customer.Id)).IsActive);
            }
            if (scenario == "inactive")
            {
                for (var attempt = 0; attempt < 10; attempt++)
                    Assert.Equal(booking.Id, (await service.CreateRequestAsync(suffix, request, key, null)).Result!.BookingId);
                Assert.Equal(1, await db.Bookings.CountAsync(x => x.PropertyId == property.Id));
                Assert.Equal(1, await db.Customers.CountAsync(x => x.PropertyId == property.Id));
            }
            Assert.Equal(0, await db.Payments.CountAsync(x => x.BookingId == booking.Id));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
