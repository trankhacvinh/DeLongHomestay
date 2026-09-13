using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DeLong.Web.Features.AdminAi;

public sealed class AiDataInvalidationInterceptor(ILogger<AiDataInvalidationInterceptor> logger) : SaveChangesInterceptor
{
    private readonly Dictionary<Guid, HashSet<string>> pending = [];
    private readonly Dictionary<Guid, HashSet<string>> pendingRooms = [];

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        await InvalidateAsync(eventData.Context, cancellationToken);
        return result;
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        InvalidateAsync(eventData.Context, CancellationToken.None).GetAwaiter().GetResult();
        return result;
    }

    private async Task InvalidateAsync(DbContext? context, CancellationToken cancellationToken)
    {
        if (context is AppDbContext db)
        {
            try
            {
                if (pendingRooms.Count > 0)
                {
                    var roomProperties = await db.Rooms.AsNoTracking().Where(x => pendingRooms.Keys.Contains(x.Id))
                        .Select(x => new { x.Id, x.PropertyId }).ToListAsync(cancellationToken);
                    foreach (var room in roomProperties)
                    {
                        if (!pending.TryGetValue(room.PropertyId, out var propertyTags)) pending[room.PropertyId] = propertyTags = [];
                        propertyTags.UnionWith(pendingRooms[room.Id]);
                    }
                }
                foreach (var (propertyId, tags) in pending)
                {
                    await db.PropertyAiKnowledgeSnapshots.Where(x => x.PropertyId == propertyId)
                        .ExecuteUpdateAsync(setters => setters
                            .SetProperty(x => x.IsDirty, true)
                            .SetProperty(x => x.Version, x => x.Version + 1)
                            .SetProperty(x => x.UpdatedAtUtc, DateTime.UtcNow), cancellationToken);
                    await db.AiResponseCache.Where(x => x.PropertyId == propertyId && tags.Contains(x.InvalidationTag)).ExecuteDeleteAsync(cancellationToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "AI cache invalidation failed after a successful business write.");
            }
        }
        pending.Clear();
        pendingRooms.Clear();
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        pending.Clear();
        pendingRooms.Clear();
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        pending.Clear();
        pendingRooms.Clear();
        return Task.CompletedTask;
    }

    private void Capture(DbContext? context)
    {
        pending.Clear();
        pendingRooms.Clear();
        if (context is null) return;
        foreach (var entry in context.ChangeTracker.Entries().Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            if (!TryPropertyId(entry.Entity, out var propertyId))
            {
                var roomId = entry.Entity switch { RoomRate rate => rate.RoomId, RoomAmenity amenity => amenity.RoomId, _ => Guid.Empty };
                if (roomId == Guid.Empty) continue;
                if (!pendingRooms.TryGetValue(roomId, out var roomTags)) pendingRooms[roomId] = roomTags = [];
                roomTags.UnionWith(Tags(entry.Entity));
                continue;
            }
            foreach (var tag in Tags(entry.Entity))
            {
                if (!pending.TryGetValue(propertyId, out var tags)) pending[propertyId] = tags = [];
                tags.Add(tag);
            }
        }
    }

    private static bool TryPropertyId(object entity, out Guid propertyId)
    {
        var value = entity.GetType().GetProperty("PropertyId")?.GetValue(entity);
        propertyId = value is Guid id ? id : Guid.Empty;
        if (propertyId == Guid.Empty && entity is RoomRate { Room: { PropertyId: var roomPropertyId } }) propertyId = roomPropertyId;
        if (propertyId == Guid.Empty && entity is RoomAmenity { Room: { PropertyId: var amenityPropertyId } }) propertyId = amenityPropertyId;
        if (propertyId == Guid.Empty && entity is Property property) propertyId = property.Id;
        return propertyId != Guid.Empty;
    }

    private static IEnumerable<string> Tags(object entity) => entity switch
    {
        Property or Room or Amenity or RoomAmenity or PropertySiteSettings => ["knowledge", "room-search"],
        RoomRate or PropertyPricingSettings or SpecialPricingDay => ["knowledge", "pricing", "availability"],
        Booking => ["availability", "operations", "reports"],
        Payment or Expense => ["reports", "finance"],
        _ => []
    };
}
