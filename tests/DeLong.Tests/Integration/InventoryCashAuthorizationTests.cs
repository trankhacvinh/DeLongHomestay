using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using DeLong.Web.Common.Auditing;
using DeLong.Web.Common.Security;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Features.Cash;
using DeLong.Web.Features.Expenses;
using DeLong.Web.Features.Inventory;
using DeLong.Web.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace DeLong.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class InventoryCashAuthorizationTests
{
    [RoomLockPostgresFact]
    public async Task Http_policies_antiforgery_owner_access_and_branch_scope_are_enforced()
    {
        await using var db = InventoryCashTests.Database(); await db.Database.MigrateAsync();
        var owner = await db.InventoryWarehouses.AsNoTracking().Select(x => x.Property).FirstOrDefaultAsync();
        if (owner is null) {
            owner = new Property { Code = Guid.NewGuid().ToString("N"), Name = "Synthetic HTTP owner" };
            db.Properties.Add(owner); await db.SaveChangesAsync();
            await new InventoryService(db, new AuditService(db), new PropertyAccessService(db)).InitializeAsync(InventoryCashTests.User(Guid.NewGuid(), "Admin"), owner.Id, default);
        }
        var foreign = new Property { Code = Guid.NewGuid().ToString("N"), Name = "Synthetic other branch" };
        db.Add(foreign);
        var principals = new Dictionary<string, ClaimsPrincipal>();
        foreach (var pair in new[] { ("admin", "Admin", owner.Id), ("manager", "Manager", owner.Id), ("outside", "Manager", foreign.Id), ("viewer", "Viewer", owner.Id), ("staff", "Staff", owner.Id) }) {
            var user = new ApplicationUser { UserName = Guid.NewGuid().ToString("N"), DisplayName = "Synthetic", IsActive = true };
            db.Users.Add(user); db.UserPropertyAccesses.Add(new UserPropertyAccess { UserId = user.Id, PropertyId = pair.Item3 });
            principals[pair.Item1] = InventoryCashTests.User(user.Id, pair.Item2);
        }
        await db.SaveChangesAsync();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<IReadOnlyDictionary<string, ClaimsPrincipal>>(principals);
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuthentication>("Test", _ => { });
        builder.Services.AddAuthorization(options => {
            options.AddPolicy("ViewInventory", p => p.RequireRole("Admin", "Manager", "Viewer"));
            options.AddPolicy("ManageInventory", p => p.RequireRole("Admin", "Manager"));
            options.AddPolicy("ViewFinance", p => p.RequireRole("Admin", "Manager", "Viewer"));
            options.AddPolicy("ManageFinance", p => p.RequireRole("Admin", "Manager"));
        });
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION")!));
        builder.Services.AddScoped<AuditService>(); builder.Services.AddScoped<PropertyAccessService>(); builder.Services.AddScoped<CurrentPropertyService>();
        builder.Services.AddScoped<InventoryService>(); builder.Services.AddScoped<InventoryImportService>(); builder.Services.AddScoped<CashService>(); builder.Services.AddScoped<ExpenseService>();
        await using var app = builder.Build();
        app.UseAuthentication(); app.UseAuthorization();
        app.MapInventoryEndpoints(); app.MapCashEndpoints(); app.MapExpenseEndpoints();
        app.MapGet("/token", (HttpContext ctx, IAntiforgery antiforgery) => Results.Ok(new { token = antiforgery.GetAndStoreTokens(ctx).RequestToken })).RequireAuthorization();
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient(new HttpClientHandler { CookieContainer = new(), UseCookies = true }) { BaseAddress = new Uri(address) };
        async Task SetUser(string name) {
            client.DefaultRequestHeaders.Remove("X-Test-User"); client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
            client.DefaultRequestHeaders.Add("X-Test-User", name);
            var token = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/token");
            client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/inventory/?month=2026-10")).StatusCode);
        await SetUser("admin"); client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/admin/inventory/units", new { name = "No token" })).StatusCode);
        using (var upload = new MultipartFormDataContent()) {
            upload.Add(new ByteArrayContent(InventoryImportService.Template("catalog")), "file", "items.xlsx");
            upload.Add(new StringContent("catalog"), "mode"); upload.Add(new StringContent("skip"), "duplicates");
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/admin/inventory/import/preview", upload)).StatusCode);
        }
        await SetUser("admin");
        var importName = "HTTP import " + Guid.NewGuid().ToString("N")[..8];
        using (var book = new ClosedXML.Excel.XLWorkbook(new MemoryStream(InventoryImportService.Template("catalog")))) {
            book.Worksheet(1).Cell(2, 1).Value = importName; book.Worksheet(1).Cell(2, 2).Value = "kG";
            using var stream = new MemoryStream(); book.SaveAs(stream);
            using var upload = new MultipartFormDataContent(); upload.Add(new ByteArrayContent(stream.ToArray()), "file", "items.xlsx");
            upload.Add(new StringContent("catalog"), "mode"); upload.Add(new StringContent("skip"), "duplicates");
            var previewResponse = await client.PostAsync("/api/admin/inventory/import/preview", upload); Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
            var preview = (await previewResponse.Content.ReadFromJsonAsync<InventoryImportPreview>())!; Assert.Equal(0, preview.Errors);
            var commit = new InventoryImportCommit(Guid.NewGuid(), preview.Token!);
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/admin/inventory/import/commit", commit)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/admin/inventory/import/commit", commit)).StatusCode);
            Assert.Single(await db.InventoryItems.AsNoTracking().Where(x => x.Name == importName).ToListAsync());
        }
        var unitResponse = await client.PostAsJsonAsync("/api/admin/inventory/units", new { name = "HTTP " + Guid.NewGuid().ToString("N")[..8] });
        Assert.Equal(HttpStatusCode.OK, unitResponse.StatusCode);
        var unitId = (await unitResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        var itemRequest = new InventoryItemRequest(Guid.NewGuid().ToString("N"), "HTTP synthetic " + Guid.NewGuid().ToString("N")[..8], unitId, 0);
        var created = await client.PostAsJsonAsync("/api/admin/inventory/items", itemRequest); Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var itemId = (await created.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        await SetUser("manager");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/inventory/?month=2026-10")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/admin/cash/?propertyId={owner.Id}&scope=all&month=2026-10")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/admin/cash/?propertyId={foreign.Id}&month=2026-10")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/admin/cash/?propertyId={owner.Id}&month=2026-10")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/admin/properties/{foreign.Id}/cash/receipts", new CashReceiptRequest(Guid.NewGuid(), DateTimeOffset.UtcNow, null, 10, "Unauthorized", null))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/admin/inventory/documents", new InventoryDocumentRequest(Guid.NewGuid(), InventoryDocumentType.Issue, DateTimeOffset.UtcNow, foreign.Id, null, [new(itemId, 1, 0)]))).StatusCode);
        await SetUser("outside");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/inventory/?month=2026-10")).StatusCode);
        await SetUser("viewer");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/inventory/import/template?mode=catalog")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/admin/inventory/import/commit", new { requestKey = Guid.NewGuid(), token = "invalid" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/inventory/?month=2026-10")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/admin/inventory/items", itemRequest)).StatusCode);
        await SetUser("staff");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/inventory/?month=2026-10")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/admin/cash/?propertyId={owner.Id}&month=2026-10")).StatusCode);
        await app.StopAsync();
    }

    private sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder,
        IReadOnlyDictionary<string, ClaimsPrincipal> users) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(users.TryGetValue(Request.Headers["X-Test-User"].ToString(), out var user)
            ? AuthenticateResult.Success(new AuthenticationTicket(user, Scheme.Name)) : AuthenticateResult.NoResult());
    }
}
