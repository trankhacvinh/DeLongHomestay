using DeLong.Web.Data;
using DeLong.Web.Features.Site;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.PublicRooms;

public sealed record PublicRoomMediaDto(
    Guid RoomId,
    string Slug,
    int ImageCount,
    string ThumbnailUrl,
    string CardUrl,
    string LargeUrl,
    string AltText);

public static class PublicRoomMediaEndpoints
{
    public static IEndpointRouteBuilder MapPublicRoomMediaEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/public/room-media", async ([FromQuery] string? siteSlug, AppDbContext db, PublicPropertyResolver resolver, CancellationToken ct) =>
        {
            var property = await resolver.ResolveAsync(siteSlug, ct);
            if (property is null) return Results.NotFound();

            var rooms = await db.Rooms.AsNoTracking()
                .Where(x => x.PropertyId == property.Id && x.Property.IsActive && x.IsActive && x.IsPublished)
                .OrderBy(x => x.SortOrder)
                .Select(x => new
                {
                    x.Id,
                    x.Code,
                    x.Slug,
                    ImageCount = x.Images.Count(),
                    Image = x.Images.OrderByDescending(i => i.IsCover).ThenBy(i => i.SortOrder).Select(i => new
                    {
                        i.ThumbnailPath,
                        i.CardPath,
                        i.LargePath,
                        i.AltText
                    }).FirstOrDefault()
                })
                .ToListAsync(ct);

            return Results.Ok(rooms.Where(x => x.Image is not null).Select(x => new PublicRoomMediaDto(
                x.Id,
                string.IsNullOrWhiteSpace(x.Slug) ? x.Code.ToLowerInvariant() : x.Slug!,
                x.ImageCount,
                x.Image!.ThumbnailPath,
                x.Image.CardPath,
                x.Image.LargePath,
                x.Image.AltText ?? string.Empty)));
        }).AllowAnonymous();

        app.MapGet("/api/public/room-preview/{roomSlug}", async (
            string roomSlug,
            [FromQuery] string? siteSlug,
            PublicPropertyResolver resolver,
            PublicRoomContentService roomContentService,
            CancellationToken ct) =>
        {
            var property = await resolver.ResolveAsync(siteSlug, ct);
            if (property is null) return Results.NotFound();
            var room = await roomContentService.GetRoomAsync(property.Id, roomSlug, ct);
            if (room is null) return Results.NotFound();

            return Results.Ok(new
            {
                room.Id,
                room.Code,
                room.Name,
                room.Slug,
                room.Capacity,
                room.ShortDescription,
                room.QuickFromPrice,
                room.Amenities,
                room.Highlights,
                room.Images
            });
        }).AllowAnonymous();

        return app;
    }
}
