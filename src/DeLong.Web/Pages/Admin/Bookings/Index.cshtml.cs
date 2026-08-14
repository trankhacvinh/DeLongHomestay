using System.Text.Json;
using DeLong.Web.Common.Security;
using DeLong.Web.Features.Bookings;
using DeLong.Web.Features.Rooms;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Admin.Bookings;

public sealed class IndexModel(
    BookingService bookingService,
    RoomService roomService,
    CurrentPropertyService currentPropertyService) : PageModel
{
    public Guid PropertyId { get; private set; }
    public string PageDataJson { get; private set; } = "{}";

    public async Task<IActionResult> OnGetAsync(Guid? propertyId, CancellationToken cancellationToken)
    {
        var property = await currentPropertyService.ResolveAsync(User, propertyId, cancellationToken);
        if (property is null) return Forbid();

        PropertyId = property.Id;
        var bookings = await bookingService.GetAllAsync(PropertyId, null, null, cancellationToken);
        var rooms = await roomService.GetAllAsync(PropertyId, cancellationToken);
        PageDataJson = JsonSerializer.Serialize(
            new { propertyId = PropertyId, propertyName = property.Name, timeZoneId = property.TimeZoneId, bookings, rooms },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return Page();
    }
}
