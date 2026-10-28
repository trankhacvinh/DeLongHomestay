using DeLong.Web.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeLong.Web.Data.Configurations;

public sealed class InventoryCashConfiguration : IEntityTypeConfiguration<InventoryWarehouse>, IEntityTypeConfiguration<InventoryUnit>,
    IEntityTypeConfiguration<InventoryItem>, IEntityTypeConfiguration<InventoryDocument>, IEntityTypeConfiguration<InventoryDocumentLine>, IEntityTypeConfiguration<CashReceipt>
{
    public void Configure(EntityTypeBuilder<InventoryWarehouse> b)
    {
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Code).HasMaxLength(20).IsRequired();
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
    }
    public void Configure(EntityTypeBuilder<InventoryUnit> b)
    {
        b.HasIndex(x => new { x.WarehouseId, x.NormalizedName }).IsUnique();
        b.Property(x => x.Name).HasMaxLength(40).IsRequired();
        b.Property(x => x.NormalizedName).HasMaxLength(40).IsRequired();
        b.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
    }
    public void Configure(EntityTypeBuilder<InventoryItem> b)
    {
        b.HasIndex(x => new { x.WarehouseId, x.Code }).IsUnique();
        b.Property(x => x.Code).HasMaxLength(40).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.ReferencePrice).HasPrecision(18, 2);
        b.HasOne(x => x.Unit).WithMany().HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        b.ToTable(t => t.HasCheckConstraint("ck_inventory_item_price", "reference_price >= 0"));
    }
    public void Configure(EntityTypeBuilder<InventoryDocument> b)
    {
        b.HasIndex(x => new { x.WarehouseId, x.RequestKey }).IsUnique();
        b.HasIndex(x => new { x.WarehouseId, x.OccurredAtUtc });
        b.Property(x => x.Code).HasMaxLength(40).IsRequired();
        b.Property(x => x.RequestHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.Note).HasMaxLength(2000);
        b.Property(x => x.VoidReason).HasMaxLength(1000);
        b.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.RecipientProperty).WithMany().HasForeignKey(x => x.RecipientPropertyId).OnDelete(DeleteBehavior.Restrict);
        b.ToTable(t => {
            t.HasCheckConstraint("ck_inventory_document_type", "type BETWEEN 0 AND 4");
            t.HasCheckConstraint("ck_inventory_document_recipient", "(type = 2 AND recipient_property_id IS NOT NULL) OR (type <> 2 AND recipient_property_id IS NULL)");
        });
    }
    public void Configure(EntityTypeBuilder<InventoryDocumentLine> b)
    {
        b.HasIndex(x => new { x.DocumentId, x.ItemId }).IsUnique();
        b.Property(x => x.ItemName).HasMaxLength(200).IsRequired();
        b.Property(x => x.UnitName).HasMaxLength(40).IsRequired();
        b.Property(x => x.Quantity).HasPrecision(18, 3);
        b.Property(x => x.UnitPrice).HasPrecision(18, 2);
        b.HasOne(x => x.Document).WithMany(x => x.Lines).HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        b.ToTable(t => t.HasCheckConstraint("ck_inventory_line_values", "quantity > 0 AND unit_price >= 0"));
    }
    public void Configure(EntityTypeBuilder<CashReceipt> b)
    {
        b.HasIndex(x => new { x.PropertyId, x.RequestKey }).IsUnique();
        b.HasIndex(x => new { x.PropertyId, x.OccurredAtUtc });
        b.Property(x => x.RequestHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.Description).HasMaxLength(500).IsRequired();
        b.Property(x => x.Note).HasMaxLength(2000);
        b.Property(x => x.VoidReason).HasMaxLength(1000);
        b.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Room).WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Restrict);
        b.ToTable(t => t.HasCheckConstraint("ck_cash_receipt_amount", "amount > 0"));
    }
}
