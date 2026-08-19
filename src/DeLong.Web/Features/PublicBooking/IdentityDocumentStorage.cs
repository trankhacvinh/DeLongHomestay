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
    private const long MaxBytes = 8L * 1024 * 1024;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int MasterKeyBytes = 32;
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("DLID1");
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp" };

    private readonly string root;
    private readonly string masterKeyPath;
    private readonly byte[]? key;

    public IdentityDocumentStorage(StoragePaths paths, IConfiguration configuration)
    {
        root = Path.Combine(paths.DataRoot, "private", "identity-documents");
        masterKeyPath = Path.Combine(paths.DataRoot, "security", "identity-master.key");
        try
        {
            Directory.CreateDirectory(root);
            key = LoadOrCreateMasterKey(
                masterKeyPath,
                root,
                configuration["Security:IdentityDocumentEncryptionKeyBase64"]);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
        {
            key = null;
        }
    }

    public bool IsConfigured => key is { Length: MasterKeyBytes };

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
        if (file.Length > MaxBytes) return (null, "Mỗi ảnh CCCD tối đa 8 MB.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var contentType = file.ContentType?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!AllowedExtensions.Contains(extension) || !AllowedContentTypes.Contains(contentType))
            return (null, "Chỉ hỗ trợ ảnh JPG, PNG hoặc WebP.");

        await using var memory = new MemoryStream((int)Math.Min(file.Length, int.MaxValue));
        await file.CopyToAsync(memory, cancellationToken);
        var imageBytes = memory.ToArray();
        if (!IsValidImage(imageBytes)) return (null, "File tải lên không phải ảnh hợp lệ.");

        var originalFileName = SafeFileName(file.FileName, extension);
        var plaintext = BuildPlaintext(contentType, originalFileName, imageBytes);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];
        var aad = AssociatedData(propertyId, bookingId, normalizedSide);
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
        var path = Path.Combine(directory, normalizedSide + ".dlid");
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

        return (new IdentityDocumentInfo(normalizedSide, contentType, originalFileName, imageBytes.LongLength), null);
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

    public async Task<IReadOnlyList<IdentityDocumentInfo>> ListAsync(
        Guid propertyId,
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        var result = new List<IdentityDocumentInfo>(2);
        foreach (var side in new[] { "front", "back" })
        {
            var document = await ReadAsync(propertyId, bookingId, side, cancellationToken);
            if (document is not null)
                result.Add(new IdentityDocumentInfo(side, document.ContentType, document.OriginalFileName, document.Bytes.LongLength));
        }
        return result;
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
        if (length <= 0 || length > MaxBytes || length > stream.Length - stream.Position)
            throw new CryptographicException("Encrypted identity document payload is invalid.");
        var bytes = reader.ReadBytes(length);
        if (bytes.Length != length) throw new CryptographicException("Encrypted identity document payload is truncated.");
        return new IdentityDocumentReadResult(bytes, contentType, fileName);
    }

    private static bool IsValidImage(byte[] bytes)
    {
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        return codec is not null && codec.Info.Width > 0 && codec.Info.Height > 0;
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
