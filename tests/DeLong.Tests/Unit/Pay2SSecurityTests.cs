using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using DeLong.Web.Features.Payments;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace DeLong.Tests.Unit;

public sealed class Pay2SSecurityTests
{
    [Fact]
    public void Credentials_are_encrypted_and_round_trip()
    {
        var protector = new Pay2SCredentialProtector(new EphemeralDataProtectionProvider());
        const string secret = "pay2s-secret";
        var encrypted = protector.Protect(secret);
        Assert.StartsWith("dp:v1:", encrypted);
        Assert.DoesNotContain(secret, encrypted, StringComparison.Ordinal);
        Assert.Equal(secret, protector.Unprotect(encrypted));
    }

    [Fact]
    public void Ipn_signature_accepts_exact_payload_and_rejects_changed_amount()
    {
        const string accessKey = "access";
        const string secretKey = "secret";
        var request = new Pay2SIpnRequest("partner", "order", "request", 250000, "BOOKABC", "booking", 12345, 0,
            "Success", "qr", "20260822120000", string.Empty, string.Empty);
        var raw = $"accessKey={accessKey}&amount={request.Amount.ToString(CultureInfo.InvariantCulture)}&extraData={request.ExtraData}&message={request.Message}&orderId={request.OrderId}&orderInfo={request.OrderInfo}&orderType={request.OrderType}&partnerCode={request.PartnerCode}&payType={request.PayType}&requestId={request.RequestId}&responseTime={request.ResponseTime}&resultCode={request.ResultCode.ToString(CultureInfo.InvariantCulture)}&transId={request.TransId.ToString(CultureInfo.InvariantCulture)}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
        var signature = Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes(raw)));
        request = request with { Signature = signature };
        Assert.True(Pay2SClient.VerifyIpn(request, accessKey, secretKey));
        Assert.False(Pay2SClient.VerifyIpn(request with { Amount = 250001 }, accessKey, secretKey));
    }

    [Fact]
    public void Redirect_signature_accepts_exact_payload_and_rejects_changed_result()
    {
        const string accessKey = "access";
        const string secretKey = "secret";
        var request = new Pay2SRedirectRequest("partner", "order", "request", "250000", "BOOKABC", "booking",
            "12345", "0", "Success", "qr", "20260822120000", string.Empty);
        var raw = $"accessKey={accessKey}&amount={request.Amount}&message={request.Message}&orderId={request.OrderId}&orderInfo={request.OrderInfo}&orderType={request.OrderType}&partnerCode={request.PartnerCode}&payType={request.PayType}&requestId={request.RequestId}&responseTime={request.ResponseTime}&resultCode={request.ResultCode}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
        var signature = Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes(raw)));
        request = request with { Signature = signature };

        Assert.True(Pay2SClient.VerifyRedirect(request, accessKey, secretKey));
        Assert.False(Pay2SClient.VerifyRedirect(request with { ResultCode = "1" }, accessKey, secretKey));
    }
}
