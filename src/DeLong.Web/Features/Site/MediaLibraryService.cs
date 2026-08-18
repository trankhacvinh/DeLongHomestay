using System.Security.Cryptography;
using DeLong.Web.Common.Operations;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;

namespace DeLong.Web.Features.Site;

public sealed record MediaAssetDto(
    Guid Id,
    Guid? PropertyId,
    string PropertyName,
    bool IsGlobal,
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
    bool CanDelete);

public sealed record MediaLibraryDto(
    IReadOnlyList<MediaAssetDto> Items,
    int TotalCount,
    long TotalBytes,
    int UnusedCount,
    long UnusedBytes);

public sealed class SaveMediaAssetMetadataRequest
{
    public string? Title { get; init; }
    public string? AltText { get; init; }
}

public sealed record MediaLibraryError(string Code, string Message);

public sealed class MediaLibraryService(
    AppDbContext db,
    ISiteAssetStorage storage,
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
        if (property is null) return new([], 0, 0, 0, 0);

        await ImportLegacyScopeAsync(property.Id, property.Code, property.Name, ct);
        if (includeGlobal) await ImportLegacyScopeAsync(null, "global", "Dùng chung", ct);

        var assets = await db.MediaAssets.AsNoTracking()
            .Include(x => x.Property)
            .Where(x => x.PropertyId == propertyId || (includeGlobal && x.PropertyId == null))
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(500)
            .ToListAsync(ct);
        return await BuildLibraryAsync(assets, propertyId, false, ct);
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
        return await BuildLibraryAsync(assets, null, true, ct);
    }

    public async Task<(MediaAssetDto? Asset, MediaLibraryError? Error)> UpdateAsync(
        Guid assetId,
        SaveMediaAssetMetadataRequest request,
        Guid? propertyScope,
        bool allowAll,
        CancellationToken ct = default)
    {
        var asset = await db.MediaAssets.Include(x => x.Property).SingleOrDefaultAsync(x => x.Id == assetId, ct);
        if (asset is null) return (null, new("not_found", "Không tìm thấy media."));
        if (!allowAll && asset.PropertyId != propertyScope)
            return (null, new("forbidden", "Bạn không có quyền sửa media này."));

        asset.Title = Clean(request.Title, 300);
        asset.AltText = Clean(request.AltText, 300);
        await db.SaveChangesAsync(ct);
        var usage = await GetUsageCountAsync(asset, ct);
        return (ToDto(asset, asset.Property?.Name ?? "Dùng chung", usage), null);
    }

    public async Task<MediaLibraryError?> DeleteAsync(
        Guid assetId,
        Guid? propertyScope,
        bool allowAll,
        CancellationToken ct = default)
    {
        var asset = await db.MediaAssets.SingleOrDefaultAsync(x => x.Id == assetId, ct);
        if (asset is null) return new("not_found", "Không tìm thấy media.");
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

    private async Task<MediaLibraryDto> BuildLibraryAsync(
        IReadOnlyList<MediaAsset> assets,
        Guid? propertyScope,
        bool allowAll,
        CancellationToken ct)
    {
        var corpusByScope = new Dictionary<string, IReadOnlyList<string>>();
        var result = new List<MediaAssetDto>(assets.Count);
        foreach (var asset in assets)
        {
            var scopeKey = asset.PropertyId?.ToString() ?? "global-all";
            if (!corpusByScope.TryGetValue(scopeKey, out var corpus))
            {
                corpus = await GetUsageCorpusAsync(asset.PropertyId, ct);
                corpusByScope[scopeKey] = corpus;
            }
            var usage = CountUsage(corpus, BaseUrl(asset.Url));
            var canDelete = allowAll || asset.PropertyId == propertyScope;
            result.Add(ToDto(asset, asset.Property?.Name ?? "Dùng chung", usage, canDelete));
        }

        var totalBytes = result.Sum(x => x.ByteSize);
        var unused = result.Where(x => x.UsageCount == 0).ToArray();
        return new(result, result.Count, totalBytes, unused.Length, unused.Sum(x => x.ByteSize));
    }

    private async Task<int> GetUsageCountAsync(MediaAsset asset, CancellationToken ct)
    {
        var corpus = await GetUsageCorpusAsync(asset.PropertyId, ct);
        return CountUsage(corpus, BaseUrl(asset.Url));
    }

    private async Task<IReadOnlyList<string>> GetUsageCorpusAsync(Guid? propertyId, CancellationToken ct)
    {
        var allScopes = propertyId is null;
        var texts = new List<string>();

        var sectionQuery = db.Set<HomeSection>().AsNoTracking();
        if (!allScopes) sectionQuery = sectionQuery.Where(x => x.PropertyId == propertyId);
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

    private string UploadsRoot() => Directory.GetParent(paths.MediaPublicRoot)?.FullName ?? paths.MediaPublicRoot;

    private static MediaAssetDto ToDto(MediaAsset asset, string propertyName, int usage, bool canDelete = true) =>
        new(asset.Id, asset.PropertyId, propertyName, asset.PropertyId is null, asset.Url, asset.OriginalFileName,
            asset.ContentType, asset.ByteSize, asset.Width, asset.Height, asset.AltText, asset.Title, asset.Sha256,
            asset.CreatedAtUtc, usage, canDelete);

    private static int CountUsage(IEnumerable<string> corpus, string path) =>
        string.IsNullOrWhiteSpace(path) ? 0 : corpus.Count(text => !string.IsNullOrWhiteSpace(text) && text.Contains(path, StringComparison.OrdinalIgnoreCase));

    private static string BaseUrl(string value)
    {
        var url = (value ?? string.Empty).Trim();
        var query = url.IndexOf('?');
        return query >= 0 ? url[..query] : url;
    }

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
