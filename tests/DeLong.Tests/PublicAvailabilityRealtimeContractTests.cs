using System.Text.Json;
using DeLong.Web.Features.Operations;
using Xunit;

namespace DeLong.Tests;

public sealed class PublicAvailabilityRealtimeContractTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Public_availability_event_contains_only_room_operational_metadata()
    {
        var roomId = Guid.NewGuid();
        var payload = new PublicAvailabilityRealtimeEvent(
            OperationsEventTypes.BookingUpdated,
            roomId,
            new DateTime(2026, 8, 20, 1, 2, 3, DateTimeKind.Utc));

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("booking.updated", json, StringComparison.Ordinal);
        Assert.Contains(roomId.ToString(), json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bookingId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("customer", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("guest", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("phone", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("identity", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cccd", json, StringComparison.OrdinalIgnoreCase);
    }
}