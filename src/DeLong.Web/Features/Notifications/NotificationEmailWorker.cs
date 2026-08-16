using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Notifications;

public sealed class NotificationEmailWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<NotificationEmailWorker> logger) : BackgroundService
{
    private static readonly int[] RetryMinutes = [1, 2, 5, 15, 30, 60, 180, 360];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await ProcessBatchAsync(stoppingToken);
                await Task.Delay(processed > 0 ? TimeSpan.FromSeconds(2) : TimeSpan.FromSeconds(10), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Notification email worker batch failed.");
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            }
        }
    }

    private async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settingsService = scope.ServiceProvider.GetRequiredService<NotificationSettingsService>();
        var sender = scope.ServiceProvider.GetRequiredService<NotificationEmailSender>();
        var now = DateTime.UtcNow;
        var batch = await db.Set<NotificationEmailOutbox>()
            .Where(x => x.SentAtUtc == null && x.AttemptCount < RetryMinutes.Length && x.NextAttemptAtUtc <= now)
            .OrderBy(x => x.NextAttemptAtUtc)
            .Take(10)
            .ToListAsync(cancellationToken);

        foreach (var outbox in batch)
        {
            var settings = await db.Set<PropertyNotificationSettings>()
                .SingleOrDefaultAsync(x => x.PropertyId == outbox.PropertyId, cancellationToken);
            var (profile, _, profileError) = await settingsService.GetDeliveryProfileAsync(outbox.PropertyId, true, cancellationToken);
            if (profileError?.Code == "email_disabled")
            {
                outbox.NextAttemptAtUtc = DateTime.UtcNow.AddMinutes(5);
                await db.SaveChangesAsync(cancellationToken);
                continue;
            }
            if (profileError is not null || profile is null)
            {
                RecordFailure(outbox, settings, profileError?.Message ?? "Không thể đọc cấu hình SMTP.");
                await db.SaveChangesAsync(cancellationToken);
                continue;
            }

            try
            {
                var recipients = outbox.ToRecipients.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                await sender.SendAsync(profile, recipients, outbox.Subject, outbox.BodyText, cancellationToken);
                outbox.SentAtUtc = DateTime.UtcNow;
                outbox.LastError = null;
                if (settings is not null)
                {
                    settings.LastEmailSentAtUtc = outbox.SentAtUtc;
                    settings.LastEmailError = null;
                    settings.LastEmailErrorAtUtc = null;
                }
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "SMTP delivery failed for notification outbox {OutboxId}.", outbox.Id);
                RecordFailure(outbox, settings, ex.Message);
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        return batch.Count;
    }

    private static void RecordFailure(NotificationEmailOutbox outbox, PropertyNotificationSettings? settings, string error)
    {
        outbox.AttemptCount += 1;
        outbox.LastError = Truncate(error, 2000);
        var index = Math.Clamp(outbox.AttemptCount - 1, 0, RetryMinutes.Length - 1);
        outbox.NextAttemptAtUtc = DateTime.UtcNow.AddMinutes(RetryMinutes[index]);
        if (settings is not null)
        {
            settings.LastEmailError = outbox.LastError;
            settings.LastEmailErrorAtUtc = DateTime.UtcNow;
        }
    }

    private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];
}
