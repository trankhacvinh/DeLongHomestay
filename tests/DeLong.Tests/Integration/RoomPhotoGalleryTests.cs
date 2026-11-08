using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using DeLong.Web.Common.Security;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Features.PublicRooms;
using DeLong.Web.Features.Rooms;
using DeLong.Web.Features.Site;
using DeLong.Web.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DeLong.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class RoomPhotoGalleryTests
{
    [RoomLockPostgresFact]
    public async Task Gallery_endpoints_enforce_publishing_roles_and_property_scope_without_exposing_internal_content()
    {
        var connection = Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION")!;
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
        await db.Database.MigrateAsync();
        var key = Guid.NewGuid().ToString("N")[..12];
        var property = new Property { Code = "PH-" + key, Name = "Main property", SiteSlug = "photos-" + key, IsActive = true };
        var other = new Property { Code = "PH2-" + key, Name = "Other property", SiteSlug = "other-" + key, IsActive = true };
        var staff = new ApplicationUser { Id = Guid.NewGuid(), UserName = key };
        var room = new Room { Property = property, Code = "BLUE", Name = "Blue", IsActive = true, IsPublished = true, GuestGuideHtml = "PRIVATE GUIDE" };
        var draft = new Room { Property = property, Code = "DRAFT", Name = "Draft", IsActive = true, IsPublished = false };
        var foreign = new Room { Property = other, Code = "OTHER", Name = "Other", IsActive = true, IsPublished = true };
        var empty = new Room { Property = property, Code = "EMPTY", Name = "Empty", IsActive = true, IsPublished = true };
        RoomImage Photo(string name, bool cover, int order) => new()
        {
            Room = room, OriginalFileName = name + ".jpg", OriginalStoragePath = "/private-original/" + name,
            ThumbnailPath = "/thumb/" + name, CardPath = "/card/" + name, LargePath = "/large/" + name,
            IsCover = cover, SortOrder = order, ContentType = "image/webp"
        };
        db.AddRange(property, other, staff, room, draft, foreign, empty,
            new UserPropertyAccess { Property = property, User = staff }, Photo("last", false, 2), Photo("first", true, 10), Photo("middle", false, 1));
        await db.SaveChangesAsync();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddDbContext<AppDbContext>(x => x.UseNpgsql(connection));
        builder.Services.AddScoped<RoomService>();
        builder.Services.AddScoped<PropertyAccessService>();
        builder.Services.AddScoped<PublicPropertyResolver>();
        builder.Services.AddScoped<PublicRoomContentService>();
        builder.Services.AddAntiforgery();
        builder.Services.AddAuthentication("Fixture").AddCookie("Fixture", options =>
        {
            options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
            options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
        });
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("ViewRooms", policy => policy.RequireRole("Admin", "Manager", "Staff", "Media", "Viewer"));
            options.AddPolicy("ManageRooms", policy => policy.RequireRole("Admin", "Manager"));
        });
        await using var app = builder.Build();
        app.UseAuthentication();
        app.Use(async (context, next) =>
        {
            var role = context.Request.Headers["X-Fixture-Role"].ToString();
            if (role.Length > 0) context.User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, staff.Id.ToString()), new Claim(ClaimTypes.Role, role)
            ], "Fixture"));
            await next();
        });
        app.UseAuthorization(); app.MapRoomEndpoints(); app.MapPublicRoomMediaEndpoints();
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        string Public(Guid roomId, string? slug = null) => $"/api/public/rooms/{roomId}/photos?siteSlug={slug ?? property.SiteSlug}";
        string Admin(Guid propertyId, Guid roomId) => $"/api/admin/properties/{propertyId}/rooms/{roomId}/photos";

        var response = await client.GetAsync(Public(room.Id));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("PRIVATE GUIDE", json);
        Assert.DoesNotContain("private-original", json);
        var gallery = JsonSerializer.Deserialize<RoomPhotoGalleryDto>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(room.Id, gallery.RoomId);
        Assert.Equal(property.Name, gallery.PropertyName);
        Assert.Equal(new[] { "/card/first", "/card/middle", "/card/last" }, gallery.Images.Select(x => x.CardUrl));
        Assert.Empty((await client.GetFromJsonAsync<RoomPhotoGalleryDto>(Public(empty.Id)))!.Images);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Public(draft.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Public(foreign.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Public(foreign.Id, other.SiteSlug))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Admin(property.Id, room.Id))).StatusCode);

        client.DefaultRequestHeaders.Add("X-Fixture-Role", "Staff");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Admin(property.Id, draft.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Admin(property.Id, foreign.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Admin(other.Id, foreign.Id))).StatusCode);
        await db.Rooms.Where(x => x.Id == room.Id).ExecuteUpdateAsync(set => set.SetProperty(x => x.IsActive, false));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Public(room.Id))).StatusCode);
        await app.StopAsync();
    }
}
