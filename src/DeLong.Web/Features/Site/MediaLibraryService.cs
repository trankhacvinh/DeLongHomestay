using System.Security.Cryptography;
using DeLong.Web.Common.Operations;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Features.Rooms;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;

namespace DeLong.Web.Features.Site;

public sealed record MediaAssetDto(
    Guid Id,
    Guid? PropertyId,
    string PropertyName,
    bool IsGlobal,
    string Kind,
    string Url,
    string OriginalFileName,
    string ContentType,
    long ByteSize,
    int Width,
    int Height,
    string AltText,
    string Title,
    string Sha256,
    DateTime CreatedAtUtc,
    int UsageCount,
    bool CanDelete,
    bool CanEdit,
    Guid? RoomId = null,
    string? RoomName = null,
    string? RoomCode = null,
    string? LargeUrl = null,
    string? CardUrl = null,
    string? ThumbnailUrl = null,
    bool IsCover = false);

public sealed record MediaLibraryDto(
    IReadOnlyList<MediaAssetDto> Items,
    int TotalCount,
    long TotalBytes,
    int UnusedCount,
    long UnusedBytes,
    int RoomImageCount,
    long RoomImageBytes);

public sealed class SaveMediaAssetMetadataRequest
{
    public string? Title { get; init; }
    public string? AltText { get; init; }
}

public sealed record MediaLibraryError(string Code, string Message);

