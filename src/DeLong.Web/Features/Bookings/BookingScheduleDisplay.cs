using DeLong.Web.Data;
using DeLong.Web.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Bookings;

public sealed record BookingScheduleDisplay(bool IsFlexibleTime, bool IsMixedSlot)
{
    public static BookingScheduleDisplay Classify(
        BookingType type, DateTime checkInUtc, DateTime checkOutUtc,
        IReadOnlyList<(DateTime Start, DateTime End)> presets,
        IReadOnlyList<(DateTime Start, DateTime End)> segments)
    {
        if (type != BookingType.TimeSlot) return new(false, false);
        var snapshots = segments.OrderBy(x => x.Start).ToList();
        if (snapshots.Count > 0 && snapshots[0].Start == checkInUtc && snapshots[^1].End == checkOutUtc)
            return new(false, snapshots.Count > 1);
        var ordered = presets.Distinct().OrderBy(x => x.Start).ThenBy(x => x.End).ToList();
        if (ordered.Any(x => x.Start == checkInUtc && x.End == checkOutUtc)) return new(false, false);
        // A full sequence may include the normal cleaning gaps between presets. A partial
        // second preset is a flexible stay, rather than a second complete booked preset.
        foreach (var first in ordered.Where(x => x.Start == checkInUtc))
        {
            var reachableEnds = new HashSet<DateTime> { first.End };
            foreach (var next in ordered.Where(x => x.Start >= first.End && x.End <= checkOutUtc))
            {
                if (!reachableEnds.Any(end => end <= next.Start)) continue;
                if (next.End == checkOutUtc) return new(false, true);
                reachableEnds.Add(next.End);
            }
        }
        return new(true, false);
    }

    public static async Task<IReadOnlyDictionary<Guid, BookingScheduleDisplay>> LoadAsync(
        AppDbContext db, Guid propertyId, IReadOnlyCollection<Guid> bookingIds, CancellationToken cancellationToken)
    {
        if (bookingIds.Count == 0) return new Dictionary<Guid, BookingScheduleDisplay>();
        var bookings = await db.Bookings.AsNoTracking()
            .Where(x => x.PropertyId == propertyId && bookingIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id, x.RoomId, x.RoomRateId, x.Type, x.CheckInUtc, x.CheckOutUtc, x.Room.Property.TimeZoneId,
                Segments = x.RateSegments.Select(s => new { s.CheckInUtc, s.CheckOutUtc }).ToList()
            }).ToListAsync(cancellationToken);
        var roomIds = bookings.Select(x => x.RoomId).Distinct().ToArray();
        var referencedRates = bookings.Where(x => x.RoomRateId.HasValue).Select(x => x.RoomRateId!.Value).Distinct().ToArray();
        var rates = await db.RoomRates.AsNoTracking()
            .Where(x => x.Room.PropertyId == propertyId && roomIds.Contains(x.RoomId) &&
                        x.Type != RoomRateType.Nightly && (x.IsActive || referencedRates.Contains(x.Id)))
            .Select(x => new { x.RoomId, x.StartTime, x.EndTime, x.Type, x.IsOvernight })
            .ToListAsync(cancellationToken);
        var result = new Dictionary<Guid, BookingScheduleDisplay>();
        foreach (var booking in bookings)
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(booking.TimeZoneId);
            var firstDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(booking.CheckInUtc, zone));
            var presets = new List<(DateTime Start, DateTime End)>();
            // A display classification must not expand an arbitrarily long historical stay.
            if (booking.CheckOutUtc - booking.CheckInUtc <= TimeSpan.FromDays(31))
            {
                var lastDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(booking.CheckOutUtc, zone));
                for (var date = firstDate; date <= lastDate; date = date.AddDays(1))
                foreach (var rate in rates.Where(x => x.RoomId == booking.RoomId))
                {
                    var endDate = rate.Type == RoomRateType.Overnight || rate.IsOvernight || rate.EndTime <= rate.StartTime ? date.AddDays(1) : date;
                    var start = TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(rate.StartTime), zone);
                    var end = TimeZoneInfo.ConvertTimeToUtc(endDate.ToDateTime(rate.EndTime), zone);
                    presets.Add((start, end));
                }
            }
            result[booking.Id] = Classify(booking.Type, booking.CheckInUtc, booking.CheckOutUtc, presets,
                booking.Segments.Select(x => (x.CheckInUtc, x.CheckOutUtc)).ToList());
        }
        return result;
    }
}
