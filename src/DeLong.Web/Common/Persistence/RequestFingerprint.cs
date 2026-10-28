using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DeLong.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Common.Persistence;

public static class RequestFingerprint
{
    public static string Create(object request) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))));
    public static Task LockAsync(AppDbContext db, string key, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);
}
