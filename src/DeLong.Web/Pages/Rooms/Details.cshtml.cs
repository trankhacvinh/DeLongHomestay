
using DeLong.Web.Data;
using DeLong.Web.Features.PublicRooms;
using DeLong.Web.Features.Rooms;
using DeLong.Web.Features.Site;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Pages.Rooms;

public sealed class DetailsModel(
    PublicRoomContentService publicRoomContentService,
    PublicPropertyResolver publicPropertyResolver,
    AppDbContext db) : PageModel
{
    public PublicRoomDetailDto Room { get; private set; } = null!;
    public string PropertyName { get; private set; } = string.Empty;
    public string? SiteSlug { get; private set; }

    public async Task<IActionResult> OnGetAsync(string code, string? siteSlug, CancellationToken cancellationToken)
    {
        var property = await publicPropertyResolver.ResolveAsync(siteSlug, cancellationToken);
        if (property is null) return NotFound();
        PropertyName = property.Name;
        SiteSlug = property.SiteSlug;
        var requested = code.Trim();

        // Root /rooms/{code} was the old DELONG-only route. Keep it as a canonical redirect,
        // while every actual detail page carries its property scope explicitly.
        if (string.IsNullOrWhiteSpace(siteSlug))
            return RedirectPermanent(PublicUrlBuilder.Room(property.SiteSlug, requested));

        var room = await publicRoomContentService.GetRoomAsync(property.Id, requested, cancellationToken);
        if (room is null && !string.IsNullOrWhiteSpace(requested))
        {
            var legacyCandidates = await db.Rooms.AsNoTracking()
                .Where(x => x.PropertyId == property.Id && x.Property.IsActive && x.IsActive && x.IsPublished && string.IsNullOrEmpty(x.Slug))
                .Select(x => new { x.Code, x.Name })
                .ToListAsync(cancellationToken);
            var normalized = requested.ToLowerInvariant();
            var matches = legacyCandidates.Where(x => RoomContentService.CreateSlug(x.Name) == normalized).Take(2).ToList();
            if (matches.Count == 1)
                room = await publicRoomContentService.GetRoomAsync(property.Id, matches[0].Code, cancellationToken);
        }
        if (room is null) return NotFound();
        Room = room;
        return Page();
    }
}
