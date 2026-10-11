using DeLong.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.DataQuality;

public sealed class DataQualityWorker(IServiceScopeFactory scopes, ILogger<DataQualityWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(30));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var ids = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Properties.AsNoTracking()
                    .Where(x => x.IsActive).Select(x => x.Id).ToListAsync(stoppingToken);
                foreach (var id in ids)
                {
                    try
                    {
                        await using var propertyScope = scopes.CreateAsyncScope();
                        await propertyScope.ServiceProvider.GetRequiredService<DataQualityService>().ScanAsync(id, stoppingToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    { logger.LogError(ex, "Data quality scan failed for property {PropertyId}", id); }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex) { logger.LogError(ex, "Could not run data quality scan"); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
