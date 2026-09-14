using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.AdminAi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DeLong.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class AiAccessGatewayConcurrencyTests
{
    [PricingPostgreSqlFact]
    public async Task Concurrent_requests_cannot_both_reserve_the_last_token_quota()
    {
        var options = await CreateOptionsAsync();
        var propertyId = await CreateProfileAsync(options, monthlyTokenLimit: 1000);

        var decisions = await Task.WhenAll(
            ReserveAsync(options, propertyId, 200, 600),
            ReserveAsync(options, propertyId, 200, 600));

        Assert.Single(decisions, x => x.IsAllowed);
        Assert.Single(decisions, x => !x.IsAllowed && x.Error!.Contains("token", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(decisions, x => !x.IsAllowed && x.Error!.Contains("1,000", StringComparison.Ordinal));
        await AssertSingleReservationAsync(options, propertyId, 800);
    }

    [PricingPostgreSqlFact]
    public async Task Concurrent_requests_cannot_both_reserve_the_last_budget_amount()
    {
        var options = await CreateOptionsAsync();
        var propertyId = await CreateProfileAsync(options, monthlyBudgetUsd: 0.0015m, inputCost: 1m, outputCost: 1m);

        var decisions = await Task.WhenAll(
            ReserveAsync(options, propertyId, 400, 400),
            ReserveAsync(options, propertyId, 400, 400));

        Assert.Single(decisions, x => x.IsAllowed);
        Assert.Single(decisions, x => !x.IsAllowed && x.Error!.Contains("ngân sách", StringComparison.OrdinalIgnoreCase));
        await AssertSingleReservationAsync(options, propertyId, 800);
    }

    [PricingPostgreSqlFact]
    public async Task Completing_failed_provider_call_releases_reservation_and_records_failure()
    {
        var options = await CreateOptionsAsync();
        var propertyId = await CreateProfileAsync(options, monthlyTokenLimit: 1000);
        Guid reservationId;
        await using (var reserveDb = new AppDbContext(options))
        {
            var decision = await new AiAccessGateway(reserveDb).ReserveProviderCallAsync(
                propertyId, AiAudience.Admin, 200, 600, default);
            Assert.True(decision.IsAllowed);
            reservationId = decision.ReservationId!.Value;
        }

        await using (var completeDb = new AppDbContext(options))
        {
            await new AiAccessGateway(completeDb).CompleteProviderCallAsync(reservationId, new AiUsageRecord
            {
                PropertyId = propertyId,
                Audience = AiAudience.Admin,
                Provider = AiProviderKind.OpenAi,
                Model = "test-model",
                Operation = "Chat",
                IsSuccess = false,
                ErrorCode = "provider_unavailable"
            }, default);
        }

        await using var verifyDb = new AppDbContext(options);
        Assert.False(await verifyDb.AiUsageReservations.AnyAsync(x => x.PropertyId == propertyId));
        var usage = await verifyDb.AiUsageRecords.AsNoTracking().SingleAsync(x => x.PropertyId == propertyId);
        Assert.False(usage.IsSuccess);
        Assert.Equal("provider_unavailable", usage.ErrorCode);
    }

    [PricingPostgreSqlFact]
    public async Task Switching_provider_uses_that_provider_monthly_quota_instead_of_previous_provider_usage()
    {
        var options = await CreateOptionsAsync();
        var propertyId = await CreateProfileAsync(options, monthlyTokenLimit: 1000, provider: AiProviderKind.DeepSeek);
        await using (var seedDb = new AppDbContext(options))
        {
            seedDb.AiUsageRecords.Add(new AiUsageRecord
            {
                PropertyId = propertyId,
                Audience = AiAudience.Admin,
                Provider = AiProviderKind.Gemini,
                Model = "gemini-test",
                Operation = "Chat",
                InputTokens = 900,
                OutputTokens = 100,
                IsSuccess = true
            });
            await seedDb.SaveChangesAsync();
        }

        await using var reserveDb = new AppDbContext(options);
        var decision = await new AiAccessGateway(reserveDb).ReserveProviderCallAsync(
            propertyId, AiAudience.Admin, 200, 600, default);

        Assert.True(decision.IsAllowed);
        Assert.Equal(AiProviderKind.DeepSeek, decision.Profile!.Provider);
    }

    [PricingPostgreSqlFact]
    public async Task Global_kill_switch_blocks_admin_and_customer_without_creating_reservations()
    {
        var options = await CreateOptionsAsync();
        var propertyId = await CreateProfileAsync(options);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Ai:GloballyEnabled"] = "false" })
            .Build();
        await using var db = new AppDbContext(options);
        var gateway = new AiAccessGateway(db, configuration);

        var admin = await gateway.ReserveProviderCallAsync(propertyId, AiAudience.Admin, 100, 100, default);
        var customer = await gateway.AuthorizeAsync(propertyId, AiAudience.Customer, default);

        Assert.False(admin.IsAllowed);
        Assert.False(customer.IsAllowed);
        Assert.Contains("toàn hệ thống", admin.Error);
        Assert.False(await db.AiUsageReservations.AnyAsync(x => x.PropertyId == propertyId));
        Assert.Equal(2, await db.AiUsageRecords.CountAsync(x => x.PropertyId == propertyId && x.ErrorCode == "access_global_disabled"));
    }

    private static async Task<DbContextOptions<AppDbContext>> CreateOptionsAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION"))
            .Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();
        return options;
    }

    private static async Task<Guid> CreateProfileAsync(
        DbContextOptions<AppDbContext> options,
        int monthlyTokenLimit = 0,
        decimal monthlyBudgetUsd = 0,
        decimal inputCost = 0,
        decimal outputCost = 0,
        AiProviderKind provider = AiProviderKind.OpenAi)
    {
        await using var db = new AppDbContext(options);
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var property = new Property { Code = $"AIQ-{suffix}", Name = $"AI quota {suffix}" };
        db.Properties.Add(property);
        db.PropertyAiProfiles.Add(new PropertyAiProfile
        {
            Property = property,
            IsEnabled = true,
            Provider = provider,
            ProtectedApiKey = "integration-test-key",
            MonthlyTokenLimit = monthlyTokenLimit,
            MonthlyBudgetUsd = monthlyBudgetUsd,
            InputCostPerMillionTokensUsd = inputCost,
            OutputCostPerMillionTokensUsd = outputCost
        });
        await db.SaveChangesAsync();
        return property.Id;
    }

    private static async Task<AiUsageReservationDecision> ReserveAsync(
        DbContextOptions<AppDbContext> options,
        Guid propertyId,
        int inputTokens,
        int outputTokens)
    {
        await using var db = new AppDbContext(options);
        return await new AiAccessGateway(db).ReserveProviderCallAsync(
            propertyId, AiAudience.Admin, inputTokens, outputTokens, default);
    }

    private static async Task AssertSingleReservationAsync(
        DbContextOptions<AppDbContext> options,
        Guid propertyId,
        int expectedTokens)
    {
        await using var db = new AppDbContext(options);
        var reservation = await db.AiUsageReservations.AsNoTracking().SingleAsync(x => x.PropertyId == propertyId);
        Assert.Equal(expectedTokens, reservation.ReservedTokens);
    }
}
