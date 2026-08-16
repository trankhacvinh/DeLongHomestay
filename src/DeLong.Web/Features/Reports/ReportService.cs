using DeLong.Web.Data;
using DeLong.Web.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Reports;

public sealed record ReportRoomDto(string RoomName, int BookingCount, decimal BookingValue, double BookedHours);
public sealed record ReportSourceDto(string Source, int BookingCount, decimal BookingValue);
public sealed record ReportTrendDto(string Month, decimal NetReceipts, decimal Expenses, decimal NetCashFlow);

public sealed record ReportSnapshotDto(
    int BookingCount,
    decimal BookingValue,
    double BookedHours,
    decimal NetReceipts,
    decimal Expenses,
    decimal NetCashFlow,
    decimal Outstanding,
    IReadOnlyList<ReportRoomDto> ByRoom,
    IReadOnlyList<ReportSourceDto> BySource,
    IReadOnlyList<ReportTrendDto> Trend);

public sealed class ReportService(AppDbContext db)
{
    public async Task<ReportSnapshotDto> GetAsync(
        Guid propertyId,
        DateTime fromUtc,
        DateTime toUtc,
        TimeZoneInfo timeZone,
        CancellationToken cancellationToken = default)
    {
        var bookingRows = await db.Bookings
            .AsNoTracking()
            .Where(x => x.PropertyId == propertyId &&
                        x.CheckInUtc >= fromUtc && x.CheckInUtc < toUtc &&
                        x.Status != BookingStatus.Cancelled && x.Status != BookingStatus.NoShow)
            .Select(x => new
            {
                x.RoomId,
                RoomName = x.Room.Name,
                x.Source,
                x.CheckInUtc,
                x.CheckOutUtc,
                Value = x.RoomAmount + x.ExtraAmount - x.DiscountAmount
            })
            .ToListAsync(cancellationToken);

        var netReceipts = await db.Payments
            .AsNoTracking()
            .Where(x => x.PropertyId == propertyId && !x.IsVoided &&
                        x.OccurredAtUtc >= fromUtc && x.OccurredAtUtc < toUtc)
            .SumAsync(x => x.Type == PaymentType.Receipt ? x.Amount : -x.Amount, cancellationToken);

        var expenseTotal = await db.Expenses
            .AsNoTracking()
            .Where(x => x.PropertyId == propertyId && !x.IsVoided &&
                        x.OccurredAtUtc >= fromUtc && x.OccurredAtUtc < toUtc)
            .SumAsync(x => x.Amount, cancellationToken);

        var outstanding = await db.Bookings
            .AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.Status != BookingStatus.Cancelled && x.Status != BookingStatus.NoShow)
            .Select(x => x.RoomAmount + x.ExtraAmount - x.DiscountAmount -
                         x.Payments.Where(p => !p.IsVoided).Sum(p => p.Type == PaymentType.Receipt ? p.Amount : -p.Amount))
            .Where(balance => balance > 0)
            .SumAsync(cancellationToken);

        var byRoom = bookingRows
            .GroupBy(x => new { x.RoomId, x.RoomName })
            .Select(group => new ReportRoomDto(
                group.Key.RoomName,
                group.Count(),
                group.Sum(x => x.Value),
                Math.Round(group.Sum(x => (x.CheckOutUtc - x.CheckInUtc).TotalHours), 1)))
            .OrderByDescending(x => x.BookingValue)
            .ToList();

        var bySource = bookingRows
            .GroupBy(x => string.IsNullOrWhiteSpace(x.Source) ? "Không xác định" : x.Source!)
            .Select(group => new ReportSourceDto(group.Key, group.Count(), group.Sum(x => x.Value)))
            .OrderByDescending(x => x.BookingValue)
            .ToList();

        var trendStartLocal = TimeZoneInfo.ConvertTimeFromUtc(fromUtc, timeZone).Date.AddMonths(-5);
        trendStartLocal = new DateTime(trendStartLocal.Year, trendStartLocal.Month, 1);
        var trendStartUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(trendStartLocal, DateTimeKind.Unspecified), timeZone);

        var trendPayments = await db.Payments
            .AsNoTracking()
            .Where(x => x.PropertyId == propertyId && !x.IsVoided && x.OccurredAtUtc >= trendStartUtc && x.OccurredAtUtc < toUtc)
            .Select(x => new { x.OccurredAtUtc, x.Type, x.Amount })
            .ToListAsync(cancellationToken);

        var trendExpenses = await db.Expenses
            .AsNoTracking()
            .Where(x => x.PropertyId == propertyId && !x.IsVoided && x.OccurredAtUtc >= trendStartUtc && x.OccurredAtUtc < toUtc)
            .Select(x => new { x.OccurredAtUtc, x.Amount })
            .ToListAsync(cancellationToken);

        var trend = Enumerable.Range(0, 6)
            .Select(index => trendStartLocal.AddMonths(index))
            .Select(month =>
            {
                var next = month.AddMonths(1);
                var receipts = trendPayments
                    .Where(x =>
                    {
                        var local = TimeZoneInfo.ConvertTimeFromUtc(x.OccurredAtUtc, timeZone);
                        return local >= month && local < next;
                    })
                    .Sum(x => x.Type == PaymentType.Receipt ? x.Amount : -x.Amount);
                var expenses = trendExpenses
                    .Where(x =>
                    {
                        var local = TimeZoneInfo.ConvertTimeFromUtc(x.OccurredAtUtc, timeZone);
                        return local >= month && local < next;
                    })
                    .Sum(x => x.Amount);
                return new ReportTrendDto(month.ToString("yyyy-MM"), receipts, expenses, receipts - expenses);
            })
            .ToList();

        return new ReportSnapshotDto(
            bookingRows.Count,
            bookingRows.Sum(x => x.Value),
            Math.Round(bookingRows.Sum(x => (x.CheckOutUtc - x.CheckInUtc).TotalHours), 1),
            netReceipts,
            expenseTotal,
            netReceipts - expenseTotal,
            outstanding,
            byRoom,
            bySource,
            trend);
    }
}
