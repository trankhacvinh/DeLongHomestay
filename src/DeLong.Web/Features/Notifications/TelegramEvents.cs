namespace DeLong.Web.Features.Notifications;

public static class TelegramEvents
{
    public static readonly string[] All = ["booking-requested", "booking-confirmed", "booking-cancelled", "booking-checked-in", "booking-completed", "payment-receipt", "pay2s-failed", "pay2s-paid-after-expiry"];

    public static string[] Parse(string? stored) => stored is null ? [.. All] : stored.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static bool Allows(string? stored, string type)
    {
        var key = type.Split(':')[0];
        if (key == "booking-email-only") key = "booking-requested";
        return Parse(stored).Contains(key, StringComparer.Ordinal);
    }
}
