using System.Security.Claims;
using System.Text.Json;
using DeLong.Web.Features.Staff;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Admin.Staff;

public sealed class IndexModel(StaffAccountService staffAccountService) : PageModel
{
    public string PageDataJson { get; private set; } = "{}";

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Challenge();
        var data = await staffAccountService.GetPageDataAsync(userId, cancellationToken);
        PageDataJson = JsonSerializer.Serialize(data, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return Page();
    }
}
