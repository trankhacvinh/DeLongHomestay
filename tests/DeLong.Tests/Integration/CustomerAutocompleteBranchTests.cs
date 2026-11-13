using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Features.Customers;
using DeLong.Web.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeLong.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class CustomerAutocompleteBranchTests
{
    [RoomLockPostgresFact]
    public async Task Staff_booking_form_finds_a_guest_from_another_branch_they_can_open_by_phone()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION")!).Options);
        await db.Database.MigrateAsync();
        var key = Guid.NewGuid().ToString("N")[..10];
        Property Branch(string name) => new() { Code = $"{name}-{key}", Name = $"{name} {key}", SiteSlug = $"{name.ToLowerInvariant()}-{key}", TimeZoneId = "Asia/Ho_Chi_Minh", IsActive = true };
        var here = Branch("Here");
        var other = Branch("LaRoman");
        var hidden = Branch("NoAccess");
        var phone = $"079{Random.Shared.Next(1000000, 9999999)}";
        var staff = new ApplicationUser { Id = Guid.NewGuid(), UserName = $"ac-{key}", DisplayName = "Staff" };
        db.AddRange(here, other, hidden, staff,
            new Customer { PropertyId = other.Id, Name = "Khách La Roman", Phone = $"+84 {phone[1..]}", NormalizedPhone = phone, Email = "old@example.test", IsActive = true },
            new Customer { PropertyId = hidden.Id, Name = "Khách không có quyền", Phone = phone, NormalizedPhone = phone, IsActive = true },
            new UserPropertyAccess { PropertyId = here.Id, UserId = staff.Id },
            new UserPropertyAccess { PropertyId = other.Id, UserId = staff.Id });
        await db.SaveChangesAsync();
        var service = new CustomerService(db);

        var found = Assert.Single(await service.AutocompleteAsync(here.Id, staff.Id, phone));
        Assert.Null(found.Id);
        Assert.Equal("Khách La Roman", found.Name);
        Assert.Equal(other.Name, found.PropertyName);
        Assert.Equal("old@example.test", found.Email);

        // Once the guest is a customer of this branch, only that local profile is offered.
        var local = new Customer { PropertyId = here.Id, Name = "Khách ở đây", Phone = phone, NormalizedPhone = phone, IsActive = true };
        db.Add(local);
        await db.SaveChangesAsync();
        var localOnly = Assert.Single(await service.AutocompleteAsync(here.Id, staff.Id, phone));
        Assert.Equal(local.Id, localOnly.Id);
        Assert.Null(localOnly.PropertyName);
    }
}
