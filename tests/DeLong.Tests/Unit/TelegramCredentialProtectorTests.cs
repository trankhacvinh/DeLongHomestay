using DeLong.Web.Features.Notifications;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace DeLong.Tests.Unit;

public sealed class TelegramCredentialProtectorTests
{
    [Fact]
    public void Telegram_token_round_trips_without_storing_plaintext()
    {
        var protector = new TelegramCredentialProtector(new EphemeralDataProtectionProvider());
        const string token = "123456:telegram-test-token";

        var protectedValue = protector.Protect(token);

        Assert.NotEqual(token, protectedValue);
        Assert.StartsWith("dp:v1:", protectedValue);
        Assert.True(protector.TryUnprotect(protectedValue, out var plaintext));
        Assert.Equal(token, plaintext);
    }
}
