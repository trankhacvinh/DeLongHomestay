using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Features.Site;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeLong.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class PublicPropertyContactsTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Public_contacts_expose_each_active_property_with_its_site_settings()
    {
        var connectionString = Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var withSettings = new Property { Code = $"CT-{suffix}", Name = $"Contact Test {suffix}", SiteSlug = $"contact-{suffix}", IsActive = true };
        var withoutSettings = new Property { Code = $"CN-{suffix}", Name = $"Contact Bare {suffix}", SiteSlug = $"contact-bare-{suffix}", IsActive = true };
        var inactive = new Property { Code = $"CI-{suffix}", Name = $"Contact Hidden {suffix}", SiteSlug = $"contact-off-{suffix}", IsActive = false };
        db.Properties.AddRange(withSettings, withoutSettings, inactive);
        await db.SaveChangesAsync();

        var service = new SiteContentService(db);
        var (_, error) = await service.SaveSettingsAsync(withSettings.Id, new SaveSiteSettingsRequest
        {
            SiteName = "De Long Contact",
            Tagline = "Long Thành · Đồng Nai",
            Address = "123 Chu Văn An, Long Thành",
            Phone = "0909 123 456",
            ZaloUrl = "https://zalo.me/0909123456",
            FacebookUrl = "https://facebook.com/delong",
            GoogleMapsUrl = "https://maps.google.com/?q=delong",
            LogoUrl = "/uploads/logo.webp",
            RobotsIndex = true
        }, allowCustomCode: false);
        Assert.Null(error);

        var contacts = await service.GetPublicContactsAsync();

        var contact = Assert.Single(contacts, x => x.PropertyId == withSettings.Id);
        Assert.Equal("De Long Contact", contact.Name);
        Assert.Equal($"contact-{suffix}", contact.SiteSlug);
        Assert.Equal("0909 123 456", contact.Phone);
        Assert.Equal("https://zalo.me/0909123456", contact.ZaloUrl);
        Assert.Equal("https://facebook.com/delong", contact.FacebookUrl);
        Assert.Equal("https://maps.google.com/?q=delong", contact.GoogleMapsUrl);
        Assert.Equal("/uploads/logo.webp", contact.LogoUrl);

        var bare = Assert.Single(contacts, x => x.PropertyId == withoutSettings.Id);
        Assert.Equal(withoutSettings.Name, bare.Name);
        Assert.Equal(string.Empty, bare.Phone);

        Assert.DoesNotContain(contacts, x => x.PropertyId == inactive.Id);
    }
}
