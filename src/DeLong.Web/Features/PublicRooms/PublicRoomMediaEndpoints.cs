using DeLong.Web.Data;
using DeLong.Web.Features.Site;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.PublicRooms;

public sealed record PublicRoomMediaDto(Guid RoomId, string ThumbnailUrl, string CardUrl, string LargeUrl, string AltText);

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
                x.Image!.ThumbnailPath,
                x.Image.CardPath,
                x.Image.LargePath,
                x.Image.AltText ?? string.Empty)));
        }).AllowAnonymous();
        return app;
    }
}
