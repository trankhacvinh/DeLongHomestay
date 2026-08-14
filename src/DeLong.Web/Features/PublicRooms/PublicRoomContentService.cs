using DeLong.Web.Data;
using DeLong.Web.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.PublicRooms;

public sealed class PublicRoomContentService(AppDbContext db)
{
    private const string PublicPropertyCode = "DELONG";

    public async Task<PublicRoomCatalogDto> GetCatalogAsync(CancellationToken cancellationToken = default)
    {
        var rooms = await db.Rooms.AsNoTracking()
            .Where(x => x.Property.Code == PublicPropertyCode && x.Property.IsActive && x.IsActive && x.IsPublished)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Code,
                x.Name,
                x.Slug,
                x.Capacity,
                x.ShortDescription,
                Amenities = x.Amenities.Where(a => a.Amenity.IsActive).Select(a => a.Amenity.Name).ToList(),
                Tags = x.Tags.Where(t => t.RoomTag.IsActive).Select(t => t.RoomTag.Name).OrderBy(t => t).ToList(),
                Cover = x.Images.OrderByDescending(i => i.IsCover).ThenBy(i => i.SortOrder)
                    .Select(i => new { i.CardPath }).FirstOrDefault(),
                Rates = x.Rates.Where(r => r.IsActive && r.Price > 0).OrderBy(r => r.SortOrder)
                    .Select(r => new { r.Id, r.Name, r.StartTime, r.EndTime, r.Type, r.Price }).ToList()
            })
            .ToListAsync(cancellationToken);

        var result = rooms.Select(x =>
        {
            var rates = x.Rates.Select(r => new PublicRoomRateDto(
                r.Id, r.Name, r.StartTime.ToString("HH:mm"), r.EndTime.ToString("HH:mm"), r.Type, r.Price)).ToList();
            return new PublicRoomCardDto(
                x.Id,
                x.Code,
                x.Name,
                x.Slug ?? x.Code.ToLowerInvariant(),
                x.Capacity,
                x.ShortDescription,
                x.Cover?.CardPath,
                HasBathtub(x.Code, x.Amenities),
                rates.Count == 0 ? 0 : rates.Min(r => r.Price),
                x.Tags,
                rates);
        }).ToList();

        return new PublicRoomCatalogDto(result);
    }

    public async Task<PublicRoomDetailDto?> GetRoomAsync(string slugOrCode, CancellationToken cancellationToken = default)
    {
        var normalized = slugOrCode.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) return null;

        var room = await db.Rooms.AsNoTracking()
            .Where(x => x.Property.Code == PublicPropertyCode && x.Property.IsActive && x.IsActive && x.IsPublished &&
                        (x.Slug == normalized.ToLower() || x.Code == normalized.ToUpper()))
            .Select(x => new
            {
                x.Id,
                x.Code,
                x.Name,
                x.Slug,
                x.Capacity,
                x.ShortDescription,
                x.DescriptionHtml,
                Amenities = x.Amenities.Where(a => a.Amenity.IsActive).Select(a => a.Amenity.Name).OrderBy(a => a).ToList(),
                Tags = x.Tags.Where(t => t.RoomTag.IsActive).Select(t => t.RoomTag.Name).OrderBy(t => t).ToList(),
                Highlights = x.Highlights.OrderBy(h => h.SortOrder).Select(h => h.Text).ToList(),
                Images = x.Images.OrderByDescending(i => i.IsCover).ThenBy(i => i.SortOrder)
                    .Select(i => new { i.Id, i.LargePath, i.CardPath, i.ThumbnailPath, i.AltText, i.IsCover, i.SortOrder }).ToList(),
                Rates = x.Rates.Where(r => r.IsActive && r.Price > 0).OrderBy(r => r.SortOrder)
                    .Select(r => new { r.Id, r.Name, r.StartTime, r.EndTime, r.Type, r.Price }).ToList()
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (room is null) return null;

        var rates = room.Rates.Select(r => new PublicRoomRateDto(
            r.Id, r.Name, r.StartTime.ToString("HH:mm"), r.EndTime.ToString("HH:mm"), r.Type, r.Price)).ToList();
        var images = room.Images.Select(i => new PublicRoomImageDto(
            i.Id, i.LargePath, i.CardPath, i.ThumbnailPath,
            string.IsNullOrWhiteSpace(i.AltText) ? room.Name : i.AltText!, i.IsCover, i.SortOrder)).ToList();

        return new PublicRoomDetailDto(
            room.Id,
            room.Code,
            room.Name,
            room.Slug ?? room.Code.ToLowerInvariant(),
            room.Capacity,
            room.ShortDescription,
            room.DescriptionHtml,
            HasBathtub(room.Code, room.Amenities),
            rates.Count == 0 ? 0 : rates.Min(r => r.Price),
            room.Amenities,
            room.Tags,
            room.Highlights,
            images,
            rates);
    }

    private static bool HasBathtub(string code, IReadOnlyCollection<string> amenities) =>
        amenities.Any(x => x.Contains("bồn tắm", StringComparison.OrdinalIgnoreCase)) ||
        code is "COCO-01" or "MOON-04" or "AMBER-05" or "ROMAN-06";
}
