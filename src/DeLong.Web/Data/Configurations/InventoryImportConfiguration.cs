using DeLong.Web.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeLong.Web.Data.Configurations;

public sealed class InventoryImportConfiguration : IEntityTypeConfiguration<InventoryImport>
{
    public void Configure(EntityTypeBuilder<InventoryImport> b)
    {
        b.HasIndex(x => new { x.WarehouseId, x.RequestKey }).IsUnique();
        b.Property(x => x.RequestHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.ResultJson).HasColumnType("jsonb").IsRequired();
        b.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
    }
}
