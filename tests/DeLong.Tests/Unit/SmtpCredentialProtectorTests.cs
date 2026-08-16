using DeLong.Web.Features.Notifications;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace DeLong.Tests.Unit;

public sealed class SmtpCredentialProtectorTests
{
    [Fact]
    public void Smtp_password_round_trips_without_storing_plaintext()
    {
        var protector = new SmtpCredentialProtector(new EphemeralDataProtectionProvider());
        const string secret = "smtp-super-secret";

        var protectedValue = protector.Protect(secret);

        Assert.StartsWith("dp:v1:", protectedValue);
        Assert.DoesNotContain(secret, protectedValue, StringComparison.Ordinal);
        Assert.Equal(secret, protector.Unprotect(protectedValue));
    }

    [Fact]
    public void Password_cannot_be_decrypted_with_an_unrelated_key_ring()
    {
        var first = new SmtpCredentialProtector(new EphemeralDataProtectionProvider());
        var second = new SmtpCredentialProtector(new EphemeralDataProtectionProvider());
        var protectedValue = first.Protect("secret");

        Assert.False(second.TryUnprotect(protectedValue, out _));
    }
}
