using System.Globalization;
using System.Security.Claims;
using System.Text;
using DeLong.Web.Common.Auditing;
using DeLong.Web.Common.Persistence;
using DeLong.Web.Common.Security;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Inventory;

public sealed class InventoryService(AppDbContext db, AuditService audit, PropertyAccessService access)
{
    private static InventoryError Invalid(string message) => new("inventory_invalid", message);
    private static readonly InventoryError Forbidden = new("forbidden", "Bạn chưa được cấp quyền kho chung của chi nhánh quản lý.", 403);
    public static int Direction(InventoryDocumentType type) => type is InventoryDocumentType.Issue or InventoryDocumentType.AdjustmentOut ? -1 : 1;
    private static Guid? Actor(ClaimsPrincipal user) => Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    private static string Normalize(string value) => value.Trim().Normalize(NormalizationForm.FormC).ToUpperInvariant();

    internal async Task<(InventoryWarehouse? Warehouse, InventoryError? Error)> ResolveAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        if (!(user.IsInRole("Admin") || user.IsInRole("Manager") || user.IsInRole("Viewer"))) return (null, Forbidden);
        var warehouse = await db.InventoryWarehouses.AsNoTracking().Include(x => x.Property).SingleOrDefaultAsync(x => x.Code == "MAIN", ct);
        if (warehouse is null) return (null, new("warehouse_missing", "Quản trị viên cần khởi tạo kho chung.", 404));
        return user.IsInRole("Admin") || await access.CanAccessAsync(user, warehouse.PropertyId, ct) ? (warehouse, null) : (null, Forbidden);
    }

    public async Task<(InventoryWarehouseDto? Result, InventoryError? Error)> InitializeAsync(ClaimsPrincipal user, Guid propertyId, CancellationToken ct)
    {
        if (!user.IsInRole("Admin")) return (null, Forbidden);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await RequestFingerprint.LockAsync(db, "inventory:setup", ct);
        var existing = await db.InventoryWarehouses.AsNoTracking().Include(x => x.Property).SingleOrDefaultAsync(x => x.Code == "MAIN", ct);
        if (existing is not null) return (ToWarehouse(existing), null);
        var property = await db.Properties.AsNoTracking().SingleOrDefaultAsync(x => x.Id == propertyId && x.IsActive, ct);
        if (property is null) return (null, Invalid("Chọn chi nhánh quản lý kho đang hoạt động."));
        var warehouse = new InventoryWarehouse { PropertyId = property.Id };
        db.InventoryWarehouses.Add(warehouse);
        audit.Add(property.Id, "InventoryWarehouse", warehouse.Id, "Created", Actor(user), after: new { warehouse.Name, warehouse.PropertyId });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return (new(warehouse.Id, property.Id, warehouse.Name, property.Name, property.TimeZoneId), null);
    }

    public async Task<(InventorySnapshotDto? Result, InventoryError? Error)> GetAsync(ClaimsPrincipal user, string month, CancellationToken ct)
    {
        var (warehouse, error) = await ResolveAsync(user, ct);
        if (warehouse is null)
            return error?.Code == "warehouse_missing" && user.IsInRole("Admin") ? (new(null, [], [], [], [], []), null) : (null, error);
        if (!DateOnly.TryParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) || date.Year >= 9999)
            return (null, Invalid("Tháng không hợp lệ."));
        var zone = TimeZoneInfo.FindSystemTimeZoneById(warehouse.Property.TimeZoneId);
        var from = TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue), zone);
        var to = TimeZoneInfo.ConvertTimeToUtc(date.AddMonths(1).ToDateTime(TimeOnly.MinValue), zone);
        var units = await db.InventoryUnits.AsNoTracking().Where(x => x.WarehouseId == warehouse.Id).OrderBy(x => x.Name).Select(x => new InventoryUnitDto(x.Id, x.Name)).ToListAsync(ct);
        var items = await db.InventoryItems.AsNoTracking().Where(x => x.WarehouseId == warehouse.Id).OrderBy(x => x.Name)
            .Select(x => new InventoryItemDto(x.Id, x.Code, x.Name, x.UnitId, x.Unit.Name, x.ReferencePrice, x.IsActive)).ToListAsync(ct);
        var movements = await db.InventoryDocumentLines.AsNoTracking().Where(x => x.Document.WarehouseId == warehouse.Id && !x.Document.IsVoided && x.Document.OccurredAtUtc < to)
            .Select(x => new { x.ItemId, x.Quantity, x.Document.Type, x.Document.OccurredAtUtc }).ToListAsync(ct);
        var stock = items.Select(item => {
            var rows = movements.Where(x => x.ItemId == item.Id).ToList();
            var opening = rows.Where(x => x.OccurredAtUtc < from || x.Type == InventoryDocumentType.Opening && x.OccurredAtUtc == from).Sum(x => Direction(x.Type) * x.Quantity);
            var period = rows.Where(x => x.OccurredAtUtc >= from && !(x.Type == InventoryDocumentType.Opening && x.OccurredAtUtc == from)).ToList();
            var received = period.Where(x => x.Type == InventoryDocumentType.Receipt).Sum(x => x.Quantity);
            var issued = period.Where(x => x.Type == InventoryDocumentType.Issue).Sum(x => x.Quantity);
            var adjustments = period.Where(x => x.Type is InventoryDocumentType.Opening or InventoryDocumentType.AdjustmentIn or InventoryDocumentType.AdjustmentOut).Sum(x => Direction(x.Type) * x.Quantity);
            return new InventoryStockDto(item.Id, item.Code, item.Name, item.UnitName, opening, received, issued, adjustments, opening + received - issued + adjustments);
        }).ToList();
        var documents = await db.InventoryDocuments.AsNoTracking().Where(x => x.WarehouseId == warehouse.Id && x.OccurredAtUtc >= from && x.OccurredAtUtc < to)
            .OrderByDescending(x => x.OccurredAtUtc).ThenByDescending(x => x.CreatedAtUtc).Select(x => new InventoryDocumentDto(x.Id, x.Code, x.Type, x.OccurredAtUtc,
                x.RecipientPropertyId, x.RecipientName ?? (x.RecipientProperty == null ? null : x.RecipientProperty.Name), x.Note, x.RecordedByUserId,
                db.Users.Where(u => u.Id == x.RecordedByUserId).Select(u => u.DisplayName != "" ? u.DisplayName : u.UserName).FirstOrDefault(), x.CreatedAtUtc, x.IsVoided, x.VoidReason,
                x.Lines.OrderBy(l => l.ItemName).Select(l => new InventoryLineDto(l.ItemId, l.ItemName, l.UnitName, l.Quantity, l.UnitPrice)).ToList())).ToListAsync(ct);
        var distribution = documents.Where(x => !x.IsVoided && x.Type == InventoryDocumentType.Issue).SelectMany(x => x.Lines.Select(l => new { Document = x, Line = l }))
            .GroupBy(x => new { Recipient = x.Document.RecipientPropertyId, Name = x.Document.RecipientPropertyId.HasValue ? "" : InventoryIdentity.Normalize(x.Document.RecipientName ?? ""), x.Line.ItemId, x.Line.ItemName, x.Line.UnitName })
            .Select(g => new InventoryDistributionDto(g.Key.Recipient, g.First().Document.RecipientName ?? "", g.Key.ItemId, g.Key.ItemName, g.Key.UnitName, g.Sum(x => x.Line.Quantity), g.Sum(x => x.Line.Quantity * x.Line.UnitPrice))).ToList();
        var destinations = await db.InventoryDocuments.AsNoTracking().Where(x => x.WarehouseId == warehouse.Id && x.Type == InventoryDocumentType.Issue)
            .Select(x => new InventoryRecipientDto(x.RecipientPropertyId, x.RecipientName ?? (x.RecipientProperty == null ? "" : x.RecipientProperty.Name))).Distinct().ToListAsync(ct);
        var recipients = destinations.Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .GroupBy(x => (x.PropertyId, InventoryIdentity.Normalize(x.Name))).Select(x => x.First()).OrderBy(x => x.Name).ToList();
        return (new(ToWarehouse(warehouse), units, items, stock, documents, distribution, recipients), null);
    }

    public async Task<(InventoryUnitDto? Result, InventoryError? Error)> AddUnitAsync(ClaimsPrincipal user, InventoryUnitRequest request, CancellationToken ct)
    {
        if (!(user.IsInRole("Admin") || user.IsInRole("Manager"))) return (null, Forbidden);
        var (warehouse, error) = await ResolveAsync(user, ct); if (warehouse is null) return (null, error);
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 40) return (null, Invalid("Tên đơn vị từ 1 đến 40 ký tự."));
        await using var tx = await db.Database.BeginTransactionAsync(ct); await RequestFingerprint.LockAsync(db, "inventory:" + warehouse.Id, ct);
        var normalized = Normalize(request.Name);
        var unit = (await db.InventoryUnits.Where(x => x.WarehouseId == warehouse.Id).ToListAsync(ct)).FirstOrDefault(x => InventoryIdentity.Normalize(x.Name) == InventoryIdentity.Normalize(request.Name));
        if (unit is null) {
            unit = new InventoryUnit { PropertyId = warehouse.PropertyId, WarehouseId = warehouse.Id, Name = request.Name.Trim(), NormalizedName = normalized };
            db.InventoryUnits.Add(unit); audit.Add(warehouse.PropertyId, "InventoryUnit", unit.Id, "Created", Actor(user), after: new { unit.Name });
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct); return (new(unit.Id, unit.Name), null);
    }

    public async Task<(InventoryItemDto? Result, InventoryError? Error)> SaveItemAsync(ClaimsPrincipal user, Guid? itemId, InventoryItemRequest request, CancellationToken ct)
    {
        if (!(user.IsInRole("Admin") || user.IsInRole("Manager"))) return (null, Forbidden);
        var (warehouse, error) = await ResolveAsync(user, ct); if (warehouse is null) return (null, error);
        if (request.Code?.Trim().Length > 40 || string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 200 ||
            request.ReferencePrice < 0 || request.ReferencePrice > 1_000_000_000m || decimal.Round(request.ReferencePrice, 2) != request.ReferencePrice)
            return (null, Invalid("Nhập tên hàng hợp lệ và giá không âm (tối đa 2 chữ số lẻ)."));
        await using var tx = await db.Database.BeginTransactionAsync(ct); await RequestFingerprint.LockAsync(db, "inventory:" + warehouse.Id, ct);
        var unit = await db.InventoryUnits.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.UnitId && x.WarehouseId == warehouse.Id, ct);
        if (unit is null) return (null, Invalid("Đơn vị không thuộc kho chung."));
        var item = itemId.HasValue ? await db.InventoryItems.SingleOrDefaultAsync(x => x.Id == itemId && x.WarehouseId == warehouse.Id, ct) : null;
        if (itemId.HasValue && item is null) return (null, new("not_found", "Không tìm thấy mặt hàng.", 404));
        var catalog = await db.InventoryItems.AsNoTracking().Where(x => x.WarehouseId == warehouse.Id).ToListAsync(ct);
        if (catalog.Any(x => x.Id != itemId && InventoryIdentity.Normalize(x.Name) == InventoryIdentity.Normalize(request.Name)))
            return (null, Invalid("Tên mặt hàng đã tồn tại. Tìm và cập nhật mặt hàng cũ."));
        var code = item?.Code ?? (string.IsNullOrWhiteSpace(request.Code) ? InventoryIdentity.NextCode(catalog.Select(x => x.Code)) : Normalize(request.Code));
        if (catalog.Any(x => x.Id != itemId && x.Code == code)) return (null, Invalid("Mã hàng đã tồn tại."));
        if (item is not null && item.UnitId != request.UnitId && await db.InventoryDocumentLines.AnyAsync(x => x.ItemId == item.Id, ct))
            return (null, Invalid("Hàng đã phát sinh phiếu không được đổi đơn vị. Tạo mã hàng mới nếu đổi quy cách."));
        var before = item is null ? null : new { item.Code, item.Name, item.UnitId, item.ReferencePrice, item.IsActive };
        if (item is null) { item = new InventoryItem { PropertyId = warehouse.PropertyId, WarehouseId = warehouse.Id }; db.InventoryItems.Add(item); }
        item.Code = code; item.Name = request.Name.Trim(); item.UnitId = unit.Id; item.ReferencePrice = request.ReferencePrice; item.IsActive = request.IsActive;
        audit.Add(warehouse.PropertyId, "InventoryItem", item.Id, before is null ? "Created" : "Updated", Actor(user), before,
            new { item.Code, item.Name, item.UnitId, item.ReferencePrice, item.IsActive });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return (new(item.Id, item.Code, item.Name, unit.Id, unit.Name, item.ReferencePrice, item.IsActive), null);
    }

    public async Task<(Guid? Result, InventoryError? Error)> PostAsync(ClaimsPrincipal user, InventoryDocumentRequest request, CancellationToken ct)
    {
        if (!(user.IsInRole("Admin") || user.IsInRole("Manager"))) return (null, Forbidden);
        var (warehouse, error) = await ResolveAsync(user, ct); if (warehouse is null) return (null, error);
        if (request.RequestKey == Guid.Empty || !Enum.IsDefined(request.Type) || request.OccurredAt == default || request.Note?.Length > 2000 ||
            request.Lines is null || request.Lines.Count is < 1 or > 100 || request.Lines.Any(x => x is null) || request.Lines.Select(x => x.ItemId).Distinct().Count() != request.Lines.Count ||
            request.Lines.Any(x => x.Quantity <= 0 || x.Quantity > 1_000_000_000m || decimal.Round(x.Quantity, 3) != x.Quantity || x.UnitPrice < 0 || x.UnitPrice > 1_000_000_000m || decimal.Round(x.UnitPrice, 2) != x.UnitPrice || x.Quantity * x.UnitPrice > 1_000_000_000_000m))
            return (null, Invalid("Phiếu cần ngày hợp lệ, 1–100 mặt hàng khác nhau; số lượng dương tối đa 3 chữ số lẻ, giá không âm."));
        if (request.RecipientName?.Length > 200) return (null, Invalid("Tên nơi nhận tối đa 200 ký tự."));
        string? recipientName = null;
        if (request.Type == InventoryDocumentType.Issue) {
            if (request.RecipientPropertyId.HasValue) {
                recipientName = await db.Properties.AsNoTracking().Where(x => x.Id == request.RecipientPropertyId && x.IsActive).Select(x => x.Name).SingleOrDefaultAsync(ct);
                if (recipientName is null) return (null, Invalid("Chi nhánh nhận không tồn tại hoặc đã ngừng hoạt động."));
                if (!user.IsInRole("Admin") && !await access.CanAccessAsync(user, request.RecipientPropertyId.Value, ct)) return (null, Forbidden);
            } else {
                if (string.IsNullOrWhiteSpace(request.RecipientName)) return (null, Invalid("Chọn chi nhánh hoặc nhập tên nơi nhận hàng."));
                recipientName = string.Join(' ', request.RecipientName.Normalize(NormalizationForm.FormC).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            }
        } else if (request.RecipientPropertyId.HasValue || request.RecipientName is not null) return (null, Invalid("Chỉ phiếu xuất có nơi nhận."));
        if (request.Type is InventoryDocumentType.AdjustmentIn or InventoryDocumentType.AdjustmentOut && string.IsNullOrWhiteSpace(request.Note)) return (null, Invalid("Điều chỉnh cần ghi rõ lý do."));
        await using var tx = await db.Database.BeginTransactionAsync(ct); await RequestFingerprint.LockAsync(db, "inventory:" + warehouse.Id, ct);
        // Retain fingerprints for retries sent by clients predating free-text recipients.
        var hash = request.RecipientName is null
            ? RequestFingerprint.Create(new { request.RequestKey, request.Type, request.OccurredAt, request.RecipientPropertyId, request.Note, request.Lines })
            : RequestFingerprint.Create(request);
        var prior = await db.InventoryDocuments.AsNoTracking().SingleOrDefaultAsync(x => x.WarehouseId == warehouse.Id && x.RequestKey == request.RequestKey, ct);
        if (prior is not null) return prior.RequestHash == hash ? (prior.Id, null) : (null, new("retry_conflict", "Mã gửi lại đã được dùng cho nội dung khác.", 409));
        var ids = request.Lines.Select(x => x.ItemId).ToArray();
        var items = await db.InventoryItems.AsNoTracking().Include(x => x.Unit).Where(x => ids.Contains(x.Id) && x.WarehouseId == warehouse.Id && x.IsActive).ToListAsync(ct);
        if (items.Count != ids.Length) return (null, Invalid("Mặt hàng không tồn tại, đã ẩn hoặc không thuộc kho chung."));
        if (request.Type == InventoryDocumentType.Opening && await db.InventoryDocumentLines.AnyAsync(x => ids.Contains(x.ItemId) && !x.Document.IsVoided, ct))
            return (null, Invalid("Tồn đầu chỉ khai báo khi mặt hàng chưa có phát sinh. Dùng phiếu điều chỉnh nếu cần sửa tồn."));
        var document = new InventoryDocument { PropertyId = warehouse.PropertyId, WarehouseId = warehouse.Id, RequestKey = request.RequestKey, RequestHash = hash,
            Code = "K" + Guid.CreateVersion7().ToString("N"), Type = request.Type, OccurredAtUtc = request.OccurredAt.UtcDateTime, RecipientPropertyId = request.RecipientPropertyId,
            RecipientName = recipientName, Note = request.Note?.Trim(), RecordedByUserId = Actor(user) };
        foreach (var row in request.Lines) {
            var item = items.Single(x => x.Id == row.ItemId);
            document.Lines.Add(new InventoryDocumentLine { PropertyId = warehouse.PropertyId, ItemId = item.Id, ItemName = item.Name, UnitName = item.Unit.Name, Quantity = row.Quantity, UnitPrice = row.UnitPrice });
        }
        if (!await BalancesValidAsync(warehouse.Id, ids, null, document, ct)) return (null, new("insufficient_stock", "Phiếu làm âm tồn tại ngày ghi nhận hoặc một ngày phát sinh sau đó.", 409));
        db.InventoryDocuments.Add(document);
        audit.Add(warehouse.PropertyId, "InventoryDocument", document.Id, "Posted", Actor(user), after: request);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return (document.Id, null);
    }

    public async Task<InventoryError?> VoidAsync(ClaimsPrincipal user, Guid documentId, string reason, CancellationToken ct)
    {
        if (!(user.IsInRole("Admin") || user.IsInRole("Manager"))) return Forbidden;
        var (warehouse, error) = await ResolveAsync(user, ct); if (warehouse is null) return error;
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1000) return Invalid("Nhập lý do hủy, tối đa 1000 ký tự.");
        await using var tx = await db.Database.BeginTransactionAsync(ct); await RequestFingerprint.LockAsync(db, "inventory:" + warehouse.Id, ct);
        var document = await db.InventoryDocuments.Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == documentId && x.WarehouseId == warehouse.Id, ct);
        if (document is null) return new("not_found", "Không tìm thấy phiếu.", 404);
        if (document.IsVoided) return null;
        if (!await BalancesValidAsync(warehouse.Id, document.Lines.Select(x => x.ItemId).ToArray(), document.Id, null, ct)) return new("insufficient_stock", "Không thể hủy vì sẽ làm âm tồn. Kiểm tra các phiếu xuất liên quan.", 409);
        document.IsVoided = true; document.VoidReason = reason.Trim(); document.VoidedAtUtc = DateTime.UtcNow; document.VoidedByUserId = Actor(user);
        audit.Add(warehouse.PropertyId, "InventoryDocument", document.Id, "Voided", Actor(user), after: new { document.VoidReason });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return null;
    }

    private async Task<bool> BalancesValidAsync(Guid warehouseId, Guid[] ids, Guid? excludeId, InventoryDocument? add, CancellationToken ct)
    {
        var entries = await db.InventoryDocumentLines.AsNoTracking().Where(x => ids.Contains(x.ItemId) && x.Document.WarehouseId == warehouseId && !x.Document.IsVoided && x.DocumentId != excludeId)
            .Select(x => new { x.ItemId, x.Document.OccurredAtUtc, x.Quantity, x.Document.Type }).ToListAsync(ct);
        var rows = entries.Select(x => (x.ItemId, At: x.OccurredAtUtc, Delta: x.Quantity * Direction(x.Type))).ToList();
        if (add is not null) rows.AddRange(add.Lines.Select(x => (x.ItemId, add.OccurredAtUtc, x.Quantity * Direction(add.Type))));
        foreach (var item in rows.GroupBy(x => x.ItemId)) {
            decimal balance = 0;
            foreach (var day in item.GroupBy(x => x.At).OrderBy(x => x.Key)) { balance += day.Sum(x => x.Delta); if (balance < 0) return false; }
        }
        return true;
    }
    public async Task<(IReadOnlyList<InventoryDocumentDto>? Result, InventoryError? Error)> HistoryAsync(ClaimsPrincipal user, Guid itemId, int skip, CancellationToken ct)
    {
        var (warehouse, error) = await ResolveAsync(user, ct); if (warehouse is null) return (null, error);
        if (!await db.InventoryItems.AsNoTracking().AnyAsync(x => x.Id == itemId && x.WarehouseId == warehouse.Id, ct)) return (null, new("not_found", "Không tìm thấy mặt hàng.", 404));
        var rows = await db.InventoryDocuments.AsNoTracking().Where(x => x.WarehouseId == warehouse.Id && x.Lines.Any(l => l.ItemId == itemId))
            .OrderByDescending(x => x.OccurredAtUtc).ThenByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip(Math.Max(0, skip)).Take(100)
            .Select(x => new InventoryDocumentDto(x.Id, x.Code, x.Type, x.OccurredAtUtc, x.RecipientPropertyId,
                x.RecipientName ?? (x.RecipientProperty == null ? null : x.RecipientProperty.Name), x.Note, x.RecordedByUserId,
                db.Users.Where(u => u.Id == x.RecordedByUserId).Select(u => u.DisplayName != "" ? u.DisplayName : u.UserName).FirstOrDefault(),
                x.CreatedAtUtc, x.IsVoided, x.VoidReason, x.Lines.Where(l => l.ItemId == itemId).Select(l => new InventoryLineDto(l.ItemId, l.ItemName, l.UnitName, l.Quantity, l.UnitPrice)).ToList())).ToListAsync(ct);
        return (rows, null);
    }
    private static InventoryWarehouseDto ToWarehouse(InventoryWarehouse x) => new(x.Id, x.PropertyId, x.Name, x.Property.Name, x.Property.TimeZoneId);
}
