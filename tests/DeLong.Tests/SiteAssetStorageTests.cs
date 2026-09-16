using DeLong.Web.Common.Operations;
using DeLong.Web.Features.Site;
using Microsoft.AspNetCore.Http;
using SkiaSharp;
using Xunit;

namespace DeLong.Tests;

public sealed class SiteAssetStorageTests
{
    [Fact]
    public async Task SaveLogoAsync_PersistsSelectedImageWithUniquePublicUrl()
    {
        var root = CreateTempRoot();
        try
        {
            var paths = Paths(root);
            paths.EnsureDirectories();
            var storage = new LocalSiteAssetStorage(paths);

            var (first, firstError) = await storage.SaveAsync("DELONG", "logo", FormImage(SKColors.Red), CancellationToken.None);
            var (second, secondError) = await storage.SaveAsync("DELONG", "logo", FormImage(SKColors.Blue), CancellationToken.None);

            Assert.Null(firstError);
            Assert.Null(secondError);
            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.NotEqual(first.Url, second.Url);
            Assert.NotEqual(first.StorageKey, second.StorageKey);
            Assert.StartsWith("/uploads/site/delong/logo-", second.Url, StringComparison.Ordinal);
            Assert.True(storage.Exists(second.StorageKey));

            var storedPath = Path.Combine(paths.SitePublicRoot, second.StorageKey["site/".Length..]);
            using var stored = SKBitmap.Decode(storedPath);
            Assert.NotNull(stored);
            var center = stored.GetPixel(stored.Width / 2, stored.Height / 2);
            Assert.True(center.Blue > 200);
            Assert.True(center.Red < 50);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveFaviconAsync_UsesSiteSiblingDirectoryAndCreates64PixelPng()
    {
        var root = CreateTempRoot();
        try
        {
            var paths = Paths(root);
            paths.EnsureDirectories();
            var storage = new LocalSiteAssetStorage(paths);

            var (asset, error) = await storage.SaveAsync("DeLong", "favicon", FormImage(SKColors.Green, 120, 80), CancellationToken.None);

            Assert.Null(error);
            Assert.NotNull(asset);
            Assert.Equal(new PathString("/uploads/site"), paths.SiteRequestPath);
            Assert.Equal(Path.Combine(root, "uploads", "site"), paths.SitePublicRoot);
            Assert.StartsWith("/uploads/site/delong/favicon-64-", asset.Url, StringComparison.Ordinal);
            Assert.Equal(64, asset.Width);
            Assert.Equal(64, asset.Height);

            var storedPath = Path.Combine(paths.SitePublicRoot, asset.StorageKey["site/".Length..]);
            using var stored = SKBitmap.Decode(storedPath);
            Assert.NotNull(stored);
            Assert.Equal(64, stored.Width);
            Assert.Equal(64, stored.Height);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static StoragePaths Paths(string root) => new(
        Path.Combine(root, "data"),
        Path.Combine(root, "uploads", "rooms"),
        new PathString("/uploads/rooms"),
        DataRootExplicit: true,
        MediaPublicRootExplicit: true,
        RequirePersistent: false);

    private static FormFile FormImage(SKColor color, int width = 80, int height = 80)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(color);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        var bytes = encoded.ToArray();
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "selected.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };
    }

    private static string CreateTempRoot() => Path.Combine(Path.GetTempPath(), $"delong-site-assets-{Guid.NewGuid():N}");
}
