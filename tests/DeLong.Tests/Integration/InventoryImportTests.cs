using System.Security.Claims;
using ClosedXML.Excel;
using DeLong.Web.Common.Auditing;
using DeLong.Web.Common.Security;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Features.Inventory;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeLong.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class InventoryImportTests
{
    private static readonly DateTimeOffset OpeningAt = new(2026, 10, 1, 0, 0, 0, TimeSpan.FromHours(7));
    private static readonly InventoryImportOptions Catalog = new("catalog", "skip", null);
    private static InventoryService Inventory(AppDbContext db) => new(db, new AuditService(db), new PropertyAccessService(db));
    private static InventoryImportService Imports(AppDbContext db, IDataProtectionProvider provider) => new(db, Inventory(db), new AuditService(db), provider);
    private static ClaimsPrincipal Admin() => InventoryCashTests.User(Guid.NewGuid(), "Admin");
    private static async Task Setup(AppDbContext db, ClaimsPrincipal user)
    {
        await db.Database.MigrateAsync();
        var owner = await db.InventoryWarehouses.AsNoTracking().Select(x => x.PropertyId).FirstOrDefaultAsync();
        if (owner == Guid.Empty) { var property = new Property { Code = Guid.NewGuid().ToString("N"), Name = "Inventory import test" }; db.Add(property); await db.SaveChangesAsync(); owner = property.Id; }
        Assert.Null((await Inventory(db).InitializeAsync(user, owner, default)).Error);
    }
    private static IFormFile File(bool opening, params object?[][] rows)
    {
        using var book = new XLWorkbook(new MemoryStream(InventoryImportService.Template(opening ? "opening" : "catalog")));
        var sheet = book.Worksheet(1);
        for (var r = 0; r < rows.Length; r++) for (var c = 0; c < rows[r].Length; c++) {
            var cell = sheet.Cell(r + 2, c + 1);
            if (rows[r][c] is decimal amount) cell.Value = (double)amount;
            else if (rows[r][c] is int integer) cell.Value = integer;
            else cell.Value = rows[r][c]?.ToString() ?? "";
        }
        var stream = new MemoryStream(); book.SaveAs(stream); stream.Position = 0;
        return new FormFile(stream, 0, stream.Length, "file", "inventory.xlsx");
    }

    [RoomLockPostgresFact]
    public async Task Codes_are_automatic_stable_and_normalized_names_cannot_be_created_twice()
    {
        await using var db = InventoryCashTests.Database(); var user = Admin(); await Setup(db, user);
        var service = Inventory(db); var suffix = Guid.NewGuid().ToString("N")[..8];
        var unit = (await service.AddUnitAsync(user, new("kg"), default)).Result!;
        Assert.Equal(unit.Id, (await service.AddUnitAsync(user, new(" kG "), default)).Result!.Id);
        var first = await service.SaveItemAsync(user, null, new(null, "Hàng " + suffix, unit.Id, 0), default);
        Assert.Null(first.Error); Assert.Matches("^MH[0-9]{6,}$", first.Result!.Code);
        var second = await service.SaveItemAsync(user, null, new(null, "Khác " + suffix, unit.Id, 0), default);
        Assert.NotEqual(first.Result.Code, second.Result!.Code);
        Assert.NotNull((await service.SaveItemAsync(user, null, new(null, " HÀNG   " + suffix, unit.Id, 1), default)).Error);
        var edit = await service.SaveItemAsync(user, first.Result.Id, new(null, "Hàng sửa " + suffix, unit.Id, 2), default);
        Assert.Equal(first.Result.Code, edit.Result!.Code);
    }

    [RoomLockPostgresFact]
    public async Task Catalog_import_previews_without_writes_deduplicates_units_and_rows_and_supports_skip_update_retry()
    {
        await using var db = InventoryCashTests.Database(); var user = Admin(); await Setup(db, user);
        var provider = new EphemeralDataProtectionProvider(); var service = Imports(db, provider); var name = "Gạo " + Guid.NewGuid().ToString("N")[..8];
        var count = await db.InventoryItems.CountAsync();
        var preview = await service.PreviewAsync(user, File(false, [name, "kg", 12m], [" " + name.ToUpperInvariant() + " ", "KG", 12m], [name + " B", "kG", 15m]), Catalog, default);
        Assert.Null(preview.Error); Assert.Equal(2, preview.Result!.NewItems); Assert.Equal(1, preview.Result.Skipped);
        Assert.Equal(count, await db.InventoryItems.CountAsync());
        var request = new InventoryImportCommit(Guid.NewGuid(), preview.Result.Token!);
        var committed = await service.CommitAsync(user, request, default); Assert.Null(committed.Error); Assert.Equal(2, committed.Result!.CreatedItems);
        Assert.Equal(committed.Result, (await service.CommitAsync(user, request, default)).Result);
        Assert.Equal(count + 2, await db.InventoryItems.CountAsync());
        var item = await db.InventoryItems.AsNoTracking().SingleAsync(x => x.Name == name);
        var skip = (await service.PreviewAsync(user, File(false, [name.ToUpperInvariant(), "KG", 20m]), Catalog, default)).Result!;
        Assert.Equal("skip", skip.Rows[0].Action);
        Assert.Null((await service.CommitAsync(user, new(Guid.NewGuid(), skip.Token!), default)).Error);
        Assert.Equal(12, (await db.InventoryItems.AsNoTracking().SingleAsync(x => x.Id == item.Id)).ReferencePrice);
        db.ChangeTracker.Clear();
        var update = (await service.PreviewAsync(user, File(false, [name.ToUpperInvariant(), "kG", 20m]), Catalog with { Duplicates = "update" }, default)).Result!;
        Assert.Equal(1, update.UpdatedItems);
        Assert.Null((await service.CommitAsync(user, new(Guid.NewGuid(), update.Token!), default)).Error);
        var updated = await db.InventoryItems.AsNoTracking().SingleAsync(x => x.Id == item.Id);
        Assert.Equal(item.Code, updated.Code); Assert.Equal(20, updated.ReferencePrice);
        Assert.Single(await db.InventoryUnits.AsNoTracking().Where(x => x.WarehouseId == item.WarehouseId).ToListAsync(), x => x.Name.Trim().Equals("kg", StringComparison.OrdinalIgnoreCase));
    }

    [RoomLockPostgresFact]
    public async Task Opening_import_populates_existing_unused_items_and_new_items_without_rewriting_existing_stock()
    {
        await using var db = InventoryCashTests.Database(); var user = Admin(); await Setup(db, user);
        var provider = new EphemeralDataProtectionProvider(); var service = Imports(db, provider); var name = "Opening " + Guid.NewGuid().ToString("N")[..8];
        var catalog = (await service.PreviewAsync(user, File(false, [name, "kg", 10m]), Catalog, default)).Result!;
        Assert.Null((await service.CommitAsync(user, new(Guid.NewGuid(), catalog.Token!), default)).Error);
        db.ChangeTracker.Clear();
        var options = new InventoryImportOptions("opening", "skip", OpeningAt);
        var preview = (await service.PreviewAsync(user, File(true, [name, "KG", 1.5m, 11m], [name + " B", "kG", 2m, 12m]), options, default)).Result!;
        Assert.Equal(2, preview.OpeningItems); Assert.Equal("opening", preview.Rows[0].Action);
        var result = await service.CommitAsync(user, new(Guid.NewGuid(), preview.Token!), default); Assert.Null(result.Error);
        Assert.Equal(2, result.Result!.OpeningItems);
        var doc = await db.InventoryDocuments.AsNoTracking().Include(x => x.Lines).SingleAsync(x => x.Id == result.Result.DocumentId);
        Assert.Equal(OpeningAt.UtcDateTime, doc.OccurredAtUtc); Assert.Equal(3.5m, doc.Lines.Sum(x => x.Quantity));
        var stock = (await Inventory(db).GetAsync(user, "2026-10", default)).Result!.Stock.Single(x => x.Name == name);
        Assert.Equal(1.5m, stock.Opening); Assert.Equal(1.5m, stock.Closing);
        db.ChangeTracker.Clear();
        var reimport = (await service.PreviewAsync(user, File(true, [name, "kg", 900m, 99m]), options with { Duplicates = "update" }, default)).Result!;
        Assert.Equal(0, reimport.OpeningItems); Assert.Contains("giữ tồn", reimport.Rows[0].Message);
        Assert.Null((await service.CommitAsync(user, new(Guid.NewGuid(), reimport.Token!), default)).Error);
        Assert.Equal(1.5m, (await Inventory(db).GetAsync(user, "2026-10", default)).Result!.Stock.Single(x => x.Name == name).Closing);
        Assert.Equal(11, (await db.InventoryDocumentLines.AsNoTracking().FirstAsync(x => x.DocumentId == doc.Id && x.ItemName == name)).UnitPrice);
    }

    [RoomLockPostgresFact]
    public async Task Preview_errors_block_batch_and_invalid_files_formula_and_unit_changes_are_reported()
    {
        await using var db = InventoryCashTests.Database(); var user = Admin(); await Setup(db, user); var service = Imports(db, new EphemeralDataProtectionProvider());
        var name = "Invalid " + Guid.NewGuid().ToString("N")[..8];
        var preview = await service.PreviewAsync(user, File(true, [name, "kg", 2m, 1m], [name, "KG", 3m, 1m], [name + " bad", "", -1m, 0m]), new("opening", "skip", OpeningAt), default);
        Assert.Null(preview.Error); Assert.Equal(2, preview.Result!.Errors); Assert.Null(preview.Result.Token);
        Assert.False(await db.InventoryItems.AsNoTracking().AnyAsync(x => x.Name == name));
        using var formula = new XLWorkbook(new MemoryStream(InventoryImportService.Template("catalog")));
        formula.Worksheet(1).Cell(2, 1).Value = name; formula.Worksheet(1).Cell(2, 2).Value = "kg"; formula.Worksheet(1).Cell(2, 3).FormulaA1 = "1+2";
        var stream = new MemoryStream(); formula.SaveAs(stream); stream.Position = 0;
        var errorRow = (await service.PreviewAsync(user, new FormFile(stream, 0, stream.Length, "file", "formula.xlsx"), Catalog, default)).Result!;
        Assert.Equal(1, errorRow.Errors); Assert.Null(errorRow.Token);
        var bytes = new byte[] { 1, 2, 3 };
        Assert.NotNull((await service.PreviewAsync(user, new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "bad.xlsx"), Catalog, default)).Error);
        Assert.NotNull((await service.PreviewAsync(user, new FormFile(new MemoryStream(bytes), 0, 5 * 1024 * 1024, "file", "large.xlsx"), Catalog, default)).Error);
        var unit = (await Inventory(db).AddUnitAsync(user, new("kg"), default)).Result!;
        Assert.Null((await Inventory(db).SaveItemAsync(user, null, new(null, name, unit.Id, 0), default)).Error);
        var mismatch = (await service.PreviewAsync(user, File(false, [name, "chai", 0m]), Catalog, default)).Result!;
        Assert.Equal(1, mismatch.Errors); Assert.Contains("khác đơn vị", mismatch.Rows[0].Message);
    }

    [RoomLockPostgresFact]
    public async Task Stale_preview_tampering_and_other_users_cannot_commit()
    {
        await using var db = InventoryCashTests.Database(); var user = Admin(); await Setup(db, user); var service = Imports(db, new EphemeralDataProtectionProvider());
        var name = "Stale " + Guid.NewGuid().ToString("N")[..8];
        var preview = (await service.PreviewAsync(user, File(false, [name, "kg", 1m]), Catalog, default)).Result!;
        Assert.NotNull((await service.CommitAsync(user, new(Guid.NewGuid(), preview.Token! + "corrupt"), default)).Error);
        Assert.Equal(403, (await service.CommitAsync(Admin(), new(Guid.NewGuid(), preview.Token!), default)).Error!.Status);
        await Inventory(db).AddUnitAsync(user, new("Changed " + Guid.NewGuid().ToString("N")[..8]), default);
        Assert.Equal("preview_stale", (await service.CommitAsync(user, new(Guid.NewGuid(), preview.Token!), default)).Error!.Code);
        Assert.False(await db.InventoryItems.AsNoTracking().AnyAsync(x => x.Name == name));
        Assert.Equal(403, (await service.PreviewAsync(InventoryCashTests.User(Guid.NewGuid(), "Viewer"), File(false, [name, "kg", 1m]), Catalog, default)).Error!.Status);
    }

    [RoomLockPostgresFact]
    public async Task Concurrent_commit_is_applied_once_and_multi_sheet_requires_selection()
    {
        await using var db = InventoryCashTests.Database(); var user = Admin(); await Setup(db, user); var provider = new EphemeralDataProtectionProvider(); var service = Imports(db, provider);
        var name = "Concurrent " + Guid.NewGuid().ToString("N")[..8];
        var preview = (await service.PreviewAsync(user, File(true, [name, "KG", 2m, 0m]), new("opening", "skip", OpeningAt), default)).Result!;
        var request = new InventoryImportCommit(Guid.NewGuid(), preview.Token!);
        async Task<(InventoryImportResult? Result, InventoryError? Error)> Commit() { await using var other = InventoryCashTests.Database(); return await Imports(other, provider).CommitAsync(user, request, default); }
        var results = await Task.WhenAll(Commit(), Commit()); Assert.All(results, x => Assert.Null(x.Error)); Assert.Equal(results[0].Result, results[1].Result);
        Assert.Single(await db.InventoryImports.AsNoTracking().Where(x => x.RequestKey == request.RequestKey).ToListAsync());
        using var book = new XLWorkbook(new MemoryStream(InventoryImportService.Template("catalog")));
        book.Worksheet(1).Cell(2, 1).Value = name + " multi"; book.Worksheet(1).Cell(2, 2).Value = "kg";
        book.Worksheet(1).CopyTo("Second");
        IFormFile Multi() { var stream = new MemoryStream(); book.SaveAs(stream); stream.Position = 0; return new FormFile(stream, 0, stream.Length, "file", "multi.xlsx"); }
        Assert.NotNull((await service.PreviewAsync(user, Multi(), Catalog, default)).Error);
        Assert.Null((await service.PreviewAsync(user, Multi(), Catalog with { Sheet = "Second" }, default)).Error);
    }
}
