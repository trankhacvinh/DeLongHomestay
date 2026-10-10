using DeLong.Web.Domain.Entities;
using DeLong.Web.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeLong.Web.Data.Configurations;

public sealed class RoomBookingBlockConfiguration : IEntityTypeConfiguration<RoomBookingBlock>
{
    public void Configure(EntityTypeBuilder<RoomBookingBlock> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        entity.HasOne(x => x.Room).WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
        entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CancelledByUserId).OnDelete(DeleteBehavior.SetNull);
        entity.HasIndex(x => new { x.PropertyId, x.RoomId, x.StartUtc, x.EndUtc }).HasFilter("cancelled_at_utc IS NULL");
        entity.HasIndex(x => new { x.PropertyId, x.BatchId });
        entity.ToTable("room_booking_blocks", table => table.HasCheckConstraint("ck_room_booking_blocks_interval", "end_utc > start_utc"));
    }
}
