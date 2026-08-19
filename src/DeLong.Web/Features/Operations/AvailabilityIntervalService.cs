using DeLong.Web.Common.Operations;
using DeLong.Web.Data;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.PublicBooking;
using DeLong.Web.Features.Site;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Operations;

public sealed record AvailabilityRangeDto(DateTime StartUtc, DateTime EndUtc);

public sealed record AdminAvailabilityOccupancyDto(
    Guid BookingId,
    BookingStatus Status,
    DateTime StartUtc,
    DateTime EndUtc);

public sealed record AdminAvailabilitySlotDto(
    Guid RateId,
    string RateName,
    RoomRateType RateType,
    decimal Price,
    DateTime StartUtc,
    DateTime EndUtc,
    string State,
    double OccupiedRatio,
    IReadOnlyList<AdminAvailabilityOccupancyDto> Occupied,
    IReadOnlyList<AvailabilityRangeDto> Free);

public sealed record AdminAvailabilityDayDto(DateOnly Date, IReadOnlyList<AdminAvailabilitySlotDto> Slots);

public sealed record AdminRoomAvailabilityDto(
    Guid PropertyId,
    Guid RoomId,
    string RoomCode,
    string RoomName,
    string TimeZoneId,
    DateOnly From,
    int Days,
    IReadOnlyList<AdminAvailabilityDayDto> Calendar);

public sealed record PublicAvailabilityOccupancyDto(
    string Kind,
    DateTime StartUtc,
    DateTime EndUtc);

public sealed record PublicAvailabilitySlotDto(
    Guid RateId,
    string RateName,
    RoomRateType RateType,
    decimal Price,
    DateTime StartUtc,
    DateTime EndUtc,
    string State,
    double OccupiedRatio,
    IReadOnlyList<PublicAvailabilityOccupancyDto> Occupied,
    IReadOnlyList<AvailabilityRangeDto> Free);

public sealed record PublicAvailabilityDayDto(DateOnly Date, IReadOnlyList<PublicAvailabilitySlotDto> Slots);

public sealed record PublicRoomAvailabilityDto(
    Guid RoomId,
    string RoomCode,
    string RoomName,
    string TimeZoneId,
    DateOnly From,
    int Days,
    IReadOnlyList<PublicAvailabilityDayDto> Calendar);

public sealed record AvailabilityOccupancyInput(
    Guid BookingId,
    BookingStatus Status,
    DateTime StartUtc,
    DateTime EndUtc);

public sealed record AvailabilityProjection(
    string State,
    double OccupiedRatio,
    IReadOnlyList<AvailabilityOccupancyInput> Occupied,
    IReadOnlyList<AvailabilityRangeDto> Free);

public static class AvailabilityIntervalProjector
{
    public static AvailabilityProjection Project(
        DateTime slotStartUtc,
        DateTime slotEndUtc,
        IEnumerable<AvailabilityOccupancyInput> source)
    {
        if (slotEndUtc <= slotStartUtc)
            throw new ArgumentException("Slot end must be after slot start.");

        var occupied = source
            .Where(x => x.StartUtc < slotEndUtc && slotStartUtc < x.EndUtc)
            .Select(x => x with
            {
                StartUtc = x.StartUtc < slotStartUtc ? slotStartUtc : x.StartUtc,
                EndUtc = x.EndUtc > slotEndUtc ? slotEndUtc : x.EndUtc
            })
            .Where(x => x.EndUtc > x.StartUtc)
            .OrderBy(x => x.StartUtc)
            .ThenBy(x => x.EndUtc)
            .ToList();

        var free = new List<AvailabilityRangeDto>();
        var cursor = slotStartUtc;
        var occupiedTicks = 0L;
        foreach (var interval in occupied)
        {
            if (interval.StartUtc > cursor)
                free.Add(new AvailabilityRangeDto(cursor, interval.StartUtc));

            var effectiveStart = interval.StartUtc > cursor ? interval.StartUtc : cursor;
            if (interval.EndUtc > effectiveStart)
                occupiedTicks += interval.EndUtc.Ticks - effectiveStart.Ticks;
            if (interval.EndUtc > cursor) cursor = interval.EndUtc;
        }
        if (cursor < slotEndUtc)
            free.Add(new AvailabilityRangeDto(cursor, slotEndUtc));

        var totalTicks = slotEndUtc.Ticks - slotStartUtc.Ticks;
        var ratio = totalTicks <= 0 ? 0d : Math.Clamp((double)occupiedTicks / totalTicks, 0d, 1d);
        var state = ratio <= 0d ? "available" : ratio >= 0.999999d ? "occupied" : "partial";
        return new AvailabilityProjection(state, Math.Round(ratio, 4), occupied, free);
    }
}

