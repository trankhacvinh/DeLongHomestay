using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Features.Site;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeLong.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class GlobalBrandingTests
{
    [Fact]
    public async Task Global_home_inherits_primary_configuration_even_with_another_branch_and_keeps_explicit_overrides()
    {
        var connection = Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
        await db.Database.MigrateAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var primary = new Property { Code = "BR-" + suffix[..8], SiteSlug = suffix, Name = "Primary", IsActive = true };
        db.Add(new PropertySiteSettings { Property = primary, SiteName = "Configured brand", Tagline = "Configured tagline", LogoUrl = "/uploads/primary-logo.webp" });
        await db.SaveChangesAsync();
        var metadata = await db.Set<HomeSection>().SingleOrDefaultAsync(x => x.PropertyId == null && x.Type == GlobalSiteBrandingStore.MetadataSectionType);
        var previousJson = metadata?.ContentJson;
        try
        {
            await GlobalSiteBrandingStore.SaveAsync(db, new());
            PublicPropertyContext[] contexts = [
                new(Guid.NewGuid(), "OTHER", "Other branch", "Asia/Ho_Chi_Minh", "other-branch"),
                new(primary.Id, PublicPropertyResolver.LegacyPropertyCode, primary.Name, "Asia/Ho_Chi_Minh", suffix)];
            var service = new SiteContentService(db, new PublicPropertyResolver(db));
            var inherited = await GlobalSiteBrandingStore.ResolveAsync(db, service, contexts);
            Assert.Equal("Configured brand", inherited.SiteName);
            Assert.Equal("Configured tagline", inherited.Tagline);
            Assert.Equal("/uploads/primary-logo.webp", inherited.LogoUrl);
            await GlobalSiteBrandingStore.SaveAsync(db, new() { SiteName = "Global override", LogoUrl = "/uploads/global-logo.webp" });
            var overridden = await GlobalSiteBrandingStore.ResolveAsync(db, service, contexts);
            Assert.Equal("Global override", overridden.SiteName);
            Assert.Equal("/uploads/global-logo.webp", overridden.LogoUrl);
            Assert.Equal("Configured tagline", overridden.Tagline);
        }
        finally
        {
            var row = await db.Set<HomeSection>().SingleAsync(x => x.PropertyId == null && x.Type == GlobalSiteBrandingStore.MetadataSectionType);
            if (previousJson is null) db.Remove(row); else row.ContentJson = previousJson;
            await db.SaveChangesAsync();
        }
    }
}
