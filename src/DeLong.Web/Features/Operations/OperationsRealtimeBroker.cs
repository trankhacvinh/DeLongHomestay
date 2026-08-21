using System.Collections.Concurrent;
using System.Threading.Channels;

namespace DeLong.Web.Features.Operations;

public static class OperationsEventTypes
{
    public const string BookingCreated = "booking.created";
    public const string BookingUpdated = "booking.updated";
    public const string BookingMoved = "booking.moved";
    public const string BookingStatusChanged = "booking.status-changed";
    public const string BookingHoldExpired = "booking.hold-expired";
    public const string BookingBulkChanged = "booking.bulk-changed";
    public const string HousekeepingChanged = "housekeeping.changed";
}

public sealed record OperationsRealtimeEvent(
    Guid EventId,
    Guid PropertyId,
    string Type,
    Guid? BookingId,
    Guid? RoomId,
    DateTime OccurredAtUtc)
{
    public static OperationsRealtimeEvent Create(
        Guid propertyId,
        string type,
        Guid? bookingId = null,
        Guid? roomId = null) =>
        new(Guid.NewGuid(), propertyId, type, bookingId, roomId, DateTime.UtcNow);
}

public sealed class OperationsRealtimeBroker
{
    public static OperationsRealtimeBroker Shared { get; } = new();

    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, Channel<OperationsRealtimeEvent>>> subscriptions = new();

    public Subscription Subscribe(Guid propertyId)
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateBounded<OperationsRealtimeEvent>(new BoundedChannelOptions(64)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
        subscriptions.GetOrAdd(propertyId, _ => new ConcurrentDictionary<Guid, Channel<OperationsRealtimeEvent>>())[id] = channel;
        return new Subscription(this, propertyId, id, channel.Reader);
    }

    public void Publish(OperationsRealtimeEvent evt)
    {
        if (!subscriptions.TryGetValue(evt.PropertyId, out var propertySubscriptions)) return;
        foreach (var channel in propertySubscriptions.Values)
            channel.Writer.TryWrite(evt);
    }

    private void Unsubscribe(Guid propertyId, Guid subscriptionId)
    {
        if (!subscriptions.TryGetValue(propertyId, out var propertySubscriptions)) return;
        if (propertySubscriptions.TryRemove(subscriptionId, out var channel)) channel.Writer.TryComplete();
        if (propertySubscriptions.IsEmpty) subscriptions.TryRemove(propertyId, out _);
    }

    public sealed class Subscription : IDisposable
    {
        private readonly OperationsRealtimeBroker owner;
        private readonly Guid propertyId;
        private readonly Guid subscriptionId;
        private bool disposed;

        internal Subscription(
            OperationsRealtimeBroker owner,
            Guid propertyId,
            Guid subscriptionId,
            ChannelReader<OperationsRealtimeEvent> reader)
        {
            this.owner = owner;
            this.propertyId = propertyId;
            this.subscriptionId = subscriptionId;
            Reader = reader;
        }

        public ChannelReader<OperationsRealtimeEvent> Reader { get; }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            owner.Unsubscribe(propertyId, subscriptionId);
        }
    }
}
