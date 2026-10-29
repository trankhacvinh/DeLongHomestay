using DeLong.Web.Domain.Entities;

namespace DeLong.Web.Features.Inventory;

public sealed record InventoryError(string Code, string Message, int Status = 400);
public sealed record InventoryUnitRequest(string Name);
public sealed record InventoryItemRequest(string? Code, string Name, Guid UnitId, decimal ReferencePrice, bool IsActive = true);
public sealed record InventoryLineRequest(Guid ItemId, decimal Quantity, decimal UnitPrice);
public sealed record InventoryDocumentRequest(Guid RequestKey, InventoryDocumentType Type, DateTimeOffset OccurredAt,
    Guid? RecipientPropertyId, string? Note, IReadOnlyList<InventoryLineRequest> Lines, string? RecipientName = null);
public sealed record InventoryVoidRequest(string Reason);
public sealed record InventoryWarehouseDto(Guid Id, Guid PropertyId, string Name, string OwnerName, string TimeZoneId);
public sealed record InventoryUnitDto(Guid Id, string Name);
public sealed record InventoryItemDto(Guid Id, string Code, string Name, Guid UnitId, string UnitName, decimal ReferencePrice, bool IsActive);
public sealed record InventoryStockDto(Guid ItemId, string Code, string Name, string UnitName, decimal Opening,
    decimal Received, decimal Issued, decimal Adjustments, decimal Closing);
public sealed record InventoryLineDto(Guid ItemId, string ItemName, string UnitName, decimal Quantity, decimal UnitPrice);
public sealed record InventoryDocumentDto(Guid Id, string Code, InventoryDocumentType Type, DateTime OccurredAtUtc, Guid? RecipientPropertyId,
    string? RecipientName, string? Note, Guid? RecordedByUserId, string? RecordedByName, DateTime CreatedAtUtc, bool IsVoided, string? VoidReason, IReadOnlyList<InventoryLineDto> Lines);
public sealed record InventoryRecipientDto(Guid? PropertyId, string Name);
public sealed record InventoryDistributionDto(Guid? RecipientPropertyId, string RecipientName, Guid ItemId, string ItemName, string UnitName, decimal Quantity, decimal Value);
public sealed record InventorySnapshotDto(InventoryWarehouseDto? Warehouse, IReadOnlyList<InventoryUnitDto> Units, IReadOnlyList<InventoryItemDto> Items,
    IReadOnlyList<InventoryStockDto> Stock, IReadOnlyList<InventoryDocumentDto> Documents, IReadOnlyList<InventoryDistributionDto> Distribution,
    IReadOnlyList<InventoryRecipientDto>? Recipients = null);
