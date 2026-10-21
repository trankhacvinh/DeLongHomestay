using DeLong.Web.Data;
using DeLong.Web.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Reports;

public sealed record CalendarDayBookingDto(Guid Id, string Code, string RoomName, string CustomerName, DateTime CheckInUtc, DateTime CheckOutUtc, string Status, decimal Amount);
public sealed record CalendarDayPaymentDto(Guid Id, Guid BookingId, string BookingCode, string RoomName, string Type, string Method, DateTime OccurredAtUtc, decimal Amount);
public sealed record CalendarDaySummaryDto(DateOnly Date, int BookingCount, decimal BookingValue, decimal NetReceipts);
public sealed record CalendarDayDetailDto(CalendarDaySummaryDto Summary, IReadOnlyList<CalendarDayBookingDto> Bookings, IReadOnlyList<CalendarDayPaymentDto> Payments);

public static class BookingDailyReport
{
    public static async Task<IReadOnlyList<CalendarDayDetailDto>?> GetAsync(AppDbContext db, Guid propertyId, DateOnly from, int days, CancellationToken ct = default)
    {
        if (days is < 1 or > 62) throw new ArgumentOutOfRangeException(nameof(days));
        var zoneId = await db.Properties.AsNoTracking().Where(x => x.Id == propertyId).Select(x => x.TimeZoneId).SingleOrDefaultAsync(ct);
        if (zoneId is null) return null;
        var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        var start = TimeZoneInfo.ConvertTimeToUtc(from.ToDateTime(TimeOnly.MinValue), zone);
        var end = TimeZoneInfo.ConvertTimeToUtc(from.AddDays(days).ToDateTime(TimeOnly.MinValue), zone);
        var bookings = await db.Bookings.AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.CheckInUtc >= start && x.CheckInUtc < end && x.Status != BookingStatus.Cancelled && x.Status != BookingStatus.NoShow)
            .OrderBy(x => x.CheckInUtc).ThenBy(x => x.Id)
            .Select(x => new CalendarDayBookingDto(x.Id, x.Code, x.Room.Name, x.Customer.Name, x.CheckInUtc, x.CheckOutUtc, x.Status.ToString(),
                x.RoomAmount + x.SpecialSurchargeAmount + x.ExtraAmount - x.DiscountAmount)).ToListAsync(ct);
        var payments = await db.Payments.AsNoTracking()
            .Where(x => x.PropertyId == propertyId && !x.IsVoided && x.OccurredAtUtc >= start && x.OccurredAtUtc < end)
            .OrderBy(x => x.OccurredAtUtc).ThenBy(x => x.Id)
            .Select(x => new CalendarDayPaymentDto(x.Id, x.BookingId, x.Booking.Code, x.Booking.Room.Name, x.Type.ToString(), x.Method.ToString(), x.OccurredAtUtc, x.Amount)).ToListAsync(ct);
        DateOnly LocalDate(DateTime utc) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, zone));
        var bookingsByDay = bookings.ToLookup(x => LocalDate(x.CheckInUtc));
        var paymentsByDay = payments.ToLookup(x => LocalDate(x.OccurredAtUtc));
        return Enumerable.Range(0, days).Select(offset =>
        {
            var date = from.AddDays(offset);
            var dayBookings = bookingsByDay[date].ToList();
            var dayPayments = paymentsByDay[date].ToList();
            return new CalendarDayDetailDto(new(date, dayBookings.Count, dayBookings.Sum(x => x.Amount),
                dayPayments.Sum(x => x.Type == nameof(PaymentType.Receipt) ? x.Amount : -x.Amount)), dayBookings, dayPayments);
        }).ToList();
    }
}
