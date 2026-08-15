using DeLong.Web.Features.Site;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Booking;

public sealed class SuccessModel(SiteContentService siteContentService) : PageModel
{
    public string Code { get; private set; } = string.Empty;
    public string Room { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public string Phone { get; private set; } = string.Empty;

    public async Task OnGetAsync(string? code, string? room, decimal? amount, CancellationToken ct)
    {
        Code = code?.Trim() ?? string.Empty;
        Room = room?.Trim() ?? string.Empty;
        Amount = amount ?? 0;
        Phone = (await siteContentService.GetPublicAsync(ct))?.Settings.Phone ?? string.Empty;
    }
}
