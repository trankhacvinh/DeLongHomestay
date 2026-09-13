using DeLong.Web.Common.Operations;
using DeLong.Web.Features.Operations;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace DeLong.Tests.Unit;

public sealed class BookingCalendarDisplaySettingsStoreTests
{
    [Fact]
    public async Task Settings_are_property_scoped_persistent_and_reject_invalid_colors()
    {
        var root = Path.Combine(Path.GetTempPath(), $"delong-calendar-colors-{Guid.NewGuid():N}");
        var paths = new StoragePaths(root, Path.Combine(root, "media"), new PathString("/uploads/rooms"), true, true, false);
        var firstProperty = Guid.NewGuid();
        var secondProperty = Guid.NewGuid();
        try
        {
            var store = new BookingCalendarDisplaySettingsStore(paths);
            var defaults = await store.GetAsync(firstProperty);
            Assert.Equal("#C94B4B", defaults.UnpaidColor);
            Assert.Equal("#7C5CC4", defaults.SpecialRequestColor);

            var request = new UpdateBookingCalendarDisplaySettingsRequest
            {
                RequestedColor = "#111111", HeldColor = "#222222", ConfirmedColor = "#333333", CheckedInColor = "#444444",
                UnpaidColor = "#aa0000", FlexibleTimeColor = "#bbbb00", SpecialRequestColor = "#660099", MixedSlotColor = "#005577"
            };
            var (saved, error) = await store.SaveAsync(firstProperty, request);
            Assert.Null(error);
            Assert.Equal("#AA0000", saved!.UnpaidColor);

            var reloaded = await new BookingCalendarDisplaySettingsStore(paths).GetAsync(firstProperty);
            Assert.Equal(saved, reloaded);
            Assert.Equal(defaults, await store.GetAsync(secondProperty));

            var (_, invalidError) = await store.SaveAsync(firstProperty, WithInvalidColor());
            Assert.NotNull(invalidError);
            Assert.Equal(saved, await store.GetAsync(firstProperty));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        UpdateBookingCalendarDisplaySettingsRequest WithInvalidColor() => new()
        {
            RequestedColor = "#111111", HeldColor = "#222222", ConfirmedColor = "#333333",
            CheckedInColor = "#444444", UnpaidColor = "red", FlexibleTimeColor = "#BBBB00",
            SpecialRequestColor = "#660099", MixedSlotColor = "#005577"
        };
    }
}
