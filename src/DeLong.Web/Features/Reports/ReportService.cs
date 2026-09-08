using DeLong.Web.Data;
using DeLong.Web.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Reports;

public sealed record ReportRoomDto(string RoomName, int BookingCount, decimal BookingValue, double BookedHours);
public sealed record ReportSourceDto(string Source, int BookingCount, decimal BookingValue);
public sealed record ReportTrendDto(string Month, decimal NetReceipts, decimal Expenses, decimal NetCashFlow);
public sealed record ReportDailyDto(string Date, int BookingCount, decimal BookingValue, decimal NetReceipts, decimal Expenses, decimal NetCashFlow);
public sealed record ReportWeeklyDto(string WeekStart, string WeekEnd, decimal BookingValue, decimal NetReceipts, decimal Expenses);
public sealed record ReportStatusDto(string Status, int BookingCount, decimal BookingValue);
public sealed record ReportPaymentMethodDto(string Method, int TransactionCount, decimal GrossReceipts);
public sealed record ReportExpenseCategoryDto(string Category, int TransactionCount, decimal Amount);
public sealed record ReportWeekdayDto(int DayOfWeek, string Label, int BookingCount, decimal BookingValue, decimal NetReceipts, double BookedHours);
public sealed record ReportOutstandingBucketDto(string Key, string Label, int BookingCount, decimal Amount);
public sealed record ReportPeriodReceiptsDto(decimal Today, decimal ThisWeek, decimal SelectedMonth, decimal PreviousMonth, double? MonthChangePercent);

