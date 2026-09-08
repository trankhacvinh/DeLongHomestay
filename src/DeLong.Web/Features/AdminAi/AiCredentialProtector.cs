using Microsoft.AspNetCore.DataProtection;

namespace DeLong.Web.Features.AdminAi;

public sealed class AiCredentialProtector(IDataProtectionProvider provider)
{
    private const string Prefix = "dp:v1:";
    private readonly IDataProtector protector = provider.CreateProtector("DeLongHomestay.AdminAi.Credentials.v1");
    public string Protect(string value) => Prefix + protector.Protect(value.Trim());
    public string Unprotect(string value) => value.StartsWith(Prefix, StringComparison.Ordinal)
        ? protector.Unprotect(value[Prefix.Length..])
        : throw new InvalidOperationException("AI credential is not protected.");
}

