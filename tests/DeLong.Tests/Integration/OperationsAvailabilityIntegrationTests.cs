using System.Text.Json;
using DeLong.Web.Common.Operations;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Operations;
using DeLong.Web.Features.PublicBooking;
using DeLong.Web.Features.Site;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeLong.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class OperationsAvailabilityIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Availability_projects_real_bookings_and_public_payload_does_not_leak_booking_or_guest_data()
    {
        var connectionString = Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();

        var suffix = Guid.NewGuid().ToString("N")[..10];
        var property = new Property
        {
            Code = $"OPAV-{suffix}",
            Name = "Operations Availability Integration",
            SiteSlug = $"opav-{suffix}",
            TimeZoneId = "Asia/Ho_Chi_Minh",
            IsActive = true
        };
        var room = new Room
        {
            PropertyId = property.Id,
            Code = $"R-{suffix}",
            Name = "Interval Test Room",
            Slug = $"interval-{suffix}",
            Capacity = 4,
            IsActive = true,
            IsPublished = true
        };
        var afternoonRate = new RoomRate
        {
            RoomId = room.Id,
            Name = "Khung 12-15",
            StartTime = new TimeOnly(12, 0),
            EndTime = new TimeOnly(15, 0),
            Type = RoomRateType.TimeSlot,
            Price = 300_000m,
            SortOrder = 1,
            IsActive = true
        };
        var overnightRate = new RoomRate
        {
            RoomId = room.Id,
            Name = "Qua đêm 21-10",
            StartTime = new TimeOnly(21, 0),
            EndTime = new TimeOnly(10, 0),
            Type = RoomRateType.Overnight,
            IsOvernight = true,
            Price = 650_000m,
            SortOrder = 2,
            IsActive = true
        };
        var customer = new Customer
        {
            PropertyId = property.Id,
            Name = "PRIVATE-GUEST-NAME",
            Phone = "0909999888",
            NormalizedPhone = "0909999888",
            Email = "private-availability@example.test",
            IdentityNumber = "PRIVATE-CCCD-012345678901",
            IsActive = true
        };

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(property.TimeZoneId);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone));
        var targetDate = today.AddDays(8);

        var afternoonBooking = new Booking
        {
            PropertyId = property.Id,
            RoomId = room.Id,
            CustomerId = customer.Id,
            RoomRateId = afternoonRate.Id,
            Code = $"AV-A-{suffix}",
            Type = BookingType.TimeSlot,
            CheckInUtc = ToUtc(targetDate, new TimeOnly(12, 0), timeZone),
            CheckOutUtc = ToUtc(targetDate, new TimeOnly(14, 0), timeZone),
            Status = BookingStatus.Held,
            RateName = afternoonRate.Name,
            UnitPrice = afternoonRate.Price,
            RoomAmount = afternoonRate.Price,
            Source = "Integration"
        };
        var overnightBooking = new Booking
        {
            PropertyId = property.Id,
            RoomId = room.Id,
            CustomerId = customer.Id,
            RoomRateId = overnightRate.Id,
            Code = $"AV-N-{suffix}",
            Type = BookingType.TimeSlot,
            CheckInUtc = ToUtc(targetDate, new TimeOnly(23, 0), timeZone),
            CheckOutUtc = ToUtc(targetDate.AddDays(1), new TimeOnly(2, 0), timeZone),
            Status = BookingStatus.Confirmed,
            RateName = overnightRate.Name,
            UnitPrice = overnightRate.Price,
            RoomAmount = overnightRate.Price,
            Source = "Integration"
        };

        db.Properties.Add(property);
        db.Rooms.Add(room);
        db.RoomRates.AddRange(afternoonRate, overnightRate);
        db.Customers.Add(customer);
        db.Bookings.AddRange(afternoonBooking, overnightBooking);
        await db.SaveChangesAsync();

        var tempRoot = Path.Combine(Path.GetTempPath(), $"delong-availability-{Guid.NewGuid():N}");
        var paths = TestStoragePaths(tempRoot);
        try
        {
            var service = new AvailabilityIntervalService(db, new PublicPropertyResolver(db), paths);

            var admin = await service.GetAdminAsync(property.Id, room.Id, targetDate, 1);
            Assert.NotNull(admin);
            var adminDay = Assert.Single(admin!.Calendar);

            var afternoon = Assert.Single(adminDay.Slots.Where(x => x.RateId == afternoonRate.Id));
            Assert.Equal("partial", afternoon.State);
            Assert.Equal(0.6667d, afternoon.OccupiedRatio, 4);
            var afternoonOccupied = Assert.Single(afternoon.Occupied);
            Assert.Equal(afternoonBooking.Id, afternoonOccupied.BookingId);
            Assert.Equal(BookingStatus.Held, afternoonOccupied.Status);
            var afternoonFree = Assert.Single(afternoon.Free);
            Assert.Equal(ToUtc(targetDate, new TimeOnly(14, 0), timeZone), afternoonFree.StartUtc);
            Assert.Equal(ToUtc(targetDate, new TimeOnly(15, 0), timeZone), afternoonFree.EndUtc);

            var overnight = Assert.Single(adminDay.Slots.Where(x => x.RateId == overnightRate.Id));
            Assert.Equal("partial", overnight.State);
            var overnightOccupied = Assert.Single(overnight.Occupied);
            Assert.Equal(overnightBooking.Id, overnightOccupied.BookingId);
            Assert.Equal(BookingStatus.Confirmed, overnightOccupied.Status);
            Assert.Equal(2, overnight.Free.Count);
            Assert.Equal(ToUtc(targetDate, new TimeOnly(21, 0), timeZone), overnight.Free[0].StartUtc);
            Assert.Equal(ToUtc(targetDate, new TimeOnly(23, 0), timeZone), overnight.Free[0].EndUtc);
            Assert.Equal(ToUtc(targetDate.AddDays(1), new TimeOnly(2, 0), timeZone), overnight.Free[1].StartUtc);
            Assert.Equal(ToUtc(targetDate.AddDays(1), new TimeOnly(10, 0), timeZone), overnight.Free[1].EndUtc);

            var publicAvailability = await service.GetPublicAsync(property.SiteSlug, room.Id, targetDate, 1);
            Assert.NotNull(publicAvailability);
            var publicDay = Assert.Single(publicAvailability!.Calendar);
            var publicAfternoon = Assert.Single(publicDay.Slots.Where(x => x.RateId == afternoonRate.Id));
            Assert.Equal("held", Assert.Single(publicAfternoon.Occupied).Kind);
            var publicOvernight = Assert.Single(publicDay.Slots.Where(x => x.RateId == overnightRate.Id));
            Assert.Equal("booked", Assert.Single(publicOvernight.Occupied).Kind);

            var publicJson = JsonSerializer.Serialize(publicAvailability, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            Assert.DoesNotContain("bookingId", publicJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(afternoonBooking.Id.ToString(), publicJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(overnightBooking.Id.ToString(), publicJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(customer.Name, publicJson, StringComparison.Ordinal);
            Assert.DoesNotContain(customer.Phone, publicJson, StringComparison.Ordinal);
            Assert.DoesNotContain(customer.Email!, publicJson, StringComparison.Ordinal);
            Assert.DoesNotContain(customer.IdentityNumber!, publicJson, StringComparison.Ordinal);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Hold_marker_survives_requested_to_held_transition_then_expiry_releases_room_and_emits_event()
    {
        var connectionString = Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();

        var suffix = Guid.NewGuid().ToString("N")[..10];
        var property = new Property
        {
            Code = $"HOLD-{suffix}",
            Name = "Hold Expiry Integration",
            SiteSlug = $"hold-{suffix}",
            TimeZoneId = "Asia/Ho_Chi_Minh",
            IsActive = true
        };
        var room = new Room
        {
            PropertyId = property.Id,
            Code = $"HR-{suffix}",
            Name = "Hold Test Room",
            Slug = $"hold-room-{suffix}",
            Capacity = 2,
            IsActive = true,
            IsPublished = true
        };
        var customer = new Customer
        {
            PropertyId = property.Id,
            Name = "HOLD-PRIVATE-GUEST",
            Phone = "0911111222",
            NormalizedPhone = "0911111222",
            Email = "hold-private@example.test",
            IsActive = true
        };
        var booking = new Booking
        {
            PropertyId = property.Id,
            RoomId = room.Id,
            CustomerId = customer.Id,
            Code = $"HOLD-B-{suffix}",
            Type = BookingType.TimeSlot,
            CheckInUtc = DateTime.UtcNow.AddDays(3),
            CheckOutUtc = DateTime.UtcNow.AddDays(3).AddHours(2),
            Status = BookingStatus.Requested,
            RoomAmount = 200_000m,
            Source = "Website"
        };

        db.Properties.Add(property);
        db.Rooms.Add(room);
        db.Customers.Add(customer);
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();

        var tempRoot = Path.Combine(Path.GetTempPath(), $"delong-hold-{Guid.NewGuid():N}");
        var paths = TestStoragePaths(tempRoot);
        var store = new PublicBookingHoldStore(paths);
        var markerPath = Path.Combine(
            paths.DataRoot,
            "booking-holds",
            property.Id.ToString("N"),
            booking.Id.ToString("N") + ".json");

        try
        {
            await store.StartAsync(property.Id, booking.Id, TimeSpan.FromMinutes(1));
            Assert.True(File.Exists(markerPath));

            // This simulates the intentional short window after StartAsync has persisted the marker
            // but before the booking status changes from Requested to Held.
            await store.ReleaseExpiredAsync(db, property.Id);
            Assert.True(File.Exists(markerPath));
            Assert.Equal(BookingStatus.Requested, booking.Status);

            booking.Status = BookingStatus.Held;
            await db.SaveChangesAsync();

            // Overwrite the same marker with an already-expired deadline so the test is deterministic.
            await store.StartAsync(property.Id, booking.Id, TimeSpan.FromSeconds(-1));
            using var subscription = OperationsRealtimeBroker.Shared.Subscribe(property.Id);

            await store.ReleaseExpiredAsync(db, property.Id);
            await db.Entry(booking).ReloadAsync();

            Assert.Equal(BookingStatus.Requested, booking.Status);
            Assert.False(File.Exists(markerPath));

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var evt = await subscription.Reader.ReadAsync(timeout.Token);
            Assert.Equal(OperationsEventTypes.BookingHoldExpired, evt.Type);
            Assert.Equal(property.Id, evt.PropertyId);
            Assert.Equal(booking.Id, evt.BookingId);
            Assert.Equal(room.Id, evt.RoomId);

            var eventJson = JsonSerializer.Serialize(evt, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            Assert.DoesNotContain(customer.Name, eventJson, StringComparison.Ordinal);
            Assert.DoesNotContain(customer.Phone, eventJson, StringComparison.Ordinal);
            Assert.DoesNotContain(customer.Email!, eventJson, StringComparison.Ordinal);
            Assert.DoesNotContain("customer", eventJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("phone", eventJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("email", eventJson, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static DateTime ToUtc(DateOnly date, TimeOnly time, TimeZoneInfo timeZone) =>
        TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Unspecified),
            timeZone);

    private static StoragePaths TestStoragePaths(string root) => new(
        root,
        Path.Combine(root, "media"),
        new PathString("/uploads/rooms"),
        true,
        true,
        false);

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
