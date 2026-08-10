using System.Text.Json;
using DeLong.Web.Common.Security;
using DeLong.Web.Features.Housekeeping;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Admin.Housekeeping;

public sealed class IndexModel(
    HousekeepingService housekeepingService,
    CurrentPropertyService currentPropertyService) : PageModel
{
    public Guid PropertyId { get; private set; }
    public string PageDataJson { get; private set; } = "{}";

    public async Task<IActionResult> OnGetAsync(Guid? propertyId, CancellationToken cancellationToken)
    {
        var property = await currentPropertyService.ResolveAsync(User, propertyId, cancellationToken);
        if (property is null) return Forbid();

        PropertyId = property.Id;
        var rooms = await housekeepingService.GetAllAsync(PropertyId, cancellationToken);
        PageDataJson = JsonSerializer.Serialize(
            new
            {
                propertyId = PropertyId,
                propertyName = property.Name,
                timeZoneId = property.TimeZoneId,
                rooms
            },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return Page();
    }
}
