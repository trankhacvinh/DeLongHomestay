using System.Text.Json;
using DeLong.Web.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Admin.Imports;

[Authorize(Policy = "ManageBookings")]
public sealed class IndexModel(CurrentPropertyService currentPropertyService) : PageModel
{
    public string PageDataJson { get; private set; } = "{}";

    public async Task<IActionResult> OnGetAsync(Guid? propertyId, CancellationToken cancellationToken)
    {
        var property = await currentPropertyService.ResolveAsync(User, propertyId, cancellationToken);
        if (property is null) return Forbid();

        PageDataJson = JsonSerializer.Serialize(new
        {
            propertyId = property.Id,
            propertyName = property.Name
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return Page();
    }
}
