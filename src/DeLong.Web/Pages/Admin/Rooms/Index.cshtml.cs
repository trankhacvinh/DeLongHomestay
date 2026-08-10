using System.Text.Json;
using DeLong.Web.Data.Seed;
using DeLong.Web.Features.Rooms;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Admin.Rooms;

public sealed class IndexModel(RoomService roomService) : PageModel
{
    public Guid PropertyId { get; private set; } = DbSeeder.DeLongPropertyId;
    public string PageDataJson { get; private set; } = "{}";

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var rooms = await roomService.GetAllAsync(PropertyId, cancellationToken);
        PageDataJson = JsonSerializer.Serialize(
            new { propertyId = PropertyId, rooms },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }
}
