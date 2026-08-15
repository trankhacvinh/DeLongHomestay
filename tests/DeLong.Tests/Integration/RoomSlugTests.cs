using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Features.PublicRooms;
using DeLong.Web.Features.Rooms;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeLong.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class RoomSlugTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Creating_rooms_persists_unique_public_slugs()
    {
        var connectionString = Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();

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

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var roomService = new RoomService(db);
        var firstName = $"Nana Test {suffix}";

        var (first, firstError) = await roomService.CreateAsync(
            property.Id,
            new CreateRoomRequest($"NANA-{suffix}", firstName, 2, 900));
        Assert.Null(firstError);
        Assert.NotNull(first);

        var (second, secondError) = await roomService.CreateAsync(
            property.Id,
            new CreateRoomRequest($"NANB-{suffix}", firstName, 2, 901));
        Assert.Null(secondError);
        Assert.NotNull(second);

        var firstEntity = await db.Rooms.SingleAsync(x => x.Id == first!.Id);
        var secondEntity = await db.Rooms.SingleAsync(x => x.Id == second!.Id);
        Assert.False(string.IsNullOrWhiteSpace(firstEntity.Slug));
        Assert.False(string.IsNullOrWhiteSpace(secondEntity.Slug));
        Assert.NotEqual(firstEntity.Slug, secondEntity.Slug);
        Assert.Equal(RoomContentService.CreateSlug(firstName), firstEntity.Slug);
        Assert.StartsWith(firstEntity.Slug + "-", secondEntity.Slug, StringComparison.Ordinal);

        firstEntity.IsPublished = true;
        await db.SaveChangesAsync();

        var publicService = new PublicRoomContentService(db);
        var publicRoom = await publicService.GetRoomAsync(firstEntity.Slug!);
        Assert.NotNull(publicRoom);
        Assert.Equal(firstEntity.Id, publicRoom!.Id);
    }
}
