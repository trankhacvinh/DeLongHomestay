using Xunit;

namespace DeLong.Tests;

public sealed class TelegramBookingPaymentNotificationSourceContractTests
{
    [Fact]
    public void Booking_service_centrally_queues_created_and_status_notifications()
    {
        var source = Read("src/DeLong.Web/Features/Bookings/BookingService.cs");
        Assert.Contains("NotifyBookingCreatedAsync", source, StringComparison.Ordinal);
        Assert.Contains("NotifyBookingStatusChangedAsync", source, StringComparison.Ordinal);
        Assert.Contains("previousStatus", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Manual_receipts_queue_payment_success_but_refunds_do_not()
    {
        var source = Read("src/DeLong.Web/Features/Payments/PaymentService.cs");
        Assert.Contains("payment.Type == PaymentType.Receipt", source, StringComparison.Ordinal);
        Assert.Contains("GetNetPaidAsync", source, StringComparison.Ordinal);
        Assert.Contains("NotifyPaymentSucceededAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("payment.Type == PaymentType.Refund)\n            await TryNotifyReceiptAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Pay2s_auto_confirm_is_folded_into_single_payment_notification()
    {
        var source = Read("src/DeLong.Web/Features/Payments/Pay2SService.cs");
        Assert.Contains("NotifyPaymentSucceededAsync", source, StringComparison.Ordinal);
        Assert.Contains("autoConfirmed", source, StringComparison.Ordinal);
        Assert.Contains("NotifyPay2SFailedAsync", source, StringComparison.Ordinal);
        Assert.Contains("NotifyLatePay2SPaymentAsync", source, StringComparison.Ordinal);
        Assert.Contains("Hết thời gian thanh toán Pay2S.", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Notification_service_keeps_new_telegram_events_out_of_bell_and_uses_stable_keys()
    {
        var source = Read("src/DeLong.Web/Features/Notifications/BookingNotificationService.cs");
        Assert.Contains("payment-receipt:{paymentId:N}", source, StringComparison.Ordinal);
        Assert.Contains("pay2s-failed:{intentId:N}", source, StringComparison.Ordinal);
        Assert.Contains("BookingStatus.CheckedIn", source, StringComparison.Ordinal);
        Assert.Contains("BookingStatus.Completed", source, StringComparison.Ordinal);
        Assert.Contains("BookingStatus.Cancelled", source, StringComparison.Ordinal);
        Assert.Contains("BookingStatus.Confirmed", source, StringComparison.Ordinal);
        Assert.Contains("✅ Booking đã được tự động xác nhận sau thanh toán.", source, StringComparison.Ordinal);
        Assert.Contains("private static readonly string[] InAppTypes = [BookingRequestedType, Pay2SLatePaymentType]", source, StringComparison.Ordinal);
        Assert.Contains("Truncate(message, 4096)", source, StringComparison.Ordinal);
    }

    private static string Read(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DeLongHomestay.sln"))) directory = directory.Parent;
        if (directory is null) throw new InvalidOperationException("Could not locate repository root.");
        return File.ReadAllText(Path.Combine(directory.FullName, relativePath));
    }
}
