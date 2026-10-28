namespace DeLong.Web.Domain.Entities;

public enum InventoryDocumentType { Opening = 0, Receipt = 1, Issue = 2, AdjustmentIn = 3, AdjustmentOut = 4 }

public sealed class InventoryDocument : EntityBase
{
    public Guid PropertyId { get; set; }
    public Guid WarehouseId { get; set; }
    public InventoryWarehouse Warehouse { get; set; } = null!;
    public Guid RequestKey { get; set; }
    public string RequestHash { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public InventoryDocumentType Type { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public Guid? RecipientPropertyId { get; set; }
    public Property? RecipientProperty { get; set; }
    public string? Note { get; set; }
    public Guid? RecordedByUserId { get; set; }
    public bool IsVoided { get; set; }
    public DateTime? VoidedAtUtc { get; set; }
    public Guid? VoidedByUserId { get; set; }
    public string? VoidReason { get; set; }
    public ICollection<InventoryDocumentLine> Lines { get; set; } = [];
}

public sealed class InventoryDocumentLine : EntityBase
{
    public Guid PropertyId { get; set; }
    public Guid DocumentId { get; set; }
    public InventoryDocument Document { get; set; } = null!;
    public Guid ItemId { get; set; }
    public InventoryItem Item { get; set; } = null!;
    public string ItemName { get; set; } = string.Empty;
    public string UnitName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
