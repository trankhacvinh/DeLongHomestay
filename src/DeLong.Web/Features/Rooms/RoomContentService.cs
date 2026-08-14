using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using Ganss.Xss;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Rooms;

public sealed class RoomContentService(AppDbContext db, IRoomImageStorage imageStorage)
{
    private static readonly Regex SlugInvalid = new("[^a-z0-9]+", RegexOptions.Compiled);
    private static readonly HtmlSanitizer Sanitizer = CreateSanitizer();

    public async Task<RoomContentDto?> GetAsync(Guid propertyId, Guid roomId, CancellationToken cancellationToken = default)
    {
        var room = await db.Rooms.AsNoTracking()
            .Include(x => x.Images)
            .Include(x => x.Amenities).ThenInclude(x => x.Amenity)
            .Include(x => x.Tags).ThenInclude(x => x.RoomTag)
            .Include(x => x.Highlights)
            .SingleOrDefaultAsync(x => x.PropertyId == propertyId && x.Id == roomId, cancellationToken);
        return room is null ? null : ToDto(room);
    }

    public async Task<(RoomContentDto? Room, RoomContentError? Error)> UpdateAsync(Guid propertyId, Guid roomId, UpdateRoomContentRequest request, CancellationToken cancellationToken = default)
    {
        var room = await db.Rooms
            .Include(x => x.Amenities)
            .Include(x => x.Tags)
            .Include(x => x.Highlights)
            .SingleOrDefaultAsync(x => x.PropertyId == propertyId && x.Id == roomId, cancellationToken);
        if (room is null) return (null, new("not_found", "Không tìm thấy phòng."));

        var shortDescription = Clean(request.ShortDescription);
        if (shortDescription?.Length > 600) return (null, new("validation", "Mô tả ngắn tối đa 600 ký tự."));

        var slug = CreateSlug(string.IsNullOrWhiteSpace(request.Slug) ? room.Name : request.Slug!);
        if (string.IsNullOrWhiteSpace(slug)) slug = room.Code.ToLowerInvariant();
        if (await db.Rooms.AnyAsync(x => x.PropertyId == propertyId && x.Id != roomId && x.Slug == slug, cancellationToken))
            return (null, new("slug_exists", "Đường dẫn phòng này đã được dùng. Vui lòng chọn slug khác."));

        var amenities = NormalizeItems(request.Amenities, 20, 100);
        var tags = NormalizeItems(request.Tags, 20, 100);
        var highlights = NormalizeItems(request.Highlights, 8, 180);
        if (amenities.Error is not null) return (null, new("validation", amenities.Error));
        if (tags.Error is not null) return (null, new("validation", tags.Error));
        if (highlights.Error is not null) return (null, new("validation", highlights.Error));

        room.Slug = slug;
        room.ShortDescription = shortDescription;
        room.DescriptionHtml = string.IsNullOrWhiteSpace(request.DescriptionHtml) ? null : Sanitizer.Sanitize(request.DescriptionHtml);
        room.IsPublished = request.IsPublished;

        await SyncAmenitiesAsync(room, amenities.Items, cancellationToken);
        await SyncTagsAsync(room, tags.Items, cancellationToken);
        room.Highlights.Clear();
        for (var i = 0; i < highlights.Items.Count; i++)
            room.Highlights.Add(new RoomHighlight { Text = highlights.Items[i], SortOrder = i });

        await db.SaveChangesAsync(cancellationToken);
        return (await GetAsync(propertyId, roomId, cancellationToken), null);
    }

