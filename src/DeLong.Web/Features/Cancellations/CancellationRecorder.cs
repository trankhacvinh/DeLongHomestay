using System.Globalization;
using DeLong.Web.Common.Auditing;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Bookings;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Cancellations;

public sealed record PreparedCancellation(BookingCancellation Record, Payment? Refund, string RefundSummary);

/// <summary>
/// Dựng hồ sơ hủy (và giao dịch hoàn tiền nếu hoàn ngay) trong cùng DbContext với việc đổi trạng thái booking,
/// để hồ sơ, hoàn tiền và trạng thái được lưu trong một lần SaveChanges.
/// </summary>
public static class CancellationRecorder
{
    public const int MaxReasonLength = 1000;

    public static async Task<(PreparedCancellation? Result, BookingOperationError? Error)> PrepareAsync(
        AppDbContext db,
        AuditService audit,
        Booking booking,
        Guid? actorUserId,
        CancelDetails? details,
        CancellationToken cancellationToken)
    {
        var reason = details?.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason)) return (null, new("reason_required", "Vui lòng nhập lý do hủy."));
        if (reason.Length > MaxReasonLength) return (null, new("reason_too_long", $"Lý do hủy tối đa {MaxReasonLength} ký tự."));

        var netPaid = await db.Payments
            .Where(x => x.PropertyId == booking.PropertyId && x.BookingId == booking.Id && !x.IsVoided)
            .SumAsync(x => x.Type == PaymentType.Receipt ? x.Amount : -x.Amount, cancellationToken);
        netPaid = Math.Max(0, netPaid);

        var now = DateTime.UtcNow;
        var record = new BookingCancellation
        {
            PropertyId = booking.PropertyId,
            BookingId = booking.Id,
            CancelledAtUtc = now,
            CancelledByUserId = actorUserId,
            Reason = reason,
            TotalAmountSnapshot = booking.TotalAmount,
            PaidAmountSnapshot = netPaid
        };

        if (netPaid <= 0)
        {
            return (new PreparedCancellation(record, null, "Khách chưa thanh toán, không cần hoàn tiền"), null);
        }

        var option = details?.Refund;
        var mode = option?.Mode?.Trim().ToLowerInvariant();
        record.RefundNote = Clean(option?.Note);
        switch (mode)
        {
            case CancelRefundModes.Keep:
                record.RefundDueAmount = 0;
                return (new PreparedCancellation(record, null, $"Không hoàn, giữ lại {Money(netPaid)}"), null);

            case CancelRefundModes.Now or CancelRefundModes.Later:
                var amount = option?.Amount ?? netPaid;
                if (amount <= 0) return (null, new("refund_amount_invalid", "Số tiền hoàn phải lớn hơn 0."));
                if (amount > netPaid) return (null, new("refund_exceeds_paid", $"Số tiền hoàn không được lớn hơn số khách đã thanh toán ({Money(netPaid)})."));
                record.RefundDueAmount = amount;
                if (mode == CancelRefundModes.Later)
                    return (new PreparedCancellation(record, null, $"Chờ hoàn {Money(amount)}"), null);

                var method = option?.Method ?? PaymentMethod.BankTransfer;
                var refund = new Payment
                {
                    PropertyId = booking.PropertyId,
                    BookingId = booking.Id,
                    Type = PaymentType.Refund,
                    Method = method,
                    Amount = amount,
                    OccurredAtUtc = option?.OccurredAt?.UtcDateTime ?? now,
                    Note = Clean(option?.Note) ?? "Hoàn tiền do hủy booking",
                    RecordedByUserId = actorUserId
                };
                audit.Add(booking.PropertyId, "Booking", booking.Id, "PaymentRefunded", actorUserId,
                    after: new { PaymentId = refund.Id, PaymentType = refund.Type.ToString(), PaymentMethod = refund.Method.ToString(), PaymentAmount = refund.Amount, refund.OccurredAtUtc });
                return (new PreparedCancellation(record, refund, $"Đã hoàn {Money(amount)} ({MethodText(method)})"), null);

            default:
                return (null, new("refund_choice_required", $"Khách đã thanh toán {Money(netPaid)}. Hãy chọn hoàn tiền ngay, hoàn sau hoặc không hoàn."));
        }
    }

    /// <summary>Đánh dấu các hồ sơ hủy đang hiệu lực của booking là đã khôi phục (không còn nằm trong danh sách hủy).</summary>
    public static async Task MarkRestoredAsync(AppDbContext db, Guid bookingId, CancellationToken cancellationToken)
    {
        var active = await db.BookingCancellations
            .Where(x => x.BookingId == bookingId && x.RestoredAtUtc == null)
            .ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var item in active) item.RestoredAtUtc = now;
    }

    public static string Money(decimal value) => value.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";

    public static string MethodText(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "Tiền mặt",
        PaymentMethod.BankTransfer => "Chuyển khoản",
        PaymentMethod.Card => "Thẻ",
        PaymentMethod.Pay2S => "Pay2S",
        PaymentMethod.SePay => "SePay",
        _ => "Khác"
    };

    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        return trimmed.Length <= MaxReasonLength ? trimmed : trimmed[..MaxReasonLength];
    }
}
