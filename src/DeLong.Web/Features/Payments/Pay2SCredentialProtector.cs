using Microsoft.AspNetCore.DataProtection;

namespace DeLong.Web.Features.Payments;

public sealed class Pay2SCredentialProtector(IDataProtectionProvider provider)
{
    private const string Prefix = "dp:v1:";
    private readonly IDataProtector protector = provider.CreateProtector("DeLongHomestay.Pay2S.Credentials.v1");

    public string Protect(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Credential must not be empty.", nameof(value));
        return Prefix + protector.Protect(value.Trim());
    }

    public string Unprotect(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith(Prefix, StringComparison.Ordinal))
            throw new InvalidOperationException("Pay2S credential is not in a supported protected format.");
        return protector.Unprotect(value[Prefix.Length..]);
    }
}
