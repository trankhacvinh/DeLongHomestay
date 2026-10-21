using System.Text.Json;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Common.Auditing;

public sealed record AuditLogDto(
    Guid Id,
    string EntityType,
    Guid EntityId,
    string Action,
    Guid? ActorUserId,
    string? ActorName,
    DateTime CreatedAtUtc,
    string? BeforeJson,
    string? AfterJson);

public sealed class AuditService(AppDbContext db)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Add(
        Guid propertyId,
        string entityType,
        Guid entityId,
        string action,
        Guid? actorUserId,
        object? before = null,
        object? after = null)
    {
        db.AuditLogs.Add(new AuditLog
        {
            PropertyId = propertyId,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            ActorUserId = actorUserId,
            BeforeJson = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
            AfterJson = after is null ? null : JsonSerializer.Serialize(after, JsonOptions)
        });
    }

    public async Task<IReadOnlyList<AuditLogDto>> GetEntityHistoryAsync(
        Guid propertyId,
        string entityType,
        Guid entityId,
        CancellationToken cancellationToken = default,
        int skip = 0)
    {
        var query =
            from log in db.AuditLogs.AsNoTracking()
            join user in db.Users.AsNoTracking()
                on log.ActorUserId equals (Guid?)user.Id into users
            from user in users.DefaultIfEmpty()
            where log.PropertyId == propertyId && log.EntityType == entityType && log.EntityId == entityId
            orderby log.CreatedAtUtc descending, log.Id descending
            select new AuditLogDto(
                log.Id,
                log.EntityType,
                log.EntityId,
                log.Action,
                log.ActorUserId,
                user == null ? null : (user.DisplayName ?? user.Email ?? user.UserName),
                log.CreatedAtUtc,
                log.BeforeJson,
                log.AfterJson);

        return await query.Skip(Math.Clamp(skip, 0, 100000)).Take(50).ToListAsync(cancellationToken);
    }
    public async Task<object> GetBookingActivityAsync(Guid propertyId, Guid bookingId, CancellationToken ct = default)
    {
        var query = from log in db.AuditLogs.AsNoTracking()
                    join user in db.Users.AsNoTracking() on log.ActorUserId equals (Guid?)user.Id into users
                    from user in users.DefaultIfEmpty()
                    where log.PropertyId == propertyId && log.EntityType == "Booking" && log.EntityId == bookingId
                    select new { log.Id, log.Action, log.ActorUserId, ActorName = user == null ? null : user.DisplayName ?? user.UserName, log.CreatedAtUtc };
        var created = await query.Where(x => x.Action == "Created").OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id).FirstOrDefaultAsync(ct);
        var latest = await query.OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        return new { Created = created, Latest = latest };
    }
}
