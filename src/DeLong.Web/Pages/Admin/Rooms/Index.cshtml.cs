using System.Text.Json;
using DeLong.Web.Common.Security;
using DeLong.Web.Data.Seed;
using DeLong.Web.Features.Rooms;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Admin.Rooms;

public sealed class IndexModel(
    RoomService roomService,
    PropertyAccessService propertyAccess) : PageModel
{
    public Guid PropertyId { get; private set; } = DbSeeder.DeLongPropertyId;
    public string PageDataJson { get; private set; } = "{}";

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!await propertyAccess.CanAccessAsync(User, PropertyId, cancellationToken))
        {
            return Forbid();
        }

        var rooms = await roomService.GetAllAsync(PropertyId, cancellationToken);
        PageDataJson = JsonSerializer.Serialize(
            new { propertyId = PropertyId, rooms },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return Page();
    }
}
