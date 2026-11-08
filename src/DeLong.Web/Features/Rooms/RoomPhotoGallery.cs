using DeLong.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Rooms;

public sealed record RoomPhotoDto(string ThumbnailUrl, string CardUrl, string LargeUrl, string? AltText);
public sealed record RoomPhotoGalleryDto(Guid RoomId, string Name, string PropertyName, IReadOnlyList<RoomPhotoDto> Images);

public static class RoomPhotoGallery
{
    public static Task<RoomPhotoGalleryDto?> LoadAsync(AppDbContext db, Guid propertyId, Guid roomId, bool publicOnly, CancellationToken ct) =>
        db.Rooms.AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.Id == roomId &&
                (!publicOnly || (x.Property.IsActive && x.IsActive && x.IsPublished)))
            .Select(x => new RoomPhotoGalleryDto(x.Id, x.Name, x.Property.Name,
                x.Images.OrderByDescending(i => i.IsCover).ThenBy(i => i.SortOrder).ThenBy(i => i.Id)
                    .Select(i => new RoomPhotoDto(i.ThumbnailPath, i.CardPath, i.LargePath, i.AltText)).ToList()))
            .SingleOrDefaultAsync(ct);
}
