using DeLong.Web.Common.Operations;
using DeLong.Web.Features.Rooms;

namespace DeLong.Web.Features.Housekeeping;

public sealed record StoredRoomConditionMedia(
    string OriginalStoragePath,
    string LargeUrl,
    string CardUrl,
    string ThumbnailUrl,
    int Width,
    int Height,
    long OriginalBytes,
    string ContentType,
    string OriginalFileName,
    bool IsVideo,
    StoredRoomImage? StoredImage = null);

public interface IRoomConditionMediaStorage
{
    Task<(StoredRoomConditionMedia? Media, string? Error)> SaveAsync(Guid roomId, Guid mediaId, IFormFile file, CancellationToken ct = default);
    Task DeleteAsync(StoredRoomConditionMedia media, CancellationToken ct = default);
}

public sealed class LocalRoomConditionMediaStorage(IRoomImageStorage imageStorage, StoragePaths paths) : IRoomConditionMediaStorage
{
    private const long MaxVideoBytes = 250L * 1024 * 1024;
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".webm", ".mov"
    };

    public async Task<(StoredRoomConditionMedia? Media, string? Error)> SaveAsync(
        Guid roomId,
        Guid mediaId,
        IFormFile file,
        CancellationToken ct = default)
    {
        if (file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            var (image, error) = await imageStorage.SaveAsync(roomId, mediaId, file, ct);
            return image is null
                ? (null, error)
                : (new StoredRoomConditionMedia(
                    image.OriginalStoragePath, image.LargeUrl, image.CardUrl, image.ThumbnailUrl,
                    image.Width, image.Height, image.OriginalBytes, image.ContentType,
                    image.OriginalFileName, false, image), null);
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!VideoExtensions.Contains(extension) || !await HasValidVideoSignatureAsync(file, extension, ct))
            return (null, "Chỉ hỗ trợ ảnh JPG, PNG, WebP hoặc video MP4, WebM, MOV.");
        if (file.Length <= 0) return (null, "File video trống.");
        if (file.Length > MaxVideoBytes) return (null, "Mỗi video tối đa 250 MB.");

        var roomSegment = roomId.ToString("N");
        var mediaSegment = mediaId.ToString("N");
        var publicRoot = Path.Combine(paths.MediaPublicRoot, roomSegment, mediaSegment);
        Directory.CreateDirectory(publicRoot);
        var fileName = $"video{extension}";
        var destination = Path.Combine(publicRoot, fileName);
        await using (var stream = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            await file.CopyToAsync(stream, ct);

        var requestRoot = paths.MediaRequestPath.Value?.TrimEnd('/') ?? "/uploads/rooms";
        var url = $"{requestRoot}/{roomSegment}/{mediaSegment}/{fileName}";
        var contentType = extension == ".webm" ? "video/webm" : extension == ".mov" ? "video/quicktime" : "video/mp4";
        return (new StoredRoomConditionMedia(
            url, url, url, url, 0, 0, file.Length, contentType,
            Path.GetFileName(file.FileName), true), null);
    }

    public async Task DeleteAsync(StoredRoomConditionMedia media, CancellationToken ct = default)
    {
        if (media.StoredImage is not null)
        {
            await imageStorage.DeleteAsync(media.StoredImage, ct);
            return;
        }

        var requestRoot = paths.MediaRequestPath.Value?.TrimEnd('/') ?? "/uploads/rooms";
        if (!media.LargeUrl.StartsWith(requestRoot, StringComparison.OrdinalIgnoreCase)) return;
        var relative = media.LargeUrl[requestRoot.Length..].TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var directory = Path.GetDirectoryName(Path.Combine(paths.MediaPublicRoot, relative));
        if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    private static async Task<bool> HasValidVideoSignatureAsync(
        IFormFile file,
        string extension,
        CancellationToken cancellationToken)
    {
        var header = new byte[12];
        await using var stream = file.OpenReadStream();
        var bytesRead = await stream.ReadAsync(header.AsMemory(), cancellationToken);
        if (extension == ".webm")
            return bytesRead >= 4 && header[0] == 0x1A && header[1] == 0x45 && header[2] == 0xDF && header[3] == 0xA3;

        return bytesRead >= 12 && header[4] == (byte)'f' && header[5] == (byte)'t' &&
               header[6] == (byte)'y' && header[7] == (byte)'p';
    }
}
