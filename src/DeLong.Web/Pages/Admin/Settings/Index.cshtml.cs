using System.Text.Json;
using DeLong.Web.Common.Security;
using DeLong.Web.Features.Notifications;
using DeLong.Web.Features.Housekeeping;
using DeLong.Web.Features.Rooms;
using DeLong.Web.Features.CustomerAccounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Admin.Settings;

[Authorize(Policy = "ManageRooms")]
public sealed class IndexModel(
    RoomService roomService,
    HousekeepingService housekeepingService,
    NotificationSettingsService notificationSettingsService,
    CustomerAccountSettingsService customerAccountSettingsService,
    CurrentPropertyService currentPropertyService) : PageModel
{
    public Guid PropertyId { get; private set; }
    public string PageDataJson { get; private set; } = "{}";

    public async Task<IActionResult> OnGetAsync(Guid? propertyId, CancellationToken cancellationToken)
    {
        var property = await currentPropertyService.ResolveAsync(User, propertyId, cancellationToken);
        if (property is null) return Forbid();

        PropertyId = property.Id;
        var rooms = await roomService.GetAllAsync(PropertyId, cancellationToken);
        var housekeepingSettings = await housekeepingService.GetSettingsAsync(PropertyId, cancellationToken);
        var notificationSettings = await notificationSettingsService.GetAsync(PropertyId, cancellationToken);
        var customerAccountSettings = await customerAccountSettingsService.GetAsync(PropertyId, cancellationToken);
        PageDataJson = JsonSerializer.Serialize(
            new
            {
                propertyId = PropertyId,
                propertyName = property.Name,
                rooms,
                housekeepingSettings,
                notificationSettings,
                customerAccountSettings
            },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return Page();
    }
}
