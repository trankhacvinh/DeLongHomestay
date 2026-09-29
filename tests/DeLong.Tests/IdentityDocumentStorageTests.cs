using System.Security.Cryptography;
using DeLong.Web.Common.Operations;
using DeLong.Web.Features.PublicBooking;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using SkiaSharp;
using Xunit;

namespace DeLong.Tests;

public sealed class IdentityDocumentStorageTests
{
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    [Fact]
    public async Task AutoManagedKey_EncryptsAtRest_AndSurvivesRestart()
    {
        var root = TempRoot();
        try
        {
            var paths = Paths(root);
            var emptyConfig = new ConfigurationBuilder().Build();
            var storage = new IdentityDocumentStorage(paths, emptyConfig);
            Assert.True(storage.IsConfigured);

            var keyPath = storage.GetMasterKeyPathForDiagnostics();
            Assert.True(File.Exists(keyPath));
            Assert.Equal(32, new FileInfo(keyPath).Length);

            var propertyId = Guid.NewGuid();
            var bookingId = Guid.NewGuid();
            var (saved, error) = await storage.SaveAsync(propertyId, bookingId, "front", FormImage("cccd-front.png"));

            Assert.Null(error);
            Assert.NotNull(saved);
            var encryptedPath = storage.GetEncryptedPathForDiagnostics(propertyId, bookingId, "front");
            var encrypted = await File.ReadAllBytesAsync(encryptedPath);
            Assert.False(encrypted.AsSpan().IndexOf(TinyPng) >= 0);
            Assert.Equal("DLID1", System.Text.Encoding.ASCII.GetString(encrypted, 0, 5));

            var restarted = new IdentityDocumentStorage(paths, new ConfigurationBuilder().Build());
            var read = await restarted.ReadAsync(propertyId, bookingId, "front");
            Assert.NotNull(read);
            Assert.Equal("image/webp", read.ContentType);
            Assert.EndsWith(".webp", read.OriginalFileName, StringComparison.OrdinalIgnoreCase);
            using var optimized = SKBitmap.Decode(read.Bytes);
            Assert.NotNull(optimized);
            Assert.Equal(1, optimized.Width);
            Assert.Equal(1, optimized.Height);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task ReadAsync_FailsWhenFileIsMovedToDifferentBooking()
    {
        var root = TempRoot();
        try
        {
            var storage = new IdentityDocumentStorage(Paths(root), new ConfigurationBuilder().Build());
            var propertyId = Guid.NewGuid();
            var firstBooking = Guid.NewGuid();
            var secondBooking = Guid.NewGuid();
            await storage.SaveAsync(propertyId, firstBooking, "front", FormImage("front.png"));
            var source = storage.GetEncryptedPathForDiagnostics(propertyId, firstBooking, "front");
            var target = storage.GetEncryptedPathForDiagnostics(propertyId, secondBooking, "front");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target, true);

            await Assert.ThrowsAnyAsync<CryptographicException>(() => storage.ReadAsync(propertyId, secondBooking, "front"));
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task LegacyConfiguredKey_IsPersistedSoExternalSecretCanBeRemoved()
    {
        var root = TempRoot();
        try
        {
            var legacyKey = RandomNumberGenerator.GetBytes(32);
            var legacyConfig = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Security:IdentityDocumentEncryptionKeyBase64"] = Convert.ToBase64String(legacyKey)
                })
                .Build();
            var paths = Paths(root);
            var propertyId = Guid.NewGuid();
            var bookingId = Guid.NewGuid();
            var storage = new IdentityDocumentStorage(paths, legacyConfig);

            Assert.True(storage.IsConfigured);
            Assert.Equal(legacyKey, await File.ReadAllBytesAsync(storage.GetMasterKeyPathForDiagnostics()));
            await storage.SaveAsync(propertyId, bookingId, "front", FormImage("front.png"));

            var withoutSecret = new IdentityDocumentStorage(paths, new ConfigurationBuilder().Build());
            var read = await withoutSecret.ReadAsync(propertyId, bookingId, "front");
            Assert.NotNull(read);
            Assert.Equal("image/webp", read.ContentType);
            using var optimized = SKBitmap.Decode(read.Bytes);
            Assert.NotNull(optimized);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task SaveAsync_ResizesLargeIdentityImageBeforeEncryption()
    {
        var root = TempRoot();
        try
        {
            var storage = new IdentityDocumentStorage(Paths(root), new ConfigurationBuilder().Build());
            var source = FormImage("large-cccd.png", CreatePng(3600, 1800));
            var propertyId = Guid.NewGuid();
            var bookingId = Guid.NewGuid();

            var (saved, error) = await storage.SaveAsync(propertyId, bookingId, "front", source);

            Assert.Null(error);
            Assert.NotNull(saved);
            Assert.Equal("image/webp", saved.ContentType);
            Assert.True(saved.Bytes < source.Length);
            var read = await storage.ReadAsync(propertyId, bookingId, "front");
            Assert.NotNull(read);
            using var optimized = SKBitmap.Decode(read.Bytes);
            Assert.NotNull(optimized);
            Assert.Equal(3000, optimized.Width);
            Assert.Equal(1500, optimized.Height);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task MissingMasterKey_WithExistingEncryptedFiles_DoesNotGenerateReplacement()
    {
        var root = TempRoot();
        try
        {
            var paths = Paths(root);
            var storage = new IdentityDocumentStorage(paths, new ConfigurationBuilder().Build());
            await storage.SaveAsync(Guid.NewGuid(), Guid.NewGuid(), "front", FormImage("front.png"));
            var keyPath = storage.GetMasterKeyPathForDiagnostics();
            Assert.True(File.Exists(keyPath));
            File.Delete(keyPath);

            var restarted = new IdentityDocumentStorage(paths, new ConfigurationBuilder().Build());

            Assert.False(restarted.IsConfigured);
            Assert.False(File.Exists(keyPath));
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static FormFile FormImage(string fileName, byte[]? bytes = null)
    {
        bytes ??= TinyPng;
        var stream = new MemoryStream(bytes, writable: false);
        return new FormFile(stream, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };
    }

    private static byte[] CreatePng(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
        canvas.DrawRect(SKRect.Create(100, 100, width - 200, height - 200), paint);
        paint.Color = SKColors.White;
        canvas.DrawRect(SKRect.Create(180, 180, width - 360, height - 360), paint);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }

    private static StoragePaths Paths(string root) =>
        new(root, Path.Combine(root, "public"), new PathString("/uploads/rooms"), true, true, false);

    private static string TempRoot() => Path.Combine(Path.GetTempPath(), "delong-id-test-" + Guid.NewGuid().ToString("N"));

    private static void Cleanup(string root)
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
