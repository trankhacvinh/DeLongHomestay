using DeLong.Web.Common.Operations;
using DeLong.Web.Features.Rooms;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using SkiaSharp;
using Xunit;

namespace DeLong.Tests;

public sealed class RoomImageStorageTests
{
    [Fact]
    public async Task Upload_portrait_phone_photo_does_not_fail_from_crop_rounding()
    {
        var root = Path.Combine(Path.GetTempPath(), "delong-room-image-tests", Guid.NewGuid().ToString("N"));
        var webRoot = Path.Combine(root, "wwwroot");
        var dataRoot = Path.Combine(root, "data");
        var mediaRoot = Path.Combine(root, "media", "rooms");
        Directory.CreateDirectory(webRoot);
        try
        {
            var environment = new FakeWebHostEnvironment(root, webRoot);
            var paths = new StoragePaths(dataRoot, mediaRoot, new PathString("/uploads/rooms"), true, true, true);
            paths.EnsureDirectories();
            var storage = new LocalRoomImageStorage(paths, environment);
            var jpeg = CreateJpeg(1086, 1448);
            var formFile = new FormFile(new MemoryStream(jpeg), 0, jpeg.Length, "file", "portrait.jpg")
            {
                Headers = new HeaderDictionary(),
                ContentType = "image/jpeg"
            };
            var roomId = Guid.NewGuid();
            var imageId = Guid.NewGuid();

            var (stored, error) = await storage.SaveAsync(roomId, imageId, formFile);

            Assert.Null(error);
            Assert.NotNull(stored);
            Assert.Equal(1086, stored!.Width);
            Assert.Equal(1448, stored.Height);
            var imageFolder = Path.Combine(roomId.ToString("N"), imageId.ToString("N"));
            AssertVariantDimensions(Path.Combine(mediaRoot, imageFolder, "card.webp"), 900, 675);
            AssertVariantDimensions(Path.Combine(mediaRoot, imageFolder, "thumb.webp"), 480, 360);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Upload_accepts_phone_photo_larger_than_legacy_25_megabyte_limit()
    {
        var root = Path.Combine(Path.GetTempPath(), "delong-room-image-tests", Guid.NewGuid().ToString("N"));
        var webRoot = Path.Combine(root, "wwwroot");
        Directory.CreateDirectory(webRoot);
        try
        {
            var environment = new FakeWebHostEnvironment(root, webRoot);
            var paths = new StoragePaths(
                Path.Combine(root, "data"),
                Path.Combine(root, "media"),
                new PathString("/uploads/rooms"),
                true,
                true,
                true);
            paths.EnsureDirectories();
            var storage = new LocalRoomImageStorage(paths, environment);
            var jpeg = CreateJpeg(1200, 800);
            var source = new byte[26 * 1024 * 1024];
            jpeg.CopyTo(source, 0);
            var formFile = new FormFile(new MemoryStream(source), 0, source.Length, "file", "phone.jpg")
            {
                Headers = new HeaderDictionary(),
                ContentType = "image/jpeg"
            };

            var (stored, error) = await storage.SaveAsync(Guid.NewGuid(), Guid.NewGuid(), formFile);

            Assert.Null(error);
            Assert.NotNull(stored);
            Assert.Equal(source.LongLength, stored!.OriginalBytes);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Upload_rejects_source_larger_than_shared_image_limit()
    {
        var root = Path.Combine(Path.GetTempPath(), "delong-room-image-tests", Guid.NewGuid().ToString("N"));
        var webRoot = Path.Combine(root, "wwwroot");
        Directory.CreateDirectory(webRoot);
        try
        {
            var environment = new FakeWebHostEnvironment(root, webRoot);
            var paths = new StoragePaths(
                Path.Combine(root, "data"),
                Path.Combine(root, "media"),
                new PathString("/uploads/rooms"),
                true,
                true,
                true);
            var storage = new LocalRoomImageStorage(paths, environment);
            var formFile = new FormFile(
                new MemoryStream([0]),
                0,
                ImageUploadPolicy.MaxSourceBytes + 1,
                "file",
                "large.jpg")
            {
                Headers = new HeaderDictionary(),
                ContentType = "image/jpeg"
            };

            var (stored, error) = await storage.SaveAsync(Guid.NewGuid(), Guid.NewGuid(), formFile);

            Assert.Null(stored);
            Assert.Equal("Mỗi ảnh tối đa 60 MB.", error);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Upload_creates_original_and_optimized_webp_variants_and_can_regenerate_focal_crops()
    {
        var root = Path.Combine(Path.GetTempPath(), "delong-room-image-tests", Guid.NewGuid().ToString("N"));
        var webRoot = Path.Combine(root, "wwwroot");
        var dataRoot = Path.Combine(root, "persistent-data");
        var mediaRoot = Path.Combine(root, "persistent-media", "rooms");
        Directory.CreateDirectory(webRoot);
        try
        {
            var environment = new FakeWebHostEnvironment(root, webRoot);
            var paths = new StoragePaths(dataRoot, mediaRoot, new PathString("/uploads/rooms"), true, true, true);
            paths.EnsureDirectories();
            var storage = new LocalRoomImageStorage(paths, environment);

            var jpeg = CreateJpeg(1200, 800);

            await using var stream = new MemoryStream(jpeg);
            var formFile = new FormFile(stream, 0, jpeg.Length, "file", "room.jpg")
            {
                Headers = new HeaderDictionary(),
                ContentType = "image/jpeg"
            };

            var roomId = Guid.NewGuid();
            var imageId = Guid.NewGuid();
            var (stored, error) = await storage.SaveAsync(roomId, imageId, formFile);

            Assert.Null(error);
            Assert.NotNull(stored);
            Assert.Equal(1200, stored!.Width);
            Assert.Equal(800, stored.Height);
            Assert.StartsWith("storage://room-images/", stored.OriginalStoragePath);
            Assert.StartsWith("/uploads/rooms/", stored.LargeUrl);

            var imageFolder = Path.Combine(roomId.ToString("N"), imageId.ToString("N"));
            var original = Path.Combine(dataRoot, "room-images", imageFolder, "original.jpg");
            var large = Path.Combine(mediaRoot, imageFolder, "large.webp");
            var card = Path.Combine(mediaRoot, imageFolder, "card.webp");
            var thumb = Path.Combine(mediaRoot, imageFolder, "thumb.webp");
            Assert.True(File.Exists(original));
            Assert.True(File.Exists(large));
            Assert.True(File.Exists(card));
            Assert.True(File.Exists(thumb));
            Assert.False(File.Exists(Path.Combine(webRoot, "uploads", "rooms", imageFolder, "large.webp")));

            AssertVariantDimensions(large, 1200, 800);
            AssertVariantDimensions(card, 900, 675);
            AssertVariantDimensions(thumb, 480, 360);

            var cropError = await storage.RegenerateCropsAsync(stored, 0.12, 0.88);
            Assert.Null(cropError);
            AssertVariantDimensions(card, 900, 675);
            AssertVariantDimensions(thumb, 480, 360);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static void AssertVariantDimensions(string path, int width, int height)
    {
        using var bitmap = SKBitmap.Decode(path);
        Assert.NotNull(bitmap);
        Assert.Equal(width, bitmap.Width);
        Assert.Equal(height, bitmap.Height);
    }

    private static byte[] CreateJpeg(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(new SKColor(32, 112, 116));
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        return encoded.ToArray();
    }

    private sealed class FakeWebHostEnvironment(string contentRootPath, string webRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "DeLong.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = webRootPath;
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
