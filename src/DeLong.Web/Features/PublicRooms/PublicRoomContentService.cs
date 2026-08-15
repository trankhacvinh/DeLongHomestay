using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Site;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.PublicRooms;

public sealed class PublicRoomContentService(AppDbContext db)
{
    public async Task<PublicGlobalRoomCatalogDto> GetGlobalCatalogAsync(CancellationToken cancellationToken = default)
    {
        var properties = await db.Properties.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Code,
                x.SiteSlug,
                SiteName = db.Set<PropertySiteSettings>().Where(s => s.PropertyId == x.Id).Select(s => s.SiteName).FirstOrDefault(),
                Tagline = db.Set<PropertySiteSettings>().Where(s => s.PropertyId == x.Id).Select(s => s.Tagline).FirstOrDefault(),
                Address = db.Set<PropertySiteSettings>().Where(s => s.PropertyId == x.Id).Select(s => s.Address).FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var propertyCards = new List<PublicPropertyCardDto>();
        var globalRooms = new List<PublicGlobalRoomCardDto>();
        foreach (var property in properties)
        {
            var siteSlug = PublicPropertyResolver.EffectiveSiteSlug(property.SiteSlug, property.Code);
            var catalog = await GetCatalogAsync(property.Id, cancellationToken);
            propertyCards.Add(new PublicPropertyCardDto(
                property.Id,
                property.Name,
                string.IsNullOrWhiteSpace(property.SiteName) ? property.Name : property.SiteName!,
                siteSlug,
                property.Tagline ?? string.Empty,
                property.Address ?? string.Empty,
                catalog.Rooms.Count,
                catalog.Rooms.Select(x => x.CoverCardUrl).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))));
            globalRooms.AddRange(catalog.Rooms.Select(room => new PublicGlobalRoomCardDto(
                property.Id,
                property.Name,
                siteSlug,
                room)));
        }

        return new PublicGlobalRoomCatalogDto(propertyCards, globalRooms);
    }

    public async Task<PublicRoomCatalogDto> GetCatalogAsync(CancellationToken cancellationToken = default)
    {
        var propertyId = await db.Properties.AsNoTracking()
            .Where(x => x.Code == PublicPropertyResolver.LegacyPropertyCode && x.IsActive)
            .Select(x => (Guid?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);
        return propertyId.HasValue
            ? await GetCatalogAsync(propertyId.Value, cancellationToken)
            : new PublicRoomCatalogDto([]);
    }

    public async Task<PublicRoomCatalogDto> GetCatalogAsync(Guid propertyId, CancellationToken cancellationToken = default)
    {
        var rooms = await db.Rooms.AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.Property.IsActive && x.IsActive && x.IsPublished)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Code,
                x.Name,
                x.Slug,
                x.Capacity,
                x.ShortDescription,
                Amenities = x.Amenities.Where(a => a.Amenity.IsActive).Select(a => a.Amenity.Name).OrderBy(a => a).ToList(),
                Tags = x.Tags.Where(t => t.RoomTag.IsActive).Select(t => t.RoomTag.Name).OrderBy(t => t).ToList(),
                Cover = x.Images.OrderByDescending(i => i.IsCover).ThenBy(i => i.SortOrder)
                    .Select(i => new { i.CardPath, i.FocalX, i.FocalY }).FirstOrDefault(),
                Rates = x.Rates.Where(r => r.IsActive && r.Price > 0).OrderBy(r => r.SortOrder)
                    .Select(r => new { r.Id, r.Name, r.StartTime, r.EndTime, r.Type, r.Price }).ToList()
            })
            .ToListAsync(cancellationToken);

        var result = rooms.Select(x =>
        {
            var rates = x.Rates.Select(r => new PublicRoomRateDto(
                r.Id, r.Name, r.StartTime.ToString("HH:mm"), r.EndTime.ToString("HH:mm"), r.Type, r.Price)).ToList();
            var prices = GetPrices(rates);
            return new PublicRoomCardDto(
                x.Id,
                x.Code,
                x.Name,
                x.Slug ?? x.Code.ToLowerInvariant(),
                x.Capacity,
                x.ShortDescription,
                x.Cover?.CardPath,
                x.Cover?.FocalX ?? 0.5,
                x.Cover?.FocalY ?? 0.5,
                HasBathtub(x.Code, x.Amenities),
                prices.QuickFrom,
                prices.Overnight,
                prices.Nightly,
                x.Tags,
                x.Amenities,
                rates);
        }).ToList();

        return new PublicRoomCatalogDto(result);
    }

    public async Task<PublicRoomDetailDto?> GetRoomAsync(string slugOrCode, CancellationToken cancellationToken = default)
    {
        var propertyId = await db.Properties.AsNoTracking()
            .Where(x => x.Code == PublicPropertyResolver.LegacyPropertyCode && x.IsActive)
            .Select(x => (Guid?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);
        return propertyId.HasValue
            ? await GetRoomAsync(propertyId.Value, slugOrCode, cancellationToken)
            : null;
    }

    public async Task<PublicRoomDetailDto?> GetRoomAsync(Guid propertyId, string slugOrCode, CancellationToken cancellationToken = default)
    {
        var normalized = slugOrCode.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) return null;

        var room = await db.Rooms.AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.Property.IsActive && x.IsActive && x.IsPublished &&
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
                    .Select(i => new
                    {
                        i.Id,
                        i.LargePath,
                        i.CardPath,
                        i.ThumbnailPath,
                        i.AltText,
                        i.IsCover,
                        i.SortOrder,
                        i.Width,
                        i.Height,
                        i.FocalX,
                        i.FocalY
                    }).ToList(),
                Rates = x.Rates.Where(r => r.IsActive && r.Price > 0).OrderBy(r => r.SortOrder)
                    .Select(r => new { r.Id, r.Name, r.StartTime, r.EndTime, r.Type, r.Price }).ToList()
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (room is null) return null;

        var rates = room.Rates.Select(r => new PublicRoomRateDto(
            r.Id, r.Name, r.StartTime.ToString("HH:mm"), r.EndTime.ToString("HH:mm"), r.Type, r.Price)).ToList();
        var prices = GetPrices(rates);
        var images = room.Images.Select(i =>
        {
            var (largeWidth, largeHeight) = GetLargeDimensions(i.Width, i.Height);
            return new PublicRoomImageDto(
                i.Id,
                i.LargePath,
                i.CardPath,
                i.ThumbnailPath,
                string.IsNullOrWhiteSpace(i.AltText) ? room.Name : i.AltText!,
                i.IsCover,
                i.SortOrder,
                i.Width,
                i.Height,
                largeWidth,
                largeHeight,
                i.FocalX,
                i.FocalY);
        }).ToList();

        return new PublicRoomDetailDto(
            room.Id,
            room.Code,
            room.Name,
            room.Slug ?? room.Code.ToLowerInvariant(),
            room.Capacity,
            room.ShortDescription,
            room.DescriptionHtml,
            HasBathtub(room.Code, room.Amenities),
            prices.QuickFrom,
            prices.Overnight,
            prices.Nightly,
            room.Amenities,
            room.Tags,
            room.Highlights,
            images,
            rates);
    }

    private static (decimal QuickFrom, decimal? Overnight, decimal? Nightly) GetPrices(IReadOnlyCollection<PublicRoomRateDto> rates)
    {
        var quick = rates.Where(x => x.Type == RoomRateType.TimeSlot).Select(x => x.Price).DefaultIfEmpty(0).Min();
        var overnight = rates.Where(x => x.Type == RoomRateType.Overnight).Select(x => (decimal?)x.Price).Min();
        var nightly = rates.Where(x => x.Type == RoomRateType.Nightly).Select(x => (decimal?)x.Price).Min();
        if (quick <= 0) quick = overnight ?? nightly ?? 0;
        return (quick, overnight, nightly);
    }

    private static (int Width, int Height) GetLargeDimensions(int width, int height)
    {
        var longest = Math.Max(width, height);
        if (longest <= 1600 || longest <= 0) return (width, height);
        var scale = 1600d / longest;
        return (Math.Max(1, (int)Math.Round(width * scale)), Math.Max(1, (int)Math.Round(height * scale)));
    }

    private static bool HasBathtub(string code, IReadOnlyCollection<string> amenities) =>
        amenities.Any(x => x.Contains("bồn tắm", StringComparison.OrdinalIgnoreCase)) ||
        code is "COCO-01" or "MOON-04" or "AMBER-05" or "ROMAN-06";
}