public sealed class AvailabilityIntervalService(
    AppDbContext db,
    PublicPropertyResolver propertyResolver,
    StoragePaths storagePaths)
{
    private static readonly BookingStatus[] LockingStatuses =
        [BookingStatus.Held, BookingStatus.Confirmed, BookingStatus.CheckedIn];

    public async Task<AdminRoomAvailabilityDto?> GetAdminAsync(
        Guid propertyId,
        Guid roomId,
        DateOnly from,
        int days,
        CancellationToken cancellationToken = default)
    {
        days = Math.Clamp(days, 1, 31);
        await new PublicBookingHoldStore(storagePaths).ReleaseExpiredAsync(db, propertyId, cancellationToken);

        var property = await db.Properties.AsNoTracking()
            .Where(x => x.Id == propertyId && x.IsActive)
            .Select(x => new { x.Id, x.TimeZoneId })
            .SingleOrDefaultAsync(cancellationToken);
        if (property is null) return null;

        var room = await db.Rooms.AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.Id == roomId && x.IsActive)
            .Select(x => new { x.Id, x.Code, x.Name })
            .SingleOrDefaultAsync(cancellationToken);
        if (room is null) return null;

        var calendar = await BuildAsync(propertyId, roomId, property.TimeZoneId, from, days, cancellationToken);
        return new AdminRoomAvailabilityDto(
            propertyId, room.Id, room.Code, room.Name, property.TimeZoneId, from, days,
            calendar.Select(day => new AdminAvailabilityDayDto(
                day.Date,
                day.Slots.Select(slot => new AdminAvailabilitySlotDto(
                    slot.RateId, slot.RateName, slot.RateType, slot.Price,
                    slot.StartUtc, slot.EndUtc, slot.Projection.State, slot.Projection.OccupiedRatio,
                    slot.Projection.Occupied.Select(x => new AdminAvailabilityOccupancyDto(
                        x.BookingId, x.Status, x.StartUtc, x.EndUtc)).ToList(),
                    slot.Projection.Free)).ToList())).ToList());
    }

    public async Task<PublicRoomAvailabilityDto?> GetPublicAsync(
        string? siteSlug,
        Guid roomId,
        DateOnly from,
        int days,
        CancellationToken cancellationToken = default)
    {
        days = Math.Clamp(days, 1, 14);
        var property = await propertyResolver.ResolveAsync(siteSlug, cancellationToken);
        if (property is null) return null;

        await new PublicBookingHoldStore(storagePaths).ReleaseExpiredAsync(db, property.Id, cancellationToken);
        var room = await db.Rooms.AsNoTracking()
            .Where(x => x.PropertyId == property.Id && x.Id == roomId && x.IsActive && x.IsPublished)
            .Select(x => new { x.Id, x.Code, x.Name })
            .SingleOrDefaultAsync(cancellationToken);
        if (room is null) return null;

        var calendar = await BuildAsync(property.Id, roomId, property.TimeZoneId, from, days, cancellationToken);
        return new PublicRoomAvailabilityDto(
            room.Id, room.Code, room.Name, property.TimeZoneId, from, days,
            calendar.Select(day => new PublicAvailabilityDayDto(
                day.Date,
                day.Slots.Select(slot => new PublicAvailabilitySlotDto(
                    slot.RateId, slot.RateName, slot.RateType, slot.Price,
                    slot.StartUtc, slot.EndUtc, slot.Projection.State, slot.Projection.OccupiedRatio,
                    slot.Projection.Occupied.Select(x => new PublicAvailabilityOccupancyDto(
                        x.Status == BookingStatus.Held ? "held" : "booked",
                        x.StartUtc,
                        x.EndUtc)).ToList(),
                    slot.Projection.Free)).ToList())).ToList());
    }

    private async Task<IReadOnlyList<AvailabilityDay>> BuildAsync(
        Guid propertyId,
        Guid roomId,
        string timeZoneId,
        DateOnly from,
        int days,
        CancellationToken cancellationToken)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var rates = await db.RoomRates.AsNoTracking()
            .Where(x => x.RoomId == roomId && x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.StartTime)
            .ThenBy(x => x.Name)
            .Select(x => new RateRow(x.Id, x.Name, x.Type, x.StartTime, x.EndTime, x.IsOvernight, x.Price))
            .ToListAsync(cancellationToken);

        var windowStartLocal = from.ToDateTime(TimeOnly.MinValue);
        var windowEndLocal = from.AddDays(days + 1).ToDateTime(TimeOnly.MaxValue);
        var windowStartUtc = ToUtc(windowStartLocal, timeZone);
        var windowEndUtc = ToUtc(windowEndLocal, timeZone);
        var bookings = await db.Bookings.AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.RoomId == roomId &&
                        LockingStatuses.Contains(x.Status) &&
                        x.CheckInUtc < windowEndUtc && windowStartUtc < x.CheckOutUtc)
            .Select(x => new AvailabilityOccupancyInput(x.Id, x.Status, x.CheckInUtc, x.CheckOutUtc))
            .ToListAsync(cancellationToken);

        var result = new List<AvailabilityDay>(days);
        for (var offset = 0; offset < days; offset++)
        {
            var date = from.AddDays(offset);
            var slots = new List<AvailabilitySlot>(rates.Count);
            foreach (var rate in rates)
            {
                var startLocal = date.ToDateTime(rate.StartTime);
                var crossesMidnight = rate.Type is RoomRateType.Overnight or RoomRateType.Nightly ||
                                      rate.IsOvernight || rate.EndTime <= rate.StartTime;
                var endDate = crossesMidnight ? date.AddDays(1) : date;
                var endLocal = endDate.ToDateTime(rate.EndTime);
                var startUtc = ToUtc(startLocal, timeZone);
                var endUtc = ToUtc(endLocal, timeZone);
                var projection = AvailabilityIntervalProjector.Project(startUtc, endUtc, bookings);
                slots.Add(new AvailabilitySlot(rate.Id, rate.Name, rate.Type, rate.Price, startUtc, endUtc, projection));
            }
            result.Add(new AvailabilityDay(date, slots));
        }
        return result;
    }

    private static DateTime ToUtc(DateTime local, TimeZoneInfo timeZone) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), timeZone);

    private sealed record RateRow(
        Guid Id,
        string Name,
        RoomRateType Type,
        TimeOnly StartTime,
        TimeOnly EndTime,
        bool IsOvernight,
        decimal Price);

    private sealed record AvailabilitySlot(
        Guid RateId,
        string RateName,
        RoomRateType RateType,
        decimal Price,
        DateTime StartUtc,
        DateTime EndUtc,
        AvailabilityProjection Projection);

    private sealed record AvailabilityDay(DateOnly Date, IReadOnlyList<AvailabilitySlot> Slots);
}
