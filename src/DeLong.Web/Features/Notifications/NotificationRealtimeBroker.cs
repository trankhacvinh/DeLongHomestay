using System.Collections.Concurrent;
using System.Threading.Channels;

namespace DeLong.Web.Features.Notifications;

public sealed class NotificationRealtimeBroker
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, Channel<NotificationRealtimeEvent>>> subscriptions = new();

    public Subscription Subscribe(Guid propertyId)
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateBounded<NotificationRealtimeEvent>(new BoundedChannelOptions(32)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
        subscriptions.GetOrAdd(propertyId, _ => new ConcurrentDictionary<Guid, Channel<NotificationRealtimeEvent>>())[id] = channel;
        return new Subscription(this, propertyId, id, channel.Reader);
    }

    public void Publish(NotificationRealtimeEvent evt)
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
        private readonly NotificationRealtimeBroker owner;
        private readonly Guid propertyId;
        private readonly Guid subscriptionId;
        private bool disposed;

        internal Subscription(NotificationRealtimeBroker owner, Guid propertyId, Guid subscriptionId, ChannelReader<NotificationRealtimeEvent> reader)
        {
            this.owner = owner;
            this.propertyId = propertyId;
            this.subscriptionId = subscriptionId;
            Reader = reader;
        }

        public ChannelReader<NotificationRealtimeEvent> Reader { get; }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            owner.Unsubscribe(propertyId, subscriptionId);
        }
    }
}
