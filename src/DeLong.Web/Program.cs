using DeLong.Web.Common.Security;
using DeLong.Web.Data;
using DeLong.Web.Data.Seed;
using DeLong.Web.Features.Bookings;
using DeLong.Web.Features.Customers;
using DeLong.Web.Features.Payments;
using DeLong.Web.Features.Rooms;
using DeLong.Web.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured. Use .NET User Secrets in development.");
}

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 10;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.Name = "delong.auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.SlidingExpiration = true;
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminArea", policy => policy.RequireRole("Admin", "Manager", "Staff", "Housekeeping", "Viewer"));
    options.AddPolicy("ManageRooms", policy => policy.RequireRole("Admin", "Manager"));
    options.AddPolicy("ManageBookings", policy => policy.RequireRole("Admin", "Manager", "Staff"));
    options.AddPolicy("ManagePayments", policy => policy.RequireRole("Admin", "Manager", "Staff"));
});

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Admin", "AdminArea");
    options.Conventions.AllowAnonymousToPage("/Account/Login");
});
builder.Services.AddProblemDetails();
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
builder.Services.AddScoped<ApiAntiforgeryFilter>();
builder.Services.AddScoped<PropertyAccessService>();
builder.Services.AddScoped<PropertyAccessFilter>();
builder.Services.AddScoped<CurrentPropertyService>();
builder.Services.AddScoped<RoomService>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<PaymentService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapRazorPages();
app.MapRoomEndpoints();
app.MapCustomerEndpoints();
app.MapBookingEndpoints();
app.MapPaymentEndpoints();

if (app.Configuration.GetValue<bool>("Database:AutoMigrate") || app.Configuration.GetValue<bool>("Database:SeedOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (app.Configuration.GetValue<bool>("Database:AutoMigrate"))
    {
        await db.Database.MigrateAsync();
    }

    if (app.Configuration.GetValue<bool>("Database:SeedOnStartup"))
    {
        await DbSeeder.SeedAsync(scope.ServiceProvider, app.Configuration);
    }
}

app.Run();

public partial class Program;
