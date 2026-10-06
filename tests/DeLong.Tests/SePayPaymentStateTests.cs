using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Payments;
using Xunit;

namespace DeLong.Tests;

public sealed class SePayPaymentStateTests
{
    private static readonly DateTime Now = new(2026, 10, 4, 10, 0, 0, DateTimeKind.Utc);
    private static Pay2SPaymentIntent Intent() => new()
    {
        OrderId = "DH1234567890", Amount = 250000,
        ExpiresAtUtc = Now.AddMinutes(5), ReleaseAtUtc = Now.AddMinutes(8),
        SePayQrUrl = "https://vietqr.app/img?acc=123&bank=VCB&des=DH1234567890",
        Booking = new Booking { Code = "BK12345678", Status = BookingStatus.Held }
    };

    [Fact]
    public void Active_session_exposes_QR_and_transfer_fields()
    {
        var state = SePayPaymentState.FromIntent(Intent(), Now);
        Assert.NotNull(state.QrUrl);
        Assert.Equal("123", state.AccountNumber);
        Assert.Equal("DH1234567890", state.TransferContent);
    }

    [Fact]
    public void Expired_QR_remains_pending_during_bank_confirmation_grace()
    {
        var state = SePayPaymentState.FromIntent(Intent(), Now.AddMinutes(5));
        Assert.Equal("Pending", state.Status);
        Assert.Null(state.QrUrl);
        Assert.Null(state.TransferContent);
    }

    [Fact]
    public void Release_deadline_is_enforced_before_worker_persists_expiry()
    {
        var state = SePayPaymentState.FromIntent(Intent(), Now.AddMinutes(8));
        Assert.Equal("Expired", state.Status);
        Assert.Null(state.QrUrl);
    }

    [Theory]
    [InlineData(BookingStatus.Cancelled)]
    [InlineData(BookingStatus.Completed)]
    [InlineData(BookingStatus.NoShow)]
    public void Closed_booking_never_exposes_QR(BookingStatus status)
    {
        var intent = Intent();
        intent.Booking.Status = status;
        Assert.Null(SePayPaymentState.FromIntent(intent, Now).QrUrl);
    }

    [Theory]
    [InlineData(Pay2SPaymentIntentStatus.Succeeded)]
    [InlineData(Pay2SPaymentIntentStatus.PaidAfterExpiry)]
    public void Received_payment_status_is_preserved_after_deadline(Pay2SPaymentIntentStatus status)
    {
        var intent = Intent();
        intent.Status = status;
        var state = SePayPaymentState.FromIntent(intent, Now.AddDays(1));
        Assert.Equal(status.ToString(), state.Status);
        Assert.Null(state.QrUrl);
        Assert.Equal("/booking/success?code=BK12345678", state.SuccessUrl);
    }
}
