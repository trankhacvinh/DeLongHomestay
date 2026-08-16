using System.Diagnostics;
using System.Text.Json;
using DeLong.Web.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DeLong.Web.Common.Operations;

public sealed record StoragePaths(
    string DataRoot,
    string MediaPublicRoot,
    PathString MediaRequestPath,
    bool DataRootExplicit,
    bool MediaPublicRootExplicit,
    bool RequirePersistent)
{
    public static StoragePaths Resolve(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var dataSetting = configuration["Storage:DataRoot"]?.Trim();
        var mediaSetting = configuration["Storage:MediaPublicRoot"]?.Trim();
        var requestPathValue = configuration["Storage:MediaRequestPath"]?.Trim();
        if (string.IsNullOrWhiteSpace(requestPathValue)) requestPathValue = "/uploads/rooms";
        if (!requestPathValue.StartsWith('/')) requestPathValue = $"/{requestPathValue}";

        var dataRoot = ResolvePath(
            dataSetting,
            environment.ContentRootPath,
            Path.Combine(environment.ContentRootPath, "App_Data"));
        var webRoot = environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot");
        var mediaRoot = ResolvePath(
            mediaSetting,
            environment.ContentRootPath,
            Path.Combine(webRoot, "uploads", "rooms"));

        return new StoragePaths(
            dataRoot,
            mediaRoot,
            new PathString(requestPathValue.TrimEnd('/')),
            !string.IsNullOrWhiteSpace(dataSetting),
            !string.IsNullOrWhiteSpace(mediaSetting),
            configuration.GetValue<bool>("Storage:RequirePersistent"));
    }

    public string DataProtectionRoot => Path.Combine(DataRoot, "data-protection");
    public string OriginalRoomImagesRoot => Path.Combine(DataRoot, "room-images");

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(DataRoot);
        Directory.CreateDirectory(DataProtectionRoot);
        Directory.CreateDirectory(OriginalRoomImagesRoot);
        Directory.CreateDirectory(MediaPublicRoot);
    }

    private static string ResolvePath(string? configured, string contentRoot, string fallback)
    {
        if (string.IsNullOrWhiteSpace(configured)) return Path.GetFullPath(fallback);
        return Path.GetFullPath(Path.IsPathRooted(configured) ? configured : Path.Combine(contentRoot, configured));
    }
}

public sealed class DatabaseHealthCheck(AppDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await db.Database.CanConnectAsync(cancellationToken))
                return HealthCheckResult.Unhealthy("Không thể kết nối PostgreSQL.");

            var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();
            return pending.Length == 0
                ? HealthCheckResult.Healthy("PostgreSQL sẵn sàng và schema đã cập nhật.")
                : HealthCheckResult.Unhealthy($"Database còn {pending.Length} migration chưa áp dụng.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Không thể kết nối PostgreSQL.", ex);
        }
    }
}

public sealed class StorageHealthCheck(StoragePaths paths, IWebHostEnvironment environment, IConfiguration? configuration = null) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (paths.RequirePersistent && !environment.IsDevelopment() && (!paths.DataRootExplicit || !paths.MediaPublicRootExplicit))
            {
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    "Production yêu cầu Storage:DataRoot và Storage:MediaPublicRoot trỏ tới persistent volume."));
            }

            WriteProbe(paths.DataRoot);
            WriteProbe(paths.MediaPublicRoot);

            var minimumFreeSpaceMb = Math.Max(0, configuration?.GetValue<long?>("Storage:MinimumFreeSpaceMb") ?? 0);
            if (minimumFreeSpaceMb > 0)
            {
                var lowRoots = new List<string>();
                foreach (var root in new[] { paths.DataRoot, paths.MediaPublicRoot }.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(root))!);
                    if (drive.AvailableFreeSpace < minimumFreeSpaceMb * 1024L * 1024L) lowRoots.Add(root);
                }
                if (lowRoots.Count > 0)
                    return Task.FromResult(HealthCheckResult.Unhealthy($"Storage còn dưới {minimumFreeSpaceMb} MB dung lượng trống."));
            }

            return Task.FromResult(HealthCheckResult.Healthy("Storage có thể ghi và còn đủ dung lượng."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Storage không thể ghi.", ex));
        }
    }

    private static void WriteProbe(string root)
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, $".health-{Guid.NewGuid():N}");
        File.WriteAllText(path, "ok");
        File.Delete(path);
    }
}

