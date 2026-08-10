using DeLong.Web.Domain.Enums;

namespace DeLong.Web.Domain.Entities;

public sealed class Expense : EntityBase
{
    public Guid PropertyId { get; set; }
    public Property Property { get; set; } = null!;

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; } = PaymentMethod.BankTransfer;
    public string? Vendor { get; set; }
    public string? Reference { get; set; }
    public string? Note { get; set; }
    public Guid? RecordedByUserId { get; set; }

    public bool IsVoided { get; set; }
    public DateTime? VoidedAtUtc { get; set; }
    public Guid? VoidedByUserId { get; set; }
    public string? VoidReason { get; set; }
}