public sealed record ReportSnapshotDto(
    int BookingCount,
    int CancelledBookingCount,
    decimal BookingValue,
    decimal AverageBookingValue,
    double BookedHours,
    int ActiveRoomCount,
    double OccupancyRate,
    double CancellationRate,
    decimal GrossReceipts,
    decimal Refunds,
    decimal NetReceipts,
    decimal Expenses,
    decimal NetCashFlow,
    decimal Outstanding,
    ReportPeriodReceiptsDto PeriodReceipts,
    IReadOnlyList<ReportRoomDto> ByRoom,
    IReadOnlyList<ReportSourceDto> BySource,
    IReadOnlyList<ReportStatusDto> ByStatus,
    IReadOnlyList<ReportPaymentMethodDto> ByPaymentMethod,
    IReadOnlyList<ReportExpenseCategoryDto> ByExpenseCategory,
    IReadOnlyList<ReportWeekdayDto> ByWeekday,
    IReadOnlyList<ReportOutstandingBucketDto> OutstandingBuckets,
    IReadOnlyList<ReportDailyDto> Daily,
    IReadOnlyList<ReportWeeklyDto> Weekly,
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
        var allBookingRows = await db.Bookings
            .AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.CheckInUtc >= fromUtc && x.CheckInUtc < toUtc)
            .Select(x => new BookingRow(
                x.RoomId,
                x.Room.Name,
                x.Source,
                x.Status,
                x.CheckInUtc,
                x.CheckOutUtc,
                x.RoomAmount + x.SpecialSurchargeAmount + x.ExtraAmount - x.DiscountAmount))
            .ToListAsync(cancellationToken);

        var bookingRows = allBookingRows
            .Where(x => x.Status is not BookingStatus.Cancelled and not BookingStatus.NoShow)
            .ToList();

        var paymentRows = await db.Payments
            .AsNoTracking()
            .Where(x => x.PropertyId == propertyId && !x.IsVoided && x.OccurredAtUtc >= fromUtc && x.OccurredAtUtc < toUtc)
            .Select(x => new PaymentRow(x.OccurredAtUtc, x.Type, x.Method, x.Amount))
            .ToListAsync(cancellationToken);

        var expenseRows = await db.Expenses
            .AsNoTracking()
            .Where(x => x.PropertyId == propertyId && !x.IsVoided && x.OccurredAtUtc >= fromUtc && x.OccurredAtUtc < toUtc)
            .Select(x => new ExpenseRow(x.OccurredAtUtc, x.Category, x.Amount))
            .ToListAsync(cancellationToken);

        var grossReceipts = paymentRows.Where(x => x.Type == PaymentType.Receipt).Sum(x => x.Amount);
        var refunds = paymentRows.Where(x => x.Type == PaymentType.Refund).Sum(x => x.Amount);
        var netReceipts = grossReceipts - refunds;
        var expenseTotal = expenseRows.Sum(x => x.Amount);
        var bookingValue = bookingRows.Sum(x => x.Value);
        var bookedHours = Math.Round(bookingRows.Sum(x => (x.CheckOutUtc - x.CheckInUtc).TotalHours), 1);

        var outstandingProjection = await db.Bookings
            .AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.Status != BookingStatus.Cancelled && x.Status != BookingStatus.NoShow)
            .Select(x => new
            {
                x.CheckInUtc,
                Balance = x.RoomAmount + x.SpecialSurchargeAmount + x.ExtraAmount - x.DiscountAmount -
                          x.Payments.Where(p => !p.IsVoided)
                              .Sum(p => p.Type == PaymentType.Receipt ? p.Amount : -p.Amount)
            })
            .ToListAsync(cancellationToken);
        var outstandingRows = outstandingProjection
            .Where(x => x.Balance > 0)
            .Select(x => new OutstandingRow(x.CheckInUtc, x.Balance))
            .ToList();
        var outstanding = outstandingRows.Sum(x => x.Balance);

        var activeRoomCount = await db.Rooms.AsNoTracking()
            .CountAsync(x => x.PropertyId == propertyId && x.IsActive, cancellationToken);
        var periodHours = Math.Max(1, (toUtc - fromUtc).TotalHours * activeRoomCount);
        var occupancyRate = activeRoomCount == 0 ? 0 : Math.Round(Math.Clamp(bookedHours / periodHours * 100, 0, 100), 1);

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

        var byStatus = allBookingRows
            .GroupBy(x => x.Status)
            .Select(group => new ReportStatusDto(group.Key.ToString(), group.Count(), group.Sum(x => x.Value)))
            .OrderByDescending(x => x.BookingCount)
            .ToList();

        var byPaymentMethod = paymentRows
            .Where(x => x.Type == PaymentType.Receipt)
            .GroupBy(x => x.Method)
            .Select(group => new ReportPaymentMethodDto(group.Key.ToString(), group.Count(), group.Sum(x => x.Amount)))
            .OrderByDescending(x => x.GrossReceipts)
            .ToList();

        var byExpenseCategory = expenseRows
            .GroupBy(x => string.IsNullOrWhiteSpace(x.Category) ? "Chưa phân loại" : x.Category)
            .Select(group => new ReportExpenseCategoryDto(group.Key, group.Count(), group.Sum(x => x.Amount)))
            .OrderByDescending(x => x.Amount)
            .ToList();

        var firstLocalDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(fromUtc, timeZone));
        var lastLocalDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(toUtc.AddTicks(-1), timeZone));
        var localDates = Enumerable.Range(0, lastLocalDay.DayNumber - firstLocalDay.DayNumber + 1)
            .Select(firstLocalDay.AddDays)
            .ToList();

        var daily = localDates.Select(date =>
        {
            var dayBookings = bookingRows.Where(x => LocalDate(x.CheckInUtc, timeZone) == date).ToList();
            var dayPayments = paymentRows.Where(x => LocalDate(x.OccurredAtUtc, timeZone) == date).ToList();
            var dayExpenses = expenseRows.Where(x => LocalDate(x.OccurredAtUtc, timeZone) == date).Sum(x => x.Amount);
            var receipts = dayPayments.Sum(SignedAmount);
            return new ReportDailyDto(
                date.ToString("yyyy-MM-dd"),
                dayBookings.Count,
                dayBookings.Sum(x => x.Value),
                receipts,
                dayExpenses,
                receipts - dayExpenses);
        }).ToList();

        var weekly = daily
            .GroupBy(x => StartOfWeek(DateOnly.ParseExact(x.Date, "yyyy-MM-dd")))
            .OrderBy(x => x.Key)
            .Select(group => new ReportWeeklyDto(
                group.Key.ToString("yyyy-MM-dd"),
                group.Key.AddDays(6).ToString("yyyy-MM-dd"),
                group.Sum(x => x.BookingValue),
                group.Sum(x => x.NetReceipts),
                group.Sum(x => x.Expenses)))
            .ToList();

        var weekdayLabels = new[] { "Thứ Hai", "Thứ Ba", "Thứ Tư", "Thứ Năm", "Thứ Sáu", "Thứ Bảy", "Chủ nhật" };
        var byWeekday = Enumerable.Range(0, 7)
            .Select(index =>
            {
                var dayOfWeek = (DayOfWeek)((index + 1) % 7);
                var dayBookings = bookingRows.Where(x => LocalDate(x.CheckInUtc, timeZone).DayOfWeek == dayOfWeek).ToList();
                var dayReceipts = paymentRows
                    .Where(x => LocalDate(x.OccurredAtUtc, timeZone).DayOfWeek == dayOfWeek)
                    .Sum(SignedAmount);
                return new ReportWeekdayDto(
                    index,
                    weekdayLabels[index],
                    dayBookings.Count,
                    dayBookings.Sum(x => x.Value),
                    dayReceipts,
                    Math.Round(dayBookings.Sum(x => (x.CheckOutUtc - x.CheckInUtc).TotalHours), 1));
            })
            .ToList();

        var selectedMonthLocal = new DateTime(firstLocalDay.Year, firstLocalDay.Month, 1);
        var trendStartLocal = selectedMonthLocal.AddMonths(-11);
        var trendStartUtc = ToUtc(trendStartLocal, timeZone);

        var trendPayments = await db.Payments
            .AsNoTracking()
            .Where(x => x.PropertyId == propertyId && !x.IsVoided && x.OccurredAtUtc >= trendStartUtc && x.OccurredAtUtc < toUtc)
            .Select(x => new PaymentRow(x.OccurredAtUtc, x.Type, x.Method, x.Amount))
            .ToListAsync(cancellationToken);

        var trendExpenses = await db.Expenses
            .AsNoTracking()
            .Where(x => x.PropertyId == propertyId && !x.IsVoided && x.OccurredAtUtc >= trendStartUtc && x.OccurredAtUtc < toUtc)
            .Select(x => new ExpenseRow(x.OccurredAtUtc, x.Category, x.Amount))
            .ToListAsync(cancellationToken);

        var trend = Enumerable.Range(0, 12)
            .Select(index => trendStartLocal.AddMonths(index))
            .Select(month =>
            {
                var next = month.AddMonths(1);
                var receipts = trendPayments.Where(x => InLocalRange(x.OccurredAtUtc, month, next, timeZone)).Sum(SignedAmount);
                var expenses = trendExpenses.Where(x => InLocalRange(x.OccurredAtUtc, month, next, timeZone)).Sum(x => x.Amount);
                return new ReportTrendDto(month.ToString("yyyy-MM"), receipts, expenses, receipts - expenses);
            })
            .ToList();

        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
        var todayLocal = nowLocal.Date;
        var tomorrowLocal = todayLocal.AddDays(1);
        var weekStartLocal = todayLocal.AddDays(-WeekOffset(nowLocal.DayOfWeek));
        var weekEndLocal = weekStartLocal.AddDays(7);
        var liveFromUtc = ToUtc(weekStartLocal < todayLocal ? weekStartLocal : todayLocal, timeZone);
        var liveToUtc = ToUtc(tomorrowLocal > weekEndLocal ? tomorrowLocal : weekEndLocal, timeZone);
        var livePayments = await db.Payments
            .AsNoTracking()
            .Where(x => x.PropertyId == propertyId && !x.IsVoided && x.OccurredAtUtc >= liveFromUtc && x.OccurredAtUtc < liveToUtc)
            .Select(x => new PaymentRow(x.OccurredAtUtc, x.Type, x.Method, x.Amount))
            .ToListAsync(cancellationToken);
        var todayReceipts = livePayments.Where(x => InLocalRange(x.OccurredAtUtc, todayLocal, tomorrowLocal, timeZone)).Sum(SignedAmount);
        var weekReceipts = livePayments.Where(x => InLocalRange(x.OccurredAtUtc, weekStartLocal, weekEndLocal, timeZone)).Sum(SignedAmount);
        var previousMonthReceipts = trend.Count >= 2 ? trend[^2].NetReceipts : 0;
        double? monthChangePercent = previousMonthReceipts == 0
            ? (netReceipts == 0 ? 0d : null)
            : Math.Round((double)((netReceipts - previousMonthReceipts) / Math.Abs(previousMonthReceipts) * 100), 1);

        var cancelledBookingCount = allBookingRows.Count - bookingRows.Count;
        var cancellationRate = allBookingRows.Count == 0
            ? 0
            : Math.Round((double)cancelledBookingCount / allBookingRows.Count * 100, 1);

        var today = DateOnly.FromDateTime(nowLocal);
        var outstandingBuckets = new[]
        {
            BuildOutstandingBucket("upcoming", "Chưa đến ngày ở", outstandingRows.Where(x => LocalDate(x.CheckInUtc, timeZone) > today)),
            BuildOutstandingBucket("current", "Đến hạn / trong 7 ngày", outstandingRows.Where(x => DaysSinceCheckIn(x.CheckInUtc, today, timeZone) is >= 0 and <= 7)),
            BuildOutstandingBucket("overdue30", "Quá 8–30 ngày", outstandingRows.Where(x => DaysSinceCheckIn(x.CheckInUtc, today, timeZone) is >= 8 and <= 30)),
            BuildOutstandingBucket("overdue31", "Quá trên 30 ngày", outstandingRows.Where(x => DaysSinceCheckIn(x.CheckInUtc, today, timeZone) > 30))
        };

        return new ReportSnapshotDto(
            bookingRows.Count,
            cancelledBookingCount,
            bookingValue,
            bookingRows.Count == 0 ? 0 : Math.Round(bookingValue / bookingRows.Count, 0),
            bookedHours,
            activeRoomCount,
            occupancyRate,
            cancellationRate,
            grossReceipts,
            refunds,
            netReceipts,
            expenseTotal,
            netReceipts - expenseTotal,
            outstanding,
            new ReportPeriodReceiptsDto(todayReceipts, weekReceipts, netReceipts, previousMonthReceipts, monthChangePercent),
            byRoom,
            bySource,
            byStatus,
            byPaymentMethod,
            byExpenseCategory,
            byWeekday,
            outstandingBuckets,
            daily,
            weekly,
            trend);
    }

    private static decimal SignedAmount(PaymentRow row) => row.Type == PaymentType.Receipt ? row.Amount : -row.Amount;
    private static DateOnly LocalDate(DateTime utc, TimeZoneInfo timeZone) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, timeZone));
    private static DateTime ToUtc(DateTime local, TimeZoneInfo timeZone) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), timeZone);
    private static bool InLocalRange(DateTime utc, DateTime from, DateTime to, TimeZoneInfo timeZone)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, timeZone);
        return local >= from && local < to;
    }
    private static int WeekOffset(DayOfWeek day) => ((int)day + 6) % 7;
    private static DateOnly StartOfWeek(DateOnly date) => date.AddDays(-WeekOffset(date.DayOfWeek));
    private static int DaysSinceCheckIn(DateTime checkInUtc, DateOnly today, TimeZoneInfo timeZone) =>
        today.DayNumber - LocalDate(checkInUtc, timeZone).DayNumber;
    private static ReportOutstandingBucketDto BuildOutstandingBucket(string key, string label, IEnumerable<OutstandingRow> rows)
    {
        var materialized = rows.ToList();
        return new ReportOutstandingBucketDto(key, label, materialized.Count, materialized.Sum(x => x.Balance));
    }

    private sealed record BookingRow(Guid RoomId, string RoomName, string? Source, BookingStatus Status, DateTime CheckInUtc, DateTime CheckOutUtc, decimal Value);
    private sealed record PaymentRow(DateTime OccurredAtUtc, PaymentType Type, PaymentMethod Method, decimal Amount);
    private sealed record ExpenseRow(DateTime OccurredAtUtc, string Category, decimal Amount);
    private sealed record OutstandingRow(DateTime CheckInUtc, decimal Balance);
}
