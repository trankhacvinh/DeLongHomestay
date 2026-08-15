using System.Security.Claims;
using DeLong.Web.Common.Security;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeLong.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class PropertyContextTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Multiple_properties_require_selection_and_remember_valid_working_property()
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
            Name = "Alpha property",
            TimeZoneId = "Asia/Ho_Chi_Minh",
            IsActive = true
        };
        var second = new Property
        {
            Id = Guid.CreateVersion7(),
            Code = $"SECOND-{suffix}",
            Name = "Beta property",
            TimeZoneId = "Asia/Ho_Chi_Minh",
            IsActive = true
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
        var http = new DefaultHttpContext { User = principal };
        var accessor = new HttpContextAccessor { HttpContext = http };
        var service = new CurrentPropertyService(db, accessor);

        // Two accessible properties are ambiguous: never silently select the first one.
        Assert.Null(await service.ResolveAsync(principal));

        var selected = await service.ResolveAsync(principal, second.Id);
        Assert.NotNull(selected);
        Assert.Equal(second.Id, selected!.Id);
        Assert.Contains(CurrentPropertyService.WorkingPropertyCookieName, http.Response.Headers.SetCookie.ToString());

        // Simulate the next request carrying the remembered working-property cookie.
        var nextHttp = new DefaultHttpContext { User = principal };
        nextHttp.Request.Headers.Cookie = $"{CurrentPropertyService.WorkingPropertyCookieName}={second.Id}";
        var rememberedService = new CurrentPropertyService(db, new HttpContextAccessor { HttpContext = nextHttp });
        var remembered = await rememberedService.ResolveAsync(principal);
        Assert.NotNull(remembered);
        Assert.Equal(second.Id, remembered!.Id);

        var accessible = await rememberedService.GetAccessibleAsync(principal);
        Assert.Equal(new[] { first.Id, second.Id }, accessible.Select(x => x.Id));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Single_accessible_property_is_selected_automatically()
    {
        var connectionString = Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var userId = Guid.CreateVersion7();
        var propertyId = Guid.CreateVersion7();
        db.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = $"single-property-{suffix}@example.test",
            NormalizedUserName = $"SINGLE-PROPERTY-{suffix.ToUpperInvariant()}@EXAMPLE.TEST",
            Email = $"single-property-{suffix}@example.test",
            NormalizedEmail = $"SINGLE-PROPERTY-{suffix.ToUpperInvariant()}@EXAMPLE.TEST",
            DisplayName = "Single Property Test",
            IsActive = true,
            SecurityStamp = Guid.NewGuid().ToString("N")
        });
        db.Properties.Add(new Property
        {
            Id = propertyId,
            Code = $"ONLY-{suffix}",
            Name = "Only property",
            TimeZoneId = "Asia/Ho_Chi_Minh",
            IsActive = true
        });
        db.UserPropertyAccesses.Add(new UserPropertyAccess { UserId = userId, PropertyId = propertyId });
        await db.SaveChangesAsync();

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
            "integration"));
        var http = new DefaultHttpContext { User = principal };
        var service = new CurrentPropertyService(db, new HttpContextAccessor { HttpContext = http });

        var current = await service.ResolveAsync(principal);
        Assert.NotNull(current);
        Assert.Equal(propertyId, current!.Id);
    }
}
