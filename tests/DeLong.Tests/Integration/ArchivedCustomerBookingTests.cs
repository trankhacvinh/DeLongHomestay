using System.Text.Json;
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
public sealed class ArchivedCustomerBookingTests
{
    [RoomLockPostgresFact]
    public async Task Staff_can_reuse_and_edit_hidden_customer_without_reactivating_or_duplicating_profile()
    {
        await using var db = Database(); await db.Database.MigrateAsync();
        var property = new Property { Code = Guid.NewGuid().ToString("N"), Name = "Synthetic archive test" };
        var room = new Room { Property = property, Name = "Synthetic", Code = "R", IsActive = true };
        var customer = new Customer { Property = property, Name = "Synthetic", Phone = "0900000123", NormalizedPhone = "0900000123", IsActive = false };
        db.AddRange(room, customer); await db.SaveChangesAsync();
        var customers = new CustomerService(db);
        Assert.Equal(customer.Id, (await customers.FindOrCreateEntityAsync(property.Id, null, "Synthetic", "+84 900 000 123"))!.Id);
        Assert.Null(await customers.FindOrCreateEntityAsync(Guid.NewGuid(), customer.Id, "Synthetic", customer.Phone));
        var bookings = new BookingService(db, customers, new AuditService(db));
        var start = new DateTimeOffset(DateTime.UtcNow.AddDays(10));
        var created = await bookings.CreateAsync(property.Id, new CreateBookingRequest { CustomerId = customer.Id, RoomId = room.Id,
            CustomerName = customer.Name, CustomerPhone = customer.Phone, CheckIn = start, CheckOut = start.AddHours(3), RoomAmount = 210000, Status = BookingStatus.Confirmed });
        Assert.Null(created.Error);
        var updated = await bookings.UpdateAsync(property.Id, created.Booking!.Id, new UpdateBookingRequest { CustomerId = customer.Id, RoomId = room.Id,
            CustomerName = customer.Name, CustomerPhone = customer.Phone, CheckIn = start, CheckOut = start.AddHours(3), RoomAmount = 210000, Note = "Synthetic note" });
        Assert.Null(updated.Error); Assert.Equal(customer.Id, updated.Booking!.CustomerId);
        Assert.False((await db.Customers.AsNoTracking().SingleAsync(x => x.Id == customer.Id)).IsActive);
        Assert.Equal(1, await db.Customers.CountAsync(x => x.PropertyId == property.Id));
        customer.IsBlacklisted = true; customer.IsBlocked = true; customer.BlacklistReason = "Synthetic manual block";
        await db.SaveChangesAsync();
        var blocked = await bookings.CreateAsync(property.Id, new CreateBookingRequest { CustomerId = customer.Id, RoomId = room.Id,
            CustomerName = customer.Name, CustomerPhone = customer.Phone, CheckIn = start.AddDays(1), CheckOut = start.AddDays(1).AddHours(3), RoomAmount = 210000, Status = BookingStatus.Confirmed });
        Assert.Equal("customer_blocked", blocked.Error?.Code);
    }

    [RoomLockPostgresFact]
    public async Task Omitted_visibility_field_does_not_silently_hide_customer()
    {
        await using var db = Database(); await db.Database.MigrateAsync();
        var property = new Property { Code = Guid.NewGuid().ToString("N"), Name = "Synthetic" };
        var customer = new Customer { Property = property, Name = "Synthetic", Phone = "0900000123", NormalizedPhone = "0900000123", IsActive = true };
        db.Add(customer); await db.SaveChangesAsync();
        var request = JsonSerializer.Deserialize<UpdateCustomerRequest>("""{"name":"Synthetic","phone":"0900000123","isBlocked":false,"isBlacklisted":false}""", new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Null(request.IsActive);
        var service = new CustomerService(db);
        Assert.Null((await service.UpdateAsync(property.Id, customer.Id, request)).Error);
        Assert.True(customer.IsActive);
        Assert.Null((await service.UpdateAsync(property.Id, customer.Id, request with { IsActive = false })).Error);
        Assert.False(customer.IsActive);
        Assert.Null((await service.UpdateAsync(property.Id, customer.Id, request)).Error);
        Assert.False(customer.IsActive);
    }

    private static AppDbContext Database() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION")!).Options);
}
