using DeLong.Web.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Admin.Ai;

[Authorize(Policy = "UseAdminAi")]
public sealed class IndexModel(CurrentPropertyService currentPropertyService) : PageModel
{
    public Guid PropertyId { get; private set; }
    public async Task<IActionResult> OnGetAsync(Guid? propertyId, CancellationToken ct)
    {
        var property = await currentPropertyService.ResolveAsync(User, propertyId, ct);
        if (property is null) return Forbid();
        PropertyId = property.Id;
        return Page();
    }
}

