using DeLong.Web.Features.Site;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Booking;

public sealed class LookupModel(PublicPropertyResolver publicPropertyResolver) : PageModel
{
    public string? SiteSlug { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? siteSlug, CancellationToken cancellationToken)
    {
        var property = await publicPropertyResolver.ResolveAsync(siteSlug, cancellationToken);
        if (property is null) return NotFound();
        SiteSlug = string.IsNullOrWhiteSpace(siteSlug) ? null : property.SiteSlug;
        return Page();
    }
}
