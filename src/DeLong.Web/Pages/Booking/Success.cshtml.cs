using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Booking;

public sealed class SuccessModel : PageModel
{
    public string Code { get; private set; } = string.Empty;
    public string Room { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }

    public void OnGet(string? code, string? room, decimal? amount)
    {
        Code = code?.Trim() ?? string.Empty;
        Room = room?.Trim() ?? string.Empty;
        Amount = amount ?? 0;
    }
}
