using DeLong.Web.Domain.Enums;

namespace DeLong.Web.Features.Vouchers;

public sealed record VoucherDto(
    Guid Id,
    Guid PropertyId,
    string Code,
    string? Description,
    decimal DiscountPercent,
    VoucherApplicability AppliesTo,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    int? TotalUsageLimit,
    int? PerCustomerUsageLimit,
    Guid? CustomerId,
    string? CustomerName,
    string? CustomerPhone,
    string? CustomerEmail,
    VoucherStatus Status,
    int ReservedCount,
    int RedeemedCount,
    int? RemainingCount,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record VoucherRedemptionDto(
    Guid Id,
    Guid BookingId,
    string BookingCode,
    BookingStatus BookingStatus,
    Guid CustomerId,
    string CustomerName,
    string CustomerPhone,
    string VoucherCode,
    decimal DiscountPercent,
    decimal EligibleRoomAmount,
    decimal DiscountAmount,
    VoucherApplicability BookingScope,
    VoucherRedemptionStatus Status,
    DateTime ReservedAtUtc,
    DateTime? RedeemedAtUtc,
    DateTime? ReleasedAtUtc,
    DateTime? RestoredAtUtc,
    string? ResolutionReason);

public sealed record VoucherEmailDeliveryDto(
    Guid Id,
    string RecipientEmail,
    DateTime RequestedAtUtc,
    DateTime? SentAtUtc,
    int AttemptCount,
    string? LastError);

public sealed class SaveVoucherRequest
{
    public string? Code { get; init; }
    public string? Description { get; init; }
    public decimal DiscountPercent { get; init; }
    public VoucherApplicability AppliesTo { get; init; }
    public DateTime StartsAtUtc { get; init; }
    public DateTime EndsAtUtc { get; init; }
    public int? TotalUsageLimit { get; init; }
    public int? PerCustomerUsageLimit { get; init; }
    public Guid? CustomerId { get; init; }
    public VoucherStatus Status { get; init; }
}

public sealed record RestoreVoucherRedemptionRequest(string Reason);
public sealed record SendVoucherEmailRequest(string? RecipientEmail, string? CustomerName);

public sealed record VoucherOperationError(string Code, string Message);

public sealed record VoucherApplicationResult(
    Guid VoucherId,
    string Code,
    decimal DiscountPercent,
    decimal EligibleRoomAmount,
    decimal DiscountAmount,
    decimal TotalAfterDiscount,
    VoucherApplicability BookingScope);

public sealed class PublicVoucherPreviewRequest
{
    public Guid RoomId { get; init; }
    public string CustomerPhone { get; init; } = string.Empty;
    public IReadOnlyList<PublicVoucherSlotRequest> Slots { get; init; } = [];
    public string VoucherCode { get; init; } = string.Empty;
}

public sealed record PublicVoucherSlotRequest(string StayDate, Guid RateId);
public sealed record PublicVoucherPreviewResult(
    string VoucherCode,
    decimal DiscountPercent,
    decimal RoomAmount,
    decimal DiscountAmount,
    decimal TotalAfterDiscount,
    VoucherApplicability BookingScope);