public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger, IConfiguration configuration)
{
    private const string RequestIdHeader = "X-Request-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[RequestIdHeader].FirstOrDefault();
        if (IsSafeRequestId(incoming)) context.TraceIdentifier = incoming!;
        context.Response.Headers[RequestIdHeader] = context.TraceIdentifier;

        var stopwatch = Stopwatch.StartNew();
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["RequestId"] = context.TraceIdentifier,
            ["Method"] = context.Request.Method,
            ["Path"] = context.Request.Path.Value
        });
        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();
            var elapsedMs = Math.Round(stopwatch.Elapsed.TotalMilliseconds, 1);
            var slowThresholdMs = Math.Max(100, configuration.GetValue<double?>("Operations:SlowRequestThresholdMs") ?? 1500);
            if (elapsedMs >= slowThresholdMs)
            {
                logger.LogWarning(
                    "Slow HTTP {Method} {Path} -> {StatusCode} in {ElapsedMs} ms; trace={TraceId}",
                    context.Request.Method, context.Request.Path.Value, context.Response.StatusCode, elapsedMs, context.TraceIdentifier);
            }
            else
            {
                logger.LogInformation(
                    "HTTP {Method} {Path} -> {StatusCode} in {ElapsedMs} ms; trace={TraceId}",
                    context.Request.Method, context.Request.Path.Value, context.Response.StatusCode, elapsedMs, context.TraceIdentifier);
            }
        }
    }

    private static bool IsSafeRequestId(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 64 && value.All(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' or ':');
}

public static class ProductionStartupGuard
{
    public static IReadOnlyList<string> Validate(IConfiguration configuration, IHostEnvironment environment, StoragePaths storagePaths)
    {
        if (!environment.IsProduction()) return [];

        var errors = new List<string>();
        var warnings = new List<string>();
        if (configuration.GetValue<bool>("Database:AutoMigrate")) errors.Add("Production không được bật Database:AutoMigrate.");
        if (configuration.GetValue<bool>("Database:SeedOnStartup")) errors.Add("Production không được bật Database:SeedOnStartup.");
        if (!configuration.GetValue<bool>("Storage:RequirePersistent")) errors.Add("Production phải bật Storage:RequirePersistent.");
        if (!storagePaths.DataRootExplicit || !storagePaths.MediaPublicRootExplicit) errors.Add("Production phải cấu hình rõ Storage:DataRoot và Storage:MediaPublicRoot.");

        var allowedHosts = configuration["AllowedHosts"]?.Trim();
        if (string.IsNullOrWhiteSpace(allowedHosts) || allowedHosts == "*")
            warnings.Add("AllowedHosts đang để '*'. Nên cấu hình domain production cụ thể.");
        if ((configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? []).Length == 0)
            warnings.Add("Chưa cấu hình ReverseProxy:KnownProxies; chỉ phù hợp nếu app không đứng sau proxy ngoài loopback.");

        if (errors.Count > 0)
            throw new InvalidOperationException("Production startup guard failed: " + string.Join(" ", errors));
        return warnings;
    }
}

public static class HealthResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        var environment = context.RequestServices.GetRequiredService<IHostEnvironment>();
        var configuration = context.RequestServices.GetRequiredService<IConfiguration>();
        var exposeDetails = environment.IsDevelopment() || configuration.GetValue<bool>("Operations:ExposeHealthDetails");
        var payload = new
        {
            status = report.Status.ToString(),
            durationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 1),
            checks = report.Entries.ToDictionary(
                pair => pair.Key,
                pair => new
                {
                    status = pair.Value.Status.ToString(),
                    description = exposeDetails ? pair.Value.Description : null,
                    durationMs = Math.Round(pair.Value.Duration.TotalMilliseconds, 1)
                })
        };
        return JsonSerializer.SerializeAsync(context.Response.Body, payload, JsonOptions, context.RequestAborted);
    }
}
