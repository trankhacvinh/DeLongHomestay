using System.Net;
using System.Threading.RateLimiting;
using DeLong.Web.Common.Auditing;
using DeLong.Web.Common.Caching;
using DeLong.Web.Common.Operations;
using DeLong.Web.Common.Security;
using DeLong.Web.Data;
using DeLong.Web.Data.Seed;
using DeLong.Web.Features.Bookings;
using DeLong.Web.Features.Customers;
using DeLong.Web.Features.Expenses;
using DeLong.Web.Features.Finance;
using DeLong.Web.Features.Housekeeping;
using DeLong.Web.Features.Imports;
using DeLong.Web.Features.Payments;
using DeLong.Web.Features.Properties;
using DeLong.Web.Features.PublicBooking;
using DeLong.Web.Features.Notifications;
using DeLong.Web.Features.PublicRooms;
using DeLong.Web.Features.Reports;
using DeLong.Web.Features.Rooms;
using DeLong.Web.Features.Site;
using DeLong.Web.Features.Staff;
using DeLong.Web.Features.CustomerAccounts;
using DeLong.Web.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using ZiggyCreatures.Caching.Fusion;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured. Use .NET User Secrets in development.");
}

var storagePaths = StoragePaths.Resolve(builder.Configuration, builder.Environment);
storagePaths.EnsureDirectories();
var productionWarnings = ProductionStartupGuard.Validate(builder.Configuration, builder.Environment, storagePaths);

if (!builder.Environment.IsDevelopment())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole(options =>
    {
        options.TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";
        options.UseUtcTimestamp = true;
    });
}

builder.Services.AddSingleton(storagePaths);
builder.Services
    .AddDataProtection()
    .SetApplicationName("DeLongHomestay")
    .PersistKeysToFileSystem(new DirectoryInfo(storagePaths.DataProtectionRoot));

var publicCacheEnabled = builder.Configuration.GetValue<bool?>("Performance:PublicCacheEnabled")
    ?? !builder.Environment.IsDevelopment();
var publicCacheSeconds = Math.Clamp(builder.Configuration.GetValue<int?>("Performance:PublicCacheSeconds") ?? 30, 1, 3600);
if (publicCacheEnabled)
{
    builder.Services.AddScoped<PublicCacheInvalidationInterceptor>();
    builder.Services.AddFusionCache()
        .WithDefaultEntryOptions(new FusionCacheEntryOptions()
            .SetDuration(TimeSpan.FromSeconds(publicCacheSeconds))
            .SetFailSafe(true, TimeSpan.FromMinutes(2)));
    builder.Services.AddDbContext<AppDbContext>((services, options) =>
        options.UseNpgsql(connectionString)
            .AddInterceptors(services.GetRequiredService<PublicCacheInvalidationInterceptor>()));
}
else
{
    builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
}

builder.Services.AddHttpContextAccessor();
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"])
    .AddCheck<StorageHealthCheck>("storage", tags: ["ready"]);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 2;
    foreach (var configured in builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
    {
        if (IPAddress.TryParse(configured, out var address)) options.KnownProxies.Add(address);
    }
});

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddClaimsPrincipalFactory<ApplicationClaimsPrincipalFactory>()
    .AddDefaultTokenProviders();

builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.FromMinutes(1);
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.Name = "delong.auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.SlidingExpiration = true;
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminArea", policy => policy.RequireRole("Admin", "Manager", "Staff", "Housekeeping", "Viewer"));
    options.AddPolicy("ViewOperations", policy => policy.RequireRole("Admin", "Manager", "Staff", "Viewer"));
    options.AddPolicy("ViewRooms", policy => policy.RequireRole("Admin", "Manager", "Staff", "Viewer"));
    options.AddPolicy("ViewHousekeeping", policy => policy.RequireRole("Admin", "Manager", "Staff", "Housekeeping", "Viewer"));
    options.AddPolicy("ManageStaff", policy => policy.RequireRole("Admin"));
    options.AddPolicy("ManageProperties", policy => policy.RequireRole("Admin"));
    options.AddPolicy("ManageSiteContent", policy => policy.RequireRole("Admin", "Manager"));
    options.AddPolicy("ManageSiteCode", policy => policy.RequireRole("Admin"));
    options.AddPolicy("ManageImports", policy => policy.RequireRole("Admin", "Manager"));
    options.AddPolicy("ManageRooms", policy => policy.RequireRole("Admin", "Manager"));
    options.AddPolicy("ManageNotifications", policy => policy.RequireRole("Admin", "Manager"));
    options.AddPolicy("ManageBookings", policy => policy.RequireRole("Admin", "Manager", "Staff"));
    options.AddPolicy("ManagePayments", policy => policy.RequireRole("Admin", "Manager", "Staff"));
    options.AddPolicy("ManageHousekeeping", policy => policy.RequireRole("Admin", "Manager", "Staff", "Housekeeping"));
    options.AddPolicy("ViewFinance", policy => policy.RequireRole("Admin", "Manager", "Viewer"));
    options.AddPolicy("ManageFinance", policy => policy.RequireRole("Admin", "Manager"));
    options.AddPolicy("ViewReports", policy => policy.RequireRole("Admin", "Manager", "Viewer"));
});

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Admin", "AdminArea");
    options.Conventions.AuthorizePage("/Admin/Calendar", "ViewOperations");
    options.Conventions.AuthorizeFolder("/Admin/Bookings", "ViewOperations");
    options.Conventions.AuthorizeFolder("/Admin/Customers", "ViewOperations");
    options.Conventions.AuthorizeFolder("/Admin/Rooms", "ViewRooms");
    options.Conventions.AuthorizePage("/Admin/Rooms/Content", "ManageRooms");
    options.Conventions.AuthorizeFolder("/Admin/Housekeeping", "ViewHousekeeping");
    options.Conventions.AuthorizeFolder("/Admin/Settings", "ManageRooms");
    options.Conventions.AuthorizeFolder("/Admin/Properties", "ManageProperties");
    options.Conventions.AuthorizeFolder("/Admin/Site", "ManageSiteContent");
    options.Conventions.AuthorizeFolder("/Admin/Imports", "ManageImports");
    options.Conventions.AuthorizeFolder("/Admin/Finance", "ViewFinance");
    options.Conventions.AuthorizeFolder("/Admin/Reports", "ViewReports");
    options.Conventions.AuthorizeFolder("/Admin/Staff", "ManageStaff");
    options.Conventions.AllowAnonymousToPage("/Account/Login");

    // Public multi-property routes. Legacy root routes remain available for DELONG.
    options.Conventions.AddPageRoute("/Index", "h/{siteSlug}");
    options.Conventions.AddPageRoute("/Rooms/Index", "h/{siteSlug}/rooms");
    options.Conventions.AddPageRoute("/Rooms/Details", "h/{siteSlug}/rooms/{code}");
    options.Conventions.AddPageRoute("/Booking/Index", "h/{siteSlug}/booking");
    options.Conventions.AddPageRoute("/Booking/Lookup", "h/{siteSlug}/booking/lookup");
    options.Conventions.AddPageRoute("/Booking/Success", "h/{siteSlug}/booking/success");
    options.Conventions.AddPageRoute("/Customer/Account", "h/{siteSlug}/customer/account");
    options.Conventions.AddPageRoute("/Customer/Account", "customer/login");
    options.Conventions.AddPageRoute("/Customer/Account", "h/{siteSlug}/customer/login");
    options.Conventions.AddPageRoute("/Blog/Index", "h/{siteSlug}/blog");
    options.Conventions.AddPageRoute("/Blog/Details", "h/{siteSlug}/blog/{slug}");
    options.Conventions.AddPageRoute("/CustomPage", "{slug}");
    options.Conventions.AddPageRoute("/CustomPage", "h/{siteSlug}/{slug}");
});
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat([
        "application/json", "application/problem+json", "application/xml", "image/svg+xml"
    ]);
});

