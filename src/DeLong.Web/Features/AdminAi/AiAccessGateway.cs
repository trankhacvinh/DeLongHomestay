using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace DeLong.Web.Features.AdminAi;

public sealed record AiAccessDecision(bool IsAllowed, string? Error, PropertyAiProfile? Profile, decimal MonthSpentUsd, decimal AudienceSpentUsd);
public sealed record AiUsageReservationDecision(bool IsAllowed, string? Error, PropertyAiProfile? Profile, Guid? ReservationId);

public sealed class AiAccessGateway(AppDbContext db, IConfiguration? configuration = null)
{
    private static readonly TimeSpan ReservationLifetime = TimeSpan.FromMinutes(5);
    private bool IsGloballyEnabled => configuration?.GetValue("Ai:GloballyEnabled", true) ?? true;

    public async Task<AiAccessDecision> AuthorizeAsync(Guid propertyId, AiAudience audience, CancellationToken ct)
    {
        var profile = await db.PropertyAiProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.PropertyId == propertyId, ct);
        if (!IsGloballyEnabled)
            return await DeniedAsync("Trợ lý AI đang được tạm dừng trên toàn hệ thống.", "access_global_disabled", propertyId, audience, profile, ct);
        if (profile is null || !profile.IsEnabled || string.IsNullOrWhiteSpace(profile.ProtectedApiKey))
            return await DeniedAsync("Trợ lý AI chưa được cấu hình hoặc chưa bật.", "access_not_configured", propertyId, audience, profile, ct);
        if (audience == AiAudience.Customer && !profile.IsPublicAiEnabled)
            return await DeniedAsync("Trợ lý AI dành cho khách hiện đang tắt.", "access_public_disabled", propertyId, audience, profile, ct);

        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var usage = await db.AiUsageRecords.AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.Provider == profile.Provider && x.CreatedAtUtc >= monthStart)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Tokens = g.Sum(x => (long)x.InputTokens + x.OutputTokens),
                Cost = g.Sum(x => x.EstimatedCostUsd),
                AudienceCost = g.Where(x => x.Audience == audience).Sum(x => x.EstimatedCostUsd)
            })
            .SingleOrDefaultAsync(ct);

        var tokens = usage?.Tokens ?? 0;
        var totalCost = usage?.Cost ?? 0;
        var audienceCost = usage?.AudienceCost ?? 0;
        if (profile.MonthlyTokenLimit > 0 && tokens >= profile.MonthlyTokenLimit)
            return await DeniedAsync("Cơ sở đã đạt giới hạn token AI trong tháng.", "access_token_limit", propertyId, audience, profile, ct, totalCost, audienceCost);
        if (profile.MonthlyBudgetUsd > 0 && totalCost >= profile.MonthlyBudgetUsd)
            return await DeniedAsync("Cơ sở đã sử dụng hết ngân sách AI ước tính trong tháng.", "access_budget", propertyId, audience, profile, ct, totalCost, audienceCost);

        if (audience == AiAudience.Customer && profile.MonthlyBudgetUsd > 0)
        {
            var publicBudget = profile.MonthlyBudgetUsd * (100 - profile.AdminBudgetReservePercent) / 100m;
            if (audienceCost >= publicBudget)
                return await DeniedAsync("Ngân sách AI công khai trong tháng đã hết. Vui lòng sử dụng các chức năng thông thường trên website.", "access_public_budget", propertyId, audience, profile, ct, totalCost, audienceCost);
        }

        if (audience == AiAudience.Customer)
        {
            var today = DateTime.UtcNow.Date;
            var publicCallsToday = await db.AiUsageRecords.AsNoTracking().CountAsync(x =>
                x.PropertyId == propertyId && x.Audience == AiAudience.Customer && x.CreatedAtUtc >= today, ct);
            if (publicCallsToday >= profile.PublicRequestsPerDay)
                return await DeniedAsync("AI công khai đã đạt giới hạn lượt trong ngày. Vui lòng thử lại vào ngày mai.", "access_daily_limit", propertyId, audience, profile, ct, totalCost, audienceCost);
        }

        return new(true, null, profile, totalCost, audienceCost);
    }

    public async Task<AiUsageReservationDecision> ReserveProviderCallAsync(
        Guid propertyId,
        AiAudience audience,
        int estimatedInputTokens,
        int maxOutputTokens,
        CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var profile = await db.PropertyAiProfiles
            .FromSqlInterpolated($"SELECT * FROM property_ai_profiles WHERE property_id = {propertyId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (!IsGloballyEnabled)
            return await DeniedReservationAsync("Trợ lý AI đang được tạm dừng trên toàn hệ thống.", "access_global_disabled", propertyId, audience, profile, transaction, ct);
        if (profile is null || !profile.IsEnabled || string.IsNullOrWhiteSpace(profile.ProtectedApiKey))
            return await DeniedReservationAsync("Trợ lý AI chưa được cấu hình hoặc chưa bật.", "access_not_configured", propertyId, audience, profile, transaction, ct);
        if (audience == AiAudience.Customer && !profile.IsPublicAiEnabled)
            return await DeniedReservationAsync("Trợ lý AI dành cho khách hiện đang tắt.", "access_public_disabled", propertyId, audience, profile, transaction, ct);

        var now = DateTime.UtcNow;
        await db.AiUsageReservations
            .Where(x => x.PropertyId == propertyId && x.ExpiresAtUtc <= now)
            .ExecuteDeleteAsync(ct);

        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var actual = await db.AiUsageRecords.AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.Provider == profile.Provider && x.CreatedAtUtc >= monthStart)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Tokens = g.Sum(x => (long)x.InputTokens + x.OutputTokens),
                Cost = g.Sum(x => x.EstimatedCostUsd),
                AudienceCost = g.Where(x => x.Audience == audience).Sum(x => x.EstimatedCostUsd)
            })
            .SingleOrDefaultAsync(ct);
        var reserved = await db.AiUsageReservations.AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.ExpiresAtUtc > now)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Tokens = g.Sum(x => (long)x.ReservedTokens),
                Cost = g.Sum(x => x.ReservedCostUsd),
                AudienceCost = g.Where(x => x.Audience == audience).Sum(x => x.ReservedCostUsd)
            })
            .SingleOrDefaultAsync(ct);

        var inputTokens = Math.Max(0, estimatedInputTokens);
        var outputTokens = Math.Max(0, maxOutputTokens);
        var callTokens = checked(inputTokens + outputTokens);
        var callCost = EstimateCost(inputTokens, outputTokens, profile);
        var totalTokens = (actual?.Tokens ?? 0) + (reserved?.Tokens ?? 0);
        var totalCost = (actual?.Cost ?? 0) + (reserved?.Cost ?? 0);
        var audienceCost = (actual?.AudienceCost ?? 0) + (reserved?.AudienceCost ?? 0);

        if (profile.MonthlyTokenLimit > 0 && totalTokens + callTokens > profile.MonthlyTokenLimit)
        {
            var remainingTokens = Math.Max(0, profile.MonthlyTokenLimit - totalTokens);
            return await DeniedReservationAsync(
                $"Yêu cầu cần giữ tối đa khoảng {callTokens:N0} token nhưng hạn mức {profile.Provider} tháng này chỉ còn {remainingTokens:N0}/{profile.MonthlyTokenLimit:N0} token. Hãy tăng giới hạn token/tháng hoặc giảm token trả lời tối đa.",
                "access_token_limit", propertyId, audience, profile, transaction, ct);
        }
        if (profile.MonthlyBudgetUsd > 0 && totalCost + callCost > profile.MonthlyBudgetUsd)
            return await DeniedReservationAsync("Cơ sở không còn đủ ngân sách AI ước tính cho yêu cầu này.", "access_budget", propertyId, audience, profile, transaction, ct);
        if (audience == AiAudience.Customer && profile.MonthlyBudgetUsd > 0)
        {
            var publicBudget = profile.MonthlyBudgetUsd * (100 - profile.AdminBudgetReservePercent) / 100m;
            if (audienceCost + callCost > publicBudget)
                return await DeniedReservationAsync("Ngân sách AI công khai trong tháng đã hết. Vui lòng sử dụng các chức năng thông thường trên website.", "access_public_budget", propertyId, audience, profile, transaction, ct);
        }
        if (audience == AiAudience.Customer)
        {
            var today = now.Date;
            var completedToday = await db.AiUsageRecords.AsNoTracking().CountAsync(x =>
                x.PropertyId == propertyId && x.Audience == AiAudience.Customer && x.CreatedAtUtc >= today, ct);
            var reservedToday = await db.AiUsageReservations.AsNoTracking().CountAsync(x =>
                x.PropertyId == propertyId && x.Audience == AiAudience.Customer && x.CreatedAtUtc >= today && x.ExpiresAtUtc > now, ct);
            if (completedToday + reservedToday >= profile.PublicRequestsPerDay)
                return await DeniedReservationAsync("AI công khai đã đạt giới hạn lượt trong ngày. Vui lòng thử lại vào ngày mai.", "access_daily_limit", propertyId, audience, profile, transaction, ct);
        }

        var reservation = new AiUsageReservation
        {
            PropertyId = propertyId,
            Audience = audience,
            ReservedTokens = callTokens,
            ReservedCostUsd = callCost,
            ExpiresAtUtc = now.Add(ReservationLifetime)
        };
        db.AiUsageReservations.Add(reservation);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(true, null, profile, reservation.Id);
    }

    public async Task CompleteProviderCallAsync(Guid reservationId, AiUsageRecord usage, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var reservation = await db.AiUsageReservations
            .FromSqlInterpolated($"SELECT * FROM ai_usage_reservations WHERE id = {reservationId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (reservation is not null)
        {
            if (reservation.PropertyId != usage.PropertyId || reservation.Audience != usage.Audience)
                throw new InvalidOperationException("AI usage does not match its reservation.");
            db.AiUsageReservations.Remove(reservation);
        }
        db.AiUsageRecords.Add(usage);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public static int EstimateInputTokens(params string?[] values)
    {
        var characters = values.Where(x => !string.IsNullOrEmpty(x)).Sum(x => (long)x!.Length);
        return (int)Math.Min(int.MaxValue, Math.Max(1024, (characters + 2) / 3));
    }

    public static decimal EstimateCost(int inputTokens, int outputTokens, PropertyAiProfile profile) =>
        Math.Round(inputTokens / 1_000_000m * profile.InputCostPerMillionTokensUsd +
                   outputTokens / 1_000_000m * profile.OutputCostPerMillionTokensUsd, 8, MidpointRounding.AwayFromZero);

    private async Task<AiUsageReservationDecision> DeniedReservationAsync(
        string error,
        string errorCode,
        Guid propertyId,
        AiAudience audience,
        PropertyAiProfile? profile,
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        CancellationToken ct)
    {
        if (profile is not null)
        {
            db.AiUsageRecords.Add(BlockedUsage(propertyId, audience, profile, errorCode));
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return new(false, error, profile, null);
    }

    private async Task<AiAccessDecision> DeniedAsync(
        string error,
        string errorCode,
        Guid propertyId,
        AiAudience audience,
        PropertyAiProfile? profile,
        CancellationToken ct,
        decimal total = 0,
        decimal audienceCost = 0)
    {
        if (profile is not null)
        {
            db.AiUsageRecords.Add(BlockedUsage(propertyId, audience, profile, errorCode));
            await db.SaveChangesAsync(ct);
        }
        return new(false, error, profile, total, audienceCost);
    }

    private static AiUsageRecord BlockedUsage(
        Guid propertyId,
        AiAudience audience,
        PropertyAiProfile profile,
        string errorCode) => new()
    {
        PropertyId = propertyId,
        Audience = audience,
        Provider = profile.Provider,
        Model = profile.Model,
        Operation = "AccessDenied",
        IsSuccess = false,
        ErrorCode = errorCode
    };
}
