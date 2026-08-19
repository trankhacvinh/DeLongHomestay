using DeLong.Web.Common.Operations;
using DeLong.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.PublicBooking;

public sealed class PublicBookingHoldExpiryWorker(
    IServiceScopeFactory scopeFactory,
    StoragePaths storagePaths,
    ILogger<PublicBookingHoldExpiryWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Give startup/migrations a moment to settle before the first sweep.
        try
        {
            await Task.Delay(Interval, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not expire public booking holds.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var propertyIds = await db.Properties.AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var holds = new PublicBookingHoldStore(storagePaths);
        foreach (var propertyId in propertyIds)
            await holds.ReleaseExpiredAsync(db, propertyId, cancellationToken);
    }
}
