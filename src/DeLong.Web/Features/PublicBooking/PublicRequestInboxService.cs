using DeLong.Web.Data;
using DeLong.Web.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.PublicBooking;

public sealed record PublicRequestInboxItem(
    Guid BookingId,
    string Code,
    string CustomerName,
    string CustomerPhone,
    string RoomName,
    DateTime CheckInUtc,
    decimal TotalAmount,
    DateTime CreatedAtUtc);

public sealed class PublicRequestInboxService(AppDbContext db)
{
    public Task<int> CountAsync(Guid propertyId, CancellationToken cancellationToken = default) =>
        db.Bookings.CountAsync(x => x.PropertyId == propertyId && x.Status == BookingStatus.Requested, cancellationToken);

    public Task<List<PublicRequestInboxItem>> GetRecentAsync(Guid propertyId, int take = 5, CancellationToken cancellationToken = default) =>
        db.Bookings
            .AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.Status == BookingStatus.Requested)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(take)
            .Select(x => new PublicRequestInboxItem(
                x.Id,
                x.Code,
                x.Customer.Name,
                x.Customer.Phone,
                x.Room.Name,
                x.CheckInUtc,
                x.RoomAmount + x.ExtraAmount - x.DiscountAmount,
                x.CreatedAtUtc))
            .ToListAsync(cancellationToken);
}
