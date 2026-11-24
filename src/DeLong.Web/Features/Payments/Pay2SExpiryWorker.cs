namespace DeLong.Web.Features.Payments;

public sealed class Pay2SExpiryWorker(IServiceScopeFactory scopeFactory, ILogger<Pay2SExpiryWorker> logger) : BackgroundService
{
    // Submit creates the QR a few seconds after the booking; 15 minutes is far beyond any real delay.
    private static readonly TimeSpan AbandonedHoldGrace = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var payments = scope.ServiceProvider.GetRequiredService<Pay2SService>();
                await payments.ExpirePendingAsync(stoppingToken);
                await payments.CancelAbandonedWebHoldsAsync(AbandonedHoldGrace, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { logger.LogError(ex, "Could not expire pending Pay2S payment intents."); }
        }
    }
}
