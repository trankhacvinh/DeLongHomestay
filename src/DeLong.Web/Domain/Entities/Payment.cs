using DeLong.Web.Domain.Enums;

namespace DeLong.Web.Domain.Entities;

public sealed class Payment : EntityBase
{
    public Guid PropertyId { get; set; }
    public Property Property { get; set; } = null!;

    public Guid BookingId { get; set; }
    public Booking Booking { get; set; } = null!;

    public PaymentType Type { get; set; } = PaymentType.Receipt;
    public PaymentMethod Method { get; set; } = PaymentMethod.BankTransfer;
    public decimal Amount { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public string? Reference { get; set; }
    public string? Note { get; set; }

    public Guid? RecordedByUserId { get; set; }
    public bool IsVoided { get; set; }
    public DateTime? VoidedAtUtc { get; set; }
    public Guid? VoidedByUserId { get; set; }
    public string? VoidReason { get; set; }

    public decimal SignedAmount => Type == PaymentType.Receipt ? Amount : -Amount;
}
