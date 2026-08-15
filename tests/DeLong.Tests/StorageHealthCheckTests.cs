using DeLong.Web.Common.Operations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace DeLong.Tests;

public sealed class StorageHealthCheckTests
{
    [Fact]
    public async Task Development_allows_implicit_local_storage_even_when_persistent_is_required_by_profile()
    {
        var root = Path.Combine(Path.GetTempPath(), "delong-storage-health-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var environment = new FakeWebHostEnvironment(root, Path.Combine(root, "wwwroot"), "Development");
            var paths = new StoragePaths(
                Path.Combine(root, "App_Data"),
                Path.Combine(root, "wwwroot", "uploads", "rooms"),
                new PathString("/uploads/rooms"),
                DataRootExplicit: false,
                MediaPublicRootExplicit: false,
                RequirePersistent: true);

            var result = await new StorageHealthCheck(paths, environment)
                .CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Healthy, result.Status);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Production_requires_explicit_persistent_storage_when_required()
    {
        var root = Path.Combine(Path.GetTempPath(), "delong-storage-health-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var environment = new FakeWebHostEnvironment(root, Path.Combine(root, "wwwroot"), "Production");
            var paths = new StoragePaths(
                Path.Combine(root, "App_Data"),
                Path.Combine(root, "wwwroot", "uploads", "rooms"),
                new PathString("/uploads/rooms"),
                DataRootExplicit: false,
                MediaPublicRootExplicit: false,
                RequirePersistent: true);

            var result = await new StorageHealthCheck(paths, environment)
                .CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Unhealthy, result.Status);
            Assert.Contains("persistent volume", result.Description);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Production_accepts_explicit_app_data_and_wwwroot_uploads()
    {
        var root = Path.Combine(Path.GetTempPath(), "delong-storage-health-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var environment = new FakeWebHostEnvironment(root, Path.Combine(root, "wwwroot"), "Production");
            var paths = new StoragePaths(
                Path.Combine(root, "App_Data"),
                Path.Combine(root, "wwwroot", "uploads", "rooms"),
                new PathString("/uploads/rooms"),
                DataRootExplicit: true,
                MediaPublicRootExplicit: true,
                RequirePersistent: true);

            var result = await new StorageHealthCheck(paths, environment)
                .CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Healthy, result.Status);
            Assert.Contains("Storage có thể ghi", result.Description);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private sealed class FakeWebHostEnvironment(string contentRootPath, string webRootPath, string environmentName) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "DeLong.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = webRootPath;
        public string EnvironmentName { get; set; } = environmentName;
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
