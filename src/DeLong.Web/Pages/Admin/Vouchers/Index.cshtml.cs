using System.Text.Json;
using DeLong.Web.Common.Security;
using DeLong.Web.Features.Customers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Admin.Vouchers;

[Authorize(Policy = "ViewVouchers")]
public sealed class IndexModel(
    CurrentPropertyService currentPropertyService,
    CustomerService customerService) : PageModel
{
    public string PageDataJson { get; private set; } = "{}";

    public async Task<IActionResult> OnGetAsync(Guid? propertyId, CancellationToken cancellationToken)
    {
        var property = await currentPropertyService.ResolveAsync(User, propertyId, cancellationToken);
        if (property is null) return Forbid();

        var customers = await customerService.GetAllAsync(property.Id, null, cancellationToken);
        PageDataJson = JsonSerializer.Serialize(new
        {
            propertyId = property.Id,
            propertyName = property.Name,
            timeZoneId = property.TimeZoneId,
            canManage = User.IsInRole("Admin") || User.IsInRole("Manager"),
            customers = customers.Where(x => x.IsActive && !x.IsBlocked).Select(x => new
            {
                x.Id,
                x.Name,
                x.Phone,
                x.Email
            })
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        return Page();
    }
}
