using DeLong.Web.Common.Media;
using Xunit;

namespace DeLong.Tests.Unit;

public sealed class MediaUrlVersionerTests
{
    [Fact]
    public void Crop_version_changes_when_focal_point_changes()
    {
        var original = MediaUrlVersioner.WithCropVersion("/uploads/rooms/room/image/card.webp", 0.5, 0.5);
        var updated = MediaUrlVersioner.WithCropVersion("/uploads/rooms/room/image/card.webp", 0.625, 0.375);

        Assert.Equal("/uploads/rooms/room/image/card.webp?v=0.5-0.5", original);
        Assert.Equal("/uploads/rooms/room/image/card.webp?v=0.625-0.375", updated);
        Assert.NotEqual(original, updated);
    }

    [Fact]
    public void Crop_version_preserves_existing_query_string()
    {
        var url = MediaUrlVersioner.WithCropVersion("/uploads/rooms/room/image/thumb.webp?source=admin", 0.5, 0.25);

        Assert.Equal("/uploads/rooms/room/image/thumb.webp?source=admin&v=0.5-0.25", url);
    }
}
