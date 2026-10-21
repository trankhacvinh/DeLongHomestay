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

[Collection("PostgreSQL integration")]
public sealed class CompletedBookingAvailabilityTests
{
    private static AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION")!).Options);
    private static BookingService Bookings(AppDbContext db) => new(db, new CustomerService(db), new AuditService(db));
    private static DateOnly Date => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)).AddDays(10);
    private static DateTimeOffset Start => new(Date.ToDateTime(new TimeOnly(7, 0)), TimeSpan.Zero);

    private static async Task<(Property Property, Room Room)> SeedAsync(AppDbContext db)
    {
        await db.Database.MigrateAsync();
        var key = Guid.NewGuid().ToString("N");
        // This suite verifies the original interval rules with the optional public threshold disabled.
        var property = new Property { Code = key, Name = "Completed test", PublicMinimumRemainingMinutes = 0, SiteSlug = key, IsActive = true, TimeZoneId = "Asia/Ho_Chi_Minh" };
        var room = new Room { Property = property, Code = key, Name = "Blue", IsActive = true, IsPublished = true };
        room.Rates.Add(new RoomRate { Name = "Afternoon", StartTime = new(14, 0), EndTime = new(17, 0), Price = 250000, Type = RoomRateType.TimeSlot, IsActive = true });
        db.AddRange(property, room);
        await db.SaveChangesAsync();
        return (property, room);
    }

    private static CreateBookingRequest Order(Guid roomId, DateTimeOffset start, DateTimeOffset end) => new()
    {
        RoomId = roomId, CustomerName = "Guest", CustomerPhone = "0935527193",
        CheckIn = start, CheckOut = end, RoomAmount = 250000, Status = BookingStatus.Confirmed
    };

    private static async Task<BookingDto> CompleteAsync(AppDbContext db, Property property, Room room, DateTimeOffset start, DateTimeOffset end)
    {
        var service = Bookings(db);
        var created = await service.CreateAsync(property.Id, Order(room.Id, start, end));
        Assert.Null(created.Error);
        Assert.Null((await service.ChangeStatusAsync(property.Id, created.Booking!.Id, BookingStatus.CheckedIn)).Error);
        var completed = await service.ChangeStatusAsync(property.Id, created.Booking.Id, BookingStatus.Completed);
        Assert.Null(completed.Error);
        return completed.Booking!;
    }

    private static AvailabilityIntervalService Availability(AppDbContext db)
    {
        var root = Path.Combine(Path.GetTempPath(), "completed-test-" + Guid.NewGuid());
        return new(db, new PublicPropertyResolver(db), new StoragePaths(root, Path.Combine(root, "media"), "/uploads/rooms", true, true, false), new PricingService(db, new AuditService(db)));
    }

    [RoomLockPostgresFact]
    public async Task Completing_early_preserves_public_occupancy_admin_history_and_rejects_stale_request()
    {
        await using var db = Db();
        var (property, room) = await SeedAsync(db);
        var completed = await CompleteAsync(db, property, room, Start, Start.AddHours(3));
        Assert.Equal(Start.UtcDateTime, completed.CheckInUtc);
        Assert.Equal(Start.AddHours(3).UtcDateTime, completed.CheckOutUtc);
        var calendar = (await Availability(db).GetPublicAsync(property.SiteSlug, room.Id, Date, 2))!;
        var slot = Assert.Single(calendar.Calendar[0].Slots);
        Assert.Null(slot.BookableStartUtc);
        Assert.Equal("booked", Assert.Single(slot.Occupied).Kind);
        Assert.NotNull(Assert.Single(calendar.Calendar[1].Slots).BookableStartUtc);
        var admin = (await Availability(db).GetAdminAsync(property.Id, room.Id, Date, 1))!;
        Assert.Equal(BookingStatus.Completed, Assert.Single(Assert.Single(admin.Calendar[0].Slots).Occupied).Status);
        Assert.Equal(completed.Id, (await Bookings(db).GetAsync(property.Id, completed.Id))!.Id);
        var publicService = new PublicBookingService(db, Bookings(db));
        var catalog = await publicService.GetAvailabilityAsync(property.SiteSlug, Date);
        Assert.False(Assert.Single(Assert.Single(catalog!.Rooms).Rates).Available);
        var stale = await publicService.CreateRequestAsync(property.SiteSlug, new PublicBookingRequest
        {
            RoomId = room.Id, RateId = room.Rates.Single().Id, StayDate = Date.ToString("yyyy-MM-dd"),
            CustomerName = "Next guest", CustomerPhone = "0987654321"
        });
        Assert.Equal("booking_conflict", stale.Error?.Code);
        Assert.Single(await db.Bookings.Where(x => x.PropertyId == property.Id).ToListAsync());
    }

    [RoomLockPostgresFact]
    public async Task Create_update_and_move_cannot_overlap_completed_booking_but_exact_boundary_is_allowed()
    {
        await using var db = Db();
        var (property, room) = await SeedAsync(db);
        await CompleteAsync(db, property, room, Start, Start.AddHours(3));
        var service = Bookings(db);
        Assert.Equal("booking_conflict", (await service.CreateAsync(property.Id, Order(room.Id, Start, Start.AddHours(3)))).Error?.Code);
        var later = await service.CreateAsync(property.Id, Order(room.Id, Start.AddHours(3), Start.AddHours(6)));
        Assert.Null(later.Error);
        var update = await service.UpdateAsync(property.Id, later.Booking!.Id, new UpdateBookingRequest
        {
            RoomId = room.Id, CustomerId = later.Booking.CustomerId, CustomerName = "Guest", CustomerPhone = "0935527193",
            CheckIn = Start, CheckOut = Start.AddHours(3), RoomAmount = 250000
        });
        Assert.Equal("booking_conflict", update.Error?.Code);
        var other = new Room { PropertyId = property.Id, Code = "Other", Name = "Other", IsActive = true };
        db.Add(other); await db.SaveChangesAsync();
        var moving = await service.CreateAsync(property.Id, Order(other.Id, Start, Start.AddHours(3)));
        Assert.Null(moving.Error);
        var move = await new BookingMoveService(db, new AuditService(db), service).MoveAsync(property.Id, moving.Booking!.Id,
            new MoveBookingRequest { RoomId = room.Id, TargetDate = Date });
        Assert.Equal("booking_conflict", move.Error?.Code);
    }

    [RoomLockPostgresFact]
    public async Task Completed_partial_stay_preserves_turnover_and_remaining_bookable_window()
    {
        await using var db = Db();
        var (property, room) = await SeedAsync(db);
        await CompleteAsync(db, property, room, Start, Start.AddHours(1));
        var result = (await Availability(db).GetPublicAsync(property.SiteSlug, room.Id, Date, 1))!;
        var slot = Assert.Single(result.Calendar[0].Slots);
        Assert.Equal(Start.AddMinutes(90).UtcDateTime, slot.BookableStartUtc);
        Assert.Equal(Start.AddHours(3).UtcDateTime, slot.BookableEndUtc);
        var request = await new PublicBookingService(db, Bookings(db)).CreateRequestAsync(property.SiteSlug, new PublicBookingRequest
        {
            RoomId = room.Id, RateId = room.Rates.Single().Id, StayDate = Date.ToString("yyyy-MM-dd"),
            CustomerName = "Next guest", CustomerPhone = "0987654321"
        });
        Assert.Null(request.Error);
        Assert.Equal(slot.BookableStartUtc, request.Result!.CheckInUtc);
        Assert.Equal(slot.BookableEndUtc, request.Result.CheckOutUtc);
    }

    [RoomLockPostgresFact]
    public async Task Completed_overnight_stay_blocks_next_day_until_checkout_and_turnover_end()
    {
        await using var db = Db();
        var (property, room) = await SeedAsync(db);
        await CompleteAsync(db, property, room, Start.AddDays(-1).AddHours(7), Start.AddHours(1));
        var slot = Assert.Single((await Availability(db).GetPublicAsync(property.SiteSlug, room.Id, Date, 1))!.Calendar[0].Slots);
        Assert.Equal(Start.AddMinutes(90).UtcDateTime, slot.BookableStartUtc);
        Assert.Equal("booked", Assert.Single(slot.Occupied).Kind);
    }

    [RoomLockPostgresFact]
    public async Task Additional_receipts_settle_completed_debt_without_releasing_its_interval()
    {
        await using var db = Db();
        var (property, room) = await SeedAsync(db);
        var completed = await CompleteAsync(db, property, room, Start, Start.AddHours(3));
        var payments = new DeLong.Web.Features.Payments.PaymentService(db);
        var first = await payments.AddAsync(property.Id, completed.Id, new DeLong.Web.Features.Payments.CreatePaymentRequest
        { Type = PaymentType.Receipt, Method = PaymentMethod.Cash, Amount = 100000 }, null);
        Assert.Null(first.Error);
        Assert.Equal(150000, (await Bookings(db).GetAsync(property.Id, completed.Id))!.BalanceAmount);
        var partial = (await Availability(db).GetAdminAsync(property.Id, room.Id, Date, 1))!;
        Assert.Equal(150000, Assert.Single(Assert.Single(partial.Calendar[0].Slots).Occupied).BalanceAmount);
        var second = await payments.AddAsync(property.Id, completed.Id, new DeLong.Web.Features.Payments.CreatePaymentRequest
        { Type = PaymentType.Receipt, Method = PaymentMethod.BankTransfer, Amount = 150000 }, null);
        Assert.Null(second.Error);
        var settled = (await Bookings(db).GetAsync(property.Id, completed.Id))!;
        Assert.Equal(250000, settled.PaidAmount);
        Assert.Equal(0, settled.BalanceAmount);
        Assert.Equal(BookingStatus.Completed, settled.Status);
        var admin = (await Availability(db).GetAdminAsync(property.Id, room.Id, Date, 1))!;
        Assert.Equal(0, Assert.Single(Assert.Single(admin.Calendar[0].Slots).Occupied).BalanceAmount);
        var publicSlot = Assert.Single((await Availability(db).GetPublicAsync(property.SiteSlug, room.Id, Date, 1))!.Calendar[0].Slots);
        Assert.Null(publicSlot.BookableStartUtc);
        Assert.Equal(2, await db.Payments.CountAsync(x => x.BookingId == completed.Id));
    }

    [RoomLockPostgresFact]
    public async Task Cancellation_and_no_show_still_release_the_reserved_interval()
    {
        foreach (var status in new[] { BookingStatus.Cancelled, BookingStatus.NoShow })
        {
            await using var db = Db();
            var (property, room) = await SeedAsync(db);
            var service = Bookings(db);
            var created = await service.CreateAsync(property.Id, Order(room.Id, Start, Start.AddHours(3)));
            Assert.Null(created.Error);
            Assert.Null((await service.ChangeStatusAsync(property.Id, created.Booking!.Id, status)).Error);
            var slot = Assert.Single((await Availability(db).GetPublicAsync(property.SiteSlug, room.Id, Date, 1))!.Calendar[0].Slots);
            Assert.Empty(slot.Occupied);
            Assert.Equal(Start.UtcDateTime, slot.BookableStartUtc);
            Assert.Null((await service.CreateAsync(property.Id, Order(room.Id, Start, Start.AddHours(3)))).Error);
        }
    }
}
