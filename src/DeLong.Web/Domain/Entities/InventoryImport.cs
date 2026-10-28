namespace DeLong.Web.Domain.Entities;

public sealed class InventoryImport : EntityBase
{
    public Guid PropertyId { get; set; }
    public Guid WarehouseId { get; set; }
    public InventoryWarehouse Warehouse { get; set; } = null!;
    public Guid RequestKey { get; set; }
    public string RequestHash { get; set; } = string.Empty;
    public string ResultJson { get; set; } = string.Empty;
}
