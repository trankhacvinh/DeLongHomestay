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
    Task DeleteAsync(StoredRoomImage image, CancellationToken cancellationToken = default);
}

public sealed class LocalRoomImageStorage(IWebHostEnvironment environment) : IRoomImageStorage
{
    private const long MaxBytes = 12L * 1024 * 1024;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp" };
    private static readonly SKSamplingOptions Sampling = new(SKFilterMode.Linear, SKMipmapMode.Linear);

    public async Task<(StoredRoomImage? Image, string? Error)> SaveAsync(Guid roomId, Guid imageId, IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file.Length <= 0) return (null, "File ảnh trống.");
        if (file.Length > MaxBytes) return (null, "Mỗi ảnh tối đa 12 MB.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension) || !AllowedContentTypes.Contains(file.ContentType))
            return (null, "Chỉ hỗ trợ JPG, PNG hoặc WebP.");

        await using var memory = new MemoryStream((int)Math.Min(file.Length, int.MaxValue));
        await file.CopyToAsync(memory, cancellationToken);
        var bytes = memory.ToArray();

        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        if (codec is null) return (null, "File tải lên không phải ảnh hợp lệ.");
        using var decoded = SKBitmap.Decode(codec);
        if (decoded is null || decoded.Width <= 0 || decoded.Height <= 0) return (null, "File tải lên không phải ảnh hợp lệ.");

        using var source = NormalizeOrientation(decoded, codec.EncodedOrigin);
        var width = source.Width;
        var height = source.Height;

        var originalRoot = Path.Combine(environment.ContentRootPath, "App_Data", "room-images", roomId.ToString("N"), imageId.ToString("N"));
        var webRoot = environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot");
        var publicRoot = Path.Combine(webRoot, "uploads", "rooms", roomId.ToString("N"), imageId.ToString("N"));
        Directory.CreateDirectory(originalRoot);
        Directory.CreateDirectory(publicRoot);

        var originalName = $"original{extension}";
        var originalPath = Path.Combine(originalRoot, originalName);
        await File.WriteAllBytesAsync(originalPath, bytes, cancellationToken);

        SaveWebp(ResizeMax(source, 1600), Path.Combine(publicRoot, "large.webp"), 82);
        SaveWebp(ResizeCrop(source, 900, 675), Path.Combine(publicRoot, "card.webp"), 82);
        SaveWebp(ResizeCrop(source, 480, 360), Path.Combine(publicRoot, "thumb.webp"), 80);

        var urlRoot = $"/uploads/rooms/{roomId:N}/{imageId:N}";
        return (new StoredRoomImage(
            Path.GetRelativePath(environment.ContentRootPath, originalPath).Replace('\\', '/'),
            $"{urlRoot}/large.webp",
            $"{urlRoot}/card.webp",
            $"{urlRoot}/thumb.webp",
            width,
            height,
            file.Length,
            file.ContentType,
            Path.GetFileName(file.FileName)), null);
    }

    public Task DeleteAsync(StoredRoomImage image, CancellationToken cancellationToken = default)
    {
        var originalPath = Path.Combine(environment.ContentRootPath, image.OriginalStoragePath.Replace('/', Path.DirectorySeparatorChar));
        var originalDirectory = Path.GetDirectoryName(originalPath);
        if (!string.IsNullOrWhiteSpace(originalDirectory) && Directory.Exists(originalDirectory)) Directory.Delete(originalDirectory, true);

        var webRoot = environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot");
        var publicPath = image.LargeUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var publicDirectory = Path.GetDirectoryName(Path.Combine(webRoot, publicPath));
        if (!string.IsNullOrWhiteSpace(publicDirectory) && Directory.Exists(publicDirectory)) Directory.Delete(publicDirectory, true);
        return Task.CompletedTask;
    }

    private static SKBitmap ResizeMax(SKBitmap source, int maxSize)
    {
        if (source.Width <= maxSize && source.Height <= maxSize) return source.Copy();
        var scale = Math.Min((float)maxSize / source.Width, (float)maxSize / source.Height);
        return DrawScaled(source, Math.Max(1, (int)Math.Round(source.Width * scale)), Math.Max(1, (int)Math.Round(source.Height * scale)), false);
    }

    private static SKBitmap ResizeCrop(SKBitmap source, int width, int height) => DrawScaled(source, width, height, true);

    private static SKBitmap DrawScaled(SKBitmap source, int width, int height, bool crop)
    {
        var target = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(target);
        canvas.Clear(SKColors.Transparent);
        var scale = crop
            ? Math.Max((float)width / source.Width, (float)height / source.Height)
            : Math.Min((float)width / source.Width, (float)height / source.Height);
        var drawWidth = source.Width * scale;
        var drawHeight = source.Height * scale;
        var destination = SKRect.Create((width - drawWidth) / 2f, (height - drawHeight) / 2f, drawWidth, drawHeight);
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
