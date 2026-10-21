using System.Security.Claims;
using System.Text.Json;
using DeLong.Web.Common.Auditing;
using DeLong.Web.Common.Security;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Bookings;
using DeLong.Web.Features.Customers;
using DeLong.Web.Features.Finance;
using DeLong.Web.Features.PublicBooking;
using DeLong.Web.Features.Reports;
using DeLong.Web.Features.Rooms;
using DeLong.Web.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeLong.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class ReportingScopeTests
{
    [RoomLockPostgresFact]
    public async Task Admin_reports_include_unassigned_and_inactive_branches_without_granting_operational_access()
    {
        await using var f = await Fixture.Create();
        var admin = f.User("Admin");
        var scope = (await f.Context.ResolveReportingScopeAsync(admin, f.First.Id, "all"))!;
        Assert.True(scope.IsAll);
        Assert.Contains(scope.Properties, x => x.Id == f.Second.Id);
        Assert.False(await new PropertyAccessService(f.Db).CanAccessAsync(admin, f.Second.Id));
        Assert.Null(await f.Context.ResolveAsync(admin, f.Second.Id));
        var selected = (await f.Context.ResolveReportingScopeAsync(admin, f.First.Id, f.Second.Id.ToString()))!;
        Assert.Equal(f.Second.Id, selected.SelectedProperty.Id);
        Assert.False(selected.IsAll);
        Assert.Null(await f.Context.ResolveReportingScopeAsync(admin, f.First.Id, Guid.NewGuid().ToString()));

        var page = f.Reports(admin);
        Assert.IsType<PageResult>(await page.OnGetAsync("2033-03", f.First.Id, f.Second.Id.ToString(), default));
        using var json = JsonDocument.Parse(page.PageDataJson);
        Assert.Equal(350000, json.RootElement.GetProperty("report").GetProperty("bookingValue").GetDecimal());
        Assert.True(json.RootElement.GetProperty("canViewAllProperties").GetBoolean());
        Assert.IsType<FileContentResult>(await page.OnGetExportAsync("2033-03", f.First.Id, "all", default));

        Assert.IsType<PageResult>(await page.OnGetAsync("2033-03", f.First.Id, "all", default));
        using var combined = JsonDocument.Parse(page.PageDataJson);
        var rooms = combined.RootElement.GetProperty("report").GetProperty("byRoom").EnumerateArray().ToList();
        Assert.Contains(rooms, x => x.GetProperty("roomName").GetString()!.Contains(f.Second.Name));

        var dashboard = f.Dashboard(admin);
        Assert.IsType<PageResult>(await dashboard.OnGetAsync(f.First.Id, "all", default));
        Assert.True(dashboard.IsAllScope);
        Assert.Contains(dashboard.Properties, x => x.Id == f.Second.Id);
    }

    [RoomLockPostgresFact]
    public async Task Branch_roles_cannot_expand_scope_through_url_or_excel_export()
    {
        await using var f = await Fixture.Create();
        foreach (var role in new[] { "Manager", "Viewer", "Staff" })
        {
            var user = f.User(role);
            var own = (await f.Context.ResolveReportingScopeAsync(user, f.First.Id, null))!;
            Assert.Single(own.Properties); Assert.Equal(f.First.Id, own.SelectedProperty.Id);
            Assert.Null(await f.Context.ResolveReportingScopeAsync(user, f.First.Id, "all"));
            Assert.Null(await f.Context.ResolveReportingScopeAsync(user, f.First.Id, f.Second.Id.ToString()));
            Assert.Null(await f.Context.ResolveReportingScopeAsync(user, f.Second.Id, null));
            Assert.Null(await f.Context.ResolveReportingScopeAsync(user, f.First.Id, "invalid"));
            var page = f.Reports(user);
            Assert.IsType<ForbidResult>(await page.OnGetAsync("2033-03", f.First.Id, "all", default));
            Assert.IsType<ForbidResult>(await page.OnGetExportAsync("2033-03", f.First.Id, f.Second.Id.ToString(), default));
            Assert.IsType<ForbidResult>(await f.Dashboard(user).OnGetAsync(f.First.Id, "all", default));
        }
        var managerPage = f.Reports(f.User("Manager"));
        Assert.IsType<PageResult>(await managerPage.OnGetAsync("2033-03", f.First.Id, null, default));
        using var json = JsonDocument.Parse(managerPage.PageDataJson);
        Assert.Equal(250000, json.RootElement.GetProperty("report").GetProperty("bookingValue").GetDecimal());
        Assert.False(json.RootElement.GetProperty("canViewAllProperties").GetBoolean());
        Assert.Single(json.RootElement.GetProperty("properties").EnumerateArray());
    }

    [RoomLockPostgresFact]
    public async Task Reporting_middleware_exception_is_limited_to_Admin_read_pages()
    {
        await using var f = await Fixture.Create();
        async Task<bool> Passed(string path, string role)
        {
            var called = false;
            var http = new DefaultHttpContext { User = f.User(role) };
            http.Request.Path = path; http.Request.Method = "GET";
            http.Request.QueryString = new QueryString($"?propertyId={f.Second.Id}");
            await new WorkingPropertyMiddleware(_ => { called = true; return Task.CompletedTask; }).InvokeAsync(http, f.Context);
            return called;
        }
        Assert.True(await Passed("/Admin/Reports", "Admin"));
        Assert.True(await Passed("/Admin", "Admin"));
        Assert.False(await Passed("/Admin/CalendarV2", "Admin"));
        Assert.False(await Passed("/Admin/Reports", "Manager"));
    }

    private sealed class Fixture(AppDbContext db, ApplicationUser actor, Property first, Property second) : IAsyncDisposable
    {
        public AppDbContext Db => db;
        public Property First => first;
        public Property Second => second;
        public CurrentPropertyService Context { get; } = new(db, new HttpContextAccessor { HttpContext = new DefaultHttpContext() });
        public ClaimsPrincipal User(string role) => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actor.Id.ToString()), new Claim(ClaimTypes.Role, role)], "test"));
        public DeLong.Web.Pages.Admin.Reports.IndexModel Reports(ClaimsPrincipal user) => new(new ReportService(db), new ReportExcelExportService(), Context)
        { PageContext = new PageContext { HttpContext = new DefaultHttpContext { User = user } } };
        public DeLong.Web.Pages.Admin.IndexModel Dashboard(ClaimsPrincipal user) => new(Context, new BookingService(db, new CustomerService(db), new AuditService(db)),
            new RoomService(db), new FinanceService(db), new PublicRequestInboxService(db))
        { PageContext = new PageContext { HttpContext = new DefaultHttpContext { User = user } } };
        public ValueTask DisposeAsync() => db.DisposeAsync();
        public static async Task<Fixture> Create()
        {
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION")!).Options);
            await db.Database.MigrateAsync();
            var key = Guid.NewGuid().ToString("N");
            var first = new Property { Code = "A" + key[..12], Name = "Synthetic A " + key };
            var second = new Property { Code = "B" + key[..12], Name = "Synthetic B " + key, IsActive = false };
            var actor = new ApplicationUser { UserName = key, DisplayName = "Synthetic reporter" };
            foreach (var (property, amount) in new[] { (first, 250000m), (second, 350000m) })
            {
                var room = new Room { Property = property, Code = "R", Name = "Synthetic room" };
                var customer = new Customer { Property = property, Name = "Synthetic", Phone = "0900000000", NormalizedPhone = "0900000000" };
                db.Bookings.Add(new Booking { Property = property, Room = room, Customer = customer, Code = key[..10], CheckInUtc = new(2033, 3, 10, 3, 0, 0, DateTimeKind.Utc),
                    CheckOutUtc = new(2033, 3, 10, 6, 0, 0, DateTimeKind.Utc), RoomAmount = amount, Status = BookingStatus.Confirmed });
            }
            db.Add(actor); db.UserPropertyAccesses.Add(new() { User = actor, Property = first });
            await db.SaveChangesAsync(); return new(db, actor, first, second);
        }
    }
}
