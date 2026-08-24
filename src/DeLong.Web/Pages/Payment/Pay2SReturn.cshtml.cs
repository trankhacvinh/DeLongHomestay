using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Payment;

public sealed class Pay2SReturnModel : PageModel
{
    public string OrderId { get; private set; } = string.Empty;

    public void OnGet(string? orderId) => OrderId = orderId?.Trim() ?? string.Empty;
}
