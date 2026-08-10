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
        CancellationToken cancellationToken = default)
    {
        var query =
            from log in db.AuditLogs.AsNoTracking()
            join user in db.Users.AsNoTracking()
                on log.ActorUserId equals (Guid?)user.Id into users
            from user in users.DefaultIfEmpty()
            where log.PropertyId == propertyId && log.EntityType == entityType && log.EntityId == entityId
            orderby log.CreatedAtUtc descending
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

        return await query.ToListAsync(cancellationToken);
    }
}
