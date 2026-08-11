using System.Text.Json;
using DeLong.Web.Common.Security;
using DeLong.Web.Features.Rooms;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Admin.Rooms;

public sealed class IndexModel(RoomService roomService, CurrentPropertyService currentPropertyService) : PageModel
{
    public Guid PropertyId { get; private set; }
    public string PageDataJson { get; private set; } = "{}";

    public async Task<IActionResult> OnGetAsync(Guid? propertyId, CancellationToken cancellationToken)
    {
        var currentProperty = await currentPropertyService.ResolveAsync(User, propertyId, cancellationToken);
        if (currentProperty is null) return Forbid();

        PropertyId = currentProperty.Id;
        var rooms = await roomService.GetAllAsync(PropertyId, cancellationToken);
        PageDataJson = JsonSerializer.Serialize(
            new { propertyId = PropertyId, propertyName = currentProperty.Name, rooms },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return Page();
    }
}
