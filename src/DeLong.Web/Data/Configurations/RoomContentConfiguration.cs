using DeLong.Web.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeLong.Web.Data.Configurations;

public sealed class RoomContentRoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> entity)
    {
        entity.HasIndex(x => new { x.PropertyId, x.Slug }).IsUnique();
        entity.Property(x => x.Slug).HasMaxLength(180);
        entity.Property(x => x.ShortDescription).HasMaxLength(600);
        entity.Property(x => x.DescriptionHtml).HasColumnType("text");
    }
}

public sealed class RoomImageConfiguration : IEntityTypeConfiguration<RoomImage>
{
    public void Configure(EntityTypeBuilder<RoomImage> entity)
    {
        entity.HasIndex(x => new { x.RoomId, x.SortOrder });
        entity.Property(x => x.OriginalFileName).HasMaxLength(255).IsRequired();
        entity.Property(x => x.OriginalStoragePath).HasMaxLength(500).IsRequired();
        entity.Property(x => x.LargePath).HasMaxLength(500).IsRequired();
        entity.Property(x => x.CardPath).HasMaxLength(500).IsRequired();
        entity.Property(x => x.ThumbnailPath).HasMaxLength(500).IsRequired();
        entity.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        entity.Property(x => x.AltText).HasMaxLength(300);
        entity.Property(x => x.FocalX).HasDefaultValue(0.5d);
        entity.Property(x => x.FocalY).HasDefaultValue(0.5d);
        entity.HasOne(x => x.Room).WithMany(x => x.Images).HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class AmenityConfiguration : IEntityTypeConfiguration<Amenity>
{
    public void Configure(EntityTypeBuilder<Amenity> entity)
    {
        entity.HasIndex(x => new { x.PropertyId, x.NormalizedName }).IsUnique();
        entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
        entity.Property(x => x.NormalizedName).HasMaxLength(100).IsRequired();
        entity.Property(x => x.IconKey).HasMaxLength(50);
        entity.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class RoomAmenityConfiguration : IEntityTypeConfiguration<RoomAmenity>
{
    public void Configure(EntityTypeBuilder<RoomAmenity> entity)
    {
        entity.HasKey(x => new { x.RoomId, x.AmenityId });
        entity.HasOne(x => x.Room).WithMany(x => x.Amenities).HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(x => x.Amenity).WithMany(x => x.Rooms).HasForeignKey(x => x.AmenityId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class AmenityPresetConfiguration : IEntityTypeConfiguration<AmenityPreset>
{
    public void Configure(EntityTypeBuilder<AmenityPreset> entity)
    {
        entity.HasIndex(x => new { x.PropertyId, x.NormalizedName }).IsUnique();
        entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
        entity.Property(x => x.NormalizedName).HasMaxLength(120).IsRequired();
        entity.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class AmenityPresetItemConfiguration : IEntityTypeConfiguration<AmenityPresetItem>
{
    public void Configure(EntityTypeBuilder<AmenityPresetItem> entity)
    {
        entity.HasKey(x => new { x.AmenityPresetId, x.AmenityId });
        entity.HasOne(x => x.AmenityPreset).WithMany(x => x.Items).HasForeignKey(x => x.AmenityPresetId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(x => x.Amenity).WithMany().HasForeignKey(x => x.AmenityId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class RoomTagConfiguration : IEntityTypeConfiguration<RoomTag>
{
    public void Configure(EntityTypeBuilder<RoomTag> entity)
    {
        entity.HasIndex(x => new { x.PropertyId, x.NormalizedName }).IsUnique();
        entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
        entity.Property(x => x.NormalizedName).HasMaxLength(100).IsRequired();
        entity.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class RoomTagAssignmentConfiguration : IEntityTypeConfiguration<RoomTagAssignment>
{
    public void Configure(EntityTypeBuilder<RoomTagAssignment> entity)
    {
        entity.HasKey(x => new { x.RoomId, x.RoomTagId });
        entity.HasOne(x => x.Room).WithMany(x => x.Tags).HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(x => x.RoomTag).WithMany(x => x.Rooms).HasForeignKey(x => x.RoomTagId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class RoomHighlightConfiguration : IEntityTypeConfiguration<RoomHighlight>
{
    public void Configure(EntityTypeBuilder<RoomHighlight> entity)
    {
        entity.HasIndex(x => new { x.RoomId, x.SortOrder });
        entity.Property(x => x.Text).HasMaxLength(180).IsRequired();
        entity.HasOne(x => x.Room).WithMany(x => x.Highlights).HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Cascade);
    }
}
