using System.Text.Json;
using DeLong.Web.Common.Security;
using DeLong.Web.Features.Rooms;
using DeLong.Web.Features.Site;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Admin.Rooms;

[Authorize(Policy = "ManageRooms")]
public sealed class ContentModel(
    RoomContentService contentService,
    CurrentPropertyService currentPropertyService) : PageModel
{
    public Guid PropertyId { get; private set; }
    public Guid RoomId { get; private set; }
    public string PageDataJson { get; private set; } = "{}";

    public async Task<IActionResult> OnGetAsync(Guid roomId, Guid? propertyId, CancellationToken cancellationToken)
    {
        var currentProperty = await currentPropertyService.ResolveAsync(User, propertyId, cancellationToken);
        if (currentProperty is null) return Forbid();

        var content = await contentService.GetAsync(currentProperty.Id, roomId, cancellationToken);
        if (content is null) return NotFound();

        var amenityCatalog = await contentService.GetAmenityCatalogAsync(currentProperty.Id, cancellationToken);
        var amenityPresets = await contentService.GetAmenityPresetsAsync(currentProperty.Id, cancellationToken);
        var siteSlug = PublicPropertyResolver.ToSiteSlug(currentProperty.Code);

        PropertyId = currentProperty.Id;
        RoomId = roomId;
        PageDataJson = JsonSerializer.Serialize(
            new
            {
                propertyId = PropertyId,
                propertyName = currentProperty.Name,
                siteSlug,
                publicBasePath = $"/h/{Uri.EscapeDataString(siteSlug)}",
                room = content,
                amenityCatalog,
                amenityPresets
            },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return Page();
    }
}
