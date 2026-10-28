using System.Security.Claims;
using ClosedXML.Excel;
using DeLong.Web.Common.Auditing;
using DeLong.Web.Common.Security;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Cash;
using DeLong.Web.Features.Expenses;
using DeLong.Web.Features.Finance;
using DeLong.Web.Features.Inventory;
using DeLong.Web.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeLong.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class InventoryCashTests
{
    internal static AppDbContext Database() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION")!).Options);
    internal static ClaimsPrincipal User(Guid id, string role) => new(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, id.ToString()), new(ClaimTypes.Role, role)], "Test"));
    private static DateTimeOffset At(int day) => new(2026, 10, day, 9, 0, 0, TimeSpan.FromHours(7));

    [RoomLockPostgresFact]
    public async Task Common_warehouse_handles_decimal_opening_months_retry_backdates_voids_and_concurrent_issues()
    {
        await using var db = Database(); await db.Database.MigrateAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var owner = await db.InventoryWarehouses.AsNoTracking().Select(x => x.Property).FirstOrDefaultAsync()
            ?? new Property { Code = "OWNER" + suffix, Name = "Synthetic owner", TimeZoneId = "Asia/Ho_Chi_Minh" };
        var branch = new Property { Code = "DEST" + suffix, Name = "Synthetic recipient" };
        if (db.Entry(owner).State == EntityState.Detached && !await db.Properties.AnyAsync(x => x.Id == owner.Id)) db.Properties.Add(owner);
        db.Properties.Add(branch); await db.SaveChangesAsync();
        var principal = User(Guid.NewGuid(), "Admin");
        var service = new InventoryService(db, new AuditService(db), new PropertyAccessService(db));
        var initialized = await service.InitializeAsync(principal, owner.Id, default); Assert.Null(initialized.Error);
        Assert.Equal(initialized.Result!.Id, (await service.InitializeAsync(principal, branch.Id, default)).Result!.Id);
        var unit = await service.AddUnitAsync(principal, new("Túi " + suffix), default); Assert.Null(unit.Error);
        Assert.Equal(unit.Result!.Id, (await service.AddUnitAsync(principal, new(" TÚI " + suffix + " "), default)).Result!.Id);
        var item = await service.SaveItemAsync(principal, null, new("SKU" + suffix, "Synthetic original " + suffix, unit.Result.Id, 7.12m), default); Assert.Null(item.Error);
        var itemId = item.Result!.Id;
        InventoryDocumentRequest Document(InventoryDocumentType type, decimal quantity, int day = 1) => new(Guid.NewGuid(), type, At(day), type == InventoryDocumentType.Issue ? branch.Id : null, "Synthetic note", [new(itemId, quantity, 7.12m)]);
        var openingRequest = Document(InventoryDocumentType.Opening, 10.5m) with { OccurredAt = new(2026, 10, 1, 0, 0, 0, TimeSpan.FromHours(7)) };
        var opening = await service.PostAsync(principal, openingRequest, default); Assert.Null(opening.Error);
        Assert.NotNull((await service.PostAsync(principal, Document(InventoryDocumentType.Opening, 1), default)).Error);
        var request = Document(InventoryDocumentType.Receipt, 2.25m, 10);
        Assert.NotNull((await service.PostAsync(principal, request with { Lines = [new(itemId, 0, 0)] }, default)).Error);
        Assert.NotNull((await service.PostAsync(principal, request with { Lines = [new(itemId, 1.0001m, 0)] }, default)).Error);
        Assert.NotNull((await service.PostAsync(principal, request with { Lines = [new(itemId, 1, 0), new(itemId, 2, 0)] }, default)).Error);
        Assert.NotNull((await service.PostAsync(principal, Document(InventoryDocumentType.AdjustmentIn, 1) with { Note = " " }, default)).Error);
        var receipt = await service.PostAsync(principal, request, default); Assert.Null(receipt.Error);
        Assert.Equal(receipt.Result, (await service.PostAsync(principal, request, default)).Result);
        Assert.Equal("retry_conflict", (await service.PostAsync(principal, request with { Note = "Changed" }, default)).Error!.Code);
        Assert.Null((await service.PostAsync(principal, Document(InventoryDocumentType.Issue, 4.25m, 11), default)).Error);
        var snapshot = (await service.GetAsync(principal, "2026-10", default)).Result!;
        var stock = snapshot.Stock.Single(x => x.ItemId == itemId);
        Assert.Equal(10.5m, stock.Opening); Assert.Equal(2.25m, stock.Received); Assert.Equal(4.25m, stock.Issued); Assert.Equal(8.5m, stock.Closing);
        Assert.Equal(4.25m, snapshot.Distribution.Single(x => x.ItemId == itemId).Quantity);
        Assert.Equal(8.5m, (await service.GetAsync(principal, "2026-11", default)).Result!.Stock.Single(x => x.ItemId == itemId).Opening);
        Assert.Equal("insufficient_stock", (await service.PostAsync(principal, Document(InventoryDocumentType.Issue, 1) with { OccurredAt = At(1).AddDays(-2) }, default)).Error!.Code);
        Assert.Equal("insufficient_stock", (await service.VoidAsync(principal, opening.Result!.Value, "Synthetic", default))!.Code);
        var anotherUnit = (await service.AddUnitAsync(principal, new("Cái " + suffix), default)).Result!;
        Assert.NotNull((await service.SaveItemAsync(principal, itemId, new(item.Result.Code, item.Result.Name, anotherUnit.Id, 2), default)).Error);
        Assert.Null((await service.SaveItemAsync(principal, itemId, new(item.Result.Code, "Renamed " + suffix, unit.Result.Id, 999), default)).Error);
        var history = (await service.HistoryAsync(principal, itemId, 0, default)).Result!;
        Assert.All(history.SelectMany(x => x.Lines), x => { Assert.Equal("Synthetic original " + suffix, x.ItemName); Assert.Equal(7.12m, x.UnitPrice); });
        var emptyItem = (await service.SaveItemAsync(principal, null, new("EMPTY" + suffix, "Empty " + suffix, unit.Result.Id, 0), default)).Result!;
        var count = await db.InventoryDocuments.CountAsync();
        var atomic = Document(InventoryDocumentType.Issue, 1, 12) with { Lines = [new(itemId, 1, 0), new(emptyItem.Id, 1, 0)] };
        Assert.NotNull((await service.PostAsync(principal, atomic, default)).Error);
        Assert.Equal(count, await db.InventoryDocuments.CountAsync());
        async Task<(Guid? Result, InventoryError? Error)> Issue() {
            await using var other = Database();
            return await new InventoryService(other, new AuditService(other), new PropertyAccessService(other)).PostAsync(principal, Document(InventoryDocumentType.Issue, 5, 12), default);
        }
        var results = await Task.WhenAll(Issue(), Issue());
        Assert.Single(results, x => x.Error is null); Assert.Single(results, x => x.Error?.Code == "insufficient_stock");
        var winning = results.Single(x => x.Error is null).Result!.Value;
        Assert.Equal(3.5m, (await service.GetAsync(principal, "2026-10", default)).Result!.Stock.Single(x => x.ItemId == itemId).Closing);
        Assert.Null(await service.VoidAsync(principal, winning, "Synthetic correction", default));
        Assert.Null(await service.VoidAsync(principal, winning, "Retry", default));
        Assert.Equal(8.5m, (await service.GetAsync(principal, "2026-10", default)).Result!.Stock.Single(x => x.ItemId == itemId).Closing);
        Assert.Null((await service.PostAsync(principal, Document(InventoryDocumentType.AdjustmentIn, 2, 13), default)).Error);
        Assert.Null((await service.PostAsync(principal, Document(InventoryDocumentType.AdjustmentOut, .5m, 14), default)).Error);
        var adjusted = (await service.GetAsync(principal, "2026-10", default)).Result!.Stock.Single(x => x.ItemId == itemId);
        Assert.Equal(1.5m, adjusted.Adjustments); Assert.Equal(10, adjusted.Closing);
        Assert.Null((await service.SaveItemAsync(principal, itemId, new(item.Result.Code, "Renamed " + suffix, unit.Result.Id, 999, false), default)).Error);
        Assert.NotNull((await service.PostAsync(principal, Document(InventoryDocumentType.Issue, 1, 12), default)).Error);
    }

    [RoomLockPostgresFact]
    public async Task Cash_uses_actual_cash_payments_handles_refunds_common_expenses_room_filters_and_month_boundaries()
    {
        await using var db = Database(); await db.Database.MigrateAsync();
        var key = Guid.NewGuid().ToString("N")[..20];
        var property = new Property { Code = key, Name = "Synthetic cash", TimeZoneId = "Asia/Ho_Chi_Minh" };
        var room = new Room { Property = property, Name = "R1", Code = "R1" };
        var room2 = new Room { Property = property, Name = "R2", Code = "R2" };
        var customer = new Customer { Property = property, Name = "Synthetic", Phone = "0900000123", NormalizedPhone = "0900000123" };
        var booking = new Booking { Property = property, Room = room, Customer = customer, Code = key, Status = BookingStatus.Confirmed, RoomAmount = 1000, CheckInUtc = At(5).UtcDateTime, CheckOutUtc = At(5).AddHours(3).UtcDateTime };
        var second = new Booking { Property = property, Room = room2, Customer = customer, Code = "SECOND" + key[..8], Status = BookingStatus.Confirmed, RoomAmount = 80, CheckInUtc = At(5).UtcDateTime, CheckOutUtc = At(5).AddHours(3).UtcDateTime };
        db.AddRange(booking, second);
        Payment Payment(decimal amount, PaymentMethod method = PaymentMethod.Cash, PaymentType type = PaymentType.Receipt) => new() { Property = property, Booking = booking, Amount = amount, Method = method, Type = type, OccurredAtUtc = At(1).UtcDateTime };
        db.AddRange(Payment(100), Payment(20, type: PaymentType.Refund), Payment(200, PaymentMethod.BankTransfer), Payment(300, PaymentMethod.SePay));
        var voided = Payment(500); voided.IsVoided = true; db.Add(voided);
        var boundary = Payment(900); boundary.OccurredAtUtc = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.FromHours(7)).UtcDateTime; db.Add(boundary);
        db.Add(new Payment { Property = property, Booking = second, Amount = 80, Method = PaymentMethod.Cash, OccurredAtUtc = At(2).UtcDateTime });
        await db.SaveChangesAsync();
        var service = new CashService(db, new AuditService(db));
        var manual = new CashReceiptRequest(Guid.NewGuid(), At(2), null, 30, "Other income", "Synthetic note");
        Assert.NotNull((await service.AddAsync(property.Id, manual with { Amount = 0 }, null, default)).Error);
        Assert.NotNull((await service.AddAsync(property.Id, manual with { Amount = .001m }, null, default)).Error);
        Assert.NotNull((await service.AddAsync(property.Id, manual with { RoomId = Guid.NewGuid() }, null, default)).Error);
        var first = await service.AddAsync(property.Id, manual, null, default); Assert.Null(first.Error);
        Assert.Equal(first.Result, (await service.AddAsync(property.Id, manual, null, default)).Result);
        Assert.NotNull((await service.AddAsync(property.Id, manual with { Amount = 31 }, null, default)).Error);
        Assert.Null((await service.AddAsync(property.Id, manual with { RequestKey = Guid.NewGuid(), RoomId = room.Id, Amount = 5 }, null, default)).Error);
        var expenses = new ExpenseService(db);
        Assert.Null((await expenses.AddAsync(property.Id, new() { RequestKey = Guid.NewGuid(), Category = "Vật tư", Description = "Room cost", Amount = 10, Method = PaymentMethod.Cash, RoomId = room.Id, OccurredAt = At(2) }, null)).Error);
        Assert.Null((await expenses.AddAsync(property.Id, new() { Category = "Vật tư: chung", Description = "Common cost", Amount = 7, Method = PaymentMethod.Cash, OccurredAt = At(2) }, null)).Error);
        Assert.Null((await expenses.AddAsync(property.Id, new() { Category = "Vật tư", Description = "Transfer cost", Amount = 999, Method = PaymentMethod.BankTransfer, OccurredAt = At(2) }, null)).Error);
        var expenseRequest = new CreateExpenseRequest { RequestKey = Guid.NewGuid(), Category = "Vật tư", Description = "Void test", Amount = 50, Method = PaymentMethod.Cash, OccurredAt = At(2) };
        var extraExpense = await expenses.AddAsync(property.Id, expenseRequest, null);
        Assert.Equal(extraExpense.Expense!.Id, (await expenses.AddAsync(property.Id, expenseRequest, null)).Expense!.Id);
        Assert.Null((await expenses.VoidAsync(property.Id, extraExpense.Expense.Id, "Synthetic", null)).Error);
        var branch = new CurrentPropertyDto(property.Id, property.Code, property.Name, property.TimeZoneId);
        var scope = new ReportingPropertyScope(branch, branch, [branch], false);
        var report = (await service.GetAsync(scope, "2026-10", null, default)).Result!;
        Assert.Equal(new CashTotalsDto(180, 35, 20, 17, 178), report.Totals);
        Assert.Equal(178, report.ByRoom.Sum(x => x.Net)); Assert.Equal(178, report.ByDay.Sum(x => x.Net));
        Assert.Equal(7, report.ByExpense.Single(x => x.Key == "Vật tư: chung").Expenses);
        var otherProperty = new Property { Code = "OTHER" + key, Name = "Other synthetic branch", TimeZoneId = property.TimeZoneId };
        db.Properties.Add(otherProperty); await db.SaveChangesAsync();
        Assert.Null((await service.AddAsync(otherProperty.Id, manual with { RequestKey = Guid.NewGuid(), Amount = 9000 }, null, default)).Error);
        var otherBranch = new CurrentPropertyDto(otherProperty.Id, otherProperty.Code, otherProperty.Name, otherProperty.TimeZoneId);
        var adminSelected = new ReportingPropertyScope(branch, branch, [branch, otherBranch], false);
        Assert.Equal(178, (await service.GetAsync(adminSelected, "2026-10", null, default)).Result!.Totals.Net);
        Assert.Equal(9178, (await service.GetAsync(adminSelected with { IsAll = true }, "2026-10", null, default)).Result!.Totals.Net);
        Assert.Equal(75, (await service.GetAsync(scope, "2026-10", room.Id, default)).Result!.Totals.Net);
        Assert.Equal(900, (await service.GetAsync(scope, "2026-11", null, default)).Result!.Totals.BookingReceipts);
        Assert.NotNull((await service.GetAsync(scope, "2026-10", Guid.NewGuid(), default)).Error);
        Assert.Null(await service.VoidAsync(property.Id, first.Result!.Value, "Synthetic", null, default));
        Assert.Equal(first.Result, (await service.AddAsync(property.Id, manual, null, default)).Result);
        Assert.Equal(148, (await service.GetAsync(scope, "2026-10", null, default)).Result!.Totals.Net);
        var file = OperationalExcelExport.Create(new OperationalSheet("Test", ["Amount", "Note"], [new object?[] { report.Totals.Net, "=SUM(A1:A2)" }]));
        using var book = new XLWorkbook(new MemoryStream(file));
        Assert.Equal(178, book.Worksheet(1).Cell(2, 1).GetValue<decimal>());
        Assert.False(book.Worksheet(1).Cell(2, 2).HasFormula);
    }
}
