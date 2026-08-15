using System.Security.Claims;
using DeLong.Web.Common.Security;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeLong.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class PropertyContextTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Current_property_defaults_to_oldest_accessible_property_and_honors_explicit_selection()
    {
        var connectionString = Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var userId = Guid.CreateVersion7();
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = $"property-context-{suffix}@example.test",
            NormalizedUserName = $"PROPERTY-CONTEXT-{suffix.ToUpperInvariant()}@EXAMPLE.TEST",
            Email = $"property-context-{suffix}@example.test",
            NormalizedEmail = $"PROPERTY-CONTEXT-{suffix.ToUpperInvariant()}@EXAMPLE.TEST",
            DisplayName = "Property Context Test",
            IsActive = true,
            SecurityStamp = Guid.NewGuid().ToString("N")
        };
        var first = new Property
        {
            Id = Guid.CreateVersion7(),
            Code = $"FIRST-{suffix}",
            Name = "First property",
            TimeZoneId = "Asia/Ho_Chi_Minh",
            IsActive = true,
            CreatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var second = new Property
        {
            Id = Guid.CreateVersion7(),
            Code = $"SECOND-{suffix}",
            Name = "A property that sorts earlier by name",
            TimeZoneId = "Asia/Ho_Chi_Minh",
            IsActive = true,
            CreatedAtUtc = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        db.Users.Add(user);
        db.Properties.AddRange(first, second);
        db.UserPropertyAccesses.AddRange(
            new UserPropertyAccess { UserId = userId, PropertyId = first.Id },
            new UserPropertyAccess { UserId = userId, PropertyId = second.Id });
        await db.SaveChangesAsync();

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
            "integration"));
        var service = new CurrentPropertyService(db);

        var defaultProperty = await service.ResolveAsync(principal);
        Assert.NotNull(defaultProperty);
        Assert.Equal(first.Id, defaultProperty!.Id);

        var selectedProperty = await service.ResolveAsync(principal, second.Id);
        Assert.NotNull(selectedProperty);
        Assert.Equal(second.Id, selectedProperty!.Id);

        var accessible = await service.GetAccessibleAsync(principal);
        Assert.Equal(new[] { first.Id, second.Id }, accessible.Select(x => x.Id));
    }
}
