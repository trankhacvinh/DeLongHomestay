using System.Security.Cryptography;
using System.Text;
using DeLong.Web.Common.Operations;
using SkiaSharp;

namespace DeLong.Web.Features.PublicBooking;

public sealed record IdentityDocumentInfo(
    string Side,
    string ContentType,
    string OriginalFileName,
    long Bytes);

public sealed record IdentityDocumentReadResult(
    byte[] Bytes,
    string ContentType,
    string OriginalFileName);

public sealed class IdentityDocumentStorage
{
    private const int MaxStoredDimension = 3000;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int MasterKeyBytes = 32;
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("DLID1");
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp" };

    private static readonly TimeSpan KeyRetryInterval = TimeSpan.FromSeconds(30);
    private static readonly string[] Sides = ["front", "back", "second-front", "second-back"];

    private readonly string root;
    private readonly string masterKeyPath;
    private readonly IConfiguration configuration;
    private readonly object keyGate = new();
    private volatile byte[]? key;
    private long nextKeyAttemptTicks;

    // Registered as a singleton: the master key is loaded once instead of on every request.
    public IdentityDocumentStorage(StoragePaths paths, IConfiguration configuration)
    {
        root = Path.Combine(paths.DataRoot, "private", "identity-documents");
        masterKeyPath = Path.Combine(paths.DataRoot, "security", "identity-master.key");
        this.configuration = configuration;
        TryLoadKey();
    }

    public bool IsConfigured => key is { Length: MasterKeyBytes } || TryLoadKey();

    // A missing or unreadable key is retried at most every 30 seconds, so restoring DataRoot
    // does not need an application restart while the hot path stays free of file I/O.
    private bool TryLoadKey()
    {
        if (DateTime.UtcNow.Ticks < Interlocked.Read(ref nextKeyAttemptTicks)) return false;
        lock (keyGate)
        {
            if (key is { Length: MasterKeyBytes }) return true;
            if (DateTime.UtcNow.Ticks < Interlocked.Read(ref nextKeyAttemptTicks)) return false;
            byte[]? loaded;
            try
            {
                Directory.CreateDirectory(root);
                loaded = LoadOrCreateMasterKey(
                    masterKeyPath,
                    root,
                    configuration["Security:IdentityDocumentEncryptionKeyBase64"]);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
            {
                loaded = null;
            }
            if (loaded is { Length: MasterKeyBytes })
            {
                key = loaded;
                return true;
            }
            Interlocked.Exchange(ref nextKeyAttemptTicks, DateTime.UtcNow.Add(KeyRetryInterval).Ticks);
            return false;
        }
    }

    public async Task<(IdentityDocumentInfo? Document, string? Error)> SaveAsync(
        Guid propertyId,
        Guid bookingId,
        string side,
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured) return (null, "Kho CCCD không thể khởi tạo khóa mã hóa. Hãy kiểm tra quyền ghi DataRoot hoặc khôi phục DataRoot đầy đủ từ bản sao lưu.");
        var normalizedSide = NormalizeSide(side);
        if (normalizedSide is null) return (null, "Mặt giấy tờ không hợp lệ.");
        if (file.Length <= 0) return (null, "Ảnh CCCD trống.");
        if (file.Length > ImageUploadPolicy.MaxSourceBytes)
            return (null, $"Mỗi ảnh CCCD tối đa {ImageUploadPolicy.MaxSourceMegabytes} MB.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var contentType = file.ContentType?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!AllowedExtensions.Contains(extension) || !AllowedContentTypes.Contains(contentType))
            return (null, "Chỉ hỗ trợ ảnh JPG, PNG hoặc WebP.");

        await using var memory = new MemoryStream((int)Math.Min(file.Length, int.MaxValue));
        await file.CopyToAsync(memory, cancellationToken);
        var optimized = OptimizeImage(memory.ToArray());
        if (optimized is null) return (null, "File tải lên không phải ảnh hợp lệ.");
        var imageBytes = optimized;

        const string optimizedContentType = "image/webp";
        var originalFileName = SafeFileName(file.FileName, ".webp");
        return (await WriteEncryptedAsync(propertyId, bookingId, normalizedSide, optimizedContentType, originalFileName, imageBytes, cancellationToken), null);
    }

    public async Task<IdentityDocumentInfo?> CopyAsync(
        Guid propertyId, Guid sourceOwnerId, Guid targetOwnerId, string side,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeSide(side);
        if (!IsConfigured || normalized is null) return null;
        var saved = await ReadAsync(propertyId, sourceOwnerId, normalized, cancellationToken);
        if (saved is null) return null;
        try
        {
            return await WriteEncryptedAsync(propertyId, targetOwnerId, normalized, saved.ContentType,
                saved.OriginalFileName, saved.Bytes, cancellationToken);
        }
        finally { CryptographicOperations.ZeroMemory(saved.Bytes); }
    }

    private async Task<IdentityDocumentInfo> WriteEncryptedAsync(
        Guid propertyId, Guid bookingId, string side, string contentType, string originalFileName,
        byte[] imageBytes, CancellationToken cancellationToken)
    {
        var plaintext = BuildPlaintext(contentType, originalFileName, imageBytes);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];
        var aad = AssociatedData(propertyId, bookingId, side);
        using (var aes = new AesGcm(key!, TagSize))
            aes.Encrypt(nonce, plaintext, ciphertext, tag, aad);
        CryptographicOperations.ZeroMemory(plaintext);

        var envelope = new byte[Magic.Length + NonceSize + TagSize + ciphertext.Length];
        var offset = 0;
        Buffer.BlockCopy(Magic, 0, envelope, offset, Magic.Length); offset += Magic.Length;
        Buffer.BlockCopy(nonce, 0, envelope, offset, NonceSize); offset += NonceSize;
        Buffer.BlockCopy(tag, 0, envelope, offset, TagSize); offset += TagSize;
        Buffer.BlockCopy(ciphertext, 0, envelope, offset, ciphertext.Length);

        var directory = DirectoryFor(propertyId, bookingId);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, side + ".dlid");
        var temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            await File.WriteAllBytesAsync(temp, envelope, cancellationToken);
            File.Move(temp, path, true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
            CryptographicOperations.ZeroMemory(ciphertext);
        }

