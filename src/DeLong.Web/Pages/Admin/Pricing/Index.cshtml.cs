using System.Text.Json;
using DeLong.Web.Common.Security;
using DeLong.Web.Features.Rooms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Admin.Pricing;

[Authorize(Policy = "ViewRooms")]
public sealed class IndexModel(CurrentPropertyService currentPropertyService, RoomService roomService) : PageModel
{
    public string PageDataJson { get; private set; } = "{}";

    public async Task<IActionResult> OnGetAsync(Guid? propertyId, CancellationToken cancellationToken)
    {
        var property = await currentPropertyService.ResolveAsync(User, propertyId, cancellationToken);
        if (property is null) return Forbid();
        PageDataJson = JsonSerializer.Serialize(new
        {
            propertyId = property.Id,
            propertyName = property.Name,
            canManage = User.IsInRole("Admin") || User.IsInRole("Manager"),
            rooms = await roomService.GetAllAsync(property.Id, cancellationToken)
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return Page();
    }
}
