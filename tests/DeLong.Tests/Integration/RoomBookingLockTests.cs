using System.Text.Json;
using DeLong.Web.Common.Auditing;
using DeLong.Web.Common.Operations;
using DeLong.Web.Features.Operations;
using DeLong.Web.Features.Site;
using DeLong.Web.Features.Pricing;
using DeLong.Web.Common.Caching;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Bookings;
using DeLong.Web.Features.Customers;
using DeLong.Web.Features.Payments;
using DeLong.Web.Features.PublicBooking;
using DeLong.Web.Features.PublicRooms;
using DeLong.Web.Features.Rooms;
using DeLong.Web.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace DeLong.Tests.Integration;

public sealed class RoomLockPostgresFactAttribute : FactAttribute
{
    public RoomLockPostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION")))
            Skip = "DELONG_TEST_CONNECTION is required for real PostgreSQL verification.";
    }
}

[Collection("PostgreSQL integration")]
public sealed class RoomBookingLockTests
{
    private static AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION")!).Options);

    private static BookingService Bookings(AppDbContext db) => new(db, new CustomerService(db), new AuditService(db));

    private static async Task<(Property Property, Room Room, ApplicationUser Actor)> SeedAsync(AppDbContext db)
    {
        await db.Database.MigrateAsync();
        var key = Guid.NewGuid().ToString("N");
        var property = new Property { Code = key, Name = "Room lock test", SiteSlug = key, IsActive = true };
        var room = new Room { Property = property, Code = "BLUE", Name = "Blue", IsActive = true, IsPublished = true };
        room.Rates.Add(new RoomRate { Name = "Khung chiều", StartTime = new(14, 0), EndTime = new(17, 0), Price = 250000, Type = RoomRateType.TimeSlot, IsActive = true });
        room.Rates.Add(new RoomRate { Name = "Theo đêm", StartTime = new(14, 0), EndTime = new(12, 0), Price = 500000, Type = RoomRateType.Nightly, IsActive = true });
        var actor = new ApplicationUser { Id = Guid.NewGuid(), UserName = key, DisplayName = "Quản lý thử nghiệm" };
        db.AddRange(property, room, actor);
        await db.SaveChangesAsync();
        return (property, room, actor);
    }

    private static CreateBookingRequest Request(Guid roomId, int day = 10) => new()
    {
        RoomId = roomId, CustomerName = "Guest", CustomerPhone = "0935527193",
        CheckIn = new DateTimeOffset(2030, 1, day, 7, 0, 0, TimeSpan.Zero),
        CheckOut = new DateTimeOffset(2030, 1, day, 10, 0, 0, TimeSpan.Zero),
        RoomAmount = 250000, Status = BookingStatus.Held
    };

