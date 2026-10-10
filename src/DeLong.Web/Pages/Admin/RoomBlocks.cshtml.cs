using System.Text.Json;
using DeLong.Web.Common.Security;
using DeLong.Web.Features.Rooms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Admin;

[Authorize(Policy = "ManageRooms")]
public sealed class RoomBlocksModel(CurrentPropertyService currentProperty, RoomService rooms, RoomBookingBlockService blocks) : PageModel
{
    public string PageDataJson { get; private set; } = "{}";
    public async Task<IActionResult> OnGetAsync(Guid? propertyId, CancellationToken cancellationToken)
    {
        var property = await currentProperty.ResolveAsync(User, propertyId, cancellationToken);
        if (property is null) return Forbid();
        PageDataJson = JsonSerializer.Serialize(new { propertyId = property.Id, propertyName = property.Name,
            timeZoneId = property.TimeZoneId, rooms = await rooms.GetAllAsync(property.Id, cancellationToken),
            blocks = await blocks.GetAllAsync(property.Id, cancellationToken) }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return Page();
    }
}
