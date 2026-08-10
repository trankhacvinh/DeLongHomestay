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
        return await db.AuditLogs
            .AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.EntityType == entityType && x.EntityId == entityId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new AuditLogDto(
                x.Id,
                x.EntityType,
                x.EntityId,
                x.Action,
                x.ActorUserId,
                x.CreatedAtUtc,
                x.BeforeJson,
                x.AfterJson))
            .ToListAsync(cancellationToken);
    }
}