        return new IdentityDocumentInfo(side, contentType, originalFileName, imageBytes.LongLength);
    }

    public async Task<IdentityDocumentReadResult?> ReadAsync(
        Guid propertyId,
        Guid bookingId,
        string side,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured) return null;
        var normalizedSide = NormalizeSide(side);
        if (normalizedSide is null) return null;
        var path = Path.Combine(DirectoryFor(propertyId, bookingId), normalizedSide + ".dlid");
        if (!File.Exists(path)) return null;

        var envelope = await File.ReadAllBytesAsync(path, cancellationToken);
        if (envelope.Length <= Magic.Length + NonceSize + TagSize || !envelope.AsSpan(0, Magic.Length).SequenceEqual(Magic))
            throw new CryptographicException("Encrypted identity document envelope is invalid.");

        var offset = Magic.Length;
        var nonce = envelope.AsSpan(offset, NonceSize).ToArray(); offset += NonceSize;
        var tag = envelope.AsSpan(offset, TagSize).ToArray(); offset += TagSize;
        var ciphertext = envelope.AsSpan(offset).ToArray();
        var plaintext = new byte[ciphertext.Length];
        var aad = AssociatedData(propertyId, bookingId, normalizedSide);
        try
        {
            using (var aes = new AesGcm(key!, TagSize))
                aes.Decrypt(nonce, ciphertext, tag, plaintext, aad);
            return ParsePlaintext(plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(ciphertext);
        }
    }

    /// <summary>
    /// Lists which sides are stored without decrypting them. Callers only need the side list
    /// (admin booking detail, customer profile counts); decrypting every photo here made each
    /// poll read and decrypt up to four full images. Use <see cref="ReadAsync"/> to verify or
    /// download a document.
    /// </summary>
    public Task<IReadOnlyList<IdentityDocumentInfo>> ListAsync(
        Guid propertyId,
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = new List<IdentityDocumentInfo>(4);
        if (!IsConfigured) return Task.FromResult<IReadOnlyList<IdentityDocumentInfo>>(result);
        var directory = DirectoryFor(propertyId, bookingId);
        if (!Directory.Exists(directory)) return Task.FromResult<IReadOnlyList<IdentityDocumentInfo>>(result);
        foreach (var side in Sides)
        {
            var file = new FileInfo(Path.Combine(directory, side + ".dlid"));
            if (!file.Exists || file.Length <= Magic.Length + NonceSize + TagSize) continue;
            var payloadBytes = Math.Max(0, file.Length - Magic.Length - NonceSize - TagSize);
            result.Add(new IdentityDocumentInfo(side, "image/webp", side + ".webp", payloadBytes));
        }
        return Task.FromResult<IReadOnlyList<IdentityDocumentInfo>>(result);
    }

    public Task DeleteAsync(Guid propertyId, Guid bookingId, CancellationToken cancellationToken = default)
    {
        var directory = DirectoryFor(propertyId, bookingId);
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
        return Task.CompletedTask;
    }

    public string GetEncryptedPathForDiagnostics(Guid propertyId, Guid bookingId, string side)
    {
        var normalizedSide = NormalizeSide(side) ?? throw new ArgumentException("Invalid side.", nameof(side));
        return Path.Combine(DirectoryFor(propertyId, bookingId), normalizedSide + ".dlid");
    }

    public string GetMasterKeyPathForDiagnostics() => masterKeyPath;

    private string DirectoryFor(Guid propertyId, Guid bookingId) =>
        Path.Combine(root, propertyId.ToString("N"), bookingId.ToString("N"));

    private static byte[]? LoadOrCreateMasterKey(string path, string identityRoot, string? legacyConfiguredKey)
    {
        var legacyKey = ParseKey(legacyConfiguredKey);
        var directory = Path.GetDirectoryName(path)!;
        try
        {
            Directory.CreateDirectory(directory);
            if (File.Exists(path)) return ReadMasterKey(path);

            // Never silently generate a replacement key when encrypted CCCD already exists.
            // In that situation the operator must restore the missing DataRoot/security key file,
            // or keep the legacy external key configured long enough for it to be persisted here.
            if (legacyKey is null && HasEncryptedDocuments(identityRoot)) return null;

            var candidate = legacyKey ?? RandomNumberGenerator.GetBytes(MasterKeyBytes);
            try
            {
                using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    stream.Write(candidate, 0, candidate.Length);
                    stream.Flush(true);
                }
                RestrictKeyPermissions(path);
                return candidate;
            }
            catch (IOException) when (File.Exists(path))
            {
                CryptographicOperations.ZeroMemory(candidate);
                return ReadMasterKey(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Backward compatibility for a deployment that already supplied the old secret:
                // continue using it even when this filesystem cannot persist the convenience key.
                if (legacyKey is not null) return legacyKey;
                CryptographicOperations.ZeroMemory(candidate);
                return null;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
        {
            return legacyKey;
        }
    }

    private static byte[]? ReadMasterKey(string path)
    {
        var value = File.ReadAllBytes(path);
        if (value.Length == MasterKeyBytes) return value;
        CryptographicOperations.ZeroMemory(value);
        return null;
    }

    private static bool HasEncryptedDocuments(string identityRoot)
    {
        if (!Directory.Exists(identityRoot)) return false;
        return Directory.EnumerateFiles(identityRoot, "*.dlid", SearchOption.AllDirectories).Any();
    }

    private static void RestrictKeyPermissions(string path)
    {
        if (OperatingSystem.IsWindows()) return;
        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            // Some mounted filesystems do not expose Unix chmod semantics. The key remains under
            // private DataRoot; deployment-level directory permissions still apply.
        }
    }

    private static byte[]? ParseKey(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        try
        {
            var value = Convert.FromBase64String(raw.Trim());
            return value.Length == MasterKeyBytes ? value : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string? NormalizeSide(string? side) => side?.Trim().ToLowerInvariant() switch
    {
        "front" => "front",
        "back" => "back",
        "second-front" => "second-front",
        "second-back" => "second-back",
        _ => null
    };

    private static byte[] AssociatedData(Guid propertyId, Guid bookingId, string side) =>
        Encoding.UTF8.GetBytes($"DeLongHomestay|identity-v1|{propertyId:N}|{bookingId:N}|{side}");

    private static byte[] BuildPlaintext(string contentType, string fileName, byte[] imageBytes)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
        {
            writer.Write(contentType);
            writer.Write(fileName);
            writer.Write(imageBytes.Length);
            writer.Write(imageBytes);
        }
        return stream.ToArray();
    }

    private static IdentityDocumentReadResult ParsePlaintext(byte[] plaintext)
    {
        using var stream = new MemoryStream(plaintext, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, true);
        var contentType = reader.ReadString();
        var fileName = reader.ReadString();
        var length = reader.ReadInt32();
        if (length <= 0 || length > ImageUploadPolicy.MaxSourceBytes || length > stream.Length - stream.Position)
            throw new CryptographicException("Encrypted identity document payload is invalid.");
        var bytes = reader.ReadBytes(length);
        if (bytes.Length != length) throw new CryptographicException("Encrypted identity document payload is truncated.");
        return new IdentityDocumentReadResult(bytes, contentType, fileName);
    }

    private const int StoredWebpQuality = 82;

    private static byte[]? OptimizeImage(byte[] bytes)
    {
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        if (codec is null || codec.Info.Width <= 0 || codec.Info.Height <= 0) return null;
        if ((long)codec.Info.Width * codec.Info.Height > ImageUploadPolicy.MaxPixels) return null;
        using var decoded = DecodeNearMax(codec, MaxStoredDimension);
        if (decoded is null) return null;
        using var resized = ResizeMax(decoded, MaxStoredDimension);
        using var normalized = NormalizeOrientation(resized ?? decoded, codec.EncodedOrigin);
        using var image = SKImage.FromBitmap(normalized ?? resized ?? decoded);
        using var encoded = image.Encode(SKEncodedImageFormat.Webp, StoredWebpQuality);
        return encoded?.ToArray();
    }

    // Let the codec downscale while decoding (JPEG can decode at 1/2, 1/4, 1/8) so a 12MP phone
    // photo never materialises as a full-resolution bitmap. Falls back to a full decode.
    private static SKBitmap? DecodeNearMax(SKCodec codec, int maxSize)
    {
        var info = codec.Info;
        var longest = Math.Max(info.Width, info.Height);
        if (longest > maxSize)
        {
            var scaled = codec.GetScaledDimensions((float)maxSize / longest);
            if (scaled.Width > 0 && scaled.Height > 0 && (scaled.Width < info.Width || scaled.Height < info.Height) &&
                Math.Max(scaled.Width, scaled.Height) >= maxSize / 2)
            {
                var target = new SKBitmap(new SKImageInfo(scaled.Width, scaled.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
                var result = codec.GetPixels(target.Info, target.GetPixels());
                if (result is SKCodecResult.Success or SKCodecResult.IncompleteInput) return target;
                target.Dispose();
            }
        }
        return SKBitmap.Decode(codec, new SKImageInfo(info.Width, info.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
    }

    /// <summary>Returns a resized copy, or null when the source already fits.</summary>
    private static SKBitmap? ResizeMax(SKBitmap source, int maxSize)
    {
        if (source.Width <= maxSize && source.Height <= maxSize) return null;
        var scale = Math.Min((float)maxSize / source.Width, (float)maxSize / source.Height);
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));
        var target = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(target);
        canvas.Clear(SKColors.White);
        canvas.DrawBitmap(source, SKRect.Create(0, 0, width, height), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        canvas.Flush();
        return target;
    }

    /// <summary>Returns a rotated copy, or null when no rotation is needed.</summary>
    private static SKBitmap? NormalizeOrientation(SKBitmap source, SKEncodedOrigin origin)
    {
        if (origin is SKEncodedOrigin.Default or SKEncodedOrigin.TopLeft) return null;
        var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var target = new SKBitmap(new SKImageInfo(swap ? source.Height : source.Width, swap ? source.Width : source.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(target);
        switch (origin)
        {
            case SKEncodedOrigin.TopRight:
                canvas.Translate(target.Width, 0); canvas.Scale(-1, 1); break;
            case SKEncodedOrigin.BottomRight:
                canvas.Translate(target.Width, target.Height); canvas.RotateDegrees(180); break;
            case SKEncodedOrigin.BottomLeft:
                canvas.Translate(0, target.Height); canvas.Scale(1, -1); break;
            case SKEncodedOrigin.LeftTop:
                canvas.RotateDegrees(90); canvas.Scale(1, -1); break;
            case SKEncodedOrigin.RightTop:
                canvas.Translate(target.Width, 0); canvas.RotateDegrees(90); break;
            case SKEncodedOrigin.RightBottom:
                canvas.Translate(target.Width, target.Height); canvas.RotateDegrees(90); canvas.Scale(-1, 1); break;
            case SKEncodedOrigin.LeftBottom:
                canvas.Translate(0, target.Height); canvas.RotateDegrees(-90); break;
        }
        canvas.DrawBitmap(source, 0, 0, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None), null);
        canvas.Flush();
        return target;
    }

    private static string SafeFileName(string? value, string extension)
    {
        var stem = Path.GetFileNameWithoutExtension(value ?? "cccd");
        stem = new string(stem.Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or ' ').ToArray()).Trim();
        if (string.IsNullOrWhiteSpace(stem)) stem = "cccd";
        if (stem.Length > 80) stem = stem[..80];
        return stem + extension;
    }
}
