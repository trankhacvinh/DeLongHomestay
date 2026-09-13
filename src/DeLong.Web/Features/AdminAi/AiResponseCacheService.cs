using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.AdminAi;

public sealed class AiResponseCacheService(AppDbContext db)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string CreateKey(Guid propertyId, AiAudience audience, string intent, object? parameters, long dataVersion)
    {
        var normalizedIntent = NormalizeIntent(intent);
        var parametersJson = CanonicalJson(parameters);
        var source = $"{propertyId:N}|{audience}|{normalizedIntent}|{dataVersion}|{parametersJson}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant();
    }

    public async Task<string?> GetAsync(Guid propertyId, AiAudience audience, string cacheKey, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var row = await db.AiResponseCache.SingleOrDefaultAsync(x => x.PropertyId == propertyId && x.Audience == audience && x.CacheKey == cacheKey && x.ExpiresAtUtc > now, ct);
        if (row is null) return null;
        row.HitCount++;
        row.LastHitAtUtc = now;
        row.UpdatedAtUtc = now;
        await db.SaveChangesAsync(ct);
        return row.ResponseJson;
    }

    public async Task SetAsync(Guid propertyId, AiAudience audience, string intent, object? parameters, string responseJson,
        string invalidationTag, long dataVersion, TimeSpan ttl, CancellationToken ct)
    {
        var key = CreateKey(propertyId, audience, intent, parameters, dataVersion);
        var row = await db.AiResponseCache.SingleOrDefaultAsync(x => x.PropertyId == propertyId && x.Audience == audience && x.CacheKey == key, ct);
        row ??= new AiResponseCache { PropertyId = propertyId, Audience = audience, CacheKey = key };
        if (db.Entry(row).State == EntityState.Detached) db.AiResponseCache.Add(row);
        row.Intent = NormalizeIntent(intent);
        row.ParametersJson = CanonicalJson(parameters);
        row.ResponseJson = responseJson;
        row.InvalidationTag = invalidationTag.Trim().ToLowerInvariant();
        row.DataVersion = dataVersion;
        row.ExpiresAtUtc = DateTime.UtcNow.Add(ttl);
        row.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public Task<int> InvalidateAsync(Guid propertyId, string invalidationTag, CancellationToken ct) =>
        db.AiResponseCache.Where(x => x.PropertyId == propertyId && x.InvalidationTag == invalidationTag.Trim().ToLowerInvariant())
            .ExecuteDeleteAsync(ct);

    public static string NormalizeIntent(string value) => string.Join(' ', value.Trim().ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string CanonicalJson(object? value)
    {
        if (value is null) return "{}";
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(value, Json));
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) WriteCanonical(writer, document.RootElement);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var property in element.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal))
            {
                writer.WritePropertyName(property.Name);
                WriteCanonical(writer, property.Value);
            }
            writer.WriteEndObject();
            return;
        }
        if (element.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (var item in element.EnumerateArray()) WriteCanonical(writer, item);
            writer.WriteEndArray();
            return;
        }
        element.WriteTo(writer);
    }
}
