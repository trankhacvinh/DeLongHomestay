using DeLong.Web.Domain.Enums;

namespace DeLong.Web.Features.Payments;

public sealed record PaymentDto(
    Guid Id,
    Guid PropertyId,
    Guid BookingId,
    PaymentType Type,
    PaymentMethod Method,
    decimal Amount,
    decimal SignedAmount,
    DateTime OccurredAtUtc,
    string? Reference,
    string? Note,
    Guid? RecordedByUserId,
    bool IsVoided,
    DateTime? VoidedAtUtc,
    Guid? VoidedByUserId,
    string? VoidReason);

public sealed class CreatePaymentRequest
{
    public PaymentType Type { get; init; } = PaymentType.Receipt;
    public PaymentMethod Method { get; init; } = PaymentMethod.BankTransfer;
    public decimal Amount { get; init; }
    public DateTimeOffset? OccurredAt { get; init; }
    public string? Reference { get; init; }
    public string? Note { get; init; }
}

public sealed record VoidPaymentRequest(string Reason);
public sealed record PaymentOperationError(string Code, string Message);
