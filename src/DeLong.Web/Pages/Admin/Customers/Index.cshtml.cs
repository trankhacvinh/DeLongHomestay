using System.Text.Json;
using DeLong.Web.Common.Security;
using DeLong.Web.Features.Customers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Admin.Customers;

public sealed class IndexModel(
    CustomerService customerService,
    CurrentPropertyService currentPropertyService) : PageModel
{
    public Guid PropertyId { get; private set; }
    public string PageDataJson { get; private set; } = "{}";

    public async Task<IActionResult> OnGetAsync(Guid? propertyId, CancellationToken cancellationToken)
    {
        var property = await currentPropertyService.ResolveAsync(User, propertyId, cancellationToken);
        if (property is null) return Forbid();

        PropertyId = property.Id;
        var customers = await customerService.GetAllAsync(PropertyId, null, cancellationToken);
        PageDataJson = JsonSerializer.Serialize(
            new { propertyId = PropertyId, propertyName = property.Name, customers },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return Page();
    }
}
