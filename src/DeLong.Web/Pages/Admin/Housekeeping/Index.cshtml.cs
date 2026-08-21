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
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(property.TimeZoneId);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone));
        var schedule = await housekeepingService.GetScheduleAsync(PropertyId, today, 1, cancellationToken);
        var conditionTags = await housekeepingService.GetConditionTagsAsync(PropertyId, cancellationToken);
        var conditionReports = await housekeepingService.GetConditionReportsAsync(PropertyId, take: 30, cancellationToken: cancellationToken);
        PageDataJson = JsonSerializer.Serialize(
            new
            {
                propertyId = PropertyId,
                propertyName = property.Name,
                timeZoneId = property.TimeZoneId,
                today = today.ToString("yyyy-MM-dd"),
                rooms,
                schedule,
                conditionTags,
                conditionReports
            },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return Page();
    }
}