builder.Services.AddProblemDetails();
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("public-booking", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.AddPolicy("public-lookup", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.AddPolicy("account-login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            $"{httpContext.Connection.RemoteIpAddress}:account",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 8,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

builder.Services.AddScoped<ApiAntiforgeryFilter>();
builder.Services.AddScoped<PropertyAccessService>();
builder.Services.AddScoped<PropertyAccessFilter>();
builder.Services.AddScoped<CurrentPropertyService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<PropertyAdminService>();
builder.Services.AddScoped<PublicPropertyResolver>();
builder.Services.AddScoped<SiteContentService>();
builder.Services.AddScoped<CustomPageStore>();
builder.Services.AddScoped<PropertyEditorialContentService>();
builder.Services.AddScoped<GlobalEditorialShowcaseService>();
builder.Services.AddScoped<MediaLibraryService>();
builder.Services.AddSingleton<ISiteAssetStorage, LocalSiteAssetStorage>();
builder.Services.AddScoped<RoomService>();
builder.Services.AddScoped<RoomRateService>();
builder.Services.AddScoped<RoomContentService>();
builder.Services.AddSingleton<IRoomImageStorage, LocalRoomImageStorage>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<BookingMoveService>();
builder.Services.AddScoped<ExcelBookingImportService>();
builder.Services.AddScoped<LegacyCalendarConversionService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<HousekeepingService>();
builder.Services.AddScoped<ExpenseService>();
builder.Services.AddScoped<FinanceService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<StaffAccountService>();
builder.Services.AddScoped<CustomerAccountSettingsService>();
builder.Services.AddScoped<CustomerAccountService>();
builder.Services.AddScoped<PublicBookingService>();
builder.Services.AddScoped<PublicBookingLookupService>();
builder.Services.AddScoped<PublicRoomContentService>();
builder.Services.AddScoped<PublicRequestInboxService>();
builder.Services.AddSingleton<NotificationRealtimeBroker>();
builder.Services.AddSingleton<SmtpCredentialProtector>();
builder.Services.AddScoped<NotificationSettingsService>();
builder.Services.AddScoped<BookingNotificationService>();
builder.Services.AddSingleton<NotificationEmailSender>();
builder.Services.AddHostedService<NotificationEmailWorker>();

var app = builder.Build();
foreach (var warning in productionWarnings) app.Logger.LogWarning("Production startup warning: {Warning}", warning);
if (publicCacheEnabled)
    app.Logger.LogInformation("FusionCache public read cache enabled with {PublicCacheSeconds}s TTL.", publicCacheSeconds);
else if (app.Environment.IsDevelopment())
    app.Logger.LogInformation("FusionCache public read cache is disabled by default in Development; public reads query the database directly.");
else
    app.Logger.LogWarning("FusionCache public read cache is disabled; public reads will query the database directly.");
if (app.Environment.IsDevelopment())
{
    app.Logger.LogInformation("Development static asset caching is disabled; JS/CSS/media responses use no-store.");
    if (!storagePaths.DataRootExplicit)
    {
        app.Logger.LogWarning(
            "Development Storage:DataRoot is repo-local at {DataRoot}. Configure a stable absolute Storage:DataRoot with user secrets to keep auth/data-protection keys and private development data stable across checkouts.",
            storagePaths.DataRoot);
    }
}

app.UseForwardedHeaders();
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        var headers = context.Response.Headers;
        headers.TryAdd("X-Content-Type-Options", "nosniff");
        headers.TryAdd("X-Frame-Options", "SAMEORIGIN");
        headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
        headers.TryAdd("Permissions-Policy", "camera=(self), microphone=(), geolocation=()");
        return Task.CompletedTask;
    });
    await next();
});
app.UseMiddleware<RequestLoggingMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseResponseCompression();
app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/api") &&
               !context.Request.Path.StartsWithSegments("/health"),
    branch => branch.UseStatusCodePagesWithReExecute("/not-found"));
void PrepareStaticResponse(StaticFileResponseContext context) =>
    StaticAssetCachePolicy.Apply(context.Context, app.Environment.IsDevelopment());

app.UseStaticFiles(new StaticFileOptions { OnPrepareResponse = PrepareStaticResponse });

var defaultMediaRoot = Path.GetFullPath(Path.Combine(
    app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot"),
    "uploads",
    "rooms"));
if (!string.Equals(defaultMediaRoot, storagePaths.MediaPublicRoot, StringComparison.OrdinalIgnoreCase))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(storagePaths.MediaPublicRoot),
        RequestPath = storagePaths.MediaRequestPath,
        OnPrepareResponse = PrepareStaticResponse
    });
}

app.UseRouting();
app.UseAuthentication();
app.UseMiddleware<ForcePasswordChangeMiddleware>();
app.UseAuthorization();
app.UseMiddleware<WorkingPropertyMiddleware>();
app.UseRateLimiter();
app.UseAntiforgery();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = HealthResponseWriter.WriteAsync
}).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = HealthResponseWriter.WriteAsync
}).AllowAnonymous();

app.MapGet("/api/antiforgery/token", (
    Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery,
    HttpContext httpContext) =>
{
    var tokens = antiforgery.GetAndStoreTokens(httpContext);
    return Results.Ok(new { token = tokens.RequestToken });
}).AllowAnonymous();

app.MapRazorPages();
app.MapPropertyAdminEndpoints();
app.MapSiteContentEndpoints();
app.MapCustomPageEndpoints();
app.MapPublicShellDesignerEndpoints();
app.MapPropertyEditorialContentEndpoints();
app.MapMediaLibraryEndpoints();
app.MapPublicSeoEndpoints();
app.MapRoomEndpoints();
app.MapRoomRateEndpoints();
app.MapRoomContentEndpoints();
app.MapCustomerEndpoints();
app.MapBookingEndpoints();
app.MapImportEndpoints();
app.MapPaymentEndpoints();
app.MapHousekeepingEndpoints();
app.MapExpenseEndpoints();
app.MapAuditEndpoints();
app.MapStaffAccountEndpoints();
app.MapCustomerAccountEndpoints();
app.MapPublicRoomMediaEndpoints();
app.MapPublicBookingEndpoints();
app.MapPublicBookingLookupEndpoints();
app.MapNotificationEndpoints();

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
