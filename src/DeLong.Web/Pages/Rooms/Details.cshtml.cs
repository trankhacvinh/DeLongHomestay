using DeLong.Web.Features.PublicBooking;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Rooms;

public sealed class DetailsModel(PublicBookingService publicBookingService) : PageModel
{
    public PublicRoomDto Room { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(string code, CancellationToken cancellationToken)
    {
        var room = await publicBookingService.GetRoomAsync(code, cancellationToken);
        if (room is null) return NotFound();
        Room = room;
        return Page();
    }
}
