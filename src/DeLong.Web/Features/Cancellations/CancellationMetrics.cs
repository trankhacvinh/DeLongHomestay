using DeLong.Web.Data;
using DeLong.Web.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Cancellations;

public sealed record CancellationMetricsDto(decimal RefundsOwed, int OwedBookingCount, decimal KeptInPeriod);

/// <summary>
/// Chỉ số hoàn tiền dùng cho Thu chi và Báo cáo. Doanh thu/dòng tiền vẫn tính theo ngày phát sinh giao dịch
/// (thu trừ hoàn); hai chỉ số này chỉ làm rõ phần tiền còn nợ khách và phần tiền giữ lại từ booking hủy.
/// </summary>
public static class CancellationMetrics
{
    public static async Task<CancellationMetricsDto> GetAsync(AppDbContext db, Guid propertyId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
    {
        var rows = await db.BookingCancellations.AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.RestoredAtUtc == null && x.Booking.Status == BookingStatus.Cancelled && x.PaidAmountSnapshot > 0)
            .Select(x => new
            {
                x.CancelledAtUtc,
                x.PaidAmountSnapshot,
                x.RefundDueAmount,
                Refunded = db.Payments.Where(p => p.BookingId == x.BookingId && p.Type == PaymentType.Refund && !p.IsVoided).Sum(p => (decimal?)p.Amount) ?? 0
            })
            .ToListAsync(cancellationToken);

        var owed = rows.Select(x => Math.Max(0, x.RefundDueAmount - x.Refunded)).ToList();
        var kept = rows.Where(x => x.CancelledAtUtc >= fromUtc && x.CancelledAtUtc < toUtc)
            .Sum(x => Math.Max(0, x.PaidAmountSnapshot - Math.Max(x.RefundDueAmount, x.Refunded)));
        return new CancellationMetricsDto(owed.Sum(), owed.Count(x => x > 0), kept);
    }
}
