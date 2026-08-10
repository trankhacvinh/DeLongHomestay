using System.Text.Json;
using DeLong.Web.Common.Security;
using DeLong.Web.Data.Seed;
using DeLong.Web.Features.Bookings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Admin.Bookings;

public sealed class IndexModel(
    BookingService bookingService,
    PropertyAccessService propertyAccess) : PageModel
{
    public Guid PropertyId { get; private set; } = DbSeeder.DeLongPropertyId;
    public string PageDataJson { get; private set; } = "{}";

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!await propertyAccess.CanAccessAsync(User, PropertyId, cancellationToken)) return Forbid();

        var bookings = await bookingService.GetAllAsync(PropertyId, null, null, cancellationToken);
        PageDataJson = JsonSerializer.Serialize(
            new { propertyId = PropertyId, timeZoneId = "Asia/Ho_Chi_Minh", bookings },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return Page();
    }
}
