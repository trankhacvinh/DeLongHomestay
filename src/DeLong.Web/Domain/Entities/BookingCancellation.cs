namespace DeLong.Web.Domain.Entities;

/// <summary>
/// Hồ sơ một lần hủy booking. Số tiền đã hoàn không lưu ở đây mà luôn lấy từ các giao dịch
/// <see cref="Payment"/> loại Refund của booking để Thu chi, Báo cáo và danh sách hủy luôn khớp nhau.
/// </summary>
public sealed class BookingCancellation : EntityBase
{
    public Guid PropertyId { get; set; }
    public Property Property { get; set; } = null!;

    public Guid BookingId { get; set; }
    public Booking Booking { get; set; } = null!;

    public DateTime CancelledAtUtc { get; set; } = DateTime.UtcNow;
    public Guid? CancelledByUserId { get; set; }

    /// <summary>Null với hồ sơ dựng lại từ lịch sử cũ khi chưa có ô nhập lý do.</summary>
    public string? Reason { get; set; }

    public decimal TotalAmountSnapshot { get; set; }

    /// <summary>Tiền khách đã thanh toán ròng (thu trừ hoàn) tại thời điểm hủy.</summary>
    public decimal PaidAmountSnapshot { get; set; }

    /// <summary>Số tiền dự kiến hoàn cho khách. 0 khi không phải hoàn (không thu hoặc giữ cọc).</summary>
    public decimal RefundDueAmount { get; set; }

    public string? RefundNote { get; set; }

    /// <summary>Có giá trị khi booking được khôi phục; hồ sơ này không còn tính vào danh sách.</summary>
    public DateTime? RestoredAtUtc { get; set; }

    public bool IsBackfilled { get; set; }
}
