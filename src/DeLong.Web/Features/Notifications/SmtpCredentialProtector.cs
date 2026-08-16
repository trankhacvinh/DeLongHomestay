using Microsoft.AspNetCore.DataProtection;

namespace DeLong.Web.Features.Notifications;

public sealed class SmtpCredentialProtector
{
    private const string Prefix = "dp:v1:";
    private readonly IDataProtector protector;

    public SmtpCredentialProtector(IDataProtectionProvider provider)
    {
        protector = provider.CreateProtector("DeLongHomestay.Notifications.SmtpPassword.v1");
    }

    public string Protect(string plaintext)
    {
        if (string.IsNullOrWhiteSpace(plaintext)) throw new ArgumentException("SMTP password must not be empty.", nameof(plaintext));
        return Prefix + protector.Protect(plaintext);
    }

    public string Unprotect(string protectedValue)
    {
        if (string.IsNullOrWhiteSpace(protectedValue) || !protectedValue.StartsWith(Prefix, StringComparison.Ordinal))
            throw new InvalidOperationException("SMTP password is not in a supported protected format.");
        return protector.Unprotect(protectedValue[Prefix.Length..]);
    }

    public bool TryUnprotect(string? protectedValue, out string? plaintext)
    {
        plaintext = null;
        if (string.IsNullOrWhiteSpace(protectedValue)) return true;
        try
        {
            plaintext = Unprotect(protectedValue);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
