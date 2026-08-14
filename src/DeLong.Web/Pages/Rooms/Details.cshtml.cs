using DeLong.Web.Features.PublicRooms;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Rooms;

public sealed class DetailsModel(PublicRoomContentService publicRoomContentService) : PageModel
{
    public PublicRoomDetailDto Room { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(string code, CancellationToken cancellationToken)
    {
        var room = await publicRoomContentService.GetRoomAsync(code, cancellationToken);
        if (room is null) return NotFound();
        Room = room;
        return Page();
    }
}