    public async Task<(RoomImageDto? Image, RoomContentError? Error)> UploadImageAsync(Guid propertyId, Guid roomId, IFormFile file, CancellationToken cancellationToken = default)
    {
        var room = await db.Rooms.Include(x => x.Images).SingleOrDefaultAsync(x => x.PropertyId == propertyId && x.Id == roomId, cancellationToken);
        if (room is null) return (null, new("not_found", "Không tìm thấy phòng."));
        if (room.Images.Count >= 20) return (null, new("image_limit", "Mỗi phòng tối đa 20 ảnh."));

        var imageId = Guid.CreateVersion7();
        var (stored, error) = await imageStorage.SaveAsync(roomId, imageId, file, cancellationToken);
        if (stored is null) return (null, new("image_invalid", error ?? "Không thể xử lý ảnh."));

        var image = new RoomImage
        {
            Id = imageId,
            RoomId = roomId,
            OriginalFileName = stored.OriginalFileName,
            OriginalStoragePath = stored.OriginalStoragePath,
            LargePath = stored.LargeUrl,
            CardPath = stored.CardUrl,
            ThumbnailPath = stored.ThumbnailUrl,
            ContentType = stored.ContentType,
            OriginalBytes = stored.OriginalBytes,
            Width = stored.Width,
            Height = stored.Height,
            IsCover = room.Images.Count == 0,
            SortOrder = room.Images.Count == 0 ? 0 : room.Images.Max(x => x.SortOrder) + 1
        };
        db.RoomImages.Add(image);
        await db.SaveChangesAsync(cancellationToken);
        return (ToImageDto(image), null);
    }

    public async Task<(RoomImageDto? Image, RoomContentError? Error)> UpdateImageAsync(Guid propertyId, Guid roomId, Guid imageId, UpdateRoomImageRequest request, CancellationToken cancellationToken = default)
    {
        var room = await db.Rooms.Include(x => x.Images).SingleOrDefaultAsync(x => x.PropertyId == propertyId && x.Id == roomId, cancellationToken);
        if (room is null) return (null, new("not_found", "Không tìm thấy phòng."));
        var image = room.Images.SingleOrDefault(x => x.Id == imageId);
        if (image is null) return (null, new("not_found", "Không tìm thấy ảnh."));

        var alt = Clean(request.AltText);
        if (alt?.Length > 300) return (null, new("validation", "Alt text tối đa 300 ký tự."));
        image.AltText = alt;
        if (request.IsCover)
        {
            foreach (var item in room.Images) item.IsCover = item.Id == imageId;
        }
        else if (image.IsCover && room.Images.Count > 1)
        {
            var replacement = room.Images.Where(x => x.Id != image.Id).OrderBy(x => x.SortOrder).First();
            image.IsCover = false;
            replacement.IsCover = true;
        }
        await db.SaveChangesAsync(cancellationToken);
        return (ToImageDto(image), null);
    }

    public async Task<RoomContentError?> ReorderImagesAsync(Guid propertyId, Guid roomId, ReorderRoomImagesRequest request, CancellationToken cancellationToken = default)
    {
        var images = await db.RoomImages.Where(x => x.RoomId == roomId && x.Room.PropertyId == propertyId).ToListAsync(cancellationToken);
        if (images.Count != request.ImageIds.Distinct().Count() || images.Any(x => !request.ImageIds.Contains(x.Id)))
            return new("validation", "Danh sách sắp xếp ảnh không hợp lệ.");
        for (var i = 0; i < request.ImageIds.Count; i++) images.Single(x => x.Id == request.ImageIds[i]).SortOrder = i;
        await db.SaveChangesAsync(cancellationToken);
        return null;
    }

    public async Task<RoomContentError?> DeleteImageAsync(Guid propertyId, Guid roomId, Guid imageId, CancellationToken cancellationToken = default)
    {
        var room = await db.Rooms.Include(x => x.Images).SingleOrDefaultAsync(x => x.PropertyId == propertyId && x.Id == roomId, cancellationToken);
        if (room is null) return new("not_found", "Không tìm thấy phòng.");
        var image = room.Images.SingleOrDefault(x => x.Id == imageId);
        if (image is null) return new("not_found", "Không tìm thấy ảnh.");

        var stored = new StoredRoomImage(image.OriginalStoragePath, image.LargePath, image.CardPath, image.ThumbnailPath, image.Width, image.Height, image.OriginalBytes, image.ContentType, image.OriginalFileName);
        var wasCover = image.IsCover;
        db.RoomImages.Remove(image);
        if (wasCover)
        {
            var replacement = room.Images.Where(x => x.Id != imageId).OrderBy(x => x.SortOrder).FirstOrDefault();
            if (replacement is not null) replacement.IsCover = true;
        }
        await db.SaveChangesAsync(cancellationToken);
        await imageStorage.DeleteAsync(stored, cancellationToken);
        return null;
    }

