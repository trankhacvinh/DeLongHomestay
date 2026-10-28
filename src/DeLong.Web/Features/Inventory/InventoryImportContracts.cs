namespace DeLong.Web.Features.Inventory;

public sealed record InventoryImportRow(int Row, string Name, string Unit, decimal? Quantity, decimal? Price, string? Error = null);
public sealed record InventoryImportOptions(string Mode, string Duplicates, DateTimeOffset? OpeningAt, string? Sheet = null);
public sealed record InventoryImportPreviewRow(int Row, string Name, string Unit, decimal? Quantity, decimal? Price,
    string Action, string Message, Guid? ItemId, decimal OpeningQuantity);
public sealed record InventoryImportPreview(string? Token, string Sheet, int NewItems, int UpdatedItems, int NewUnits, int OpeningItems,
    int Skipped, int Errors, IReadOnlyList<InventoryImportPreviewRow> Rows);
public sealed record InventoryImportCommit(Guid RequestKey, string Token);
public sealed record InventoryImportResult(int CreatedItems, int UpdatedItems, int CreatedUnits, int OpeningItems, int Skipped, Guid? DocumentId);
