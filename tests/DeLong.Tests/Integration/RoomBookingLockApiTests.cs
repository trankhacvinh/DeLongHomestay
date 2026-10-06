using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using DeLong.Web.Common.Security;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Features.Rooms;
using DeLong.Web.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DeLong.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class RoomBookingLockApiTests
{
    [RoomLockPostgresFact, Trait("Category", "Integration")]
    public async Task Real_endpoints_enforce_roles_branch_access_and_antiforgery()
    {
        var connection = Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION")!;
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();
        var key = Guid.NewGuid().ToString("N");
        var property = new Property { Code = key, Name = "API lock test" };
        var other = new Property { Code = key + "-other", Name = "Other branch" };
        var actor = new ApplicationUser { Id = Guid.NewGuid(), UserName = key, DisplayName = "Manager" };
        var room = new Room { Property = property, Code = "A", Name = "A" };
        var otherRoom = new Room { Property = other, Code = "B", Name = "B" };
        db.AddRange(property, other, actor, room, otherRoom,
            new UserPropertyAccess { Property = property, User = actor });
        await db.SaveChangesAsync();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddDbContext<AppDbContext>(x => x.UseNpgsql(connection));
        builder.Services.AddScoped<RoomService>();
        builder.Services.AddScoped<PropertyAccessService>();
        builder.Services.AddAntiforgery(x => x.HeaderName = "X-CSRF-TOKEN");
        builder.Services.AddAuthentication("FixtureCookie").AddCookie("FixtureCookie", x =>
        {
            x.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
            x.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
        });
        builder.Services.AddAuthorization(x =>
        {
            x.AddPolicy("ViewRooms", p => p.RequireRole("Admin", "Manager", "Staff", "Media", "Viewer"));
            x.AddPolicy("ManageRooms", p => p.RequireRole("Admin", "Manager"));
        });
        await using var app = builder.Build();
        app.UseAuthentication();
        // Test host identity only; all production endpoint policies and filters run unchanged.
        app.Use(async (context, next) =>
        {
            var role = context.Request.Headers["X-Fixture-Role"].ToString();
            if (!string.IsNullOrEmpty(role)) context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, actor.Id.ToString()), new Claim(ClaimTypes.Role, role)], "Fixture"));
            await next();
        });
        app.UseAuthorization();
        app.MapGet("/csrf", (HttpContext context, IAntiforgery antiforgery) =>
            Results.Ok(antiforgery.GetAndStoreTokens(context).RequestToken));
        app.MapRoomEndpoints();
        await app.StartAsync();
        using var client = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = false })
        { BaseAddress = new Uri(app.Urls.Single()) };
        async Task<HttpStatusCode> Lock(string role, Guid propertyId, Guid roomId, bool csrf = true)
        {
            client.DefaultRequestHeaders.Remove("X-Fixture-Role");
            if (role != "") client.DefaultRequestHeaders.Add("X-Fixture-Role", role);
            client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
            if (csrf)
            {
                var token = await client.GetFromJsonAsync<string>("/csrf");
                client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);
            }
            using var response = await client.PutAsJsonAsync($"/api/admin/properties/{propertyId}/rooms/{roomId}/booking-lock", new { isLocked = true, reason = "Bảo trì" });
            return response.StatusCode;
        }
        Assert.Equal(HttpStatusCode.Unauthorized, await Lock("", property.Id, room.Id, false));
        foreach (var role in new[] { "Staff", "Media", "Viewer" })
            Assert.Equal(HttpStatusCode.Forbidden, await Lock(role, property.Id, room.Id));
        Assert.Equal(HttpStatusCode.Forbidden, await Lock("Manager", other.Id, otherRoom.Id));
        Assert.Equal(HttpStatusCode.BadRequest, await Lock("Manager", property.Id, room.Id, false));
        foreach (var role in new[] { "Manager", "Admin" })
            Assert.Equal(HttpStatusCode.OK, await Lock(role, property.Id, room.Id));
        db.ChangeTracker.Clear();
        Assert.True((await db.Rooms.SingleAsync(x => x.Id == room.Id)).IsBookingLocked);
        Assert.False((await db.Rooms.SingleAsync(x => x.Id == otherRoom.Id)).IsBookingLocked);
        await app.StopAsync();
    }
}