    private async Task SyncAmenitiesAsync(Room room, IReadOnlyList<string> names, CancellationToken ct)
    {
        room.Amenities.Clear();
        foreach (var name in names)
        {
            var normalized = NormalizeName(name);
            var amenity = await db.Amenities.SingleOrDefaultAsync(x => x.PropertyId == room.PropertyId && x.NormalizedName == normalized, ct);
            if (amenity is null)
            {
                amenity = new Amenity { PropertyId = room.PropertyId, Name = name, NormalizedName = normalized, IsActive = true };
                db.Amenities.Add(amenity);
            }
            room.Amenities.Add(new RoomAmenity { Room = room, Amenity = amenity });
        }
    }

    private async Task SyncTagsAsync(Room room, IReadOnlyList<string> names, CancellationToken ct)
    {
        room.Tags.Clear();
        foreach (var name in names)
        {
            var normalized = NormalizeName(name);
            var tag = await db.RoomTags.SingleOrDefaultAsync(x => x.PropertyId == room.PropertyId && x.NormalizedName == normalized, ct);
            if (tag is null)
            {
                tag = new RoomTag { PropertyId = room.PropertyId, Name = name, NormalizedName = normalized, IsActive = true };
                db.RoomTags.Add(tag);
            }
            room.Tags.Add(new RoomTagAssignment { Room = room, RoomTag = tag });
        }
    }

    public static string CreateSlug(string raw)
    {
        var value = raw.Trim().ToLowerInvariant().Replace('đ', 'd');
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) builder.Append(ch);
        }
        var slug = SlugInvalid.Replace(builder.ToString().Normalize(NormalizationForm.FormC), "-").Trim('-');
        return slug.Length <= 180 ? slug : slug[..180].TrimEnd('-');
    }

    private static (IReadOnlyList<string> Items, string? Error) NormalizeItems(IReadOnlyList<string>? input, int maxItems, int maxLength)
    {
        var items = (input ?? []).Select(x => x?.Trim() ?? string.Empty).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (items.Count > maxItems) return ([], $"Tối đa {maxItems} mục.");
        if (items.Any(x => x.Length > maxLength)) return ([], $"Mỗi mục tối đa {maxLength} ký tự.");
        return (items, null);
    }

    private static string NormalizeName(string value) => value.Trim().ToUpperInvariant();
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static RoomContentDto ToDto(Room room) => new(
        room.Id, room.Code, room.Name, room.Slug ?? CreateSlug(room.Name), room.ShortDescription, room.DescriptionHtml, room.IsPublished,
        room.Amenities.Where(x => x.Amenity.IsActive).Select(x => x.Amenity.Name).OrderBy(x => x).ToList(),
        room.Tags.Where(x => x.RoomTag.IsActive).Select(x => x.RoomTag.Name).OrderBy(x => x).ToList(),
        room.Highlights.OrderBy(x => x.SortOrder).Select(x => x.Text).ToList(),
        room.Images.OrderByDescending(x => x.IsCover).ThenBy(x => x.SortOrder).Select(ToImageDto).ToList());

    private static RoomImageDto ToImageDto(RoomImage image) => new(image.Id, image.LargePath, image.CardPath, image.ThumbnailPath, image.AltText, image.IsCover, image.SortOrder, image.Width, image.Height, image.OriginalBytes);

    private static HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        foreach (var tag in new[] { "p", "br", "strong", "b", "em", "i", "h2", "h3", "ul", "ol", "li", "a", "blockquote" }) sanitizer.AllowedTags.Add(tag);
        sanitizer.AllowedAttributes.Clear();
        foreach (var attribute in new[] { "href", "target", "rel" }) sanitizer.AllowedAttributes.Add(attribute);
        sanitizer.AllowedSchemes.Clear();
        foreach (var scheme in new[] { "http", "https", "mailto" }) sanitizer.AllowedSchemes.Add(scheme);
        return sanitizer;
    }
}