public sealed class MediaLibraryService(
    AppDbContext db,
    ISiteAssetStorage storage,
    IRoomImageStorage roomImageStorage,
    StoragePaths paths)
{
    public async Task<(MediaAssetDto? Asset, MediaLibraryError? Error)> UploadAsync(
        Guid? propertyId,
        IFormFile file,
        CancellationToken ct = default)
    {
        string propertyCode;
        string propertyName;
        if (propertyId is Guid id)
        {
            var property = await db.Properties.AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new { x.Code, x.Name })
                .SingleOrDefaultAsync(ct);
            if (property is null) return (null, new("not_found", "Không tìm thấy cơ sở."));
            propertyCode = property.Code;
            propertyName = property.Name;
        }
        else
        {
            propertyCode = "global";
            propertyName = "Dùng chung";
        }

        if (file.Length <= 0) return (null, new("validation", "File ảnh trống."));
        string sha;
        await using (var source = file.OpenReadStream())
            sha = Convert.ToHexString(await SHA256.HashDataAsync(source, ct)).ToLowerInvariant();

        var existing = await db.MediaAssets.AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.Kind == "section" && x.Sha256 == sha)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);
        if (existing is not null && storage.Exists(existing.StorageKey))
        {
            var usage = await GetUsageCountAsync(existing, ct);
            return (ToDto(existing, propertyName, usage), null);
        }

        var (stored, error) = await storage.SaveAsync(propertyCode, "section", file, ct);
        if (error is not null || stored is null)
            return (null, new("validation", error ?? "Không thể lưu ảnh."));

        var displayName = CleanName(Path.GetFileNameWithoutExtension(file.FileName));
        var asset = new MediaAsset
        {
            PropertyId = propertyId,
            Kind = "section",
            Url = stored.Url,
            StorageKey = stored.StorageKey,
            OriginalFileName = CleanFileName(file.FileName),
            ContentType = "image/webp",
            Sha256 = sha,
            ByteSize = stored.ByteSize,
            Width = stored.Width,
            Height = stored.Height,
            AltText = displayName,
            Title = displayName
        };
        db.MediaAssets.Add(asset);
        await db.SaveChangesAsync(ct);
        return (ToDto(asset, propertyName, 0), null);
    }

    public async Task<MediaLibraryDto> ListForPropertyAsync(Guid propertyId, bool includeGlobal = true, CancellationToken ct = default)
    {
        var property = await db.Properties.AsNoTracking()
            .Where(x => x.Id == propertyId)
            .Select(x => new { x.Id, x.Code, x.Name })
            .SingleOrDefaultAsync(ct);
        if (property is null) return EmptyLibrary();

        await ImportLegacyScopeAsync(property.Id, property.Code, property.Name, ct);
        if (includeGlobal) await ImportLegacyScopeAsync(null, "global", "Dùng chung", ct);

        var assets = await db.MediaAssets.AsNoTracking()
            .Include(x => x.Property)
            .Where(x => x.PropertyId == propertyId || (includeGlobal && x.PropertyId == null))
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(500)
            .ToListAsync(ct);
        var roomImages = await db.RoomImages.AsNoTracking()
            .Include(x => x.Room).ThenInclude(x => x.Property)
            .Where(x => x.Room.PropertyId == propertyId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(500)
            .ToListAsync(ct);
        return await BuildLibraryAsync(assets, roomImages, propertyId, false, ct);
    }

    public async Task<MediaLibraryDto> ListAllAsync(CancellationToken ct = default)
    {
        var properties = await db.Properties.AsNoTracking()
            .Select(x => new { x.Id, x.Code, x.Name })
            .ToListAsync(ct);
        await ImportLegacyScopeAsync(null, "global", "Dùng chung", ct);
        foreach (var property in properties)
            await ImportLegacyScopeAsync(property.Id, property.Code, property.Name, ct);

        var assets = await db.MediaAssets.AsNoTracking()
            .Include(x => x.Property)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(1000)
            .ToListAsync(ct);
        var roomImages = await db.RoomImages.AsNoTracking()
            .Include(x => x.Room).ThenInclude(x => x.Property)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(1000)
            .ToListAsync(ct);
        return await BuildLibraryAsync(assets, roomImages, null, true, ct);
    }

    public async Task<(MediaAssetDto? Asset, MediaLibraryError? Error)> UpdateAsync(
        Guid assetId,
        SaveMediaAssetMetadataRequest request,
        Guid? propertyScope,
        bool allowAll,
        CancellationToken ct = default)
    {
        var asset = await db.MediaAssets.Include(x => x.Property).SingleOrDefaultAsync(x => x.Id == assetId, ct);
        if (asset is not null)
        {
            if (!allowAll && asset.PropertyId != propertyScope)
                return (null, new("forbidden", "Bạn không có quyền sửa media này."));

            asset.Title = Clean(request.Title, 300);
            asset.AltText = Clean(request.AltText, 300);
            await db.SaveChangesAsync(ct);
            var usage = await GetUsageCountAsync(asset, ct);
            return (ToDto(asset, asset.Property?.Name ?? "Dùng chung", usage), null);
        }

        var roomImage = await db.RoomImages
            .Include(x => x.Room).ThenInclude(x => x.Property)
            .SingleOrDefaultAsync(x => x.Id == assetId, ct);
        if (roomImage is null) return (null, new("not_found", "Không tìm thấy media."));
        if (!allowAll && roomImage.Room.PropertyId != propertyScope)
            return (null, new("forbidden", "Bạn không có quyền sửa ảnh phòng này."));

        roomImage.AltText = Clean(request.AltText, 300);
        await db.SaveChangesAsync(ct);
        var roomUsage = await GetRoomImageUsageCountAsync(roomImage, ct);
        return (ToRoomDto(roomImage, roomUsage, true), null);
    }

    public async Task<MediaLibraryError?> DeleteAsync(
        Guid assetId,
        Guid? propertyScope,
        bool allowAll,
        CancellationToken ct = default)
    {
        var asset = await db.MediaAssets.SingleOrDefaultAsync(x => x.Id == assetId, ct);
        if (asset is not null)
        {
            if (!allowAll && asset.PropertyId != propertyScope)
                return new("forbidden", "Bạn không có quyền xóa media này.");

            var usage = await GetUsageCountAsync(asset, ct);
            if (usage > 0)
                return new("in_use", $"Media đang được sử dụng ở {usage} vị trí. Hãy thay ảnh ở các vị trí đó trước khi xóa.");

            await storage.DeleteAsync(asset.StorageKey, ct);
            db.MediaAssets.Remove(asset);
            await db.SaveChangesAsync(ct);
            return null;
        }

        var roomImage = await db.RoomImages
            .Include(x => x.Room).ThenInclude(x => x.Images)
            .SingleOrDefaultAsync(x => x.Id == assetId, ct);
        if (roomImage is null) return new("not_found", "Không tìm thấy media.");
        if (!allowAll && roomImage.Room.PropertyId != propertyScope)
            return new("forbidden", "Bạn không có quyền xóa ảnh phòng này.");

        var roomUsage = await GetRoomImageUsageCountAsync(roomImage, ct);
        if (roomUsage > 0)
            return new("in_use", $"Ảnh phòng đang được tham chiếu thêm ở {roomUsage} vị trí ngoài gallery phòng. Hãy thay các tham chiếu đó trước khi xóa.");

        var stored = new StoredRoomImage(
            roomImage.OriginalStoragePath,
            roomImage.LargePath,
            roomImage.CardPath,
            roomImage.ThumbnailPath,
            roomImage.Width,
            roomImage.Height,
            roomImage.OriginalBytes,
            roomImage.ContentType,
            roomImage.OriginalFileName);
        var wasCover = roomImage.IsCover;
        db.RoomImages.Remove(roomImage);
        if (wasCover)
        {
            var replacement = roomImage.Room.Images
                .Where(x => x.Id != roomImage.Id)
                .OrderBy(x => x.SortOrder)
                .FirstOrDefault();
            if (replacement is not null) replacement.IsCover = true;
        }
        await db.SaveChangesAsync(ct);
        await roomImageStorage.DeleteAsync(stored, ct);
        return null;
    }

    private async Task<MediaLibraryDto> BuildLibraryAsync(
        IReadOnlyList<MediaAsset> assets,
        IReadOnlyList<RoomImage> roomImages,
        Guid? propertyScope,
        bool allowAll,
        CancellationToken ct)
    {
        var corpusByScope = new Dictionary<string, IReadOnlyList<string>>();
        var result = new List<MediaAssetDto>(assets.Count + roomImages.Count);
        foreach (var asset in assets)
        {
            var scopeKey = asset.PropertyId?.ToString() ?? "global-all";
            if (!corpusByScope.TryGetValue(scopeKey, out var corpus))
            {
                corpus = await GetUsageCorpusAsync(asset.PropertyId, ct);
                corpusByScope[scopeKey] = corpus;
            }
            var usage = CountUsage(corpus, [BaseUrl(asset.Url)]);
            var canManage = allowAll || asset.PropertyId == propertyScope;
            result.Add(ToDto(asset, asset.Property?.Name ?? "Dùng chung", usage, canManage));
        }

        foreach (var image in roomImages)
        {
            var scopeKey = image.Room.PropertyId.ToString();
            if (!corpusByScope.TryGetValue(scopeKey, out var corpus))
            {
                corpus = await GetUsageCorpusAsync(image.Room.PropertyId, ct);
                corpusByScope[scopeKey] = corpus;
            }
            var usage = CountUsage(corpus, [BaseUrl(image.LargePath), BaseUrl(image.CardPath), BaseUrl(image.ThumbnailPath)]);
            var canManage = allowAll || image.Room.PropertyId == propertyScope;
            result.Add(ToRoomDto(image, usage, canManage));
        }

        result = result.OrderByDescending(x => x.CreatedAtUtc).ToList();
        var totalBytes = result.Sum(x => x.ByteSize);
        var unused = result.Where(x => x.Kind != "room" && x.UsageCount == 0).ToArray();
        var roomItems = result.Where(x => x.Kind == "room").ToArray();
        return new(
            result,
            result.Count,
            totalBytes,
            unused.Length,
            unused.Sum(x => x.ByteSize),
            roomItems.Length,
            roomItems.Sum(x => x.ByteSize));
    }

    private async Task<int> GetUsageCountAsync(MediaAsset asset, CancellationToken ct)
    {
        var corpus = await GetUsageCorpusAsync(asset.PropertyId, ct);
        return CountUsage(corpus, [BaseUrl(asset.Url)]);
    }

    private async Task<int> GetRoomImageUsageCountAsync(RoomImage image, CancellationToken ct)
    {
        var propertyId = image.Room?.PropertyId;
        if (propertyId is null)
        {
            propertyId = await db.Rooms.AsNoTracking()
                .Where(x => x.Id == image.RoomId)
                .Select(x => (Guid?)x.PropertyId)
                .SingleOrDefaultAsync(ct);
        }
        if (propertyId is null) return 0;
        var corpus = await GetUsageCorpusAsync(propertyId, ct);
        return CountUsage(corpus, [BaseUrl(image.LargePath), BaseUrl(image.CardPath), BaseUrl(image.ThumbnailPath)]);
    }

    private async Task<IReadOnlyList<string>> GetUsageCorpusAsync(Guid? propertyId, CancellationToken ct)
    {
        var allScopes = propertyId is null;
        var texts = new List<string>();

        var sectionQuery = db.Set<HomeSection>().AsNoTracking();
        if (!allScopes) sectionQuery = sectionQuery.Where(x => x.PropertyId == propertyId || x.PropertyId == null);
        texts.AddRange(await sectionQuery.Select(x => x.ContentJson).ToListAsync(ct));

        var galleryQuery = db.PropertyGalleryItems.AsNoTracking();
        if (!allScopes) galleryQuery = galleryQuery.Where(x => x.PropertyId == propertyId);
        texts.AddRange(await galleryQuery.Select(x => x.ImageUrl).ToListAsync(ct));

        var postQuery = db.BlogPosts.AsNoTracking();
        if (!allScopes) postQuery = postQuery.Where(x => x.PropertyId == propertyId);
        var posts = await postQuery.Select(x => new { x.CoverImageUrl, x.BodyHtml }).ToListAsync(ct);
        foreach (var post in posts)
        {
            if (!string.IsNullOrWhiteSpace(post.CoverImageUrl)) texts.Add(post.CoverImageUrl);
            if (!string.IsNullOrWhiteSpace(post.BodyHtml)) texts.Add(post.BodyHtml);
        }

        var settingsQuery = db.Set<PropertySiteSettings>().AsNoTracking();
        if (!allScopes) settingsQuery = settingsQuery.Where(x => x.PropertyId == propertyId);
        var settings = await settingsQuery.Select(x => new { x.CoverImageUrl, x.LogoUrl, x.FaviconUrl, x.OgImageUrl }).ToListAsync(ct);
        foreach (var item in settings)
        {
            if (!string.IsNullOrWhiteSpace(item.CoverImageUrl)) texts.Add(item.CoverImageUrl);
            if (!string.IsNullOrWhiteSpace(item.LogoUrl)) texts.Add(item.LogoUrl);
            if (!string.IsNullOrWhiteSpace(item.FaviconUrl)) texts.Add(item.FaviconUrl);
            if (!string.IsNullOrWhiteSpace(item.OgImageUrl)) texts.Add(item.OgImageUrl);
        }

        var roomQuery = db.Rooms.AsNoTracking();
        if (!allScopes) roomQuery = roomQuery.Where(x => x.PropertyId == propertyId);
        texts.AddRange(await roomQuery.Where(x => x.DescriptionHtml != null).Select(x => x.DescriptionHtml!).ToListAsync(ct));

        return texts;
    }

    private async Task ImportLegacyScopeAsync(Guid? propertyId, string propertyCode, string propertyName, CancellationToken ct)
    {
        var safeProperty = SafeProperty(propertyCode);
        var root = Path.Combine(UploadsRoot(), "site", safeProperty);
        if (!Directory.Exists(root)) return;

        var files = Directory.EnumerateFiles(root, "section-*.*", SearchOption.TopDirectoryOnly)
            .Where(path => Path.GetExtension(path).ToLowerInvariant() is ".webp" or ".png" or ".jpg" or ".jpeg")
            .Take(1000)
            .ToArray();
        if (files.Length == 0) return;

        var existingKeys = (await db.MediaAssets.AsNoTracking()
            .Where(x => x.PropertyId == propertyId)
            .Select(x => x.StorageKey)
            .ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var added = false;
        foreach (var path in files)
        {
            var fileName = Path.GetFileName(path);
            var storageKey = $"site/{safeProperty}/{fileName}";
            if (existingKeys.Contains(storageKey)) continue;

            byte[] bytes;
            try { bytes = await File.ReadAllBytesAsync(path, ct); }
            catch { continue; }
            var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            using var data = SKData.CreateCopy(bytes);
            using var codec = SKCodec.Create(data);
            if (codec is null) continue;
            var title = CleanName(Path.GetFileNameWithoutExtension(fileName));
            db.MediaAssets.Add(new MediaAsset
            {
                PropertyId = propertyId,
                Kind = "section",
                Url = $"/uploads/{storageKey}",
                StorageKey = storageKey,
                OriginalFileName = fileName,
                ContentType = ContentTypeFromExtension(Path.GetExtension(fileName)),
                Sha256 = sha,
                ByteSize = bytes.LongLength,
                Width = codec.Info.Width,
                Height = codec.Info.Height,
                AltText = title,
                Title = title,
                CreatedAtUtc = File.GetCreationTimeUtc(path),
                UpdatedAtUtc = File.GetLastWriteTimeUtc(path)
            });
            existingKeys.Add(storageKey);
            added = true;
        }
        if (added) await db.SaveChangesAsync(ct);
    }

    private MediaAssetDto ToRoomDto(RoomImage image, int usage, bool canManage)
    {
        var bytes = RoomImagePhysicalBytes(image);
        var roomName = image.Room?.Name ?? "Phòng";
        var roomCode = image.Room?.Code ?? string.Empty;
        var propertyName = image.Room?.Property?.Name ?? "Cơ sở";
        var title = string.IsNullOrWhiteSpace(roomCode) ? roomName : $"{roomName} · {roomCode}";
        return new(
            image.Id,
            image.Room?.PropertyId,
            propertyName,
            false,
            "room",
            image.LargePath,
            image.OriginalFileName,
            image.ContentType,
            bytes,
            image.Width,
            image.Height,
            image.AltText ?? string.Empty,
            title,
            string.Empty,
            image.CreatedAtUtc,
            usage,
            canManage,
            canManage,
            image.RoomId,
            roomName,
            roomCode,
            image.LargePath,
            image.CardPath,
            image.ThumbnailPath,
            image.IsCover);
    }

    private long RoomImagePhysicalBytes(RoomImage image)
    {
        var total = Math.Max(0, image.OriginalBytes);
        total += PublicFileBytes(image.LargePath);
        total += PublicFileBytes(image.CardPath);
        total += PublicFileBytes(image.ThumbnailPath);
        return total;
    }

    private long PublicFileBytes(string url)
    {
        try
        {
            var requestRoot = paths.MediaRequestPath.Value?.TrimEnd('/') ?? "/uploads/rooms";
            if (!url.StartsWith(requestRoot, StringComparison.OrdinalIgnoreCase)) return 0;
            var relative = url[requestRoot.Length..].TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var file = new FileInfo(Path.Combine(paths.MediaPublicRoot, relative));
            return file.Exists ? file.Length : 0;
        }
        catch { return 0; }
    }

    private string UploadsRoot() => Directory.GetParent(paths.MediaPublicRoot)?.FullName ?? paths.MediaPublicRoot;

    private static MediaAssetDto ToDto(MediaAsset asset, string propertyName, int usage, bool canManage = true) =>
        new(
            asset.Id,
            asset.PropertyId,
            propertyName,
            asset.PropertyId is null,
            asset.Kind,
            asset.Url,
            asset.OriginalFileName,
            asset.ContentType,
            asset.ByteSize,
            asset.Width,
            asset.Height,
            asset.AltText,
            asset.Title,
            asset.Sha256,
            asset.CreatedAtUtc,
            usage,
            canManage,
            canManage);

    private static int CountUsage(IEnumerable<string> corpus, IEnumerable<string> rawPaths)
    {
        var paths = rawPaths.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (paths.Length == 0) return 0;
        return corpus.Count(text => !string.IsNullOrWhiteSpace(text) && paths.Any(path => text.Contains(path, StringComparison.OrdinalIgnoreCase)));
    }

    private static string BaseUrl(string value)
    {
        var url = (value ?? string.Empty).Trim();
        var query = url.IndexOf('?');
        return query >= 0 ? url[..query] : url;
    }

    private static MediaLibraryDto EmptyLibrary() => new([], 0, 0, 0, 0, 0, 0);

    private static string SafeProperty(string value) =>
        new(value.ToLowerInvariant().Where(ch => char.IsLetterOrDigit(ch) || ch == '-').ToArray());

    private static string CleanFileName(string value)
    {
        var name = Path.GetFileName(value ?? string.Empty).Trim();
        if (name.Length > 300) name = name[..300];
        return string.IsNullOrWhiteSpace(name) ? "image" : name;
    }

    private static string CleanName(string value)
    {
        var text = string.Join(' ', (value ?? string.Empty).Replace('-', ' ').Replace('_', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return Clean(text, 300);
    }

    private static string Clean(string? value, int max = 300)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max];
    }

    private static string ContentTypeFromExtension(string extension) => extension.ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        _ => "image/webp"
    };
}
