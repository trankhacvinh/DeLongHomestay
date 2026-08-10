using System.Text.Json;
using DeLong.Web.Common.Security;
using DeLong.Web.Data.Seed;
using DeLong.Web.Features.Customers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Admin.Customers;

public sealed class IndexModel(
    CustomerService customerService,
    PropertyAccessService propertyAccess) : PageModel
{
    public Guid PropertyId { get; private set; } = DbSeeder.DeLongPropertyId;
    public string PageDataJson { get; private set; } = "{}";

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!await propertyAccess.CanAccessAsync(User, PropertyId, cancellationToken)) return Forbid();

        var customers = await customerService.GetAllAsync(PropertyId, null, cancellationToken);
        PageDataJson = JsonSerializer.Serialize(
            new { propertyId = PropertyId, customers },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return Page();
    }
}
