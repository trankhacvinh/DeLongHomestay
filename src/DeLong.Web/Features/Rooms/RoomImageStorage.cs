using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

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

    public async Task<(StoredRoomImage? Image, string? Error)> SaveAsync(Guid roomId, Guid imageId, IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file.Length <= 0) return (null, "File ảnh trống.");
        if (file.Length > MaxBytes) return (null, "Mỗi ảnh tối đa 12 MB.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension) || !AllowedContentTypes.Contains(file.ContentType))
            return (null, "Chỉ hỗ trợ JPG, PNG hoặc WebP.");

        await using var memory = new MemoryStream((int)Math.Min(file.Length, int.MaxValue));
        await file.CopyToAsync(memory, cancellationToken);
        memory.Position = 0;

        Image image;
        try
        {
            image = await Image.LoadAsync(memory, cancellationToken);
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException)
        {
            return (null, "File tải lên không phải ảnh hợp lệ.");
        }

        using (image)
        {
            image.Mutate(x => x.AutoOrient());
            var width = image.Width;
            var height = image.Height;

            var originalRoot = Path.Combine(environment.ContentRootPath, "App_Data", "room-images", roomId.ToString("N"), imageId.ToString("N"));
            var publicRoot = Path.Combine(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"), "uploads", "rooms", roomId.ToString("N"), imageId.ToString("N"));
            Directory.CreateDirectory(originalRoot);
            Directory.CreateDirectory(publicRoot);

            var originalName = $"original{extension}";
            var originalPath = Path.Combine(originalRoot, originalName);
            memory.Position = 0;
            await using (var originalFile = File.Create(originalPath))
                await memory.CopyToAsync(originalFile, cancellationToken);

            var encoder = new WebpEncoder { Quality = 82, FileFormat = WebpFileFormatType.Lossy };
            var largePath = Path.Combine(publicRoot, "large.webp");
            var cardPath = Path.Combine(publicRoot, "card.webp");
            var thumbnailPath = Path.Combine(publicRoot, "thumb.webp");

            using (var large = image.Clone(x => x.Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new Size(1600, 1600), Sampler = KnownResamplers.Lanczos3 })))
                await large.SaveAsWebpAsync(largePath, encoder, cancellationToken);
            using (var card = image.Clone(x => x.Resize(new ResizeOptions { Mode = ResizeMode.Crop, Size = new Size(900, 675), Sampler = KnownResamplers.Lanczos3, Position = AnchorPositionMode.Center })))
                await card.SaveAsWebpAsync(cardPath, encoder, cancellationToken);
            using (var thumb = image.Clone(x => x.Resize(new ResizeOptions { Mode = ResizeMode.Crop, Size = new Size(480, 360), Sampler = KnownResamplers.Lanczos3, Position = AnchorPositionMode.Center })))
                await thumb.SaveAsWebpAsync(thumbnailPath, encoder, cancellationToken);

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
    }

    public Task DeleteAsync(StoredRoomImage image, CancellationToken cancellationToken = default)
    {
        var originalPath = Path.Combine(environment.ContentRootPath, image.OriginalStoragePath.Replace('/', Path.DirectorySeparatorChar));
        var originalDirectory = Path.GetDirectoryName(originalPath);
        if (!string.IsNullOrWhiteSpace(originalDirectory) && Directory.Exists(originalDirectory)) Directory.Delete(originalDirectory, true);

        var publicPath = image.LargeUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var publicDirectory = Path.GetDirectoryName(Path.Combine(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"), publicPath));
        if (!string.IsNullOrWhiteSpace(publicDirectory) && Directory.Exists(publicDirectory)) Directory.Delete(publicDirectory, true);
        return Task.CompletedTask;
    }
}
