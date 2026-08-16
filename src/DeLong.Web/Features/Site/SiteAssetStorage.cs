using DeLong.Web.Common.Operations;
using SkiaSharp;

namespace DeLong.Web.Features.Site;

public sealed record StoredSiteAsset(string Url, int Width, int Height);

public interface ISiteAssetStorage
{
    Task<(StoredSiteAsset? Asset, string? Error)> SaveAsync(string propertyCode, string kind, IFormFile file, CancellationToken ct = default);
}

public sealed class LocalSiteAssetStorage(StoragePaths paths) : ISiteAssetStorage
{
    private const long MaxBytes = 12L * 1024 * 1024;
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
        { "image/jpeg", "image/png", "image/webp" };

    public async Task<(StoredSiteAsset? Asset, string? Error)> SaveAsync(
        string propertyCode,
        string kind,
        IFormFile file,
        CancellationToken ct = default)
    {
        if (file.Length <= 0) return (null, "File ảnh trống.");
        if (file.Length > MaxBytes) return (null, "Mỗi ảnh tối đa 12 MB.");
        if (!AllowedTypes.Contains(file.ContentType)) return (null, "Chỉ hỗ trợ JPG, PNG hoặc WebP.");
        if (kind is not ("cover" or "logo" or "favicon" or "og" or "section")) return (null, "Loại ảnh website không hợp lệ.");

        await using var memory = new MemoryStream((int)Math.Min(file.Length, int.MaxValue));
        await file.CopyToAsync(memory, ct);
        using var data = SKData.CreateCopy(memory.ToArray());
        using var codec = SKCodec.Create(data);
        if (codec is null) return (null, "File tải lên không phải ảnh hợp lệ.");
        using var decoded = SKBitmap.Decode(codec);
        if (decoded is null || decoded.Width <= 0 || decoded.Height <= 0) return (null, "File tải lên không phải ảnh hợp lệ.");

        var safeProperty = new string(propertyCode.ToLowerInvariant().Where(ch => char.IsLetterOrDigit(ch) || ch == '-').ToArray());
        var roomsRoot = paths.MediaPublicRoot;
        var uploadsRoot = Directory.GetParent(roomsRoot)?.FullName ?? roomsRoot;
        var publicRoot = Path.Combine(uploadsRoot, "site", safeProperty);
        Directory.CreateDirectory(publicRoot);

        string fileName;
        SKBitmap output;
        SKEncodedImageFormat format;
        int quality;
        switch (kind)
        {
            case "cover":
                fileName = "cover.webp";
                output = ResizeCrop(decoded, 1600, 1000);
                format = SKEncodedImageFormat.Webp;
                quality = 86;
                break;
            case "favicon":
                fileName = "favicon-64.png";
                output = ResizeContain(decoded, 64, 64);
                format = SKEncodedImageFormat.Png;
                quality = 100;
                break;
            case "og":
                fileName = "og.webp";
                output = ResizeCrop(decoded, 1200, 630);
                format = SKEncodedImageFormat.Webp;
                quality = 84;
                break;
            case "logo":
                fileName = "logo.webp";
                output = ResizeMax(decoded, 900);
                format = SKEncodedImageFormat.Webp;
                quality = 86;
                break;
            default:
                fileName = $"section-{Guid.NewGuid():N}.webp";
                output = ResizeMax(decoded, 1800);
                format = SKEncodedImageFormat.Webp;
                quality = 84;
                break;
        }

        var outputWidth = output.Width;
        var outputHeight = output.Height;
        using (output)
        using (var image = SKImage.FromBitmap(output))
        using (var encoded = image.Encode(format, quality))
        await using (var stream = File.Create(Path.Combine(publicRoot, fileName)))
            encoded.SaveTo(stream);

        var version = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return (new StoredSiteAsset($"/uploads/site/{safeProperty}/{fileName}?v={version}", outputWidth, outputHeight), null);
    }

    private static SKBitmap ResizeMax(SKBitmap source, int maxSize)
    {
        if (source.Width <= maxSize && source.Height <= maxSize) return source.Copy();
        var scale = Math.Min((float)maxSize / source.Width, (float)maxSize / source.Height);
        return Resize(source, Math.Max(1, (int)Math.Round(source.Width * scale)), Math.Max(1, (int)Math.Round(source.Height * scale)));
    }

    private static SKBitmap ResizeContain(SKBitmap source, int width, int height)
    {
        var target = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(target);
        canvas.Clear(SKColors.Transparent);
        var scale = Math.Min((float)width / source.Width, (float)height / source.Height);
        var w = source.Width * scale;
        var h = source.Height * scale;
        canvas.DrawBitmap(source, SKRect.Create((width - w) / 2, (height - h) / 2, w, h), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
        canvas.Flush();
        return target;
    }

    private static SKBitmap ResizeCrop(SKBitmap source, int width, int height)
    {
        var target = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(target);
        var scale = Math.Max((float)width / source.Width, (float)height / source.Height);
        var w = source.Width * scale;
        var h = source.Height * scale;
        canvas.DrawBitmap(source, SKRect.Create((width - w) / 2, (height - h) / 2, w, h), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
        canvas.Flush();
        return target;
    }

    private static SKBitmap Resize(SKBitmap source, int width, int height)
    {
        var target = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(target);
        canvas.Clear(SKColors.Transparent);
        canvas.DrawBitmap(source, SKRect.Create(0, 0, width, height), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
        canvas.Flush();
        return target;
    }
}
