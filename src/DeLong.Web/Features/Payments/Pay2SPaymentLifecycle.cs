using DeLong.Web.Domain.Enums;

namespace DeLong.Web.Features.Payments;

public static class Pay2SPaymentLifecycle
{
    public static DateTime CalculateReleaseAtUtc(
        DateTime paymentExpiresAtUtc,
        int settlementGraceMinutes,
        bool useSettlementGrace) =>
        paymentExpiresAtUtc.AddMinutes(useSettlementGrace ? settlementGraceMinutes : 0);

    public static bool CanResume(Pay2SPaymentIntentStatus status, DateTime nowUtc, DateTime paymentExpiresAtUtc) =>
        status == Pay2SPaymentIntentStatus.Pending && nowUtc < paymentExpiresAtUtc;

    public static bool IsInSettlementGrace(Pay2SPaymentIntentStatus status, DateTime nowUtc, DateTime paymentExpiresAtUtc, DateTime releaseAtUtc) =>
        status == Pay2SPaymentIntentStatus.Pending && nowUtc >= paymentExpiresAtUtc && nowUtc < releaseAtUtc;

    public static bool ShouldRelease(Pay2SPaymentIntentStatus status, DateTime nowUtc, DateTime releaseAtUtc) =>
        status == Pay2SPaymentIntentStatus.Pending && nowUtc >= releaseAtUtc;

    public static bool IsSuccessfulPaymentLate(Pay2SPaymentIntentStatus status, BookingStatus bookingStatus, DateTime nowUtc, DateTime releaseAtUtc) =>
        status == Pay2SPaymentIntentStatus.Expired || bookingStatus == BookingStatus.Cancelled || nowUtc >= releaseAtUtc;
}
