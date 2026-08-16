using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ZiggyCreatures.Caching.Fusion;

namespace DeLong.Web.Common.Caching;

public static class PublicCacheKeys
{
    public const string Tag = "public-content";
    public const string ActiveProperties = "public:properties:active";
    public const string GlobalSections = "public:site:global-sections";
    public const string GlobalRooms = "public:rooms:global";
    public const string GlobalGallery = "public:editorial:gallery:global";
    public const string GlobalPosts = "public:editorial:posts:global";
    public const string GlobalShowcase = "public:editorial:showcase";
    public static string Site(Guid propertyId) => $"public:site:{propertyId:N}";
    public static string Rooms(Guid propertyId) => $"public:rooms:{propertyId:N}";
    public static string Room(Guid propertyId, string slugOrCode) => $"public:room:{propertyId:N}:{slugOrCode.Trim().ToLowerInvariant()}";
    public static string Gallery(Guid propertyId) => $"public:editorial:gallery:{propertyId:N}";
    public static string Posts(Guid propertyId) => $"public:editorial:posts:{propertyId:N}";
    public static string GalleryLayout(Guid propertyId) => $"public:editorial:gallery-layout:{propertyId:N}";
}

/// <summary>
/// Public content is read far more often than it is edited. Any successful write to a public-facing
/// content entity invalidates the single public-content tag. This intentionally favors correctness
/// and simple invalidation over fine-grained cache bookkeeping; admin writes are rare and the cache
/// will warm lazily again on the next public request.
/// </summary>
public sealed class PublicCacheInvalidationInterceptor(IFusionCache cache) : SaveChangesInterceptor
{
    private static readonly HashSet<string> PublicEntityNames = new(StringComparer.Ordinal)
    {
        "Property", "PropertySiteSettings", "HomeSection", "GlobalEditorialShowcase",
        "PropertyGalleryItem", "BlogPost", "Room", "RoomRate", "RoomImage", "RoomHighlight",
        "RoomAmenity", "Amenity", "RoomTag", "RoomTagAssignment"
    };
    private bool invalidateAfterSave;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        invalidateAfterSave = HasPublicChanges(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        invalidateAfterSave = HasPublicChanges(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        InvalidateIfNeeded();
        return result;
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        InvalidateIfNeeded();
        return ValueTask.FromResult(result);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => invalidateAfterSave = false;
    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        invalidateAfterSave = false;
        return Task.CompletedTask;
    }

    private static bool HasPublicChanges(DbContext? db) =>
        db?.ChangeTracker.Entries().Any(entry =>
            (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted) &&
            PublicEntityNames.Contains(entry.Metadata.ClrType.Name)) == true;

    private void InvalidateIfNeeded()
    {
        if (!invalidateAfterSave) return;
        invalidateAfterSave = false;
        cache.RemoveByTag(PublicCacheKeys.Tag);
    }
}
