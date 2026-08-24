using DeLong.Web.Domain.Enums;

namespace DeLong.Web.Domain.Entities;

public sealed class Pay2SPaymentIntent : EntityBase
{
    public Guid PropertyId { get; set; }
    public Property Property { get; set; } = null!;
    public Guid BookingId { get; set; }
    public Booking Booking { get; set; } = null!;
    public string OrderId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
    public string OrderInfo { get; set; } = string.Empty;
    public string? SiteSlug { get; set; }
    public decimal Amount { get; set; }
    public Pay2SPaymentIntentStatus Status { get; set; } = Pay2SPaymentIntentStatus.Pending;
    public string? PayUrl { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime ReleaseAtUtc { get; set; }
    public bool CancelBookingOnExpiry { get; set; }
    public long? TransactionId { get; set; }
    public string? PayType { get; set; }
    public int? ResultCode { get; set; }
    public string? ProviderMessage { get; set; }
    public DateTime? CallbackReceivedAtUtc { get; set; }
    public DateTime? LastCallbackAttemptAtUtc { get; set; }
    public string? LastCallbackErrorCode { get; set; }
    public Guid? PaymentId { get; set; }
    public Payment? Payment { get; set; }
    public string? LatePaymentResolution { get; set; }
    public string? LatePaymentResolutionNote { get; set; }
    public DateTime? LatePaymentResolvedAtUtc { get; set; }
    public Guid? LatePaymentResolvedByUserId { get; set; }
}
