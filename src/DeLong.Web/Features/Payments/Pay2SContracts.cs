using System.Text.Json.Serialization;

namespace DeLong.Web.Features.Payments;

public sealed record Pay2SSettingsDto(
    bool Enabled,
    bool Sandbox,
    string PartnerCode,
    string PartnerName,
    bool AccessKeyConfigured,
    bool SecretKeyConfigured,
    string BankAccountNumber,
    string BankId,
    string ApiEndpoint,
    string CallbackBaseUrl,
    int HoldMinutes,
    int SettlementGraceMinutes);

public sealed record UpdatePay2SSettingsRequest(
    bool Enabled,
    bool Sandbox,
    string? PartnerCode,
    string? PartnerName,
    string? AccessKey,
    string? SecretKey,
    string? BankAccountNumber,
    string? BankId,
    string? ApiEndpoint,
    string? CallbackBaseUrl,
    int HoldMinutes,
    int SettlementGraceMinutes,
    bool ClearCredentials = false);

public sealed record Pay2SProfile(
    Guid PropertyId,
    string PartnerCode,
    string PartnerName,
    string AccessKey,
    string SecretKey,
    string BankAccountNumber,
    string BankId,
    string ApiEndpoint,
    string CallbackBaseUrl,
    int HoldMinutes,
    int SettlementGraceMinutes);

public sealed record Pay2SOperationError(string Code, string Message);

public sealed record Pay2SCustomerData(string Name, string Phone, string? Email);

public sealed record CreatePay2SIntentRequest(Guid BookingId, bool CancelBookingOnExpiry = true);
public sealed record Pay2SIntentDto(Guid Id, Guid BookingId, string OrderId, decimal Amount, string Status, string? PayUrl, DateTime ExpiresAtUtc, DateTime ReleaseAtUtc);

public sealed record ResolveLatePay2SRequest(string Action, string? Note = null, Guid? RoomId = null, DateTime? CheckInUtc = null, DateTime? CheckOutUtc = null);

public sealed record Pay2SIpnRequest(
    string PartnerCode,
    string OrderId,
    string RequestId,
    long Amount,
    string OrderInfo,
    string OrderType,
    long TransId,
    int ResultCode,
    string Message,
    string PayType,
    string ResponseTime,
    string ExtraData,
    [property: JsonPropertyName("m2signature")] string Signature);

public sealed record Pay2SRedirectRequest(
    string PartnerCode,
    string OrderId,
    string RequestId,
    string Amount,
    string OrderInfo,
    string OrderType,
    string TransId,
    string ResultCode,
    string Message,
    string PayType,
    string ResponseTime,
    [property: JsonPropertyName("m2signature")] string Signature);
