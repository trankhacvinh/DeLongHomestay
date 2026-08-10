using DeLong.Web.Domain.Enums;

namespace DeLong.Web.Features.Expenses;

public sealed record ExpenseDto(
    Guid Id,
    Guid PropertyId,
    DateTime OccurredAtUtc,
    string Category,
    string Description,
    decimal Amount,
    PaymentMethod Method,
    string? Vendor,
    string? Reference,
    string? Note,
    Guid? RecordedByUserId,
    bool IsVoided,
    DateTime? VoidedAtUtc,
    Guid? VoidedByUserId,
    string? VoidReason);

public sealed class CreateExpenseRequest
{
    public DateTimeOffset? OccurredAt { get; init; }
    public string Category { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public PaymentMethod Method { get; init; } = PaymentMethod.BankTransfer;
    public string? Vendor { get; init; }
    public string? Reference { get; init; }
    public string? Note { get; init; }
}

public sealed record VoidExpenseRequest(string Reason);
public sealed record ExpenseOperationError(string Code, string Message);
