using DeLong.Web.Data;
using DeLong.Web.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Payments;

public static class Pay2SIntentLifecycleManager
{
    public static async Task<int> CloseOpenIntentsAsync(
        AppDbContext db,
        Guid propertyId,
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        var intents = await db.Pay2SPaymentIntents
            .Where(x => x.PropertyId == propertyId &&
                        x.BookingId == bookingId &&
                        (x.Status == Pay2SPaymentIntentStatus.Pending ||
                         x.Status == Pay2SPaymentIntentStatus.Failed))
            .ToListAsync(cancellationToken);

        foreach (var intent in intents)
            intent.Status = Pay2SPaymentIntentStatus.Expired;

        return intents.Count;
    }
}
