using System.Text.Json;
using DeLong.Web.Common.Security;
using DeLong.Web.Features.Housekeeping;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Admin.RoomConditionReports;

public sealed class IndexModel(
    HousekeepingService housekeepingService,
    CurrentPropertyService currentPropertyService) : PageModel
{
    public Guid PropertyId { get; private set; }
    public string PageDataJson { get; private set; } = "{}";

    public async Task<IActionResult> OnGetAsync(Guid? propertyId, Guid? roomId, CancellationToken ct)
    {
        var property = await currentPropertyService.ResolveAsync(User, propertyId, ct);
        if (property is null) return Forbid();

        PropertyId = property.Id;
        var rooms = await housekeepingService.GetAllAsync(PropertyId, ct);
        var tags = await housekeepingService.GetConditionTagsAsync(PropertyId, ct);
        var reports = await housekeepingService.GetConditionReportsAsync(PropertyId, take: 100, cancellationToken: ct);
        PageDataJson = JsonSerializer.Serialize(new
        {
            propertyId = PropertyId,
            propertyName = property.Name,
            timeZoneId = property.TimeZoneId,
            selectedRoomId = roomId,
            rooms,
            conditionTags = tags,
            conditionReports = reports
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return Page();
    }
}
