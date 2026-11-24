using DeLong.Web.Common.Auditing;
using DeLong.Web.Data;
using DeLong.Web.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.PublicBooking;

/// <summary>
/// Nghị định 13/2023: chỉ giữ ảnh CCCD trong thời gian cần thiết. Khi bật
/// <c>IdentityDocuments:RetentionDaysAfterCheckout</c> (số ngày &gt; 0), job này xóa ảnh CCCD của booking đã kết thúc
/// (hoàn tất, hủy, không đến) quá số ngày đó sau giờ trả phòng, và ghi audit "IdentityPurged".
/// Mặc định tắt (0): chủ cơ sở tự chọn thời hạn và cập nhật chính sách bảo mật trước khi bật.
/// Ảnh khách tự lưu trong tài khoản (để dùng lại lần sau) không bị xóa ở đây.
/// </summary>
public sealed class IdentityRetentionWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<IdentityRetentionWorker> logger) : BackgroundService
{
    private static readonly BookingStatus[] EndedStatuses = [BookingStatus.Completed, BookingStatus.Cancelled, BookingStatus.NoShow];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(6));
        do
        {
            try
            {
                var days = configuration.GetValue<int?>("IdentityDocuments:RetentionDaysAfterCheckout") ?? 0;
                if (days > 0) await PurgeAsync(TimeSpan.FromDays(days), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { logger.LogError(ex, "Could not purge expired identity documents."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task<int> PurgeAsync(TimeSpan retention, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var storage = scope.ServiceProvider.GetRequiredService<IdentityDocumentStorage>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cutoff = DateTime.UtcNow - retention;
        var purged = 0;
        foreach (var batch in storage.EnumerateBookingFolders().Chunk(200))
        {
            var ids = batch.Select(x => x.BookingId).ToArray();
            var due = await db.Bookings.AsNoTracking()
                .Where(x => ids.Contains(x.Id) && EndedStatuses.Contains(x.Status) && x.CheckOutUtc < cutoff)
                .Select(x => new { x.Id, x.PropertyId })
                .ToListAsync(cancellationToken);
            foreach (var booking in due)
            {
                await storage.DeleteAsync(booking.PropertyId, booking.Id, cancellationToken);
                new AuditService(db).Add(booking.PropertyId, "Booking", booking.Id, "IdentityPurged", null,
                    after: new { RetentionDays = (int)retention.TotalDays });
                purged++;
            }
            if (due.Count > 0) await db.SaveChangesAsync(cancellationToken);
        }
        if (purged > 0) logger.LogInformation("Purged identity documents of {Count} ended bookings.", purged);
        return purged;
    }
}
