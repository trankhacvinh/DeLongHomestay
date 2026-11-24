using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Admin;

// The v1 calendar was replaced by CalendarV2 (same features, maintained in one place).
// Keep the old URL working for bookmarks and old links.
public sealed class CalendarModel : PageModel
{
    public IActionResult OnGet(DateOnly? from, DateOnly? to, Guid? propertyId) =>
        RedirectToPage("/Admin/CalendarV2", new
        {
            from = from?.ToString("yyyy-MM-dd"),
            to = to?.ToString("yyyy-MM-dd"),
            propertyId
        });
}
