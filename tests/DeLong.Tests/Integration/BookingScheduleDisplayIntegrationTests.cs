using DeLong.Web.Common.Auditing;
using DeLong.Web.Common.Operations;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Bookings;
using DeLong.Web.Features.Customers;
using DeLong.Web.Features.Operations;
using DeLong.Web.Features.Pricing;
using DeLong.Web.Features.Site;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeLong.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class BookingScheduleDisplayIntegrationTests
{
    [RoomLockPostgresFact]
    public async Task Staff_create_edit_and_reload_classify_actual_hours_in_booking_and_calendar_without_rewriting_history()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION")!).Options);
        await db.Database.MigrateAsync();
        var key = Guid.NewGuid().ToString("N");
        var property = new Property { Code = key, Name = "Synthetic color test", TimeZoneId = "Asia/Ho_Chi_Minh", IsActive = true };
        var room = new Room { Property = property, Code = "R", Name = "Synthetic", IsActive = true };
        var first = new RoomRate { Room = room, Name = "Khung 1", StartTime = new(10, 30), EndTime = new(13, 30), Price = 250000, IsActive = true };
        var second = new RoomRate { Room = room, Name = "Khung 2", StartTime = new(14, 0), EndTime = new(17, 0), Price = 250000, IsActive = true };
        db.AddRange(first, second); await db.SaveChangesAsync();
        var audit = new AuditService(db);
        var service = new BookingService(db, new CustomerService(db), audit);
        var start = new DateTimeOffset(DateTime.UtcNow.AddDays(8).Date.AddHours(3).AddMinutes(30), TimeSpan.Zero);
        async Task<BookingDto> Create(int days, double hours) {
            var result = await service.CreateAsync(property.Id, new CreateBookingRequest {
                RoomId = room.Id, CustomerName = "Synthetic", CustomerPhone = "0900000123", RoomRateId = first.Id,
                CheckIn = start.AddDays(days), CheckOut = start.AddDays(days).AddHours(hours), RoomAmount = 250000, Status = BookingStatus.Confirmed
            });
            Assert.Null(result.Error); return result.Booking!;
        }
        var regular = await Create(0, 3);
        Assert.False(regular.IsFlexibleTime); Assert.False(regular.IsMixedSlot);
        var flexible = await Create(1, 4);
        Assert.True(flexible.IsFlexibleTime); Assert.False(flexible.IsMixedSlot);
        var mixed = await Create(2, 6.5);
        Assert.False(mixed.IsFlexibleTime); Assert.True(mixed.IsMixedSlot); Assert.Equal(0, mixed.RateSegmentCount);
        var edited = await service.UpdateAsync(property.Id, regular.Id, new UpdateBookingRequest {
            RoomId = room.Id, CustomerId = regular.CustomerId, CustomerName = regular.CustomerName, CustomerPhone = regular.CustomerPhone,
            RoomRateId = first.Id, CheckIn = start, CheckOut = start.AddHours(4), RoomAmount = regular.RoomAmount
        });
        Assert.Null(edited.Error); Assert.True(edited.Booking!.IsFlexibleTime);
        db.ChangeTracker.Clear();
        Assert.True((await service.GetAsync(property.Id, regular.Id))!.IsFlexibleTime);
        var all = await service.GetAllAsync(property.Id);
        Assert.True(all.Single(x => x.Id == mixed.Id).IsMixedSlot);
        Assert.Equal(regular.RoomAmount, all.Single(x => x.Id == regular.Id).RoomAmount);
        Assert.Equal(first.Id, all.Single(x => x.Id == regular.Id).RoomRateId);
        Assert.Equal(0, await db.BookingRateSegments.CountAsync(x => x.Booking.PropertyId == property.Id));

        var root = Path.Combine(Path.GetTempPath(), "delong-display-" + key);
        var paths = new StoragePaths(root, Path.Combine(root, "media"), new PathString("/uploads"), true, true, false);
        try {
            var calendar = await new AvailabilityIntervalService(db, new PublicPropertyResolver(db), paths, new PricingService(db, audit))
                .GetAdminAsync(property.Id, room.Id, DateOnly.FromDateTime(start.UtcDateTime), 3);
            var occupied = calendar!.Calendar.SelectMany(x => x.Slots).SelectMany(x => x.Occupied).ToList();
            Assert.All(occupied.Where(x => x.BookingId == mixed.Id), x => Assert.True(x.IsMixedSlot));
            Assert.All(occupied.Where(x => x.BookingId == flexible.Id || x.BookingId == regular.Id), x => Assert.True(x.IsFlexibleTime));
            Assert.Contains(occupied, x => x.BookingId == mixed.Id);
            Assert.Contains(occupied, x => x.BookingId == regular.Id);
        } finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        var otherProperty = await BookingScheduleDisplay.LoadAsync(db, Guid.NewGuid(), [regular.Id], default);
        Assert.Empty(otherProperty);
    }
}
