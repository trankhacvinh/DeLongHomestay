using System.Security.Cryptography;
using DeLong.Web.Common.Operations;
using DeLong.Web.Features.PublicBooking;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace DeLong.Tests;

public sealed class IdentityDocumentStorageTests
{
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    [Fact]
    public async Task SaveAsync_EncryptsAtRest_AndReadAsyncRestoresImage()
    {
        var root = Path.Combine(Path.GetTempPath(), "delong-id-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var key = RandomNumberGenerator.GetBytes(32);
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Security:IdentityDocumentEncryptionKeyBase64"] = Convert.ToBase64String(key)
                })
                .Build();
            var paths = new StoragePaths(root, Path.Combine(root, "public"), new PathString("/uploads/rooms"), true, true, false);
            var storage = new IdentityDocumentStorage(paths, configuration);
            await using var input = new MemoryStream(TinyPng);
            var file = new FormFile(input, 0, TinyPng.Length, "file", "cccd-front.png")
            {
                Headers = new HeaderDictionary(),
                ContentType = "image/png"
            };
            file.Headers.ContentType = new StringValues("image/png");
            var propertyId = Guid.NewGuid();
            var bookingId = Guid.NewGuid();

            var (saved, error) = await storage.SaveAsync(propertyId, bookingId, "front", file);

            Assert.Null(error);
            Assert.NotNull(saved);
            var encryptedPath = storage.GetEncryptedPathForDiagnostics(propertyId, bookingId, "front");
            var encrypted = await File.ReadAllBytesAsync(encryptedPath);
            Assert.False(encrypted.AsSpan().IndexOf(TinyPng) >= 0);
            Assert.Equal("DLID1", System.Text.Encoding.ASCII.GetString(encrypted, 0, 5));

            var read = await storage.ReadAsync(propertyId, bookingId, "front");
            Assert.NotNull(read);
            Assert.Equal("image/png", read.ContentType);
            Assert.Equal(TinyPng, read.Bytes);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task ReadAsync_FailsWhenFileIsMovedToDifferentBooking()
    {
        var root = Path.Combine(Path.GetTempPath(), "delong-id-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Security:IdentityDocumentEncryptionKeyBase64"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                })
                .Build();
            var paths = new StoragePaths(root, Path.Combine(root, "public"), new PathString("/uploads/rooms"), true, true, false);
            var storage = new IdentityDocumentStorage(paths, configuration);
            await using var input = new MemoryStream(TinyPng);
            var file = new FormFile(input, 0, TinyPng.Length, "file", "front.png") { Headers = new HeaderDictionary(), ContentType = "image/png" };
            var propertyId = Guid.NewGuid();
            var firstBooking = Guid.NewGuid();
            var secondBooking = Guid.NewGuid();
            await storage.SaveAsync(propertyId, firstBooking, "front", file);
            var source = storage.GetEncryptedPathForDiagnostics(propertyId, firstBooking, "front");
            var target = storage.GetEncryptedPathForDiagnostics(propertyId, secondBooking, "front");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target, true);

            await Assert.ThrowsAnyAsync<CryptographicException>(() => storage.ReadAsync(propertyId, secondBooking, "front"));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void MissingKey_DisablesStorage()
    {
        var root = Path.Combine(Path.GetTempPath(), "delong-id-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new StoragePaths(root, Path.Combine(root, "public"), new PathString("/uploads/rooms"), true, true, false);
            var storage = new IdentityDocumentStorage(paths, new ConfigurationBuilder().Build());
            Assert.False(storage.IsConfigured);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
