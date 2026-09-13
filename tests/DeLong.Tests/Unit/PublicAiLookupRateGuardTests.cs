using DeLong.Web.Features.PublicAi;
using Xunit;

namespace DeLong.Tests.Unit;

public sealed class PublicAiLookupRateGuardTests
{
    [Fact]
    public void Allows_five_attempts_and_blocks_the_sixth_in_the_same_window()
    {
        var guard = new PublicAiLookupRateGuard();
        var propertyId = Guid.NewGuid();
        var now = new DateTime(2026, 9, 11, 1, 0, 0, DateTimeKind.Utc);

        for (var index = 0; index < 5; index++)
            Assert.True(guard.TryAcquire(propertyId, "127.0.0.1", now.AddSeconds(index)));

        Assert.False(guard.TryAcquire(propertyId, "127.0.0.1", now.AddMinutes(9)));
    }

    [Fact]
    public void Starts_a_new_window_after_ten_minutes_and_scopes_by_property_and_client()
    {
        var guard = new PublicAiLookupRateGuard();
        var propertyId = Guid.NewGuid();
        var now = new DateTime(2026, 9, 11, 1, 0, 0, DateTimeKind.Utc);

        for (var index = 0; index < 5; index++) guard.TryAcquire(propertyId, "client-a", now);

        Assert.True(guard.TryAcquire(propertyId, "client-b", now));
        Assert.True(guard.TryAcquire(Guid.NewGuid(), "client-a", now));
        Assert.True(guard.TryAcquire(propertyId, "client-a", now.AddMinutes(10)));
    }
}
