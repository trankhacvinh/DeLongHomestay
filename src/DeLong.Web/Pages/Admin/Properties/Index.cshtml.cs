using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Admin.Properties;

[Authorize(Policy = "ManageProperties")]
public sealed class IndexModel : PageModel
{
    public void OnGet() { }
}
