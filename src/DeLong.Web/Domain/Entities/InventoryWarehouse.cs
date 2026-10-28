namespace DeLong.Web.Domain.Entities;

public sealed class InventoryWarehouse : EntityBase
{
    public Guid PropertyId { get; set; }
    public Property Property { get; set; } = null!;
    public string Code { get; set; } = "MAIN";
    public string Name { get; set; } = "Kho chung";
}

public sealed class InventoryUnit : EntityBase
{
    public Guid PropertyId { get; set; }
    public Guid WarehouseId { get; set; }
    public InventoryWarehouse Warehouse { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
}

public sealed class InventoryItem : EntityBase
{
    public Guid PropertyId { get; set; }
    public Guid WarehouseId { get; set; }
    public InventoryWarehouse Warehouse { get; set; } = null!;
    public Guid UnitId { get; set; }
    public InventoryUnit Unit { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal ReferencePrice { get; set; }
    public bool IsActive { get; set; } = true;
}
