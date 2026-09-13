using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace DeLong.Web.Features.PublicAi;

public sealed class PublicAiLookupRateGuard
{
    private const int MaximumAttempts = 5;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(10);
    private readonly ConcurrentDictionary<string, AttemptWindow> attempts = new(StringComparer.Ordinal);

    public bool TryAcquire(Guid propertyId, string? clientAddress, DateTime nowUtc)
    {
        var clientKey = Hash($"{propertyId:N}:{clientAddress ?? "unknown"}");
        while (true)
        {
            if (!attempts.TryGetValue(clientKey, out var current))
            {
                if (attempts.TryAdd(clientKey, new AttemptWindow(nowUtc, 1))) return true;
                continue;
            }

            var next = nowUtc - current.StartedAtUtc >= Window
                ? new AttemptWindow(nowUtc, 1)
                : current with { Count = current.Count + 1 };
            if (!attempts.TryUpdate(clientKey, next, current)) continue;
            CleanupExpired(nowUtc);
            return next.Count <= MaximumAttempts;
        }
    }

    private void CleanupExpired(DateTime nowUtc)
    {
        if (attempts.Count < 1_000) return;
        foreach (var item in attempts.Where(x => nowUtc - x.Value.StartedAtUtc >= Window))
            attempts.TryRemove(item.Key, out _);
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private sealed record AttemptWindow(DateTime StartedAtUtc, int Count);
}
