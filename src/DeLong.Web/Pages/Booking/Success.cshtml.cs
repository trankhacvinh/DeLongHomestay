using DeLong.Web.Features.Site;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Booking;

public sealed class SuccessModel(
    SiteContentService siteContentService,
    PublicPropertyResolver publicPropertyResolver) : PageModel
{
    public string Code { get; private set; } = string.Empty;
    public string Room { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public string Phone { get; private set; } = string.Empty;
    public string SiteName { get; private set; } = string.Empty;
    public string? SiteSlug { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? siteSlug, string? code, string? room, decimal? amount, CancellationToken ct)
    {
        var property = await publicPropertyResolver.ResolveAsync(siteSlug, ct);
        if (property is null) return NotFound();
        SiteSlug = string.IsNullOrWhiteSpace(siteSlug) ? null : property.SiteSlug;
        Code = code?.Trim() ?? string.Empty;
        Room = room?.Trim() ?? string.Empty;
        Amount = amount ?? 0;
        var site = await siteContentService.GetPublicAsync(SiteSlug, ct);
        SiteName = site?.Settings.SiteName ?? property.Name;
        Phone = site?.Settings.Phone ?? string.Empty;
        return Page();
    }
}
