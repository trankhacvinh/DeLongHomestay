using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeLong.Web.Features.Payments;

public sealed class Pay2SClient(HttpClient httpClient)
{
    public const string CurrentApiEndpoint = "https://payment.pay2s.vn/v1/gateway/api/create";

    public async Task<(Pay2SCreateResponse? Response, Pay2SOperationError? Error)> CreateAsync(
        Pay2SProfile profile,
        string requestId,
        string orderId,
        string orderInfo,
        decimal amount,
        Pay2SCustomerData customer,
        CancellationToken ct)
    {
        if (decimal.Truncate(amount) != amount || amount <= 0 || amount > long.MaxValue)
            return (null, new("amount_invalid", "Pay2S chỉ nhận số tiền nguyên dương hợp lệ."));

        var amountText = decimal.ToInt64(amount).ToString(CultureInfo.InvariantCulture);
        var redirectUrl = $"{profile.CallbackBaseUrl}/payment/pay2s/return?orderId={Uri.EscapeDataString(orderId)}";
        var ipnUrl = $"{profile.CallbackBaseUrl}/api/payments/pay2s/ipn";
        const string requestType = "pay2s";
        const string signatureVersion = "2";
        var metadata = new
        {
            invoiceType = "none",
            customerInfo = new
            {
                buyerContactName = customer.Name,
                buyerCompanyName = string.Empty,
                taxCode = string.Empty,
                citizenId = string.Empty,
                address = string.Empty,
                email = customer.Email ?? string.Empty,
                phone = customer.Phone
            },
            items = new[]
            {
                new
                {
                    itemCode = "ROOM_BOOKING",
                    externalItemId = orderId,
                    sourceProductName = "Đặt phòng",
                    itemName = "Dịch vụ lưu trú",
                    unit = "Lượt",
                    quantity = 1,
                    unitPrice = decimal.ToInt64(amount),
                    taxRate = 0
                }
            },
            invoiceOptions = new
            {
                requested = false,
                buyerNotTakingInvoice = true,
                paymentMethod = "Chuyển khoản",
                note = string.Empty,
                source = "api"
            }
        };
        var extraData = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(metadata, JsonOptions)));
        var raw = $"accessKey={profile.AccessKey}&amount={amountText}&bankAccounts=Array&extraData={extraData}&ipnUrl={ipnUrl}&orderId={orderId}&orderInfo={orderInfo}&partnerCode={profile.PartnerCode}&redirectUrl={redirectUrl}&requestId={requestId}&requestType={requestType}&signatureVersion={signatureVersion}";
        var signature = Sign(raw, profile.SecretKey);
        var payload = new Pay2SCreateRequest(profile.AccessKey, profile.PartnerCode, profile.PartnerName, requestId,
            amountText, orderId, orderInfo, requestType, [new(profile.BankAccountNumber, profile.BankId)], redirectUrl,
            ipnUrl, requestType, signatureVersion, extraData, signature);
        try
        {
            using var response = await httpClient.PostAsJsonAsync(profile.ApiEndpoint, payload, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<Pay2SCreateResponse>(body, JsonOptions);
            if (!response.IsSuccessStatusCode || result is null || result.ResultCode != 0 || string.IsNullOrWhiteSpace(result.PayUrl))
                return (null, new("pay2s_create_failed", result?.Message ?? "Pay2S không tạo được phiên thanh toán."));
            return (result, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (null, new("pay2s_unavailable", $"Không kết nối được Pay2S: {ex.Message}"));
        }
    }

    public static bool VerifyIpn(Pay2SIpnRequest request, string accessKey, string secretKey)
    {
        var raw = $"accessKey={accessKey}&amount={request.Amount.ToString(CultureInfo.InvariantCulture)}&extraData={request.ExtraData}&message={request.Message}&orderId={request.OrderId}&orderInfo={request.OrderInfo}&orderType={request.OrderType}&partnerCode={request.PartnerCode}&payType={request.PayType}&requestId={request.RequestId}&responseTime={request.ResponseTime}&resultCode={request.ResultCode.ToString(CultureInfo.InvariantCulture)}&transId={request.TransId.ToString(CultureInfo.InvariantCulture)}";
        var expected = Encoding.ASCII.GetBytes(Sign(raw, secretKey));
        var actual = Encoding.ASCII.GetBytes(request.Signature?.Trim().ToLowerInvariant() ?? string.Empty);
        return expected.Length == actual.Length && CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    public static bool VerifyRedirect(Pay2SRedirectRequest request, string accessKey, string secretKey)
    {
        var raw = $"accessKey={accessKey}&amount={request.Amount}&message={request.Message}&orderId={request.OrderId}&orderInfo={request.OrderInfo}&orderType={request.OrderType}&partnerCode={request.PartnerCode}&payType={request.PayType}&requestId={request.RequestId}&responseTime={request.ResponseTime}&resultCode={request.ResultCode}";
        var expected = Encoding.ASCII.GetBytes(Sign(raw, secretKey));
        var actual = Encoding.ASCII.GetBytes(request.Signature?.Trim().ToLowerInvariant() ?? string.Empty);
        return expected.Length == actual.Length && CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    internal static string Sign(string raw, string secretKey)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
        return Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes(raw)));
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}

public sealed record Pay2SCreateRequest(
    string AccessKey, string PartnerCode, string PartnerName, string RequestId, string Amount, string OrderId,
    string OrderInfo, string OrderType, IReadOnlyList<Pay2SBankAccount> BankAccounts, string RedirectUrl,
    string IpnUrl, string RequestType, string SignatureVersion, string ExtraData, string Signature);
public sealed record Pay2SBankAccount(
    [property: JsonPropertyName("account_number")] string AccountNumber,
    [property: JsonPropertyName("bank_id")] string BankId);
public sealed record Pay2SCreateResponse(
    string? PartnerCode, string? RequestId, string? OrderId, string? OrderInfo, string? Amount, string? Message, int ResultCode,
    [property: JsonPropertyName("payUrl")] string? PayUrl);
