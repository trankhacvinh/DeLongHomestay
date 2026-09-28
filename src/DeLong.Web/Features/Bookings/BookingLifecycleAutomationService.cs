using DeLong.Web.Data;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Operations;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Bookings;

public sealed record BookingLifecycleAutomationResult(int CheckedIn, int CheckedOut);

public sealed class BookingLifecycleAutomationService(AppDbContext db, BookingService bookingService)
{
    private const int BatchSize = 200;

    public async Task<BookingLifecycleAutomationResult> ProcessDueAsync(
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        utcNow = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        var checkedIn = await ProcessAsync(
            db.Bookings.AsNoTracking()
                .Where(x => x.Property.IsActive && x.Property.AutomaticCheckInEnabled &&
                            x.Status == BookingStatus.Confirmed && x.CheckInUtc <= utcNow)
                .OrderBy(x => x.CheckInUtc)
                .Select(x => new DueBooking(x.PropertyId, x.Id))
                .Take(BatchSize),
            BookingStatus.CheckedIn,
            cancellationToken);

        // Query again after check-in so a server restart can catch up a booking whose
        // complete stay elapsed while the application was offline.
        var checkedOut = await ProcessAsync(
            db.Bookings.AsNoTracking()
                .Where(x => x.Property.IsActive && x.Property.AutomaticCheckOutEnabled &&
                            x.Status == BookingStatus.CheckedIn && x.CheckOutUtc <= utcNow)
                .OrderBy(x => x.CheckOutUtc)
                .Select(x => new DueBooking(x.PropertyId, x.Id))
                .Take(BatchSize),
            BookingStatus.Completed,
            cancellationToken);

        return new(checkedIn, checkedOut);
    }

    private async Task<int> ProcessAsync(
        IQueryable<DueBooking> query,
        BookingStatus targetStatus,
        CancellationToken cancellationToken)
    {
        var due = await query.ToListAsync(cancellationToken);
        var changed = 0;
        foreach (var item in due)
        {
            var (booking, error) = await bookingService.ChangeStatusAsync(
                item.PropertyId, item.BookingId, targetStatus, cancellationToken: cancellationToken);
            if (error is not null || booking is null) continue;

            changed++;
            OperationsRealtimeBroker.Shared.Publish(OperationsRealtimeEvent.Create(
                item.PropertyId,
                OperationsEventTypes.BookingStatusChanged,
                booking.Id,
                booking.RoomId));
        }

        return changed;
    }

    private sealed record DueBooking(Guid PropertyId, Guid BookingId);
}
