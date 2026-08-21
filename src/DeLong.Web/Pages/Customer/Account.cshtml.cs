using DeLong.Web.Features.CustomerAccounts;
using DeLong.Web.Features.Site;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Customer;

public sealed class AccountModel : PageModel
{
    public IActionResult OnGet(string? siteSlug = null)
    {
        var scopePrefix = PublicPropertyResolver.ScopePrefix(siteSlug);
        var accountUrl = $"{scopePrefix}/customer/account";
        var loginUrl = $"{scopePrefix}/customer/login";
        var isLoginRoute = Request.Path.Value?.EndsWith("/customer/login", StringComparison.OrdinalIgnoreCase) == true;
        var isCustomer = User.Identity?.IsAuthenticated == true && User.IsInRole(CustomerAccountService.CustomerRole);

        if (isCustomer)
            return isLoginRoute ? LocalRedirect(accountUrl) : Page();

        return isLoginRoute ? Page() : LocalRedirect(loginUrl);
    }
}
