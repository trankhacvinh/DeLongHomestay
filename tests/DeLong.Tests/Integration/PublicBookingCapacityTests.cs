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
public sealed class PublicBookingCapacityTests
{
    [Fact]
    public async Task Configured_capacity_is_enforced_without_changing_included_guests_or_retry_calendar()
    {
        var connection = Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
        await db.Database.MigrateAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var property = new Property { Id = Guid.CreateVersion7(), Code = suffix[..8], SiteSlug = suffix, Name = "Capacity test", TimeZoneId = "Asia/Ho_Chi_Minh", IsActive = true };
        var room = new Room { Id = Guid.CreateVersion7(), Property = property, Code = "R", Name = "Room", Slug = "room", Capacity = 2, IsActive = true, IsPublished = true };
        var rate = new RoomRate { Id = Guid.CreateVersion7(), Room = room, Name = "Day", Type = RoomRateType.TimeSlot, StartTime = new(11, 0), EndTime = new(14, 0), Price = 250000, IsActive = true };
        db.RoomRates.Add(rate);
        await db.SaveChangesAsync();
        var root = Path.Combine(Path.GetTempPath(), "delong-capacity-" + suffix);
        var paths = new StoragePaths(root, Path.Combine(root, "public"), new PathString("/uploads/rooms"), true, true, false);
        var config = new ConfigurationBuilder().Build();
        try
        {
            var audit = new AuditService(db);
            var service = new PublicBookingCoreV2Service(db, new PublicPropertyResolver(db),
                new PublicBookingService(db, new BookingService(db, new CustomerService(db), audit)),
                new VoucherService(db, audit, paths, config), paths, config);
            var policy = await new BookingPolicyStore(paths, config).GetAsync(property.Id);
            PublicBookingRequest Request(int guests, int dayOffset = 5) => new() {
                RoomId = room.Id, RateId = rate.Id, Type = BookingType.TimeSlot,
                StayDate = DateTime.UtcNow.AddDays(dayOffset).ToString("yyyy-MM-dd"), CustomerName = "Synthetic Guest", CustomerPhone = "0901234567", CustomerEmail = "guest@example.test",
                GuestCount = guests, PolicyAccepted = true, PolicyVersion = policy.PolicyVersion,
                HasIdentityFront = true, HasIdentityBack = true, HasSecondIdentityFront = true, HasSecondIdentityBack = true
            };
            Assert.Equal("guest_limit", (await service.CreateRequestAsync(suffix, Request(3), "over-limit", null)).Error?.Code);
            Assert.Equal("validation", (await service.CreateRequestAsync(suffix, Request(0), "zero", null)).Error?.Code);
            Assert.Empty(await db.Bookings.Where(x => x.PropertyId == property.Id).ToListAsync());
            room.Capacity = 3;
            await db.SaveChangesAsync();
            var key = "capacity-" + suffix;
            var (result, error) = await service.CreateRequestAsync(suffix, Request(3), key, null);
            Assert.Null(error); Assert.NotNull(result);
            Assert.Equal(350000, result.TotalAmount);
            var retry = await service.CreateRequestAsync(suffix, Request(3), key, null);
            Assert.Null(retry.Error); Assert.Equal(result.BookingId, retry.Result!.BookingId);
            Assert.Equal(1, await db.Bookings.CountAsync(x => x.PropertyId == property.Id));
            var booking = await db.Bookings.SingleAsync(x => x.Id == result.BookingId);
            Assert.Equal(BookingStatus.Held, booking.Status);
            Assert.Equal(rate.Id, booking.RoomRateId);
            Assert.Equal(3, (await new BookingGuestDetailsStore(paths).GetAsync(property.Id, booking.Id))!.GuestCount);
            Directory.Move(Path.Combine(root, "private", "booking-details"), Path.Combine(root, "private", "booking-details-backup"));
            await File.WriteAllTextAsync(Path.Combine(root, "private", "booking-details"), "Synthetic write failure");
            await Assert.ThrowsAnyAsync<IOException>(async () =>
                await service.CreateRequestAsync(suffix, Request(2, 6), "failed-" + suffix, null));
            Assert.Equal(1, await db.Bookings.CountAsync(x => x.PropertyId == property.Id));
            Assert.Equal(BookingStatus.Held, await db.Bookings.Where(x => x.Id == result.BookingId).Select(x => x.Status).SingleAsync());
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
