using DeLong.Web.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeLong.Web.Data.Configurations;

public sealed class PropertySiteSettingsConfiguration : IEntityTypeConfiguration<PropertySiteSettings>
{
    public void Configure(EntityTypeBuilder<PropertySiteSettings> entity)
    {
        entity.HasIndex(x => x.PropertyId).IsUnique();
        entity.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Cascade);
        entity.Property(x => x.SiteName).HasMaxLength(200);
        entity.Property(x => x.Tagline).HasMaxLength(300);
        entity.Property(x => x.Address).HasMaxLength(500);
        entity.Property(x => x.Phone).HasMaxLength(50);
        entity.Property(x => x.Email).HasMaxLength(254);
        entity.Property(x => x.FacebookUrl).HasMaxLength(1000);
        entity.Property(x => x.ZaloUrl).HasMaxLength(1000);
        entity.Property(x => x.GoogleMapsUrl).HasMaxLength(1000);
        entity.Property(x => x.LogoUrl).HasMaxLength(1000);
        entity.Property(x => x.FaviconUrl).HasMaxLength(1000);
        entity.Property(x => x.OgImageUrl).HasMaxLength(1000);
        entity.Property(x => x.MetaTitle).HasMaxLength(200);
        entity.Property(x => x.MetaDescription).HasMaxLength(500);
        entity.Property(x => x.CanonicalBaseUrl).HasMaxLength(1000);
        entity.Property(x => x.OgTitle).HasMaxLength(200);
        entity.Property(x => x.OgDescription).HasMaxLength(500);
        entity.Property(x => x.GoogleSiteVerification).HasMaxLength(300);
        entity.Property(x => x.CustomCss).HasMaxLength(50_000);
        entity.Property(x => x.CustomJs).HasMaxLength(100_000);
    }
}

public sealed class HomeSectionConfiguration : IEntityTypeConfiguration<HomeSection>
{
    public void Configure(EntityTypeBuilder<HomeSection> entity)
    {
        entity.HasIndex(x => new { x.PropertyId, x.SortOrder });
        entity.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Cascade);
        entity.Property(x => x.Type).HasMaxLength(30).IsRequired();
        entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
        entity.Property(x => x.Variant).HasMaxLength(40).IsRequired();
        entity.Property(x => x.ContentJson).HasColumnType("jsonb").IsRequired();
    }
}
