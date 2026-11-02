using DeLong.Web.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeLong.Web.Data.Configurations;

public sealed class BookingCancellationConfiguration : IEntityTypeConfiguration<BookingCancellation>
{
    public void Configure(EntityTypeBuilder<BookingCancellation> entity)
    {
        entity.HasIndex(x => new { x.PropertyId, x.CancelledAtUtc });
        entity.HasIndex(x => x.BookingId).IsUnique().HasFilter("restored_at_utc IS NULL")
            .HasDatabaseName("ux_booking_cancellations_active_booking");
        entity.Property(x => x.Reason).HasMaxLength(1000);
        entity.Property(x => x.RefundNote).HasMaxLength(1000);
        entity.Property(x => x.TotalAmountSnapshot).HasPrecision(18, 2);
        entity.Property(x => x.PaidAmountSnapshot).HasPrecision(18, 2);
        entity.Property(x => x.RefundDueAmount).HasPrecision(18, 2);
        entity.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(x => x.Booking).WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Cascade);
    }
}
