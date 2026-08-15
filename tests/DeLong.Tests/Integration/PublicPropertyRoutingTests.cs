using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Features.PublicRooms;
using DeLong.Web.Features.Site;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeLong.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class PublicPropertyRoutingTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Public_site_scope_resolves_rooms_and_cms_for_the_requested_property()
    {
        var connectionString = Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var code = $"NANA-{suffix}".ToUpperInvariant();
        var siteSlug = $"nana-public-{suffix}";
        var property = new Property
        {
            Code = code,
            Name = $"Nana Homestay {suffix}",
            SiteSlug = siteSlug,
            TimeZoneId = "Asia/Ho_Chi_Minh",
            IsActive = true
        };
        db.Properties.Add(property);
        db.Set<PropertySiteSettings>().Add(new PropertySiteSettings
        {
            PropertyId = property.Id,
            SiteName = $"Nana Public {suffix}",
            Phone = "0900000000",
            RobotsIndex = true
        });

        var room = new Room
        {
            PropertyId = property.Id,
            Code = $"NN-{suffix}".ToUpperInvariant(),
            Name = $"Nana 1 {suffix}",
            Slug = $"nana-1-{suffix}",
            Capacity = 2,
            SortOrder = 1,
            IsActive = true,
            IsPublished = true
        };
        db.Rooms.Add(room);
        await db.SaveChangesAsync();

        var resolver = new PublicPropertyResolver(db);
        var resolved = await resolver.ResolveAsync(siteSlug);
        Assert.NotNull(resolved);
        Assert.Equal(property.Id, resolved!.Id);
        Assert.Equal(siteSlug, resolved.SiteSlug);
        Assert.NotEqual(PublicPropertyResolver.ToSiteSlug(code), resolved.SiteSlug);
        Assert.Null(await resolver.ResolveAsync(PublicPropertyResolver.ToSiteSlug(code)));

        var roomService = new PublicRoomContentService(db);
        var catalog = await roomService.GetCatalogAsync(property.Id);
        Assert.Contains(catalog.Rooms, x => x.Id == room.Id);
        var globalCatalog = await roomService.GetGlobalCatalogAsync();
        Assert.Contains(globalCatalog.Properties, x => x.Id == property.Id && x.SiteSlug == siteSlug);
        Assert.Contains(globalCatalog.Rooms, x => x.PropertyId == property.Id && x.PropertySiteSlug == siteSlug && x.Room.Id == room.Id);
        var detail = await roomService.GetRoomAsync(property.Id, room.Slug!);
        Assert.NotNull(detail);
        Assert.Equal(room.Id, detail!.Id);

        var siteService = new SiteContentService(db);
        var site = await siteService.GetPublicAsync(siteSlug);
        Assert.NotNull(site);
        Assert.Equal(property.Id, site!.Settings.PropertyId);
        Assert.Equal($"Nana Public {suffix}", site.Settings.SiteName);

        Assert.Null(await resolver.ResolveAsync($"missing-{suffix}"));
    }

    [Fact]
    public void Public_site_slug_is_stable_and_human_readable()
    {
        Assert.Equal("de-long", PublicPropertyResolver.ToSiteSlug("DELONG"));
        Assert.Equal("nana-02", PublicPropertyResolver.ToSiteSlug("NANA_02"));
        Assert.Equal("nana", PublicPropertyResolver.EffectiveSiteSlug("NANA", "NANA_02"));
        Assert.Equal("/h/nana-02", PublicPropertyResolver.ScopePrefix("nana-02"));
        Assert.Equal("/h/nana-02/rooms/family-room", PublicUrlBuilder.Room("nana-02", "family-room"));
        Assert.Equal("/h/nana-02/booking?date=2026-08-15&room=NN-1", PublicUrlBuilder.Booking("nana-02", "2026-08-15", "NN-1"));
    }
}
