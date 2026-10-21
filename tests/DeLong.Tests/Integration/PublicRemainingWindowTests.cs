using DeLong.Web.Common.Auditing;
using DeLong.Web.Common.Operations;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Bookings;
using DeLong.Web.Features.Customers;
using DeLong.Web.Features.Operations;
using DeLong.Web.Features.Pricing;
using DeLong.Web.Features.PublicBooking;
using DeLong.Web.Features.Site;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeLong.Tests.Integration;

public sealed class RemainingWindowPostgresTheoryAttribute : TheoryAttribute
{
    public RemainingWindowPostgresTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION")))
            Skip = "DELONG_TEST_CONNECTION is required for isolated PostgreSQL verification.";
    }
}

[Collection("PostgreSQL integration")]
public sealed class PublicRemainingWindowTests
{
    [RemainingWindowPostgresTheory]
    [InlineData(119, 120, false)]
    [InlineData(120, 120, false)]
    [InlineData(121, 120, true)]
    [InlineData(90, 0, true)]
    public async Task Availability_catalog_and_public_submission_apply_the_same_policy(int minutes, int threshold, bool allowed)
    {
        await Run(minutes, threshold, allowed, false);
    }

    [RoomLockPostgresFact]
    public async Task Selecting_a_combo_cannot_bypass_a_short_partial_slot() => await Run(120, 120, false, true);

    private static async Task Run(int remainingMinutes, int threshold, bool allowed, bool combo)
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION")!).Options);
        await db.Database.MigrateAsync();
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
        var end = DateTime.SpecifyKind(date.ToDateTime(new TimeOnly(10, 0)), DateTimeKind.Utc);
        var key = Guid.NewGuid().ToString("N");
        var property = new Property { Code = key, Name = "Synthetic", SiteSlug = key, PublicMinimumRemainingMinutes = threshold };
        var room = new Room { Property = property, Code = "R", Name = "Synthetic room", IsActive = true, IsPublished = true };
        var rate = new RoomRate { Room = room, Name = "Khung 2", StartTime = new(14, 0), EndTime = new(17, 0), Price = 210000, IsActive = true };
        var next = new RoomRate { Room = room, Name = "Khung 3", StartTime = new(17, 30), EndTime = new(20, 30), Price = 210000, IsActive = true, SortOrder = 2 };
        var customer = new Customer { Property = property, Name = "Synthetic previous", Phone = "0900000000", NormalizedPhone = "0900000000" };
        var previous = new Booking { Property = property, Room = room, Customer = customer, Code = "SYNTHETIC", Status = BookingStatus.Confirmed,
            CheckInUtc = end.AddHours(-5), CheckOutUtc = end.AddMinutes(-remainingMinutes - 30), RoomAmount = 210000 };
        db.AddRange(previous, rate, next); await db.SaveChangesAsync();
        var root = Path.Combine(Path.GetTempPath(), "remaining-test-" + key);
        try
        {
            var bookingService = new BookingService(db, new CustomerService(db), new AuditService(db));
            var service = new PublicBookingService(db, bookingService);
            var availability = new AvailabilityIntervalService(db, new PublicPropertyResolver(db),
                new StoragePaths(root, Path.Combine(root, "media"), "/uploads/rooms", true, true, false), new PricingService(db, new AuditService(db)));
            var calendar = (await availability.GetPublicAsync(key, room.Id, date, 1))!;
            var slot = calendar.Calendar[0].Slots.Single(x => x.RateId == rate.Id);
            Assert.Equal(allowed, slot.BookableStartUtc.HasValue);
            if (!allowed) Assert.Equal("occupied", slot.State);
            var admin = (await availability.GetAdminAsync(property.Id, room.Id, date, 1))!;
            Assert.Equal("partial", admin.Calendar[0].Slots.Single(x => x.RateId == rate.Id).State);
            var catalog = (await service.GetCatalogAsync(key, date))!;
            Assert.Equal(allowed, catalog.Rooms.Single().Rates.Single(x => x.Id == rate.Id).Available);
            var request = new PublicBookingRequest { RoomId = room.Id, RateId = rate.Id, StayDate = date.ToString("yyyy-MM-dd"), CustomerName = "Synthetic next", CustomerPhone = "0901234567",
                Slots = combo ? [new(date.ToString("yyyy-MM-dd"), rate.Id), new(date.ToString("yyyy-MM-dd"), next.Id)] : [] };
            var result = await service.CreateRequestAsync(key, request);
            if (allowed)
            {
                Assert.Null(result.Error); Assert.NotNull(result.Result);
                var booking = await db.Bookings.AsNoTracking().SingleAsync(x => x.Id == result.Result.BookingId);
                Assert.Equal(BookingStatus.Held, booking.Status);
            }
            else
            {
                Assert.Equal("remaining_time_too_short", result.Error?.Code);
                Assert.Equal(1, await db.Bookings.CountAsync(x => x.PropertyId == property.Id));
                // Staff can still sell the real free interval without overlap.
                Assert.Null((await bookingService.CreateAsync(property.Id, new CreateBookingRequest { RoomId = room.Id, CustomerName = "Synthetic staff", CustomerPhone = "0909876543",
                    CheckIn = new(end.AddMinutes(-remainingMinutes), TimeSpan.Zero), CheckOut = new(end, TimeSpan.Zero), RoomAmount = 210000, Status = BookingStatus.Confirmed })).Error);
            }
            Assert.Equal(previous.CheckOutUtc, (await db.Bookings.AsNoTracking().SingleAsync(x => x.Id == previous.Id)).CheckOutUtc);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
