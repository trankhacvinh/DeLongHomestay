using DeLong.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Bookings;

internal static class BookingRateSegmentCleanup
{
    // Segments are a snapshot of the slots the guest booked (times + price per slot). Once staff move
    // the booking to other times or another room they describe a booking that no longer exists, and
    // reports, vouchers and the AI read them as truth. Drop them; the booking keeps its own totals.
    public static async Task RemoveAsync(AppDbContext db, Guid bookingId, CancellationToken cancellationToken)
    {
        var segments = await db.BookingRateSegments.Where(x => x.BookingId == bookingId).ToListAsync(cancellationToken);
        if (segments.Count > 0) db.BookingRateSegments.RemoveRange(segments);
    }
}
