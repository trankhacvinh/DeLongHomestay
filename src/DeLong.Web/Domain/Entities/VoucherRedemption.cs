using DeLong.Web.Domain.Enums;

namespace DeLong.Web.Domain.Entities;

public sealed class VoucherRedemption : EntityBase
{
    public Guid PropertyId { get; set; }
    public Property Property { get; set; } = null!;

    public Guid VoucherId { get; set; }
    public Voucher Voucher { get; set; } = null!;

    public Guid BookingId { get; set; }
    public Booking Booking { get; set; } = null!;

    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public Guid? UserId { get; set; }

    public string VoucherCode { get; set; } = string.Empty;
    public decimal DiscountPercent { get; set; }
    public decimal EligibleRoomAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public VoucherApplicability BookingScope { get; set; }
    public VoucherRedemptionStatus Status { get; set; } = VoucherRedemptionStatus.Reserved;

    public DateTime ReservedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? RedeemedAtUtc { get; set; }
    public DateTime? ReleasedAtUtc { get; set; }
    public DateTime? RestoredAtUtc { get; set; }
    public Guid? RestoredByUserId { get; set; }
    public string? ResolutionReason { get; set; }
}
