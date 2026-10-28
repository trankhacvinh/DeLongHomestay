using System.Globalization;
using System.IO.Compression;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using DeLong.Web.Common.Auditing;
using DeLong.Web.Common.Persistence;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Inventory;

public sealed class InventoryImportService(AppDbContext db, InventoryService inventory, AuditService audit, IDataProtectionProvider protection)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly IDataProtector protector = protection.CreateProtector("DeLong.InventoryImport.v1");
    private sealed record Ticket(Guid WarehouseId, string UserId, DateTime ExpiresUtc, InventoryImportOptions Options, string Sheet, IReadOnlyList<InventoryImportRow> Rows, string StateHash);
    private sealed record State(IReadOnlyList<InventoryItem> Items, IReadOnlyList<InventoryUnit> Units, IReadOnlySet<Guid> UsedItems, string Hash);
    private static InventoryError Invalid(string message, string code = "import_invalid", int status = 400) => new(code, message, status);
    private static bool CanManage(ClaimsPrincipal user) => user.IsInRole("Admin") || user.IsInRole("Manager");

    public async Task<(InventoryImportPreview? Result, InventoryError? Error)> PreviewAsync(ClaimsPrincipal user, IFormFile file, InventoryImportOptions options, CancellationToken ct)
    {
        if (!CanManage(user)) return (null, Invalid("Bạn không có quyền nhập kho.", "forbidden", 403));
        var (warehouse, error) = await inventory.ResolveAsync(user, ct); if (warehouse is null) return (null, error);
        if (options.Mode is not ("catalog" or "opening") || options.Duplicates is not ("skip" or "update") || options.Mode == "opening" && (!options.OpeningAt.HasValue || options.OpeningAt == default(DateTimeOffset)))
            return (null, Invalid("Chọn loại import, cách xử lý trùng và ngày tồn đầu hợp lệ."));
        var (sheet, rows, fileError) = await ReadAsync(file, options, ct); if (fileError is not null) return (null, fileError);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await RequestFingerprint.LockAsync(db, "inventory:" + warehouse.Id, ct);
        var state = await ReadStateAsync(warehouse.Id, ct);
        var plan = Plan(rows!, options, state);
        var errors = plan.Count(x => x.Action == "error");
        var token = errors > 0 ? null : protector.Protect(JsonSerializer.Serialize(new Ticket(warehouse.Id, user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "", DateTime.UtcNow.AddMinutes(20), options, sheet!, rows!, state.Hash), Json));
        return (new(token, sheet!, plan.Count(x => x.Action == "create"), plan.Count(x => x.Action == "update"),
            plan.Where(x => x.Action != "skip" && x.Action != "error").Select(x => InventoryIdentity.Normalize(x.Unit)).Distinct().Count(x => !state.Units.Any(u => InventoryIdentity.Normalize(u.Name) == x)),
            plan.Count(x => x.OpeningQuantity > 0), plan.Count(x => x.Action == "skip"), errors, plan), null);
    }

    public async Task<(InventoryImportResult? Result, InventoryError? Error)> CommitAsync(ClaimsPrincipal user, InventoryImportCommit request, CancellationToken ct)
    {
        if (!CanManage(user)) return (null, Invalid("Bạn không có quyền nhập kho.", "forbidden", 403));
        if (request.RequestKey == Guid.Empty || string.IsNullOrWhiteSpace(request.Token) || request.Token.Length > 1_000_000) return (null, Invalid("Bản xem trước không hợp lệ."));
        var (warehouse, error) = await inventory.ResolveAsync(user, ct); if (warehouse is null) return (null, error);
        Ticket? ticket;
        try { ticket = JsonSerializer.Deserialize<Ticket>(protector.Unprotect(request.Token), Json); }
        catch (Exception e) when (e is CryptographicException or JsonException or FormatException) { return (null, Invalid("Bản xem trước không hợp lệ. Hãy chọn file và xem trước lại.")); }
        if (ticket is null || ticket.WarehouseId != warehouse.Id || ticket.UserId != user.FindFirstValue(ClaimTypes.NameIdentifier)) return (null, Invalid("Bản xem trước không thuộc tài khoản/kho này.", "forbidden", 403));
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await RequestFingerprint.LockAsync(db, "inventory:" + warehouse.Id, ct);
        var hash = RequestFingerprint.Create(request.Token);
        var previous = await db.InventoryImports.AsNoTracking().SingleOrDefaultAsync(x => x.WarehouseId == warehouse.Id && x.RequestKey == request.RequestKey, ct);
        if (previous is not null) return previous.RequestHash == hash ? (JsonSerializer.Deserialize<InventoryImportResult>(previous.ResultJson, Json), null) : (null, Invalid("Mã gửi lại đã dùng cho lần import khác.", "retry_conflict", 409));
        if (ticket.ExpiresUtc < DateTime.UtcNow) return (null, Invalid("Bản xem trước đã hết hạn. Hãy xem trước lại."));
        var state = await ReadStateAsync(warehouse.Id, ct);
        if (state.Hash != ticket.StateHash) return (null, Invalid("Dữ liệu kho đã thay đổi. Hãy xem trước lại trước khi import.", "preview_stale", 409));
        var plan = Plan(ticket.Rows, ticket.Options, state);
        if (plan.Any(x => x.Action == "error")) return (null, Invalid("File còn dòng lỗi. Hãy sửa và xem trước lại."));
        var units = state.Units.GroupBy(x => InventoryIdentity.Normalize(x.Name)).ToDictionary(x => x.Key, x => x.First());
        var codes = state.Items.Select(x => x.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        InventoryDocument? opening = null;
        var created = 0; var updated = 0; var newUnits = 0;
        foreach (var row in plan.Where(x => x.Action != "skip")) {
            if (!units.TryGetValue(InventoryIdentity.Normalize(row.Unit), out var unit)) {
                unit = new InventoryUnit { PropertyId = warehouse.PropertyId, WarehouseId = warehouse.Id, Name = row.Unit.Trim(), NormalizedName = InventoryIdentity.Normalize(row.Unit) };
                units.Add(InventoryIdentity.Normalize(row.Unit), unit); db.InventoryUnits.Add(unit); newUnits++;
            }
            InventoryItem item;
            if (!row.ItemId.HasValue) {
                item = new InventoryItem { PropertyId = warehouse.PropertyId, WarehouseId = warehouse.Id, Name = row.Name.Trim(), UnitId = unit.Id, Code = InventoryIdentity.NextCode(codes), ReferencePrice = row.Price ?? 0 };
                codes.Add(item.Code); db.InventoryItems.Add(item); created++;
            } else {
                item = state.Items.Single(x => x.Id == row.ItemId);
                if (row.Action == "update") {
                    item = db.InventoryItems.Local.FirstOrDefault(x => x.Id == row.ItemId) ?? db.InventoryItems.Attach(item).Entity;
                    item.Name = row.Name.Trim(); if (row.Price.HasValue) item.ReferencePrice = row.Price.Value; updated++;
                }
            }
            if (row.OpeningQuantity > 0) {
                opening ??= new InventoryDocument { PropertyId = warehouse.PropertyId, WarehouseId = warehouse.Id, RequestKey = request.RequestKey,
                    RequestHash = hash, Code = "K" + Guid.CreateVersion7().ToString("N"), Type = InventoryDocumentType.Opening,
                    OccurredAtUtc = ticket.Options.OpeningAt!.Value.UtcDateTime, RecordedByUserId = Actor(user), Note = "Import tồn đầu" };
                opening.Lines.Add(new InventoryDocumentLine { PropertyId = warehouse.PropertyId, ItemId = item.Id, ItemName = item.Name, UnitName = unit.Name, Quantity = row.OpeningQuantity, UnitPrice = row.Price ?? item.ReferencePrice });
            }
        }
        if (opening is not null) db.InventoryDocuments.Add(opening);
        var result = new InventoryImportResult(created, updated, newUnits, opening?.Lines.Count ?? 0, plan.Count(x => x.Action == "skip"), opening?.Id);
        db.InventoryImports.Add(new InventoryImport { PropertyId = warehouse.PropertyId, WarehouseId = warehouse.Id, RequestKey = request.RequestKey, RequestHash = hash, ResultJson = JsonSerializer.Serialize(result, Json) });
        audit.Add(warehouse.PropertyId, "InventoryImport", request.RequestKey, "Imported", Actor(user), after: new { ticket.Options, ticket.Sheet, Rows = plan, Result = result });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return (result, null);
    }

    private async Task<State> ReadStateAsync(Guid warehouseId, CancellationToken ct)
    {
        var items = await db.InventoryItems.AsNoTracking().Where(x => x.WarehouseId == warehouseId).OrderBy(x => x.Id).ToListAsync(ct);
        var units = await db.InventoryUnits.AsNoTracking().Where(x => x.WarehouseId == warehouseId).OrderBy(x => x.Id).ToListAsync(ct);
        var used = await db.InventoryDocumentLines.AsNoTracking().Where(x => x.Document.WarehouseId == warehouseId && !x.Document.IsVoided).Select(x => x.ItemId).Distinct().OrderBy(x => x).ToListAsync(ct);
        var hash = RequestFingerprint.Create(new { Items = items.Select(x => new { x.Id, x.Name, x.Code, x.UnitId, x.ReferencePrice, x.IsActive, x.UpdatedAtUtc }), Units = units.Select(x => new { x.Id, x.Name, x.NormalizedName }), Used = used });
        return new(items, units, used.ToHashSet(), hash);
    }

    private static List<InventoryImportPreviewRow> Plan(IReadOnlyList<InventoryImportRow> rows, InventoryImportOptions options, State state)
    {
        var result = new List<InventoryImportPreviewRow>();
        var seen = new Dictionary<string, InventoryImportRow>();
        foreach (var row in rows) {
            var name = InventoryIdentity.Normalize(row.Name); var unitName = InventoryIdentity.Normalize(row.Unit);
            string? error = row.Error;
            if (name.Length is < 1 or > 200 || unitName.Length is < 1 or > 40) error ??= "Tên hàng/đơn vị trống hoặc quá dài.";
            if (row.Price is < 0 or > 1_000_000_000m || row.Price.HasValue && decimal.Round(row.Price.Value, 2) != row.Price) error ??= "Đơn giá phải không âm, tối đa 2 chữ số lẻ.";
            if (options.Mode == "opening" && (!row.Quantity.HasValue || row.Quantity is < 0 or > 1_000_000_000m || decimal.Round(row.Quantity.Value, 3) != row.Quantity)) error ??= "Tồn đầu phải không âm, tối đa 3 chữ số lẻ.";
            if (row.Price.GetValueOrDefault() * row.Quantity.GetValueOrDefault() > 1_000_000_000_000m) error ??= "Giá trị dòng vượt giới hạn.";
            if (state.Units.Count(x => InventoryIdentity.Normalize(x.Name) == unitName) > 1) error ??= "Đơn vị trong kho đang bị trùng; cần kiểm tra trước khi nhập.";
            var matches = state.Items.Where(x => InventoryIdentity.Normalize(x.Name) == name).ToList();
            if (matches.Count > 1) error ??= "Tên này khớp nhiều mặt hàng trong kho; cần đổi tên để phân biệt.";
            var existing = matches.Count == 1 ? matches[0] : null;
            if (existing is not null) {
                var unit = state.Units.SingleOrDefault(x => x.Id == existing.UnitId);
                if (unit is null || InventoryIdentity.Normalize(unit.Name) != unitName) error ??= "Tên đã có nhưng khác đơn vị; không tự đổi quy cách.";
            }
            if (error is not null) { result.Add(new(row.Row, row.Name, row.Unit, row.Quantity, row.Price, "error", error, existing?.Id, 0)); continue; }
            if (seen.TryGetValue(name, out var prior)) {
                var same = InventoryIdentity.Normalize(prior.Unit) == unitName && prior.Price == row.Price && prior.Quantity == row.Quantity;
                result.Add(new(row.Row, row.Name, row.Unit, row.Quantity, row.Price, same ? "skip" : "error", same ? $"Trùng dòng {prior.Row} trong file; bỏ qua." : $"Trùng tên với dòng {prior.Row} nhưng khác nội dung.", existing?.Id, 0)); continue;
            }
            seen.Add(name, row);
            var opening = options.Mode == "opening" && (existing is null || existing.IsActive && !state.UsedItems.Contains(existing.Id)) ? row.Quantity.GetValueOrDefault() : 0;
            var action = existing is null ? "create" : !existing.IsActive ? "skip" : options.Duplicates == "update" ? "update" : opening > 0 ? "opening" : "skip";
            var message = existing is null ? "Thêm mặt hàng" : !existing.IsActive ? "Mặt hàng đã ẩn; bỏ qua" : action == "update" ? "Cập nhật tên/giá" : action == "opening" ? "Giữ danh mục" : "Đã có; bỏ qua";
            if (opening > 0) message += "; ghi tồn đầu";
            else if (options.Mode == "opening" && existing is not null && state.UsedItems.Contains(existing.Id)) message += "; giữ tồn đã có phát sinh";
            result.Add(new(row.Row, row.Name, row.Unit, row.Quantity, row.Price, action, message + ".", existing?.Id, opening));
        }
        return result;
    }

    private static async Task<(string? Sheet, IReadOnlyList<InventoryImportRow>? Rows, InventoryError? Error)> ReadAsync(IFormFile file, InventoryImportOptions options, CancellationToken ct)
    {
        if (file.Length is <= 0 or > 4 * 1024 * 1024 || !string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase)) return (null, null, Invalid("Chọn file .xlsx tối đa 4 MB."));
        using var input = new MemoryStream(); await file.CopyToAsync(input, ct); input.Position = 0;
        try {
            using (var zip = new ZipArchive(input, ZipArchiveMode.Read, true)) {
                if (zip.Entries.Count > 4000 || zip.Entries.Sum(x => x.Length) > 24 * 1024 * 1024) return (null, null, Invalid("File Excel quá lớn sau giải nén."));
            }
            input.Position = 0; using var book = new XLWorkbook(input);
            var candidates = new List<(IXLWorksheet Sheet, int Header, Dictionary<string, int> Columns)>();
            foreach (var sheet in book.Worksheets.Where(x => string.IsNullOrWhiteSpace(options.Sheet) || string.Equals(x.Name, options.Sheet.Trim(), StringComparison.OrdinalIgnoreCase))) {
                for (var r = 1; r <= Math.Min(20, sheet.LastRowUsed()?.RowNumber() ?? 0); r++) {
                    var columns = new Dictionary<string, int>();
                    for (var c = 1; c <= Math.Min(64, sheet.LastColumnUsed()?.ColumnNumber() ?? 0); c++) {
                        var cell = sheet.Cell(r, c); var key = cell.HasFormula ? null : HeaderKey(cell.GetString()); if (key is not null) columns.TryAdd(key, c);
                    }
                    if (columns.ContainsKey("name") && columns.ContainsKey("unit") && (options.Mode != "opening" || columns.ContainsKey("quantity"))) { candidates.Add((sheet, r, columns)); break; }
                }
            }
            if (candidates.Count != 1) return (null, null, Invalid(candidates.Count == 0 ? "Không tìm thấy bảng phù hợp. Dùng file mẫu có cột Tên mặt hàng, Đơn vị tính và Tồn đầu (nếu nhập tồn)." : "Có nhiều bảng phù hợp: " + string.Join(", ", candidates.Select(x => x.Sheet.Name)) + ". Nhập tên sheet cần đọc."));
            var selected = candidates[0]; var last = selected.Sheet.LastRowUsed()?.RowNumber() ?? selected.Header;
            if (last > 2000) return (null, null, Invalid("Sheet có quá 2.000 dòng. Chỉ giữ bảng cần import."));
            var rows = new List<InventoryImportRow>();
            for (var r = selected.Header + 1; r <= last; r++) {
                ct.ThrowIfCancellationRequested();
                var name = selected.Sheet.Cell(r, selected.Columns["name"]); var unit = selected.Sheet.Cell(r, selected.Columns["unit"]);
                IXLCell? Cell(string key) => selected.Columns.TryGetValue(key, out var column) ? selected.Sheet.Cell(r, column) : null;
                var quantity = options.Mode == "opening" ? Cell("quantity") : null; var price = Cell("price");
                if (new[] { name, unit, quantity, price }.All(x => x is null || x.IsEmpty())) continue;
                string? error = new[] { name, unit, quantity, price }.Any(x => x?.HasFormula == true) ? "Có công thức; chuyển các ô nhập thành giá trị trước khi import." : null;
                decimal? Number(IXLCell? cell, string label) {
                    if (cell is null || cell.IsEmpty() || cell.HasFormula) return null;
                    if (cell.DataType == XLDataType.Number && cell.TryGetValue<decimal>(out var value)) return value;
                    var text = cell.GetString().Trim();
                    if (decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value)) return value;
                    error ??= label + " phải là ô số Excel (không kèm ký hiệu tiền/phân cách nghìn)."; return null;
                }
                var qty = Number(quantity, "Tồn đầu"); var amount = Number(price, "Đơn giá");
                rows.Add(new(r, name.HasFormula ? "" : name.GetString().Trim(), unit.HasFormula ? "" : unit.GetString().Trim(), qty, amount, error));
                if (rows.Count > 500) return (null, null, Invalid("Mỗi lần import tối đa 500 mặt hàng."));
            }
            return rows.Count == 0 ? (null, null, Invalid("File chưa có mặt hàng.")) : (selected.Sheet.Name, rows, null);
        } catch (Exception e) when (e is not OperationCanceledException and not OutOfMemoryException) { return (null, null, Invalid("Không đọc được file Excel. Hãy lưu lại thành .xlsx và thử lại.")); }
    }

    private static string? HeaderKey(string value)
    {
        var text = InventoryIdentity.Normalize(value).Normalize(NormalizationForm.FormD);
        text = new string(text.Where(x => CharUnicodeInfo.GetUnicodeCategory(x) != UnicodeCategory.NonSpacingMark).ToArray()).Replace('Đ', 'D');
        return text switch { "TEN MAT HANG" or "MAT HANG" or "TEN HANG" or "TEN HANG HOA" => "name", "DON VI TINH" or "DON VI" or "DVT" => "unit", "TON DAU" or "TON DAU KY" or "SO LUONG" => "quantity", "DON GIA" or "GIA THAM KHAO" => "price", _ => null };
    }
    public static byte[] Template(string mode)
    {
        using var book = new XLWorkbook(); var sheet = book.AddWorksheet(mode == "opening" ? "Ton dau" : "Mat hang");
        var headers = mode == "opening" ? new[] { "Tên mặt hàng", "Đơn vị tính", "Tồn đầu", "Đơn giá" } : ["Tên mặt hàng", "Đơn vị tính", "Giá tham khảo"];
        for (var c = 0; c < headers.Length; c++) sheet.Cell(1, c + 1).Value = headers[c];
        sheet.Range(1, 1, 1, headers.Length).Style.Font.Bold = true; sheet.Range(1, 1, 1, headers.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#E6F1EF");
        sheet.Column(1).Width = 36; sheet.Column(2).Width = 20; sheet.Columns(3, headers.Length).Width = 20; sheet.SheetView.FreezeRows(1);
        using var stream = new MemoryStream(); book.SaveAs(stream); return stream.ToArray();
    }
    private static Guid? Actor(ClaimsPrincipal user) => Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
