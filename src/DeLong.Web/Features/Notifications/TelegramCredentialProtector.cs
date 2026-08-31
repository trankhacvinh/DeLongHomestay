using Microsoft.AspNetCore.DataProtection;

namespace DeLong.Web.Features.Notifications;

public sealed class TelegramCredentialProtector
{
    private const string Prefix = "dp:v1:";
    private readonly IDataProtector protector;

    public TelegramCredentialProtector(IDataProtectionProvider provider)
    {
        protector = provider.CreateProtector("DeLongHomestay.Notifications.TelegramBotToken.v1");
    }

    public string Protect(string plaintext)
    {
        if (string.IsNullOrWhiteSpace(plaintext)) throw new ArgumentException("Telegram bot token must not be empty.", nameof(plaintext));
        return Prefix + protector.Protect(plaintext.Trim());
    }

    public bool TryUnprotect(string? protectedValue, out string? plaintext)
    {
        plaintext = null;
        if (string.IsNullOrWhiteSpace(protectedValue)) return true;
        if (!protectedValue.StartsWith(Prefix, StringComparison.Ordinal)) return false;
        try
        {
            plaintext = protector.Unprotect(protectedValue[Prefix.Length..]);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
