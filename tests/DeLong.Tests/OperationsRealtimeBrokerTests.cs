using System.Text.Json;
using DeLong.Web.Features.Operations;
using Xunit;

namespace DeLong.Tests;

public sealed class OperationsRealtimeBrokerTests
{
    [Fact]
    public void Publish_is_scoped_to_property_and_event_contract_contains_only_operational_metadata()
    {
        var broker = new OperationsRealtimeBroker();
        var propertyA = Guid.NewGuid();
        var propertyB = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var roomId = Guid.NewGuid();

        using var a = broker.Subscribe(propertyA);
        using var b = broker.Subscribe(propertyB);

        var evt = OperationsRealtimeEvent.Create(
            propertyA,
            OperationsEventTypes.BookingUpdated,
            bookingId,
            roomId);
        broker.Publish(evt);

        Assert.True(a.Reader.TryRead(out var received));
        Assert.Equal(evt, received);
        Assert.False(b.Reader.TryRead(out _));

        var json = JsonSerializer.Serialize(received, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Contains(bookingId.ToString(), json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(roomId.ToString(), json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("customer", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("guest", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("phone", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("identity", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cccd", json, StringComparison.OrdinalIgnoreCase);
    }
}
