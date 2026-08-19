using DeLong.Web.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DeLong.Web.Features.Operations;

public sealed class OperationsRealtimeInterceptor(OperationsRealtimeBroker broker) : SaveChangesInterceptor
{
    private readonly List<OperationsRealtimeEvent> pending = [];

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        PublishPending();
        return result;
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        PublishPending();
        return ValueTask.FromResult(result);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => pending.Clear();

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        pending.Clear();
        return Task.CompletedTask;
    }

    private void Capture(DbContext? db)
    {
        pending.Clear();
        if (db is null) return;

        foreach (var entry in db.ChangeTracker.Entries<Booking>())
        {
            if (entry.State == EntityState.Added)
            {
                pending.Add(OperationsRealtimeEvent.Create(
                    entry.Entity.PropertyId,
                    OperationsEventTypes.BookingCreated,
                    entry.Entity.Id,
                    entry.Entity.RoomId));
                continue;
            }

            if (entry.State != EntityState.Modified) continue;

            var moved = entry.Property(x => x.RoomId).IsModified ||
                        entry.Property(x => x.CheckInUtc).IsModified ||
                        entry.Property(x => x.CheckOutUtc).IsModified;
            var statusChanged = entry.Property(x => x.Status).IsModified;

            if (moved)
            {
                pending.Add(OperationsRealtimeEvent.Create(
                    entry.Entity.PropertyId,
                    OperationsEventTypes.BookingMoved,
                    entry.Entity.Id,
                    entry.Entity.RoomId));
            }

            if (statusChanged)
            {
                var originalStatus = entry.Property(x => x.Status).OriginalValue;
                var eventType = originalStatus == Domain.Enums.BookingStatus.Held &&
                                entry.Entity.Status == Domain.Enums.BookingStatus.Requested
                    ? OperationsEventTypes.BookingHoldExpired
                    : OperationsEventTypes.BookingStatusChanged;
                pending.Add(OperationsRealtimeEvent.Create(
                    entry.Entity.PropertyId,
                    eventType,
                    entry.Entity.Id,
                    entry.Entity.RoomId));
            }

            if (!moved && !statusChanged && entry.Properties.Any(x => x.IsModified))
            {
                pending.Add(OperationsRealtimeEvent.Create(
                    entry.Entity.PropertyId,
                    OperationsEventTypes.BookingUpdated,
                    entry.Entity.Id,
                    entry.Entity.RoomId));
            }
        }

        foreach (var entry in db.ChangeTracker.Entries<Room>())
        {
            if (entry.State != EntityState.Modified || !entry.Property(x => x.HousekeepingStatus).IsModified) continue;
            pending.Add(OperationsRealtimeEvent.Create(
                entry.Entity.PropertyId,
                OperationsEventTypes.HousekeepingChanged,
                null,
                entry.Entity.Id));
        }
    }

    private void PublishPending()
    {
        if (pending.Count == 0) return;
        foreach (var evt in pending)
            broker.Publish(evt);
        pending.Clear();
    }
}
