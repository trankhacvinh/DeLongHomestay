using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using DeLong.Web.Common.Security;
using DeLong.Web.Common.Auditing;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Features.DataQuality;
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
public sealed class DataQualityApiTests
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
        db.AddRange(property, other, actor,
            new UserPropertyAccess { Property = property, User = actor });
        await db.SaveChangesAsync();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddDbContext<AppDbContext>(x => x.UseNpgsql(connection));
        builder.Services.AddScoped<AuditService>();
        builder.Services.AddScoped<DataQualityService>();
        builder.Services.AddScoped<PropertyAccessService>();
        builder.Services.AddAntiforgery(x => x.HeaderName = "X-CSRF-TOKEN");
        builder.Services.AddAuthentication("FixtureCookie").AddCookie("FixtureCookie", x =>
        {
            x.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
            x.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
        });
        builder.Services.AddAuthorization(x =>
        {
            x.AddPolicy("ManageDataQuality", p => p.RequireRole("Admin", "Manager"));
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
        app.MapDataQualityEndpoints();
        await app.StartAsync();
        using var client = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = false })
        { BaseAddress = new Uri(app.Urls.Single()) };
        var issue = new DataQualityIssue { PropertyId = property.Id, Fingerprint = "fixture", Rule = "fixture", Severity = "Medium",
            Title = "Test", BookingId = Guid.NewGuid(), LastCheckedAtUtc = DateTime.UtcNow };
        db.Add(issue); await db.SaveChangesAsync();
        async Task<HttpStatusCode> Review(string role, Guid propertyId, bool csrf = true)
        {
            client.DefaultRequestHeaders.Remove("X-Fixture-Role");
            if (role != "") client.DefaultRequestHeaders.Add("X-Fixture-Role", role);
            client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
            if (csrf)
            {
                var token = await client.GetFromJsonAsync<string>("/csrf");
                client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);
            }
            using var response = await client.PutAsJsonAsync($"/api/admin/properties/{propertyId}/data-quality/{issue.Id}", new ReviewQualityRequest("Accepted", "Đã kiểm tra"));
            return response.StatusCode;
        }
        Assert.Equal(HttpStatusCode.Unauthorized, await Review("", property.Id, false));
        foreach (var role in new[] { "Staff", "Media", "Viewer" })
            Assert.Equal(HttpStatusCode.Forbidden, await Review(role, property.Id));
        Assert.Equal(HttpStatusCode.Forbidden, await Review("Manager", other.Id));
        Assert.Equal(HttpStatusCode.BadRequest, await Review("Manager", property.Id, false));
        foreach (var role in new[] { "Manager", "Admin" })
            Assert.Equal(HttpStatusCode.OK, await Review(role, property.Id));
        client.DefaultRequestHeaders.Remove("X-Fixture-Role"); client.DefaultRequestHeaders.Add("X-Fixture-Role", "Staff");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/admin/properties/{property.Id}/data-quality/")).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Fixture-Role"); client.DefaultRequestHeaders.Add("X-Fixture-Role", "Manager");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/admin/properties/{property.Id}/data-quality/")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/admin/properties/{other.Id}/data-quality/")).StatusCode);
        await app.StopAsync();
    }
}
