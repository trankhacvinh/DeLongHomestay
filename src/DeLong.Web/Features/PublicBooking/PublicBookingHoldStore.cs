using System.Text.Json;
using DeLong.Web.Common.Operations;
using DeLong.Web.Data;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Operations;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.PublicBooking;

public sealed class PublicBookingHoldStore(StoragePaths paths)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string root = Path.Combine(paths.DataRoot, "booking-holds");

    public async Task<DateTime> StartAsync(Guid propertyId, Guid bookingId, TimeSpan duration, CancellationToken cancellationToken = default)
    {
        var expiresAtUtc = DateTime.UtcNow.Add(duration);
        var directory = PropertyDirectory(propertyId);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, bookingId.ToString("N") + ".json");
        var temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            var payload = new HoldFile { BookingId = bookingId, PropertyId = propertyId, ExpiresAtUtc = expiresAtUtc };
            await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(payload, JsonOptions), cancellationToken);
            File.Move(temp, path, true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
        return expiresAtUtc;
    }

    public async Task ReleaseExpiredAsync(AppDbContext db, Guid propertyId, CancellationToken cancellationToken = default)
    {
        var directory = PropertyDirectory(propertyId);
        if (!Directory.Exists(directory)) return;
        var now = DateTime.UtcNow;
        foreach (var path in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            HoldFile? hold;
            try
            {
                var json = await File.ReadAllTextAsync(path, cancellationToken);
                hold = JsonSerializer.Deserialize<HoldFile>(json, JsonOptions);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                continue;
            }

            if (hold is null || hold.PropertyId != propertyId)
            {
                TryDelete(path);
                continue;
            }

            var booking = await db.Bookings.SingleOrDefaultAsync(
                x => x.PropertyId == propertyId && x.Id == hold.BookingId,
                cancellationToken);
            if (booking is null || booking.Status != BookingStatus.Held)
            {
                TryDelete(path);
                continue;
            }

            if (hold.ExpiresAtUtc > now) continue;
            booking.Status = BookingStatus.Requested;
            await db.SaveChangesAsync(cancellationToken);
            TryDelete(path);
            OperationsRealtimeBroker.Shared.Publish(OperationsRealtimeEvent.Create(
                propertyId,
                OperationsEventTypes.BookingHoldExpired,
                booking.Id,
                booking.RoomId));
        }
    }

    public Task CompleteAsync(Guid propertyId, Guid bookingId)
    {
        TryDelete(Path.Combine(PropertyDirectory(propertyId), bookingId.ToString("N") + ".json"));
        return Task.CompletedTask;
    }

    private string PropertyDirectory(Guid propertyId) => Path.Combine(root, propertyId.ToString("N"));

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private sealed class HoldFile
    {
        public Guid BookingId { get; init; }
        public Guid PropertyId { get; init; }
        public DateTime ExpiresAtUtc { get; init; }
    }
}