    [RoomLockPostgresFact, Trait("Category", "Integration")]
    public async Task Lock_blocks_new_orders_preserves_old_QR_and_unlock_restores_booking_and_cache()
    {
        await using var db = Db();
        var (property, room, actor) = await SeedAsync(db);
        using var cache = new FusionCache(new FusionCacheOptions());
        var catalog = new PublicRoomContentService(db, cache);
        Assert.False((await catalog.GetCatalogAsync(property.Id)).Rooms.Single().IsBookingLocked);
        var bookings = Bookings(db);
        var created = await bookings.CreateAsync(property.Id, Request(room.Id));
        Assert.Null(created.Error);
        var original = created.Booking!;
        var intent = new Pay2SPaymentIntent
        {
            PropertyId = property.Id, BookingId = original.Id, OrderId = Guid.NewGuid().ToString("N"),
            RequestId = Guid.NewGuid().ToString("N"), Provider = PaymentMethod.SePay, Amount = 250000, Status = Pay2SPaymentIntentStatus.Pending,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5), ReleaseAtUtc = DateTime.UtcNow.AddMinutes(8),
            SePayQrUrl = "https://vietqr.app?acc=123&bank=VCB&des=DH1234567890"
        };
        db.Add(intent);
        await db.SaveChangesAsync();
        var rooms = new RoomService(db, cache);
        var locked = await rooms.SetBookingLockAsync(property.Id, room.Id, new(true, "  Bảo trì điều hòa  "), actor.Id);
        Assert.Null(locked.Error);
        Assert.True(locked.Room!.IsBookingLocked);
        Assert.Equal("Bảo trì điều hòa", locked.Room.BookingLockReason);
        Assert.Equal(actor.DisplayName, locked.Room.BookingLockedByName);
        Assert.True((await catalog.GetCatalogAsync(property.Id)).Rooms.Single().IsBookingLocked);
        var blocked = await bookings.CreateAsync(property.Id, Request(room.Id, 11));
        Assert.Equal("room_booking_locked", blocked.Error?.Code);
        Assert.Equal(original, await bookings.GetAsync(property.Id, original.Id));
        Assert.NotNull((await SePayPaymentState.LoadAsync(db, intent.OrderId, default))!.QrUrl);
        Assert.Single(await db.Bookings.Where(x => x.PropertyId == property.Id).ToListAsync());
        Assert.Null((await rooms.SetBookingLockAsync(property.Id, room.Id, new(false, null), actor.Id)).Error);
        Assert.False((await catalog.GetCatalogAsync(property.Id)).Rooms.Single().IsBookingLocked);
        Assert.Null((await bookings.CreateAsync(property.Id, Request(room.Id, 11))).Error);
        var actions = await db.AuditLogs.Where(x => x.EntityId == room.Id).OrderBy(x => x.CreatedAtUtc).Select(x => x.Action).ToListAsync();
        Assert.Equal(new[] { "BookingLocked", "BookingUnlocked" }, actions);
    }

    [RoomLockPostgresFact, Trait("Category", "Integration")]
    public async Task Public_requests_and_availability_block_time_slots_and_nights_without_exposing_reason()
    {
        await using var db = Db();
        var (property, room, actor) = await SeedAsync(db);
        Assert.Null((await new RoomService(db).SetBookingLockAsync(property.Id, room.Id, new(true, "Lý do riêng tư"), actor.Id)).Error);
        var service = new PublicBookingService(db, Bookings(db));
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3));
        var catalog = await service.GetCatalogAsync(property.SiteSlug, date);
        Assert.True(catalog!.Rooms.Single().IsBookingLocked);
        Assert.All(catalog.Rooms.Single().Rates, rate => Assert.False(rate.Available));
        Assert.DoesNotContain("Lý do riêng tư", JsonSerializer.Serialize(catalog));
        var temp = Path.Combine(Path.GetTempPath(), "room-lock-" + Guid.NewGuid());
        var paths = new StoragePaths(temp, Path.Combine(temp, "media"), "/uploads/rooms", true, true, false);
        var availability = new AvailabilityIntervalService(db, new PublicPropertyResolver(db), paths, new PricingService(db, new AuditService(db)));
        var publicCalendar = await availability.GetPublicAsync(property.SiteSlug, room.Id, date, 1);
        Assert.True(publicCalendar!.IsBookingLocked);
        Assert.All(publicCalendar.Calendar.SelectMany(x => x.Slots), slot => Assert.Null(slot.BookableStartUtc));
        Assert.DoesNotContain("Lý do riêng tư", JsonSerializer.Serialize(publicCalendar));
        var adminCalendar = await availability.GetAdminAsync(property.Id, room.Id, date, 1);
        Assert.True(adminCalendar!.IsBookingLocked);
        Assert.Equal("Lý do riêng tư", adminCalendar.BookingLockReason);
        if (Directory.Exists(temp)) Directory.Delete(temp, true);
        foreach (var type in new[] { BookingType.TimeSlot, BookingType.MultiDay })
        {
            var rate = room.Rates.Single(x => (x.Type == RoomRateType.Nightly) == (type == BookingType.MultiDay));
            var result = await service.CreateRequestAsync(property.SiteSlug, new PublicBookingRequest
            {
                RoomId = room.Id, RateId = rate.Id, Type = type, StayDate = date.ToString("yyyy-MM-dd"),
                CheckInDate = date.ToString("yyyy-MM-dd"), CheckOutDate = date.AddDays(1).ToString("yyyy-MM-dd"),
                CustomerName = "Guest", CustomerPhone = "0935527193"
            });
            Assert.Equal("room_booking_locked", result.Error?.Code);
        }
        var stay = await service.GetStayAvailabilityAsync(property.SiteSlug, date, date.AddDays(1));
        Assert.True(stay.Availability!.Rooms.Single().IsBookingLocked);
        Assert.False(stay.Availability!.Rooms.Single().Available);
    }

    [RoomLockPostgresFact, Trait("Category", "Integration")]
    public async Task Existing_booking_can_be_edited_but_moves_into_locked_room_fail()
    {
        await using var db = Db();
        var (property, room, actor) = await SeedAsync(db);
        var other = new Room { PropertyId = property.Id, Code = "OTHER", Name = "Other", IsActive = true };
        db.Add(other); await db.SaveChangesAsync();
        var service = Bookings(db);
        var old = (await service.CreateAsync(property.Id, Request(room.Id))).Booking!;
        var second = (await service.CreateAsync(property.Id, Request(other.Id, 11))).Booking!;
        await new RoomService(db).SetBookingLockAsync(property.Id, room.Id, new(true, "Sửa phòng"), actor.Id);
        UpdateBookingRequest Update(BookingDto booking, Guid roomId) => new()
        {
            RoomId = roomId, CustomerId = booking.CustomerId, CustomerName = booking.CustomerName,
            CustomerPhone = booking.CustomerPhone, CheckIn = new(booking.CheckInUtc, TimeSpan.Zero),
            CheckOut = new(booking.CheckOutUtc, TimeSpan.Zero), RoomAmount = booking.RoomAmount, Note = "Ghi chú mới"
        };
        Assert.Null((await service.UpdateAsync(property.Id, old.Id, Update(old, room.Id))).Error);
        Assert.Equal("room_booking_locked", (await service.UpdateAsync(property.Id, second.Id, Update(second, room.Id))).Error?.Code);
        var moves = new BookingMoveService(db, new AuditService(db), service);
        Assert.Null((await moves.MoveAsync(property.Id, old.Id, new() { RoomId = room.Id, TargetDate = new(2030, 1, 12) })).Error);
        Assert.Equal("room_booking_locked", (await moves.MoveAsync(property.Id, second.Id, new() { RoomId = room.Id, TargetDate = new(2030, 1, 13) })).Error?.Code);
    }

    [RoomLockPostgresFact, Trait("Category", "Integration")]
    public async Task Reasons_and_property_scope_are_validated_and_repeated_lock_is_idempotent()
    {
        await using var db = Db();
        var (property, room, actor) = await SeedAsync(db);
        var service = new RoomService(db);
        foreach (var reason in new[] { " ", new string('x', 501) })
            Assert.NotNull((await service.SetBookingLockAsync(property.Id, room.Id, new(true, reason), actor.Id)).Error);
        Assert.NotNull((await service.SetBookingLockAsync(Guid.NewGuid(), room.Id, new(true, "Bảo trì"), actor.Id)).Error);
        Assert.False((await service.GetAsync(property.Id, room.Id))!.IsBookingLocked);
        await service.SetBookingLockAsync(property.Id, room.Id, new(true, "Bảo trì"), actor.Id);
        await service.SetBookingLockAsync(property.Id, room.Id, new(true, "Bảo trì"), actor.Id);
        Assert.Single(await db.AuditLogs.Where(x => x.EntityId == room.Id).ToListAsync());
    }

    [RoomLockPostgresFact, Trait("Category", "Integration")]
    public async Task Booking_admitted_first_commits_before_lock_and_is_preserved()
    {
        await using var db = Db();
        var (property, room, actor) = await SeedAsync(db);
        await using var transaction = await db.Database.BeginTransactionAsync();
        var created = await Bookings(db).CreateAsync(property.Id, Request(room.Id));
        Assert.Null(created.Error);
        await using var lockingDb = Db();
        var locking = new RoomService(lockingDb).SetBookingLockAsync(property.Id, room.Id, new(true, "Bảo trì"), actor.Id);
        Assert.NotSame(locking, await Task.WhenAny(locking, Task.Delay(200)));
        await transaction.CommitAsync();
        await transaction.DisposeAsync();
        Assert.Null((await locking.WaitAsync(TimeSpan.FromSeconds(10))).Error);
        Assert.NotNull(await Bookings(lockingDb).GetAsync(property.Id, created.Booking!.Id));
    }

    [RoomLockPostgresFact, Trait("Category", "Integration")]
    public async Task Lock_admitted_first_prevents_concurrent_new_booking()
    {
        await using var db = Db();
        var (property, room, actor) = await SeedAsync(db);
        await using var transaction = await db.Database.BeginTransactionAsync();
        Assert.Null((await new RoomService(db).SetBookingLockAsync(property.Id, room.Id, new(true, "Bảo trì"), actor.Id)).Error);
        await using var bookingDb = Db();
        var creating = Bookings(bookingDb).CreateAsync(property.Id, Request(room.Id));
        Assert.NotSame(creating, await Task.WhenAny(creating, Task.Delay(200)));
        await transaction.CommitAsync();
        await transaction.DisposeAsync();
        Assert.Equal("room_booking_locked", (await creating.WaitAsync(TimeSpan.FromSeconds(10))).Error?.Code);
        Assert.Empty(await bookingDb.Bookings.Where(x => x.PropertyId == property.Id).ToListAsync());
    }
}
