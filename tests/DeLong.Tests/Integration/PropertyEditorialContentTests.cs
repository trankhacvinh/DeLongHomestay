using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Features.Site;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeLong.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class PropertyEditorialContentTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Property_gallery_and_blog_stay_scoped_and_global_showcase_can_filter_properties()
    {
        var connectionString = Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var a = new Property
        {
            Code = $"EDITA-{suffix}".ToUpperInvariant(),
            Name = $"Editorial A {suffix}",
            SiteSlug = $"editorial-a-{suffix}",
            TimeZoneId = "Asia/Ho_Chi_Minh",
            IsActive = true
        };
        var b = new Property
        {
            Code = $"EDITB-{suffix}".ToUpperInvariant(),
            Name = $"Editorial B {suffix}",
            SiteSlug = $"editorial-b-{suffix}",
            TimeZoneId = "Asia/Ho_Chi_Minh",
            IsActive = true
        };
        db.Properties.AddRange(a, b);
        await db.SaveChangesAsync();

        var service = new PropertyEditorialContentService(db);
        var (galleryA, galleryErrorA) = await service.CreateGalleryAsync(a.Id, new SaveGalleryItemRequest
        {
            ImageUrl = $"/uploads/site/a-{suffix}.webp",
            AltText = "A gallery",
            IsPublished = true
        });
        var (galleryB, galleryErrorB) = await service.CreateGalleryAsync(b.Id, new SaveGalleryItemRequest
        {
            ImageUrl = $"/uploads/site/b-{suffix}.webp",
            AltText = "B gallery",
            IsPublished = true
        });
        Assert.Null(galleryErrorA);
        Assert.Null(galleryErrorB);

        var (postA, postErrorA) = await service.CreatePostAsync(a.Id, new SaveBlogPostRequest
        {
            Title = $"Bài A {suffix}",
            Excerpt = "Nội dung A",
            BodyHtml = "<p>A</p><script>alert(1)</script>",
            IsPublished = true
        });
        var (postB, postErrorB) = await service.CreatePostAsync(b.Id, new SaveBlogPostRequest
        {
            Title = $"Bài B {suffix}",
            Excerpt = "Nội dung B",
            BodyHtml = "<p>B</p>",
            IsPublished = true
        });
        Assert.Null(postErrorA);
        Assert.Null(postErrorB);
        Assert.NotNull(postA);
        Assert.DoesNotContain("script", postA!.BodyHtml, StringComparison.OrdinalIgnoreCase);

        var publicA = await service.GetPublicGalleryAsync(a.Id);
        var postsA = await service.GetPublicPostsAsync(a.Id);
        Assert.Contains(publicA, x => x.Id == galleryA!.Id);
        Assert.DoesNotContain(publicA, x => x.Id == galleryB!.Id);
        Assert.Contains(postsA, x => x.Id == postA.Id);
        Assert.DoesNotContain(postsA, x => x.Id == postB!.Id);

        var showcase = new GlobalEditorialShowcaseService(db, service);
        var (settings, saveError) = await showcase.SaveAsync(new SaveGlobalEditorialShowcaseRequest
        {
            GalleryEnabled = true,
            GalleryMode = "properties",
            GalleryPropertyIds = [a.Id],
            GalleryLimit = 8,
            GalleryTitle = "Gallery A",
            BlogEnabled = true,
            BlogMode = "properties",
            BlogPropertyIds = [a.Id],
            BlogLimit = 3,
            BlogTitle = "Blog A"
        });
        Assert.Null(saveError);
        Assert.NotNull(settings);

        var global = await showcase.GetPublicAsync();
        Assert.Contains(global.Gallery, x => x.Id == galleryA!.Id);
        Assert.DoesNotContain(global.Gallery, x => x.Id == galleryB!.Id);
        Assert.Contains(global.Posts, x => x.Id == postA.Id);
        Assert.DoesNotContain(global.Posts, x => x.Id == postB!.Id);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Inactive_property_editorial_content_never_appears_publicly()
    {
        var connectionString = Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var property = new Property
        {
            Code = $"EDITH-{suffix}".ToUpperInvariant(),
            Name = $"Hidden Editorial {suffix}",
            SiteSlug = $"hidden-editorial-{suffix}",
            TimeZoneId = "Asia/Ho_Chi_Minh",
            IsActive = true
        };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        var service = new PropertyEditorialContentService(db);
        await service.CreateGalleryAsync(property.Id, new SaveGalleryItemRequest
        {
            ImageUrl = $"/uploads/site/hidden-{suffix}.webp",
            AltText = "Hidden",
            IsPublished = true
        });
        await service.CreatePostAsync(property.Id, new SaveBlogPostRequest
        {
            Title = $"Hidden {suffix}",
            BodyHtml = "<p>Hidden</p>",
            IsPublished = true
        });

        property.IsActive = false;
        await db.SaveChangesAsync();

        Assert.Empty(await service.GetPublicGalleryAsync(property.Id));
        Assert.Empty(await service.GetPublicPostsAsync(property.Id));
        Assert.DoesNotContain(await service.GetGlobalPublicGalleryAsync(), x => x.PropertyId == property.Id);
        Assert.DoesNotContain(await service.GetGlobalPublicPostsAsync(), x => x.PropertyId == property.Id);
    }
}
