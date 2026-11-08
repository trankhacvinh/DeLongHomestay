using System.Text.Json;
using DeLong.Web.Common.Auditing;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Bookings;
using DeLong.Web.Features.Customers;
using DeLong.Web.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeLong.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class BookingCreationOriginTests
{
    private static AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION")!).Options);

    private sealed record Fixture(Property Property, Room Room, Customer Customer, ApplicationUser Staff);

    private static async Task<Fixture> SeedAsync(AppDbContext db)
    {
        await db.Database.MigrateAsync();
        var key = Guid.NewGuid().ToString("N");
        var property = new Property { Code = key, Name = "Origin test", SiteSlug = key, TimeZoneId = "Asia/Ho_Chi_Minh", IsActive = true };
        var room = new Room { Property = property, Code = key, Name = "Blue", IsActive = true };
        var customer = new Customer { Property = property, Name = "Khách mẫu", Phone = "0900000000", NormalizedPhone = "0900000000" };
        var staff = new ApplicationUser { Id = Guid.NewGuid(), UserName = key, DisplayName = "Nhân viên thử" };
        db.AddRange(property, room, customer, staff);
        await db.SaveChangesAsync();
        return new(property, room, customer, staff);
    }

    private static BookingService Service(AppDbContext db) => new(db, new CustomerService(db), new AuditService(db));

    private static Booking RawBooking(Fixture f, string? source = null) => new()
    {
        PropertyId = f.Property.Id, RoomId = f.Room.Id, CustomerId = f.Customer.Id,
        Code = Guid.NewGuid().ToString("N")[..20], Type = BookingType.TimeSlot, Status = BookingStatus.Requested,
        CheckInUtc = DateTime.UtcNow.AddDays(30), CheckOutUtc = DateTime.UtcNow.AddDays(30).AddHours(3),
        RoomAmount = 250000, Source = source
    };

    [RoomLockPostgresFact]
    public async Task Customer_creation_survives_staff_source_changes_and_confirmation()
    {
        await using var db = Db();
        var f = await SeedAsync(db);
        var service = Service(db);
        var start = DateTimeOffset.UtcNow.AddDays(30);
        var created = await service.CreateAsync(f.Property.Id, new CreateBookingRequest
        {
            RoomId = f.Room.Id, CustomerId = f.Customer.Id, CustomerName = f.Customer.Name, CustomerPhone = f.Customer.Phone,
            CheckIn = start, CheckOut = start.AddHours(3), RoomAmount = 250000, Status = BookingStatus.Held,
            Source = "Website", PublicRequestKey = Guid.NewGuid().ToString("N")
        });
        Assert.Null(created.Error);
        Assert.Equal(BookingCreationOrigin.Customer, created.Booking!.CreationOrigin);

        var updated = await service.UpdateAsync(f.Property.Id, created.Booking.Id, new UpdateBookingRequest
        {
            RoomId = f.Room.Id, CustomerId = f.Customer.Id, CustomerName = f.Customer.Name, CustomerPhone = f.Customer.Phone,
            CheckIn = start, CheckOut = start.AddHours(3), RoomAmount = 250000, Source = "Zalo"
        }, f.Staff.Id);
        Assert.Null(updated.Error);
        Assert.Equal(BookingCreationOrigin.Customer, updated.Booking!.CreationOrigin);
        var confirmed = await service.ChangeStatusAsync(f.Property.Id, created.Booking.Id, BookingStatus.Confirmed, f.Staff.Id);
        Assert.Null(confirmed.Error);
        Assert.Equal(BookingCreationOrigin.Customer, confirmed.Booking!.CreationOrigin);

        db.ChangeTracker.Clear();
        var reloaded = Assert.Single(await service.GetAllAsync(f.Property.Id));
        Assert.Equal(BookingCreationOrigin.Customer, reloaded.CreationOrigin);
        Assert.Equal("Zalo", reloaded.Source);
        Assert.Equal(created.Booking.TotalAmount, reloaded.TotalAmount);
        var json = JsonSerializer.SerializeToElement(reloaded, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(1, json.GetProperty("creationOrigin").GetInt32());
    }

    [RoomLockPostgresFact]
    public async Task Staff_creator_takes_precedence_over_website_source()
    {
        await using var db = Db();
        var f = await SeedAsync(db);
        var service = Service(db);
        var start = DateTimeOffset.UtcNow.AddDays(30);
        var result = await service.CreateAsync(f.Property.Id, new CreateBookingRequest
        {
            RoomId = f.Room.Id, CustomerId = f.Customer.Id, CustomerName = f.Customer.Name, CustomerPhone = f.Customer.Phone,
            CheckIn = start, CheckOut = start.AddHours(3), RoomAmount = 250000, Source = "Website"
        }, f.Staff.Id);
        Assert.Null(result.Error);
        Assert.Equal(BookingCreationOrigin.Staff, result.Booking!.CreationOrigin);
        Assert.Equal(BookingCreationOrigin.Staff, (await service.GetAsync(f.Property.Id, result.Booking.Id))!.CreationOrigin);
    }

    [RoomLockPostgresFact]
    public async Task Staff_import_and_legacy_website_snapshot_are_resolved_from_creation_audit()
    {
        await using var db = Db();
        var f = await SeedAsync(db);
        var imported = RawBooking(f, "Excel · Facebook");
        var customer = RawBooking(f, "Điện thoại");
        db.AddRange(imported, customer);
        var audit = new AuditService(db);
        audit.Add(f.Property.Id, "Booking", imported.Id, "Imported", f.Staff.Id, after: new { ExcelRow = 2 });
        db.AuditLogs.Add(new AuditLog
        {
            PropertyId = f.Property.Id, EntityType = "Booking", EntityId = customer.Id, Action = "Created",
            AfterJson = "{\"Source\":\" website \"}"
        });
        await db.SaveChangesAsync();

        var bookings = (await Service(db).GetAllAsync(f.Property.Id)).ToDictionary(x => x.Id);
        Assert.Equal(BookingCreationOrigin.Staff, bookings[imported.Id].CreationOrigin);
        Assert.Equal(BookingCreationOrigin.Customer, bookings[customer.Id].CreationOrigin);
    }

    [RoomLockPostgresFact]
    public async Task Legacy_bookings_without_verified_creation_evidence_remain_unknown()
    {
        await using var db = Db();
        var f = await SeedAsync(db);
        var cases = new (string? Action, string? Snapshot)[]
        {
            (null, null), ("Updated", "{\"source\":\"Website\"}"), ("Imported", "{\"source\":\"Website\"}"),
            ("Created", null), ("Created", "[]"), ("Created", "{\"source\":{}}"), ("Created", "{\"source\":\"Zalo\"}")
        };
        foreach (var item in cases)
        {
            var booking = RawBooking(f, "Website");
            booking.PublicRequestKey = Guid.NewGuid().ToString("N");
            db.Add(booking);
            if (item.Action is not null) db.AuditLogs.Add(new AuditLog
            {
                PropertyId = f.Property.Id, EntityType = "Booking", EntityId = booking.Id,
                Action = item.Action, AfterJson = item.Snapshot
            });
        }
        await db.SaveChangesAsync();

        var bookings = await Service(db).GetAllAsync(f.Property.Id);
        Assert.Equal(cases.Length, bookings.Count);
        Assert.All(bookings, booking => Assert.Equal(BookingCreationOrigin.Unknown, booking.CreationOrigin));
    }

    [RoomLockPostgresFact]
    public async Task Origin_uses_first_creation_and_never_other_property_or_entity_audits()
    {
        await using var db = Db();
        var f = await SeedAsync(db);
        var other = await SeedAsync(db);
        var original = RawBooking(f);
        var unknown = RawBooking(f, "Website");
        db.AddRange(original, unknown);
        db.AuditLogs.AddRange(
            new AuditLog { PropertyId = f.Property.Id, EntityType = "Booking", EntityId = original.Id, Action = "Created", AfterJson = "{\"source\":\"Website\"}", CreatedAtUtc = DateTime.UtcNow.AddMinutes(-1) },
            new AuditLog { PropertyId = f.Property.Id, EntityType = "Booking", EntityId = original.Id, Action = "Created", ActorUserId = f.Staff.Id },
            new AuditLog { PropertyId = other.Property.Id, EntityType = "Booking", EntityId = unknown.Id, Action = "Created", ActorUserId = other.Staff.Id },
            new AuditLog { PropertyId = f.Property.Id, EntityType = "Customer", EntityId = unknown.Id, Action = "Created", ActorUserId = f.Staff.Id });
        await db.SaveChangesAsync();

        var service = Service(db);
        var bookings = (await service.GetAllAsync(f.Property.Id)).ToDictionary(x => x.Id);
        Assert.Equal(BookingCreationOrigin.Customer, bookings[original.Id].CreationOrigin);
        Assert.Equal(BookingCreationOrigin.Unknown, bookings[unknown.Id].CreationOrigin);
        Assert.Null(await service.GetAsync(other.Property.Id, original.Id));
        Assert.Empty(await service.GetAllAsync(Guid.NewGuid()));
    }
}
