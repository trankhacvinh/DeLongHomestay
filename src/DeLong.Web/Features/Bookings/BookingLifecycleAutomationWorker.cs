namespace DeLong.Web.Features.Bookings;

public sealed class BookingLifecycleAutomationWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<BookingLifecycleAutomationWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ProcessAsync(stoppingToken);
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await ProcessAsync(stoppingToken);
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var result = await scope.ServiceProvider
                .GetRequiredService<BookingLifecycleAutomationService>()
                .ProcessDueAsync(DateTime.UtcNow, cancellationToken);
            if (result.CheckedIn > 0 || result.CheckedOut > 0)
                logger.LogInformation(
                    "Automatic booking lifecycle updated {CheckedIn} check-ins and {CheckedOut} check-outs.",
                    result.CheckedIn,
                    result.CheckedOut);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not process automatic booking check-in/check-out.");
        }
    }
}
