using DeLong.Web.Common.Operations;
using SkiaSharp;

namespace DeLong.Web.Features.Rooms;

public sealed record StoredRoomImage(
    string OriginalStoragePath,
    string LargeUrl,
    string CardUrl,
    string ThumbnailUrl,
    int Width,
    int Height,
    long OriginalBytes,
    string ContentType,
    string OriginalFileName);

public interface IRoomImageStorage
{
    Task<(StoredRoomImage? Image, string? Error)> SaveAsync(Guid roomId, Guid imageId, IFormFile file, CancellationToken cancellationToken = default);
    Task<string?> RegenerateCropsAsync(StoredRoomImage image, double focalX, double focalY, CancellationToken cancellationToken = default);
    Task DeleteAsync(StoredRoomImage image, CancellationToken cancellationToken = default);
}

public sealed class LocalRoomImageStorage(StoragePaths paths, IWebHostEnvironment environment) : IRoomImageStorage
{
    private const string StoragePrefix = "storage://";
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp" };
    private static readonly SKSamplingOptions Sampling = new(SKFilterMode.Linear, SKMipmapMode.Linear);

    public async Task<(StoredRoomImage? Image, string? Error)> SaveAsync(Guid roomId, Guid imageId, IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file.Length <= 0) return (null, "File ảnh trống.");
        if (file.Length > ImageUploadPolicy.MaxSourceBytes)
            return (null, $"Mỗi ảnh tối đa {ImageUploadPolicy.MaxSourceMegabytes} MB.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension) || !AllowedContentTypes.Contains(file.ContentType))
            return (null, "Chỉ hỗ trợ JPG, PNG hoặc WebP.");

        var roomSegment = roomId.ToString("N");
        var imageSegment = imageId.ToString("N");
        var originalRoot = Path.Combine(paths.OriginalRoomImagesRoot, roomSegment, imageSegment);
        var publicRoot = Path.Combine(paths.MediaPublicRoot, roomSegment, imageSegment);
        Directory.CreateDirectory(originalRoot);
        Directory.CreateDirectory(publicRoot);

        var originalName = $"original{extension}";
        var originalPath = Path.Combine(originalRoot, originalName);
        var completed = false;
        try
        {
            await using (var original = new FileStream(
                             originalPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             1024 * 1024,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
                await file.CopyToAsync(original, cancellationToken);

            var decodedResult = DecodeNormalized(originalPath);
            if (decodedResult.Bitmap is null) return (null, decodedResult.Error);
            using var source = decodedResult.Bitmap;

            SaveWebp(ResizeMax(source, 1600), Path.Combine(publicRoot, "large.webp"), 82);
            SaveWebp(ResizeCrop(source, 900, 675, 0.5, 0.5), Path.Combine(publicRoot, "card.webp"), 82);
            SaveWebp(ResizeCrop(source, 480, 360, 0.5, 0.5), Path.Combine(publicRoot, "thumb.webp"), 80);

            var requestRoot = paths.MediaRequestPath.Value?.TrimEnd('/') ?? "/uploads/rooms";
            var urlRoot = $"{requestRoot}/{roomSegment}/{imageSegment}";
            var storagePath = $"{StoragePrefix}room-images/{roomSegment}/{imageSegment}/{originalName}";
            completed = true;
            return (new StoredRoomImage(
                storagePath,
                $"{urlRoot}/large.webp",
                $"{urlRoot}/card.webp",
                $"{urlRoot}/thumb.webp",
                source.Width,
                source.Height,
                file.Length,
                file.ContentType,
                Path.GetFileName(file.FileName)), null);
        }
        finally
        {
            if (!completed)
            {
                if (Directory.Exists(originalRoot)) Directory.Delete(originalRoot, true);
                if (Directory.Exists(publicRoot)) Directory.Delete(publicRoot, true);
            }
        }
    }

    public async Task<string?> RegenerateCropsAsync(StoredRoomImage image, double focalX, double focalY, CancellationToken cancellationToken = default)
    {
        if (focalX is < 0 or > 1 || focalY is < 0 or > 1) return "Điểm lấy nét ảnh không hợp lệ.";
        var originalPath = ResolveOriginalPath(image.OriginalStoragePath);
        if (!File.Exists(originalPath)) return "Không tìm thấy ảnh gốc để tạo lại thumbnail.";

        cancellationToken.ThrowIfCancellationRequested();
        var decodedResult = DecodeNormalized(originalPath);
        if (decodedResult.Bitmap is null) return decodedResult.Error;
        using var source = decodedResult.Bitmap;

        var publicRoot = ResolvePublicDirectory(image.LargeUrl);
        if (string.IsNullOrWhiteSpace(publicRoot)) return "Không xác định được thư mục ảnh tối ưu.";
        Directory.CreateDirectory(publicRoot);

        SaveWebp(ResizeCrop(source, 900, 675, focalX, focalY), Path.Combine(publicRoot, "card.webp"), 82);
        SaveWebp(ResizeCrop(source, 480, 360, focalX, focalY), Path.Combine(publicRoot, "thumb.webp"), 80);
        return null;
    }

    public Task DeleteAsync(StoredRoomImage image, CancellationToken cancellationToken = default)
    {
        var originalPath = ResolveOriginalPath(image.OriginalStoragePath);
        var originalDirectory = Path.GetDirectoryName(originalPath);
        if (!string.IsNullOrWhiteSpace(originalDirectory) && Directory.Exists(originalDirectory)) Directory.Delete(originalDirectory, true);

        var publicDirectory = ResolvePublicDirectory(image.LargeUrl);
        if (!string.IsNullOrWhiteSpace(publicDirectory) && Directory.Exists(publicDirectory)) Directory.Delete(publicDirectory, true);
        return Task.CompletedTask;
    }

    private string ResolveOriginalPath(string storedPath)
    {
        if (storedPath.StartsWith(StoragePrefix, StringComparison.OrdinalIgnoreCase))
        {
            var relative = storedPath[StoragePrefix.Length..].Replace('/', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(paths.DataRoot, relative));
        }

        // Backward compatibility for images stored before configurable production storage existed.
        return Path.GetFullPath(Path.Combine(
            environment.ContentRootPath,
            storedPath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private string? ResolvePublicDirectory(string publicUrl)
    {
        var requestRoot = paths.MediaRequestPath.Value?.TrimEnd('/') ?? "/uploads/rooms";
        if (publicUrl.StartsWith(requestRoot, StringComparison.OrdinalIgnoreCase))
        {
            var relative = publicUrl[requestRoot.Length..].TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var filePath = Path.Combine(paths.MediaPublicRoot, relative);
            return Path.GetDirectoryName(filePath);
        }

        // Backward compatibility for an unexpected legacy URL outside the configured request root.
        var webRoot = environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot");
        var legacyPath = Path.Combine(webRoot, publicUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        return Path.GetDirectoryName(legacyPath);
    }

    private static (SKBitmap? Bitmap, string? Error) DecodeNormalized(string path)
    {
        using var stream = new SKFileStream(path);
        if (!stream.IsValid) return (null, "Không thể đọc file ảnh đã tải lên.");
        using var codec = SKCodec.Create(stream);
        if (codec is null) return (null, "File tải lên không phải ảnh hợp lệ.");
        if ((long)codec.Info.Width * codec.Info.Height > ImageUploadPolicy.MaxPixels) return (null, "Ảnh vượt quá 80 megapixel.");
        using var decoded = SKBitmap.Decode(codec);
        if (decoded is null || decoded.Width <= 0 || decoded.Height <= 0) return (null, "File tải lên không phải ảnh hợp lệ.");
        return (NormalizeOrientation(decoded, codec.EncodedOrigin), null);
    }

    private static SKBitmap ResizeMax(SKBitmap source, int maxSize)
    {
        if (source.Width <= maxSize && source.Height <= maxSize) return source.Copy();
        var scale = Math.Min((float)maxSize / source.Width, (float)maxSize / source.Height);
        return DrawScaled(source, Math.Max(1, (int)Math.Round(source.Width * scale)), Math.Max(1, (int)Math.Round(source.Height * scale)), false, 0.5, 0.5);
    }

    private static SKBitmap ResizeCrop(SKBitmap source, int width, int height, double focalX, double focalY) =>
        DrawScaled(source, width, height, true, focalX, focalY);

    private static SKBitmap DrawScaled(SKBitmap source, int width, int height, bool crop, double focalX, double focalY)
    {
        var target = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(target);
        canvas.Clear(SKColors.Transparent);
        var scale = crop
            ? Math.Max((float)width / source.Width, (float)height / source.Height)
            : Math.Min((float)width / source.Width, (float)height / source.Height);
        var drawWidth = source.Width * scale;
        var drawHeight = source.Height * scale;
        var minimumLeft = Math.Min(width - drawWidth, 0f);
        var minimumTop = Math.Min(height - drawHeight, 0f);
        var left = crop
            ? Math.Clamp((float)(width / 2d - focalX * drawWidth), minimumLeft, 0f)
            : (width - drawWidth) / 2f;
        var top = crop
            ? Math.Clamp((float)(height / 2d - focalY * drawHeight), minimumTop, 0f)
            : (height - drawHeight) / 2f;
        var destination = SKRect.Create(left, top, drawWidth, drawHeight);
        canvas.DrawBitmap(source, destination, Sampling, null);
        canvas.Flush();
        return target;
    }

    private static void SaveWebp(SKBitmap bitmap, string path, int quality)
    {
        using (bitmap)
        using (var image = SKImage.FromBitmap(bitmap))
        using (var encoded = image.Encode(SKEncodedImageFormat.Webp, quality))
        using (var stream = File.Create(path))
            encoded.SaveTo(stream);
    }

    private static SKBitmap NormalizeOrientation(SKBitmap source, SKEncodedOrigin origin)
    {
        if (origin is SKEncodedOrigin.Default or SKEncodedOrigin.TopLeft) return source.Copy();

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

        canvas.DrawBitmap(source, 0, 0, Sampling, null);
        canvas.Flush();
        return target;
    }
}
