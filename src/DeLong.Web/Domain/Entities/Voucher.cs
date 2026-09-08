using DeLong.Web.Domain.Enums;

namespace DeLong.Web.Domain.Entities;

public sealed class Voucher : EntityBase
{
    public Guid PropertyId { get; set; }
    public Property Property { get; set; } = null!;

    public string Code { get; set; } = string.Empty;
    public string NormalizedCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal DiscountPercent { get; set; }
    public VoucherApplicability AppliesTo { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    public int? TotalUsageLimit { get; set; }
    public int? PerCustomerUsageLimit { get; set; }

    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public VoucherStatus Status { get; set; } = VoucherStatus.Draft;
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }

    public ICollection<VoucherRedemption> Redemptions { get; set; } = [];
    public ICollection<VoucherEmailDelivery> EmailDeliveries { get; set; } = [];
}
