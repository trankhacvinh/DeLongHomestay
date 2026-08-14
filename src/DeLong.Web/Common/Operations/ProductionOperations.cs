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
            return await db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("PostgreSQL sẵn sàng.")
                : HealthCheckResult.Unhealthy("Không thể kết nối PostgreSQL.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Không thể kết nối PostgreSQL.", ex);
        }
    }
}

public sealed class StorageHealthCheck(StoragePaths paths, IWebHostEnvironment environment) : IHealthCheck
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
            return Task.FromResult(HealthCheckResult.Healthy("Storage có thể ghi."));
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

public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();
            logger.LogInformation(
                "HTTP {Method} {Path} -> {StatusCode} in {ElapsedMs} ms; trace={TraceId}",
                context.Request.Method,
                context.Request.Path.Value,
                context.Response.StatusCode,
                Math.Round(stopwatch.Elapsed.TotalMilliseconds, 1),
                context.TraceIdentifier);
        }
    }
}

public static class HealthResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        var payload = new
        {
            status = report.Status.ToString(),
            durationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 1),
            checks = report.Entries.ToDictionary(
                pair => pair.Key,
                pair => new
                {
                    status = pair.Value.Status.ToString(),
                    description = pair.Value.Description,
                    durationMs = Math.Round(pair.Value.Duration.TotalMilliseconds, 1)
                })
        };
        return JsonSerializer.SerializeAsync(context.Response.Body, payload, JsonOptions, context.RequestAborted);
    }
}
