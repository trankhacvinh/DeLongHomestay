using DeLong.Web.Common.Operations;
using DeLong.Web.Features.PublicBooking;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace DeLong.Tests;

public sealed class BookingGuestDetailsStoreTests
{
    [Fact]
    public async Task SaveAndReload_PreservesGuestCountAndPolicyAudit()
    {
        var root = TempRoot();
        try
        {
            var store = new BookingGuestDetailsStore(Paths(root));
            var propertyId = Guid.NewGuid();
            var bookingId = Guid.NewGuid();
            var acceptedAt = new DateTime(2026, 8, 19, 8, 30, 0, DateTimeKind.Utc);

            await store.SaveAsync(
                propertyId,
                bookingId,
                new BookingGuestDetailsDto(4, true, 3, acceptedAt));

            var reloaded = await new BookingGuestDetailsStore(Paths(root)).GetAsync(propertyId, bookingId);

            Assert.NotNull(reloaded);
            Assert.Equal(4, reloaded.GuestCount);
            Assert.True(reloaded.PolicyAccepted);
            Assert.Equal(3, reloaded.PolicyVersion);
            Assert.Equal(acceptedAt, reloaded.PolicyAcceptedAtUtc);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task SaveAndReload_AllowsStaffBookingWithoutPolicyConsent()
    {
        var root = TempRoot();
        try
        {
            var store = new BookingGuestDetailsStore(Paths(root));
            var propertyId = Guid.NewGuid();
            var bookingId = Guid.NewGuid();

            await store.SaveAsync(
                propertyId,
                bookingId,
                new BookingGuestDetailsDto(2, false, null, null));

            var reloaded = await store.GetAsync(propertyId, bookingId);

            Assert.NotNull(reloaded);
            Assert.Equal(2, reloaded.GuestCount);
            Assert.False(reloaded.PolicyAccepted);
            Assert.Null(reloaded.PolicyVersion);
            Assert.Null(reloaded.PolicyAcceptedAtUtc);
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static StoragePaths Paths(string root) =>
        new(root, Path.Combine(root, "public"), new PathString("/uploads/rooms"), true, true, false);

    private static string TempRoot() =>
        Path.Combine(Path.GetTempPath(), "delong-booking-details-test-" + Guid.NewGuid().ToString("N"));

    private static void Cleanup(string root)
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
