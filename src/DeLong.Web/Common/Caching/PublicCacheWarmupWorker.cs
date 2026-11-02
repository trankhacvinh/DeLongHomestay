using DeLong.Web.Features.PublicRooms;
using DeLong.Web.Features.Site;

namespace DeLong.Web.Common.Caching;

/// <summary>
/// Keeps the public read cache warm so guests do not pay for a cold load.
/// The site has little traffic, so most visits used to arrive after the public entries had expired
/// and the home page waited ~2 s for the database. This worker reads the same entries the home page
/// and public layout use, at startup and then every few seconds. A read of a fresh entry is a memory
/// lookup; when an entry has expired (TTL or an admin edit), the worker pays for the reload instead of a guest.
/// </summary>
public sealed class PublicCacheWarmupWorker(
    IServiceScopeFactory scopeFactory,
    TimeSpan interval,
    ILogger<PublicCacheWarmupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var failing = false;
        using var timer = new PeriodicTimer(interval);
        do
        {
            try
            {
                await WarmAsync(stoppingToken);
                if (failing) logger.LogInformation("Public cache warm-up recovered.");
                failing = false;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                // Before first-run setup the schema can be missing; log once per failure streak, not every tick.
                if (!failing) logger.LogWarning(ex, "Public cache warm-up failed; guests will load public content from the database.");
                failing = true;
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task WarmAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var site = services.GetRequiredService<SiteContentService>();
        await site.GetPublicThemeAsync(ct);
        await site.GetGlobalPublicSectionsAsync(ct);
        await site.GetPublicContactsAsync(ct);
        await services.GetRequiredService<PublicPropertyResolver>().GetActiveAsync(ct);
        // Loads every active property's room catalog as well.
        await services.GetRequiredService<PublicRoomContentService>().GetGlobalCatalogAsync(ct);
        await services.GetRequiredService<GlobalEditorialShowcaseService>().GetPublicAsync(ct);
    }
}
