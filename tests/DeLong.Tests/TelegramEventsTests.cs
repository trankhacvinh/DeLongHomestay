using DeLong.Web.Features.Notifications;
using Xunit;
namespace DeLong.Tests;
public sealed class TelegramEventsTests
{
    [Fact] public void Legacy_null_preserves_all_previous_types()
    {
        foreach (var type in TelegramEvents.All) Assert.True(TelegramEvents.Allows(null, type));
        Assert.True(TelegramEvents.Allows(null, "payment-receipt:synthetic"));
        Assert.True(TelegramEvents.Allows(null, "booking-email-only"));
    }
    [Fact] public void Explicit_empty_disables_all_types()
    { foreach (var type in TelegramEvents.All) Assert.False(TelegramEvents.Allows("", type)); }
    [Fact] public void Category_selection_is_exact_and_supports_unique_payment_events()
    {
        Assert.True(TelegramEvents.Allows("booking-requested,payment-receipt", "booking-email-only"));
        Assert.True(TelegramEvents.Allows("payment-receipt", "payment-receipt:123"));
        Assert.False(TelegramEvents.Allows("booking-requested", "booking-confirmed"));
        Assert.False(TelegramEvents.Allows("pay2s-failed", "pay2s-paid-after-expiry"));
    }
}
