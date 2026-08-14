using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Features.PublicRooms;
using DeLong.Web.Features.Rooms;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeLong.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class RoomContentTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Room_content_is_sanitized_and_publication_controls_public_catalog()
    {
        var connectionString = Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var property = await db.Properties.SingleOrDefaultAsync(x => x.Code == "DELONG");
        if (property is null)
        {
            property = new Property
            {
                Code = "DELONG",
                Name = "De Long Test Property",
                TimeZoneId = "Asia/Ho_Chi_Minh",
                IsActive = true
            };
            db.Properties.Add(property);
            await db.SaveChangesAsync();
        }

        var room = new Room
        {
            PropertyId = property.Id,
            Code = $"CONTENT-{suffix}",
            Name = $"Phòng Cặp Đôi {suffix}",
            Capacity = 2,
            IsActive = true,
            IsPublished = false
        };
        db.Rooms.Add(room);
        await db.SaveChangesAsync();

        var requestedSlug = $"Phòng Cặp Đôi {suffix}";
        var expectedSlug = RoomContentService.CreateSlug(requestedSlug);
        var editedCode = $"EDIT-{suffix}".ToUpperInvariant();
        var editedName = $"Phòng Nội Dung {suffix}";
        var service = new RoomContentService(db, new NoopRoomImageStorage());
        var (updated, error) = await service.UpdateAsync(property.Id, room.Id, new UpdateRoomContentRequest
        {
            Code = editedCode,
            Name = editedName,
            Capacity = 3,
            Slug = requestedSlug,
            ShortDescription = "Không gian riêng tư cho hai người.",
            DescriptionHtml = "<h2>Không gian</h2><p>Yên tĩnh <strong>và riêng tư</strong>.</p><img class=\"room-image-size-50 room-image-align-right\" src=\"/uploads/rooms/a/b/large.webp\" alt=\"Phòng\"><iframe class=\"ql-video\" src=\"https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ\"></iframe><iframe src=\"https://evil.example/embed/x\"></iframe><script>alert('x')</script>",
            IsPublished = true,
            Amenities = ["Bồn tắm", "Wifi"],
            Tags = ["Couple", "Lãng mạn"],
            Highlights = ["Bồn tắm riêng", "Phù hợp cặp đôi"]
        });

        Assert.Null(error);
        Assert.NotNull(updated);
        Assert.Equal(editedCode, updated!.Code);
        Assert.Equal(editedName, updated.Name);
        Assert.Equal(3, updated.Capacity);
        Assert.Equal(expectedSlug, updated.Slug);
        Assert.True(updated.IsPublished);
        Assert.DoesNotContain("<script", updated.DescriptionHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("evil.example", updated.DescriptionHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("youtube-nocookie.com/embed/", updated.DescriptionHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/uploads/rooms/a/b/large.webp", updated.DescriptionHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("room-image-size-50", updated.DescriptionHtml, StringComparison.Ordinal);
        Assert.Contains("room-image-align-right", updated.DescriptionHtml, StringComparison.Ordinal);
        Assert.Contains("<strong>và riêng tư</strong>", updated.DescriptionHtml);
        Assert.Equal(2, updated.Amenities.Count);
        Assert.Equal(2, updated.Tags.Count);
        Assert.Equal(2, updated.Highlights.Count);

        var (preset, presetError) = await service.CreateAmenityPresetAsync(property.Id, new CreateAmenityPresetRequest
        {
            Name = $"Tiêu chuẩn {suffix}",
            Amenities = ["Wifi", "Máy lạnh", "Máy chiếu"]
        });
        Assert.Null(presetError);
        Assert.NotNull(preset);
        Assert.Equal(3, preset!.Amenities.Count);
        Assert.Contains((await service.GetAmenityPresetsAsync(property.Id)), x => x.Id == preset.Id);

        var publicService = new PublicRoomContentService(db);
        var catalog = await publicService.GetCatalogAsync();
        Assert.Contains(catalog.Rooms, x => x.Id == room.Id && x.Slug == expectedSlug && x.Name == editedName && x.Capacity == 3);

        var (hidden, hideError) = await service.UpdateAsync(property.Id, room.Id, new UpdateRoomContentRequest
        {
            Slug = updated.Slug,
            ShortDescription = updated.ShortDescription,
            DescriptionHtml = updated.DescriptionHtml,
            IsPublished = false,
            Amenities = updated.Amenities,
            Tags = updated.Tags,
            Highlights = updated.Highlights
        });
        Assert.Null(hideError);
        Assert.False(hidden!.IsPublished);
        Assert.Equal(editedCode, hidden.Code);
        Assert.Equal(editedName, hidden.Name);
        Assert.Equal(3, hidden.Capacity);

        var hiddenCatalog = await publicService.GetCatalogAsync();
        Assert.DoesNotContain(hiddenCatalog.Rooms, x => x.Id == room.Id);
    }

    private sealed class NoopRoomImageStorage : IRoomImageStorage
    {
        public Task<(StoredRoomImage? Image, string? Error)> SaveAsync(Guid roomId, Guid imageId, IFormFile file, CancellationToken cancellationToken = default) =>
            Task.FromResult<(StoredRoomImage?, string?)>((null, "not used"));

        public Task<string?> RegenerateCropsAsync(StoredRoomImage image, double focalX, double focalY, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task DeleteAsync(StoredRoomImage image, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}