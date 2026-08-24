using DeLong.Web.Features.Site;
using DeLong.Web.Features.PublicBooking;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Booking;

public sealed class SuccessModel(
    SiteContentService siteContentService,
    PublicPropertyResolver publicPropertyResolver,
    PublicBookingLookupService lookupService) : PageModel
{
    public string Code { get; private set; } = string.Empty;
    public string Room { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public decimal PaidAmount { get; private set; }
    public bool IsPay2SPaid { get; private set; }
    public string Phone { get; private set; } = string.Empty;
    public string SiteName { get; private set; } = string.Empty;
    public string? SiteSlug { get; private set; }
    public string? GuestGuideHtml { get; private set; }
    public string GuidePdfUrl { get; private set; } = string.Empty;

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
        if (!string.IsNullOrWhiteSpace(Code))
        {
            var booking = await lookupService.GetSuccessAsync(SiteSlug, Code, ct);
            if (booking is not null)
            {
                Room = booking.RoomName;
                GuestGuideHtml = booking.GuestGuideHtml;
                IsPay2SPaid = booking.IsPay2SPaid;
                PaidAmount = booking.PaidAmount;
                var scope = string.IsNullOrWhiteSpace(SiteSlug) ? string.Empty : $"&siteSlug={Uri.EscapeDataString(SiteSlug)}";
                GuidePdfUrl = $"/api/public/booking-guide-pdf?code={Uri.EscapeDataString(booking.Code)}{scope}";
            }
        }
        return Page();
    }
}
