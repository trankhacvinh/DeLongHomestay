using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Payments;

public sealed class Pay2SSettingsService(AppDbContext db, Pay2SCredentialProtector protector)
{
    public async Task<Pay2SSettingsDto> GetAsync(Guid propertyId, CancellationToken ct = default)
    {
        var entity = await db.PropertyPay2SSettings.AsNoTracking().SingleOrDefaultAsync(x => x.PropertyId == propertyId, ct);
        return ToDto(entity);
    }

    public async Task<(Pay2SSettingsDto? Settings, Pay2SOperationError? Error)> SaveAsync(
        Guid propertyId, UpdatePay2SSettingsRequest request, CancellationToken ct = default)
    {
        if (!await db.Properties.AnyAsync(x => x.Id == propertyId, ct))
            return (null, new("property_not_found", "Không tìm thấy cơ sở."));
        if (request.HoldMinutes is < 1 or > 60)
            return (null, new("hold_minutes_invalid", "Thời gian giữ phòng phải từ 1 đến 60 phút."));
        if (request.SettlementGraceMinutes is < 0 or > 15)
            return (null, new("settlement_grace_invalid", "Khoảng đệm xác nhận thanh toán phải từ 0 đến 15 phút."));
        if (!Uri.TryCreate(request.ApiEndpoint?.Trim(), UriKind.Absolute, out var apiUri) || apiUri.Scheme != Uri.UriSchemeHttps)
            return (null, new("api_endpoint_invalid", "API endpoint Pay2S phải là URL HTTPS hợp lệ."));
        if (!Uri.TryCreate(request.CallbackBaseUrl?.Trim().TrimEnd('/'), UriKind.Absolute, out var callbackUri) || callbackUri.Scheme != Uri.UriSchemeHttps)
            return (null, new("callback_url_invalid", "Domain callback phải là URL HTTPS hợp lệ."));

        var entity = await db.PropertyPay2SSettings.SingleOrDefaultAsync(x => x.PropertyId == propertyId, ct);
        var changesVerificationIdentity = entity is not null &&
            (request.ClearCredentials ||
             !string.IsNullOrWhiteSpace(request.AccessKey) ||
             !string.IsNullOrWhiteSpace(request.SecretKey) ||
             request.Enabled != entity.Enabled ||
             !string.Equals(request.PartnerCode?.Trim(), entity.PartnerCode, StringComparison.Ordinal) ||
             !string.Equals(NormalizeApiEndpoint(apiUri.ToString()), entity.ApiEndpoint, StringComparison.OrdinalIgnoreCase));
        if (changesVerificationIdentity && await db.Pay2SPaymentIntents.AsNoTracking().AnyAsync(x =>
                x.PropertyId == propertyId &&
                (x.Status == Domain.Enums.Pay2SPaymentIntentStatus.Pending ||
                 x.Status == Domain.Enums.Pay2SPaymentIntentStatus.Failed) &&
                x.ReleaseAtUtc > DateTime.UtcNow, ct))
            return (null, new(
                "pay2s_active_intents",
                "Không thể đổi, xóa hoặc tắt thông tin xác thực Pay2S khi cơ sở còn phiên thanh toán đang chờ. Hãy đợi các phiên kết thúc hoặc hủy booking liên quan trước."));

        if (entity is null)
        {
            entity = new PropertyPay2SSettings { PropertyId = propertyId };
            db.Add(entity);
        }
        if (request.ClearCredentials)
        {
            entity.AccessKeyProtected = string.Empty;
            entity.SecretKeyProtected = string.Empty;
        }
        if (!string.IsNullOrWhiteSpace(request.AccessKey)) entity.AccessKeyProtected = protector.Protect(request.AccessKey);
        if (!string.IsNullOrWhiteSpace(request.SecretKey)) entity.SecretKeyProtected = protector.Protect(request.SecretKey);

        entity.Enabled = request.Enabled;
        entity.Sandbox = request.Sandbox;
        entity.PartnerCode = request.PartnerCode?.Trim() ?? string.Empty;
        entity.PartnerName = request.PartnerName?.Trim() ?? "De Long Homestay";
        entity.BankAccountNumber = request.BankAccountNumber?.Trim() ?? string.Empty;
        entity.BankId = request.BankId?.Trim().ToUpperInvariant() ?? string.Empty;
        entity.ApiEndpoint = NormalizeApiEndpoint(apiUri.ToString());
        entity.CallbackBaseUrl = callbackUri.ToString().TrimEnd('/');
        entity.HoldMinutes = request.HoldMinutes;
        entity.SettlementGraceMinutes = request.SettlementGraceMinutes;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        if (entity.Enabled && (string.IsNullOrWhiteSpace(entity.PartnerCode) || string.IsNullOrWhiteSpace(entity.BankAccountNumber) ||
                               string.IsNullOrWhiteSpace(entity.BankId) || string.IsNullOrWhiteSpace(entity.AccessKeyProtected) ||
                               string.IsNullOrWhiteSpace(entity.SecretKeyProtected)))
            return (null, new("pay2s_incomplete", "Vui lòng nhập đủ partner code, access key, secret key, ngân hàng và số tài khoản."));

        await db.SaveChangesAsync(ct);
        return (ToDto(entity), null);
    }

    public async Task<(Pay2SProfile? Profile, Pay2SOperationError? Error)> GetProfileAsync(Guid propertyId, CancellationToken ct = default)
    {
        var entity = await db.PropertyPay2SSettings.AsNoTracking().SingleOrDefaultAsync(x => x.PropertyId == propertyId, ct);
        if (entity is null || !entity.Enabled) return (null, new("pay2s_disabled", "Cơ sở chưa bật thanh toán Pay2S."));
        try
        {
            return (new(propertyId, entity.PartnerCode, entity.PartnerName, protector.Unprotect(entity.AccessKeyProtected),
                protector.Unprotect(entity.SecretKeyProtected), entity.BankAccountNumber, entity.BankId, NormalizeApiEndpoint(entity.ApiEndpoint),
                entity.CallbackBaseUrl, entity.HoldMinutes, entity.SettlementGraceMinutes), null);
        }
        catch
        {
            return (null, new("pay2s_credentials_unreadable", "Không thể giải mã khóa Pay2S. Hãy nhập lại access key và secret key."));
        }
    }

    private static Pay2SSettingsDto ToDto(PropertyPay2SSettings? x) => new(
        x?.Enabled ?? false, x?.Sandbox ?? true, x?.PartnerCode ?? string.Empty, x?.PartnerName ?? "De Long Homestay",
        !string.IsNullOrWhiteSpace(x?.AccessKeyProtected), !string.IsNullOrWhiteSpace(x?.SecretKeyProtected),
        x?.BankAccountNumber ?? string.Empty, x?.BankId ?? "ACB",
        NormalizeApiEndpoint(x?.ApiEndpoint), x?.CallbackBaseUrl ?? string.Empty,
        x?.HoldMinutes ?? 15, x?.SettlementGraceMinutes ?? 3);

    private static string NormalizeApiEndpoint(string? endpoint) =>
        string.IsNullOrWhiteSpace(endpoint) || endpoint.Contains("sandbox-payment.pay2s.vn", StringComparison.OrdinalIgnoreCase)
            ? Pay2SClient.CurrentApiEndpoint
            : endpoint.Trim();
}
