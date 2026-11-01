using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using DeLong.Web.Common.Auditing;
using DeLong.Web.Common.Operations;
using DeLong.Web.Common.Security;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Operations;
using DeLong.Web.Features.Pricing;
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
public sealed class CalendarOverviewApiTests
{
    [RoomLockPostgresFact, Trait("Category", "Integration")]
    public async Task Overview_shows_only_assigned_branches_and_hides_guest_phone()
    {
        var connection = Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION")!;
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();

        var key = Guid.NewGuid().ToString("N")[..10];
        var branchA = new Property { Code = $"OVA-{key}", Name = "Overview A", SiteSlug = $"ova-{key}", TimeZoneId = "Asia/Ho_Chi_Minh", IsActive = true };
        var branchB = new Property { Code = $"OVB-{key}", Name = "Overview B", SiteSlug = $"ovb-{key}", TimeZoneId = "Asia/Ho_Chi_Minh", IsActive = true };
        var branchC = new Property { Code = $"OVC-{key}", Name = "Overview C", SiteSlug = $"ovc-{key}", TimeZoneId = "Asia/Ho_Chi_Minh", IsActive = true };
        Room NewRoom(Property property, string name, int sort) => new()
        {
            PropertyId = property.Id, Code = $"{name}-{key}", Name = name, Slug = $"{name.ToLowerInvariant()}-{key}",
            Capacity = 2, IsActive = true, IsPublished = true, SortOrder = sort
        };
        var roomA1 = NewRoom(branchA, "A1", 1);
        var roomA2 = NewRoom(branchA, "A2", 2);
        var roomB1 = NewRoom(branchB, "B1", 1);
        var roomC1 = NewRoom(branchC, "C1", 1);
        var rateA1 = new RoomRate { RoomId = roomA1.Id, Name = "Khung 12-15", StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(15, 0),
            Type = RoomRateType.TimeSlot, Price = 250_000m, SortOrder = 1, IsActive = true };
        var customer = new Customer { PropertyId = branchA.Id, Name = "Khach Overview", Phone = "0901234567", NormalizedPhone = "0901234567", IsActive = true };

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(branchA.TimeZoneId);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone));
        var target = today.AddDays(2);
        DateTime Utc(TimeOnly time) => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(target.ToDateTime(time), DateTimeKind.Unspecified), timeZone);
        var booking = new Booking
        {
            PropertyId = branchA.Id, RoomId = roomA1.Id, CustomerId = customer.Id, RoomRateId = rateA1.Id, Code = $"OV-{key}",
            Type = BookingType.TimeSlot, CheckInUtc = Utc(new TimeOnly(12, 0)), CheckOutUtc = Utc(new TimeOnly(15, 0)),
            Status = BookingStatus.Confirmed, RateName = rateA1.Name, UnitPrice = rateA1.Price, RoomAmount = rateA1.Price
        };

        var singleBranchUser = new ApplicationUser { Id = Guid.NewGuid(), UserName = $"ov-single-{key}", DisplayName = "Single" };
        var multiBranchUser = new ApplicationUser { Id = Guid.NewGuid(), UserName = $"ov-multi-{key}", DisplayName = "Multi" };
        db.AddRange(branchA, branchB, branchC, roomA1, roomA2, roomB1, roomC1, rateA1, customer, booking, singleBranchUser, multiBranchUser,
            new UserPropertyAccess { PropertyId = branchA.Id, UserId = singleBranchUser.Id },
            new UserPropertyAccess { PropertyId = branchA.Id, UserId = multiBranchUser.Id },
            new UserPropertyAccess { PropertyId = branchB.Id, UserId = multiBranchUser.Id });
        await db.SaveChangesAsync();

        var tempRoot = Path.Combine(Path.GetTempPath(), $"delong-overview-{Guid.NewGuid():N}");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddDbContext<AppDbContext>(x => x.UseNpgsql(connection));
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<AuditService>();
        builder.Services.AddScoped<CurrentPropertyService>();
        builder.Services.AddScoped<PricingService>();
        builder.Services.AddScoped(sp => new PublicPropertyResolver(sp.GetRequiredService<AppDbContext>()));
        builder.Services.AddSingleton(new StoragePaths(tempRoot, Path.Combine(tempRoot, "media"), new PathString("/uploads/rooms"), true, true, false));
        builder.Services.AddAuthentication("FixtureCookie").AddCookie("FixtureCookie", x =>
        {
            x.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
            x.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
        });
        builder.Services.AddAuthorization(x => x.AddPolicy("ViewOperations", p => p.RequireRole("Admin", "Manager", "Staff", "Viewer")));
        await using var app = builder.Build();
        app.UseAuthentication();
        // Test host identity only; the production endpoint policy and branch checks run unchanged.
        app.Use(async (context, next) =>
        {
            var role = context.Request.Headers["X-Fixture-Role"].ToString();
            var userId = context.Request.Headers["X-Fixture-User"].ToString();
            if (!string.IsNullOrEmpty(role)) context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Role, role)], "Fixture"));
            await next();
        });
        app.UseAuthorization();
        app.MapCalendarOverviewEndpoints();
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };

        async Task<(HttpStatusCode Status, JsonElement Body)> Get(string role, Guid userId, string query)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/admin/calendar/overview?{query}");
            if (role != "") { request.Headers.Add("X-Fixture-Role", role); request.Headers.Add("X-Fixture-User", userId.ToString()); }
            using var response = await client.SendAsync(request);
            var body = response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<JsonElement>() : default;
            return (response.StatusCode, body);
        }
        static string[] RoomNames(JsonElement body) => body.GetProperty("properties").EnumerateArray()
            .SelectMany(p => p.GetProperty("rooms").EnumerateArray()).Select(r => r.GetProperty("roomName").GetString()!).ToArray();

        try
        {
            var from = $"from={today:yyyy-MM-dd}&days=7";
            Assert.Equal(HttpStatusCode.Unauthorized, (await Get("", Guid.Empty, from)).Status);
            Assert.Equal(HttpStatusCode.Forbidden, (await Get("Housekeeping", singleBranchUser.Id, from)).Status);
            Assert.Equal(HttpStatusCode.BadRequest, (await Get("Staff", singleBranchUser.Id, "from=nope")).Status);

            var single = await Get("Staff", singleBranchUser.Id, from);
            Assert.Equal(HttpStatusCode.OK, single.Status);
            Assert.Equal(new[] { "A1", "A2" }, RoomNames(single.Body));
            Assert.Single(single.Body.GetProperty("accessibleProperties").EnumerateArray());
            Assert.Equal(HttpStatusCode.Forbidden, (await Get("Staff", singleBranchUser.Id, $"{from}&propertyId={branchB.Id}")).Status);

            var multi = await Get("Manager", multiBranchUser.Id, from);
            Assert.Equal(HttpStatusCode.OK, multi.Status);
            Assert.Equal(new[] { "A1", "A2", "B1" }, RoomNames(multi.Body).Order().ToArray());
            Assert.DoesNotContain("C1", RoomNames(multi.Body));
            Assert.Equal(new[] { "B1" }, RoomNames((await Get("Manager", multiBranchUser.Id, $"{from}&propertyId={branchB.Id}")).Body));
            Assert.Equal(HttpStatusCode.Forbidden, (await Get("Admin", multiBranchUser.Id, $"{from}&propertyId={branchC.Id}")).Status);

            var raw = single.Body.GetRawText();
            Assert.Contains("Khach Overview", raw);
            Assert.DoesNotContain("0901234567", raw);
        }
        finally
        {
            await app.StopAsync();
            try { if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true); } catch (IOException) { }
        }
    }
}
