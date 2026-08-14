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
    public async Task Upload_creates_original_and_optimized_webp_variants_and_can_regenerate_focal_crops()
    {
        var root = Path.Combine(Path.GetTempPath(), "delong-room-image-tests", Guid.NewGuid().ToString("N"));
        var webRoot = Path.Combine(root, "wwwroot");
        Directory.CreateDirectory(webRoot);
        try
        {
            var environment = new FakeWebHostEnvironment(root, webRoot);
            var storage = new LocalRoomImageStorage(environment);

            byte[] jpeg;
            using (var bitmap = new SKBitmap(1200, 800))
            using (var canvas = new SKCanvas(bitmap))
            using (var image = SKImage.FromBitmap(bitmap))
            {
                canvas.Clear(new SKColor(32, 112, 116));
                using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 90);
                jpeg = encoded.ToArray();
            }

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
            Assert.StartsWith("App_Data/room-images/", stored.OriginalStoragePath);

            var original = Path.Combine(root, stored.OriginalStoragePath.Replace('/', Path.DirectorySeparatorChar));
            var large = ToLocalWebPath(webRoot, stored.LargeUrl);
            var card = ToLocalWebPath(webRoot, stored.CardUrl);
            var thumb = ToLocalWebPath(webRoot, stored.ThumbnailUrl);
            Assert.True(File.Exists(original));
            Assert.True(File.Exists(large));
            Assert.True(File.Exists(card));
            Assert.True(File.Exists(thumb));

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

    private static string ToLocalWebPath(string webRoot, string url) =>
        Path.Combine(webRoot, url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

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
