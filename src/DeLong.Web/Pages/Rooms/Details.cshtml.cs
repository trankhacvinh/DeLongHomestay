using DeLong.Web.Data;
using DeLong.Web.Features.PublicRooms;
using DeLong.Web.Features.Rooms;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Pages.Rooms;

public sealed class DetailsModel(
    PublicRoomContentService publicRoomContentService,
    AppDbContext db) : PageModel
{
    private const string PublicPropertyCode = "DELONG";

    public PublicRoomDetailDto Room { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(string code, CancellationToken cancellationToken)
    {
        var requested = code.Trim();
        var room = await publicRoomContentService.GetRoomAsync(requested, cancellationToken);

        // Rooms created before slug persistence could be published with Slug = null while the
        // Admin editor displayed a generated name slug. Keep those old links working instead
        // of returning 404; newly created rooms persist the slug in RoomService.
        if (room is null && !string.IsNullOrWhiteSpace(requested))
        {
            var legacyCandidates = await db.Rooms
                .AsNoTracking()
                .Where(x => x.Property.Code == PublicPropertyCode &&
                            x.Property.IsActive &&
                            x.IsActive &&
                            x.IsPublished &&
                            string.IsNullOrEmpty(x.Slug))
                .Select(x => new { x.Code, x.Name })
                .ToListAsync(cancellationToken);

            var normalized = requested.ToLowerInvariant();
            var matches = legacyCandidates
                .Where(x => RoomContentService.CreateSlug(x.Name) == normalized)
                .Take(2)
                .ToList();

            if (matches.Count == 1)
                room = await publicRoomContentService.GetRoomAsync(matches[0].Code, cancellationToken);
        }

        if (room is null) return NotFound();
        Room = room;
        return Page();
    }
}
