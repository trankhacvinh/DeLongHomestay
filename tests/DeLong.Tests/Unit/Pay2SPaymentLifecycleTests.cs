using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Payments;
using Xunit;

namespace DeLong.Tests.Unit;

public sealed class Pay2SPaymentLifecycleTests
{
    private static readonly DateTime PaymentExpiresAtUtc = new(2026, 8, 23, 12, 15, 0, DateTimeKind.Utc);
    private static readonly DateTime ReleaseAtUtc = PaymentExpiresAtUtc.AddMinutes(3);

    [Fact]
    public void Public_payment_uses_grace_but_staff_payment_releases_at_expiry()
    {
        Assert.Equal(
            PaymentExpiresAtUtc.AddMinutes(3),
            Pay2SPaymentLifecycle.CalculateReleaseAtUtc(PaymentExpiresAtUtc, 3, useSettlementGrace: true));
        Assert.Equal(
            PaymentExpiresAtUtc,
            Pay2SPaymentLifecycle.CalculateReleaseAtUtc(PaymentExpiresAtUtc, 3, useSettlementGrace: false));
    }

    [Fact]
    public void Before_payment_expiry_customer_can_resume()
    {
        var now = PaymentExpiresAtUtc.AddTicks(-1);

        Assert.True(Pay2SPaymentLifecycle.CanResume(Pay2SPaymentIntentStatus.Pending, now, PaymentExpiresAtUtc));
        Assert.False(Pay2SPaymentLifecycle.IsInSettlementGrace(Pay2SPaymentIntentStatus.Pending, now, PaymentExpiresAtUtc, ReleaseAtUtc));
    }

    [Fact]
    public void At_payment_expiry_room_is_still_locked_but_payment_cannot_resume()
    {
        Assert.False(Pay2SPaymentLifecycle.CanResume(Pay2SPaymentIntentStatus.Pending, PaymentExpiresAtUtc, PaymentExpiresAtUtc));
        Assert.True(Pay2SPaymentLifecycle.IsInSettlementGrace(Pay2SPaymentIntentStatus.Pending, PaymentExpiresAtUtc, PaymentExpiresAtUtc, ReleaseAtUtc));
        Assert.False(Pay2SPaymentLifecycle.ShouldRelease(Pay2SPaymentIntentStatus.Pending, PaymentExpiresAtUtc, ReleaseAtUtc));
        Assert.False(Pay2SPaymentLifecycle.IsSuccessfulPaymentLate(Pay2SPaymentIntentStatus.Pending, BookingStatus.Held, PaymentExpiresAtUtc, ReleaseAtUtc));
    }

    [Fact]
    public void At_release_time_booking_is_released_and_success_is_late()
    {
        Assert.True(Pay2SPaymentLifecycle.ShouldRelease(Pay2SPaymentIntentStatus.Pending, ReleaseAtUtc, ReleaseAtUtc));
        Assert.True(Pay2SPaymentLifecycle.IsSuccessfulPaymentLate(Pay2SPaymentIntentStatus.Expired, BookingStatus.Cancelled, ReleaseAtUtc, ReleaseAtUtc));
    }
}
