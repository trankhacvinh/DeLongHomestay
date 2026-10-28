using DeLong.Web.Data;
using DeLong.Web.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Bookings;

public sealed record BookingTimeBlock(DateTime StartUtc, DateTime EndUtc, bool NeedsCleaning);
public sealed record BookingRateTimeSuggestion(Guid RateId, DateTime DefaultStartUtc, DateTime EndUtc, DateTime? SuggestedStartUtc, bool IsAdjusted);
public sealed record BookingTimeSuggestions(string Date, string TimeZoneId, int CleaningMinutes, IReadOnlyList<BookingRateTimeSuggestion> Rates);

public sealed class BookingTimeSuggestionService(AppDbContext db)
{
    public static DateTime? SuggestStart(DateTime start, DateTime end, IEnumerable<BookingTimeBlock> blocks, int cleaningMinutes)
    {
        if (end <= start) return null;
        var candidate = start;
        // Keep the configured end; suggest the first start after all conflicts in that window.
        foreach (var block in blocks.OrderBy(x => x.StartUtc))
        {
            var ready = block.NeedsCleaning ? block.EndUtc.AddMinutes(cleaningMinutes) : block.EndUtc;
            if (block.StartUtc < end && ready > candidate) candidate = ready;
            if (candidate >= end) return null;
        }
        return candidate;
    }

    public async Task<BookingTimeSuggestions?> GetAsync(Guid propertyId, Guid roomId, DateOnly date, CancellationToken ct)
    {
        var room = await db.Rooms.AsNoTracking().Include(x => x.Property).Include(x => x.Rates)
            .SingleOrDefaultAsync(x => x.Id == roomId && x.PropertyId == propertyId && x.IsActive && x.Property.IsActive, ct);
        if (room is null) return null;
        var cleaning = room.Property.HousekeepingCleaningMinutes is > 0 and <= 240 ? room.Property.HousekeepingCleaningMinutes : 30;
        var zone = TimeZoneInfo.FindSystemTimeZoneById(room.Property.TimeZoneId);
        var rates = room.Rates.Where(x => x.IsActive && x.Type != RoomRateType.Nightly)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToList();
        var windows = rates.Select(rate =>
        {
            var overnight = rate.Type == RoomRateType.Overnight || rate.IsOvernight || rate.EndTime <= rate.StartTime;
            var start = TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(rate.StartTime), zone);
            var end = TimeZoneInfo.ConvertTimeToUtc((overnight ? date.AddDays(1) : date).ToDateTime(rate.EndTime), zone);
            return (rate.Id, Start: start, End: end);
        }).ToList();
        if (windows.Count == 0) return new(date.ToString("yyyy-MM-dd"), zone.Id, cleaning, []);
        var from = windows.Min(x => x.Start).AddMinutes(-cleaning);
        var to = windows.Max(x => x.End);
        var bookings = await db.Bookings.AsNoTracking().Where(x => x.PropertyId == propertyId && x.RoomId == roomId &&
                (x.Status == BookingStatus.Held || x.Status == BookingStatus.Confirmed || x.Status == BookingStatus.CheckedIn || x.Status == BookingStatus.Completed) &&
                x.CheckInUtc < to && x.CheckOutUtc > from)
            .Select(x => new BookingTimeBlock(x.CheckInUtc, x.CheckOutUtc, true)).ToListAsync(ct);
        var locked = await db.RoomBookingBlocks.AsNoTracking().Where(x => x.PropertyId == propertyId && x.RoomId == roomId && x.CancelledAtUtc == null && x.StartUtc < to && x.EndUtc > from)
            .Select(x => new BookingTimeBlock(x.StartUtc, x.EndUtc, false)).ToListAsync(ct);
        var all = bookings.Concat(locked).ToList();
        var suggestions = windows.Select(window =>
        {
            var start = room.IsBookingLocked ? null : SuggestStart(window.Start, window.End, all, cleaning);
            return new BookingRateTimeSuggestion(window.Id, window.Start, window.End, start, start.HasValue && start != window.Start);
        }).ToList();
        return new(date.ToString("yyyy-MM-dd"), zone.Id, cleaning, suggestions);
    }
}
