using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.AdminAi;

public sealed class AdminAiSettingsService(AppDbContext db, AiCredentialProtector protector)
{
    public async Task<AiProfileDto> GetAsync(Guid propertyId, CancellationToken ct = default)
    {
        var x = await db.PropertyAiProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.PropertyId == propertyId, ct);
        return x is null ? new(false, Domain.Enums.AiProviderKind.OpenAi, "gpt-5-mini", false, 2000, 0) : ToDto(x);
    }

    public async Task<(AiProfileDto? Value, string? Error)> SaveAsync(Guid propertyId, SaveAiProfileRequest request, Guid userId, CancellationToken ct)
    {
        if (!Enum.IsDefined(request.Provider)) return (null, "Nhà cung cấp không hợp lệ.");
        var model = request.Model?.Trim() ?? string.Empty;
        if (model.Length is < 2 or > 120) return (null, "Tên model phải từ 2 đến 120 ký tự.");
        if (request.MaxOutputTokens is < 128 or > 32000 || request.MonthlyTokenLimit < 0) return (null, "Giới hạn token không hợp lệ.");
        var x = await db.PropertyAiProfiles.SingleOrDefaultAsync(p => p.PropertyId == propertyId, ct);
        if (x is null) { x = new PropertyAiProfile { PropertyId = propertyId }; db.PropertyAiProfiles.Add(x); }
        if (request.ClearApiKey) x.ProtectedApiKey = string.Empty;
        else if (!string.IsNullOrWhiteSpace(request.ApiKey)) x.ProtectedApiKey = protector.Protect(request.ApiKey);
        if (request.IsEnabled && string.IsNullOrWhiteSpace(x.ProtectedApiKey)) return (null, "Cần nhập API key trước khi bật trợ lý.");
        x.IsEnabled = request.IsEnabled; x.Provider = request.Provider; x.Model = model;
        x.MaxOutputTokens = request.MaxOutputTokens; x.MonthlyTokenLimit = request.MonthlyTokenLimit;
        x.UpdatedByUserId = userId; x.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return (ToDto(x), null);
    }

    public string ReadKey(PropertyAiProfile profile) => protector.Unprotect(profile.ProtectedApiKey);
    private static AiProfileDto ToDto(PropertyAiProfile x) => new(x.IsEnabled, x.Provider, x.Model, !string.IsNullOrWhiteSpace(x.ProtectedApiKey), x.MaxOutputTokens, x.MonthlyTokenLimit);
}
