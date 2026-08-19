using System.Collections.Concurrent;
using System.Text.Json;
using DeLong.Web.Common.Operations;

namespace DeLong.Web.Features.PublicBooking;

public sealed record BookingGuestDetailsDto(
    int GuestCount,
    bool PolicyAccepted,
    int? PolicyVersion,
    DateTime? PolicyAcceptedAtUtc);

public sealed class UpdateAdminBookingGuestDetailsRequest
{
    public string? CustomerEmail { get; init; }
    public int GuestCount { get; init; } = 1;
}

public sealed class BookingGuestDetailsStore(StoragePaths paths)
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly string root = Path.Combine(paths.DataRoot, "private", "booking-details");

    public async Task<BookingGuestDetailsDto?> GetAsync(
        Guid propertyId,
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        var path = PathFor(propertyId, bookingId);
        if (!File.Exists(path)) return null;

        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var stored = await JsonSerializer.DeserializeAsync<StoredBookingGuestDetails>(stream, JsonOptions, cancellationToken);
            if (stored is null) return null;
            return new BookingGuestDetailsDto(
                Math.Max(1, stored.GuestCount),
                stored.PolicyAccepted,
                stored.PolicyVersion is > 0 ? stored.PolicyVersion : null,
                stored.PolicyAcceptedAtUtc);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task SaveAsync(
        Guid propertyId,
        Guid bookingId,
        BookingGuestDetailsDto details,
        CancellationToken cancellationToken = default)
    {
        var path = PathFor(propertyId, bookingId);
        var gate = Gates.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
            var stored = new StoredBookingGuestDetails
            {
                GuestCount = Math.Max(1, details.GuestCount),
                PolicyAccepted = details.PolicyAccepted,
                PolicyVersion = details.PolicyVersion is > 0 ? details.PolicyVersion : null,
                PolicyAcceptedAtUtc = details.PolicyAcceptedAtUtc
            };
            try
            {
                await using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
                    await JsonSerializer.SerializeAsync(stream, stored, JsonOptions, cancellationToken);
                File.Move(temp, path, true);
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private string PathFor(Guid propertyId, Guid bookingId) =>
        Path.Combine(root, propertyId.ToString("N"), bookingId.ToString("N") + ".json");

    private sealed class StoredBookingGuestDetails
    {
        public int GuestCount { get; init; } = 1;
        public bool PolicyAccepted { get; init; }
        public int? PolicyVersion { get; init; }
        public DateTime? PolicyAcceptedAtUtc { get; init; }
    }
}
