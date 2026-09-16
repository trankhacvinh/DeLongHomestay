using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Features.Payments;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeLong.Tests.Unit;

public sealed class SePaySecurityTests
{
    [Fact]
    public void Webhook_requires_exact_key_and_never_accepts_missing_or_tampered_key()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=unused").Options);
        var service = new SePaySettingsService(db, new EphemeralDataProtectionProvider());
        var key = new string('a', 32);
        var protectedKey = service.Protect(key);
        Assert.True(service.Verify("Apikey " + key, protectedKey));
        Assert.False(service.Verify("Bearer " + key, protectedKey));
        Assert.False(service.Verify("Apikey " + new string('b', 32), protectedKey));
        Assert.False(service.Verify(null, protectedKey));
        Assert.False(service.Verify("Apikey " + key, "corrupted"));
    }

    [Theory]
    [InlineData("DH1234567890", "anything", "DH1234567890")]
    [InlineData(null, "SEVQR DH1234567890 chuyen tien", "DH1234567890")]
    [InlineData("WRONG", "DH1234567890", "DH1234567890")]
    [InlineData("DH97342557", "DH9734255731", "DH9734255731")]
    [InlineData(null, "DH1234567890 DH1234567891", null)]
    [InlineData(null, "XDH1234567890", null)]
    [InlineData(null, "DH12345678901", null)]
    [InlineData("", "dh1234567890", "DH1234567890")]
    [InlineData("DH12345ABCDE", "DH12345ABCDE", null)]
    public void Codes_are_exact_and_ambiguous_transfers_are_not_guessed(string? code, string content, string? expected) =>
        Assert.Equal(expected, SePayService.PaymentCode(new(1, "123", null, code, content, "in", 100)));

    [Fact]
    public void Qr_preserves_va_and_required_bank_memo_and_rejects_fractional_amounts()
    {
        var settings = new PropertySePaySettings { BankId = "VietinBank", QrAccountNumber = "VA123", MemoPrefix = "SEVQR TKP001" };
        var url = SePaySettingsService.QrUrl(settings, "DH1234567890", 250000);
        Assert.Contains("acc=VA123", url);
        Assert.Contains("amount=250000", url);
        Assert.Contains("des=SEVQR%20TKP001%20DH1234567890", url);
        Assert.Throws<ArgumentOutOfRangeException>(() => SePaySettingsService.QrUrl(settings, "DH1234567890", 1.5m));
    }
}
