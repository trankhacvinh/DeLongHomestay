using Microsoft.AspNetCore.Mvc.RazorPages;
using DeLong.Web.Data;
using DeLong.Web.Features.Payments;

namespace DeLong.Web.Pages.Payment;

public sealed class SePayModel(AppDbContext db) : PageModel
{
    public string OrderId { get; private set; } = string.Empty;
    public SePayPaymentState? PaymentState { get; private set; }
    public async Task OnGetAsync(string? orderId, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        OrderId = orderId?.Trim() ?? string.Empty;
        if (OrderId.Length > 0 && OrderId.Length <= 100)
            PaymentState = await SePayPaymentState.LoadAsync(db, OrderId, cancellationToken);
    }
}
