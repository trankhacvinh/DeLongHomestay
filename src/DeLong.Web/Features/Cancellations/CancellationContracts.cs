using DeLong.Web.Domain.Enums;

namespace DeLong.Web.Features.Cancellations;

/// <summary>Cách xử lý tiền khách đã trả khi hủy: none (không cần), now (hoàn ngay), later (hoàn sau), keep (giữ cọc).</summary>
public static class CancelRefundModes
{
    public const string None = "none";
    public const string Now = "now";
    public const string Later = "later";
    public const string Keep = "keep";
}

public sealed record CancelRefundOption(
    string? Mode,
    decimal? Amount = null,
    PaymentMethod? Method = null,
    DateTimeOffset? OccurredAt = null,
    string? Note = null);

public sealed record CancelDetails(string? Reason, CancelRefundOption? Refund = null);

/// <summary>Thông tin gửi kèm thông báo khi hủy booking.</summary>
public sealed record CancellationNotice(
    Guid CancellationId,
    string? ActorName,
    string? Reason,
    decimal PaidAmount,
    string RefundSummary);

public static class CancellationRefundStatuses
{
    public const string NotRequired = "NotRequired";
    public const string Pending = "Pending";
    public const string Partial = "Partial";
    public const string Refunded = "Refunded";
    public const string Kept = "Kept";

    /// <summary>
    /// Trạng thái hoàn tiền suy ra từ số đã thu, số cần hoàn và số đã hoàn thực tế:
    /// không thu = không cần hoàn; cần hoàn 0 = giữ cọc; còn lại so sánh số đã hoàn với số cần hoàn.
    /// </summary>
    public static string Resolve(decimal paidSnapshot, decimal dueAmount, decimal refundedAmount)
    {
        if (paidSnapshot <= 0 && dueAmount <= 0 && refundedAmount <= 0) return NotRequired;
        if (dueAmount <= 0) return refundedAmount > 0 ? Refunded : Kept;
        if (refundedAmount >= dueAmount) return Refunded;
        return refundedAmount > 0 ? Partial : Pending;
    }
}

public sealed record CancellationRefundDto(
    Guid PaymentId,
    decimal Amount,
    PaymentMethod Method,
    DateTime OccurredAtUtc,
    DateTime EnteredAtUtc,
    Guid? RecordedByUserId,
    string? RecordedByName,
    string? Reference,
    string? Note);

public sealed record CancellationRowDto(
    Guid Id,
    Guid PropertyId,
    string PropertyName,
    Guid BookingId,
    string BookingCode,
    BookingStatus BookingStatus,
    string CustomerName,
    string CustomerPhone,
    string RoomName,
    DateTime CheckInUtc,
    DateTime CheckOutUtc,
    DateTime CancelledAtUtc,
    Guid? CancelledByUserId,
    string? CancelledByName,
    string? Reason,
    bool IsBackfilled,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal RefundDueAmount,
    decimal RefundedAmount,
    decimal RemainingRefundAmount,
    decimal KeptAmount,
    string RefundStatus,
    string? RefundNote,
    DateTime? LastRefundOccurredAtUtc,
    DateTime? LastRefundEnteredAtUtc,
    string? LastRefundByName,
    IReadOnlyList<CancellationRefundDto> Refunds);

public sealed record CancellationTotalsDto(
    int Count,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal RefundedAmount,
    decimal RemainingRefundAmount,
    decimal KeptAmount,
    int PendingCount);

public sealed class UpdateRefundDueRequest
{
    public decimal Amount { get; init; }
    public string? Note { get; init; }
}

public sealed class RecordCancellationRefundRequest
{
    public decimal Amount { get; init; }
    public PaymentMethod Method { get; init; } = PaymentMethod.BankTransfer;
    public DateTimeOffset? OccurredAt { get; init; }
    public string? Reference { get; init; }
    public string? Note { get; init; }
}

public sealed class EditCancellationRefundRequest
{
    public decimal Amount { get; init; }
    public PaymentMethod Method { get; init; } = PaymentMethod.BankTransfer;
    public DateTimeOffset? OccurredAt { get; init; }
    public string? Reference { get; init; }
    public string? Note { get; init; }
}

public sealed record CancellationOperationError(string Code, string Message);
