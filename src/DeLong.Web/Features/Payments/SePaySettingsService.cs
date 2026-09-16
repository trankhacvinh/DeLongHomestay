using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace DeLong.Web.Features.Payments;

public sealed record SePaySettingsRequest(bool Enabled, string BankId, string BankAccountNumber,
    string QrAccountNumber, string AccountHolder, string SubAccount, string MemoPrefix,
    string? WebhookKey, int HoldMinutes = 15, int SettlementGraceMinutes = 3, string? ApiToken = null);

public sealed class SePaySettingsService(AppDbContext db, IDataProtectionProvider protection)
{
    private readonly IDataProtector protector = protection.CreateProtector("DeLong.SePay.Webhook.v1");
    internal string Unprotect(string key) => protector.Unprotect(key);
    public string Protect(string key) => protector.Protect(key);
    public bool Verify(string? authorization, string? protectedKey)
    {
        if (string.IsNullOrEmpty(protectedKey) || string.IsNullOrEmpty(authorization)) return false;
        try
        {
            var expected = Encoding.UTF8.GetBytes("Apikey " + protector.Unprotect(protectedKey));
            return CryptographicOperations.FixedTimeEquals(expected, Encoding.UTF8.GetBytes(authorization));
        }
        catch (CryptographicException) { return false; }
    }

    public async Task<object> GetAsync(Guid propertyId, CancellationToken ct)
    {
        var x = await db.PropertySePaySettings.AsNoTracking().SingleOrDefaultAsync(x => x.PropertyId == propertyId, ct)
            ?? new PropertySePaySettings { PropertyId = propertyId };
        return new { x.Enabled, x.BankId, x.BankAccountNumber, x.QrAccountNumber, x.AccountHolder,
            x.SubAccount, x.MemoPrefix, x.HoldMinutes, x.SettlementGraceMinutes,
            ApiTokenConfigured = x.ApiTokenProtected.Length > 0, WebhookKeyConfigured = x.WebhookKeyProtected.Length > 0 };
    }

    public async Task<string?> SaveAsync(Guid propertyId, SePaySettingsRequest request, CancellationToken ct)
    {
        if (request.HoldMinutes is < 1 or > 60 || request.SettlementGraceMinutes is < 0 or > 15)
            return "Thời gian giữ phòng phải từ 1–60 phút, đệm xác nhận từ 0–15 phút.";
        var bank = request.BankId?.Trim() ?? "";
        var account = request.BankAccountNumber?.Trim() ?? "";
        var qrAccount = request.QrAccountNumber?.Trim() ?? "";
        var sub = request.SubAccount?.Trim() ?? "";
        var memo = request.MemoPrefix?.Trim() ?? "";
        if (bank.Length > 30 || account.Length > 100 || qrAccount.Length > 100 || sub.Length > 100 ||
            memo.Length > 100 || (request.AccountHolder?.Length ?? 0) > 200 || (request.WebhookKey?.Length ?? 0) > 512 || (request.ApiToken?.Length ?? 0) > 2048)
            return "Thông tin cấu hình vượt quá độ dài cho phép.";
        if (request.Enabled && (!Regex.IsMatch(bank, "^[A-Za-z0-9]+$") || !Regex.IsMatch(account, "^[A-Za-z0-9]+$") ||
            !Regex.IsMatch(qrAccount.Length == 0 ? account : qrAccount, "^[A-Za-z0-9]+$")))
            return "Nhập mã ngân hàng, số tài khoản nhận webhook và tài khoản/VA hiển thị trên QR hợp lệ.";
        if (request.Enabled && bank.Equals("VietinBank", StringComparison.OrdinalIgnoreCase) && !memo.Contains("SEVQR", StringComparison.Ordinal))
            return "VietinBank cần tiền tố nội dung SEVQR.";
        var entity = await db.PropertySePaySettings.SingleOrDefaultAsync(x => x.PropertyId == propertyId, ct);
        var key = string.IsNullOrWhiteSpace(request.WebhookKey) ? entity?.WebhookKeyProtected : Protect(request.WebhookKey.Trim());
        if (request.Enabled && (string.IsNullOrEmpty(key) || (!string.IsNullOrWhiteSpace(request.WebhookKey) && request.WebhookKey.Trim().Length < 32)))
            return "Nhập khóa webhook riêng có ít nhất 32 ký tự.";
        entity ??= new PropertySePaySettings { PropertyId = propertyId };
        if (db.Entry(entity).State == EntityState.Detached) db.Add(entity);
        if (!string.IsNullOrWhiteSpace(request.ApiToken)) entity.ApiTokenProtected = Protect(request.ApiToken.Trim());
        entity.Enabled = request.Enabled;
        entity.BankId = bank;
        entity.BankAccountNumber = account;
        entity.QrAccountNumber = qrAccount.Length == 0 ? account : qrAccount;
        entity.AccountHolder = request.AccountHolder?.Trim() ?? "";
        entity.SubAccount = sub;
        entity.MemoPrefix = memo;
        entity.WebhookKeyProtected = key ?? "";
        entity.HoldMinutes = request.HoldMinutes;
        entity.SettlementGraceMinutes = request.SettlementGraceMinutes;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return null;
    }

    public static string QrUrl(PropertySePaySettings settings, string code, decimal amount)
    {
        if (amount <= 0 || amount != decimal.Truncate(amount)) throw new ArgumentOutOfRangeException(nameof(amount));
        return "https://vietqr.app/img?acc=" + Uri.EscapeDataString(settings.QrAccountNumber) +
            "&bank=" + Uri.EscapeDataString(settings.BankId) + "&amount=" + amount.ToString("0", System.Globalization.CultureInfo.InvariantCulture) +
            "&des=" + Uri.EscapeDataString((settings.MemoPrefix + " " + code).Trim()) + "&template=compact&showinfo=true&fullacc=true&holder=" + Uri.EscapeDataString(settings.AccountHolder);
    }
}
