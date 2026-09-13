using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.AdminAi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Xunit;

namespace DeLong.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class AiCachePersistenceTests
{
    [PricingPostgreSqlFact]
    public async Task Public_knowledge_snapshot_is_property_scoped_and_excludes_customer_and_secret_data()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION"))
            .Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var propertyA = new Property { Code = $"PUB-A-{suffix}", Name = $"Cơ sở A {suffix}" };
        var propertyB = new Property { Code = $"PUB-B-{suffix}", Name = $"Cơ sở B bí mật {suffix}" };
        var roomA = new Room { Property = propertyA, Code = $"RA-{suffix}", Name = $"Phòng A {suffix}", IsActive = true, IsPublished = true };
        var roomB = new Room { Property = propertyB, Code = $"RB-{suffix}", Name = $"Phòng B bí mật {suffix}", IsActive = true, IsPublished = true };
        var customerSecret = $"CCCD-SECRET-{suffix}";
        db.AddRange(propertyA, propertyB, roomA, roomB,
            new Customer
            {
                Property = propertyA, Name = $"Khách bí mật {suffix}", Phone = $"PHONE-{suffix}",
                NormalizedPhone = $"PHONE-{suffix}", Email = $"secret-{suffix}@example.test", IdentityNumber = customerSecret
            });
        await db.SaveChangesAsync();

        await new AiKnowledgeSnapshotService(db).RebuildAsync(propertyA.Id, default);
        var json = (await db.PropertyAiKnowledgeSnapshots.AsNoTracking()
            .SingleAsync(x => x.PropertyId == propertyA.Id)).ContentJson;

        Assert.Contains(roomA.Name, json, StringComparison.Ordinal);
        Assert.DoesNotContain(propertyB.Name, json, StringComparison.Ordinal);
        Assert.DoesNotContain(roomB.Name, json, StringComparison.Ordinal);
        Assert.DoesNotContain(customerSecret, json, StringComparison.Ordinal);
        Assert.DoesNotContain($"PHONE-{suffix}", json, StringComparison.Ordinal);
        Assert.DoesNotContain($"secret-{suffix}@example.test", json, StringComparison.Ordinal);
    }

    [PricingPostgreSqlFact]
    public async Task Persistent_cache_survives_new_context_and_rate_change_invalidates_cache_and_snapshot()
    {
        var connectionString = Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION");
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
        Guid propertyId;
        Guid rateId;
        string cacheKey;
        long snapshotVersion;

        await using (var setup = new AppDbContext(options))
        {
            await setup.Database.MigrateAsync();
            var suffix = Guid.NewGuid().ToString("N")[..10];
            var property = new Property { Code = $"AIC-{suffix}", Name = $"AI cache {suffix}" };
            var room = new Room
            {
                Property = property, Code = $"R-{suffix}", Name = "Phòng cache",
                IsActive = true, IsPublished = true
            };
            var rate = new RoomRate
            {
                Room = room, Name = "Khung 1", StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(13, 0),
                Price = 250000, IsActive = true
            };
            setup.AddRange(property, room, rate);
            await setup.SaveChangesAsync();
            propertyId = property.Id;
            rateId = rate.Id;

            var snapshot = await new AiKnowledgeSnapshotService(setup).RebuildAsync(propertyId, default);
            snapshotVersion = snapshot.Version;
            var parameters = new { date = "2026-09-12", room = room.Code };
            cacheKey = AiResponseCacheService.CreateKey(propertyId, AiAudience.Customer, "quote", parameters, snapshot.Version);
            await new AiResponseCacheService(setup).SetAsync(propertyId, AiAudience.Customer, "quote", parameters,
                "{\"total\":250000}", "pricing", snapshot.Version, TimeSpan.FromHours(1), default);
        }

        await using (var restarted = new AppDbContext(options))
        {
            var cached = await new AiResponseCacheService(restarted).GetAsync(propertyId, AiAudience.Customer, cacheKey, default);
            Assert.Equal(250000, JsonDocument.Parse(cached!).RootElement.GetProperty("total").GetInt32());
        }

        var interceptor = new AiDataInvalidationInterceptor(NullLogger<AiDataInvalidationInterceptor>.Instance);
        var mutationOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .AddInterceptors(interceptor)
            .Options;
        await using (var mutation = new AppDbContext(mutationOptions))
        {
            var rate = await mutation.RoomRates.SingleAsync(x => x.Id == rateId);
            rate.Price = 300000;
            await mutation.SaveChangesAsync();
        }

        await using var verification = new AppDbContext(options);
        Assert.Null(await new AiResponseCacheService(verification).GetAsync(propertyId, AiAudience.Customer, cacheKey, default));
        var snapshotAfter = await verification.PropertyAiKnowledgeSnapshots.AsNoTracking().SingleAsync(x => x.PropertyId == propertyId);
        Assert.True(snapshotAfter.IsDirty);
        Assert.Equal(snapshotVersion + 1, snapshotAfter.Version);
    }
}
