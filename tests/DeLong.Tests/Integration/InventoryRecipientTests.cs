using DeLong.Web.Common.Auditing;
using DeLong.Web.Common.Persistence;
using DeLong.Web.Common.Security;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Features.Cash;
using DeLong.Web.Features.Inventory;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeLong.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class InventoryRecipientTests
{
    [RoomLockPostgresFact]
    public async Task External_recipients_persist_deduplicate_across_months_and_preserve_retries_stock_and_history()
    {
        await using var db = InventoryCashTests.Database();
        await db.Database.MigrateAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var branch = new Property { Code = "RC" + suffix, Name = "Internal " + suffix, TimeZoneId = "Asia/Ho_Chi_Minh" };
        db.Properties.Add(branch); await db.SaveChangesAsync();
        var user = InventoryCashTests.User(Guid.NewGuid(), "Admin");
        var service = new InventoryService(db, new AuditService(db), new PropertyAccessService(db));
        Assert.Null((await service.InitializeAsync(user, branch.Id, default)).Error);
        var unit = (await service.AddUnitAsync(user, new("Kg " + suffix), default)).Result!;
        var item = (await service.SaveItemAsync(user, null, new(null, "Recipient item " + suffix, unit.Id, 2), default)).Result!;
        var receipt = new InventoryDocumentRequest(Guid.NewGuid(), InventoryDocumentType.Receipt,
            new(2026, 9, 1, 9, 0, 0, TimeSpan.FromHours(7)), null, null, [new(item.Id, 20, 2)]);
        var savedReceipt = await service.PostAsync(user, receipt, default); Assert.Null(savedReceipt.Error);
        var oldFingerprint = RequestFingerprint.Create(new { receipt.RequestKey, receipt.Type, receipt.OccurredAt, receipt.RecipientPropertyId, receipt.Note, receipt.Lines });
        Assert.Equal(oldFingerprint, (await db.InventoryDocuments.AsNoTracking().SingleAsync(x => x.Id == savedReceipt.Result)).RequestHash);
        Assert.Equal(savedReceipt.Result, (await service.PostAsync(user, receipt, default)).Result);
        var name = "home1-" + suffix;
        var issue = receipt with { RequestKey = Guid.NewGuid(), Type = InventoryDocumentType.Issue, OccurredAt = new(2026, 10, 2, 9, 0, 0, TimeSpan.FromHours(7)), RecipientName = "  " + name + "  ", Lines = [new(item.Id, 2, 2)] };
        var first = await service.PostAsync(user, issue, default); Assert.Null(first.Error);
        Assert.Equal(first.Result, (await service.PostAsync(user, issue, default)).Result);
        Assert.Equal("retry_conflict", (await service.PostAsync(user, issue with { RecipientName = "other" }, default)).Error?.Code);
        Assert.Null((await service.PostAsync(user, issue with { RequestKey = Guid.NewGuid(), RecipientName = name.ToUpperInvariant(), Lines = [new(item.Id, 1, 2)] }, default)).Error);
        Assert.NotNull((await service.PostAsync(user, issue with { RequestKey = Guid.NewGuid(), RecipientName = " " }, default)).Error);
        Assert.NotNull((await service.PostAsync(user, issue with { RequestKey = Guid.NewGuid(), RecipientName = new string('a', 201) }, default)).Error);
        Assert.NotNull((await service.PostAsync(user, receipt with { RequestKey = Guid.NewGuid(), RecipientName = name }, default)).Error);
        var linked = await service.PostAsync(user, issue with { RequestKey = Guid.NewGuid(), RecipientPropertyId = branch.Id, RecipientName = "Spoofed", Lines = [new(item.Id, 1, 2)] }, default);
        Assert.Null(linked.Error);
        var originalBranchName = branch.Name; branch.Name = "Renamed " + suffix; await db.SaveChangesAsync();
        var snapshot = (await service.GetAsync(user, "2026-10", default)).Result!;
        Assert.Equal(16, snapshot.Stock.Single(x => x.ItemId == item.Id).Closing);
        Assert.Equal(3, Assert.Single(snapshot.Distribution, x => x.ItemId == item.Id && x.RecipientPropertyId is null).Quantity);
        Assert.Equal(originalBranchName, snapshot.Documents.Single(x => x.Id == linked.Result).RecipientName);
        Assert.Equal(name, snapshot.Documents.Single(x => x.Id == first.Result).RecipientName);
        var later = (await service.GetAsync(user, "2026-11", default)).Result!;
        Assert.Single(later.Recipients!, x => x.PropertyId is null && x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        Assert.Null(await service.VoidAsync(user, first.Result!.Value, "Synthetic test", default));
        Assert.Equal(18, (await service.GetAsync(user, "2026-10", default)).Result!.Stock.Single(x => x.ItemId == item.Id).Closing);
    }

    [RoomLockPostgresFact]
    public async Task Cash_all_branch_reports_return_for_empty_and_populated_months_and_honor_cancellation()
    {
        await using var db = InventoryCashTests.Database(); await db.Database.MigrateAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var a = new Property { Code = "C1" + suffix, Name = "Cash month A", TimeZoneId = "Asia/Ho_Chi_Minh" };
        var b = new Property { Code = "C2" + suffix, Name = "Cash month B", TimeZoneId = "Asia/Ho_Chi_Minh" };
        db.AddRange(a, b); await db.SaveChangesAsync();
        var service = new CashService(db, new AuditService(db));
        Assert.Null((await service.AddAsync(a.Id, new(Guid.NewGuid(), new(2026,9,15,10,0,0,TimeSpan.FromHours(7)), null, 12, "Synthetic", null), null, default)).Error);
        var pa = new CurrentPropertyDto(a.Id,a.Code,a.Name,a.TimeZoneId);
        var pb = new CurrentPropertyDto(b.Id,b.Code,b.Name,b.TimeZoneId);
        var scope = new ReportingPropertyScope(pa, pa, [pa,pb], true);
        foreach (var month in new[] { "2026-09", "2026-10", "2026-08", "2026-09" }) {
            var result = await service.GetAsync(scope, month, null, default);
            Assert.Null(result.Error); Assert.Equal(month,result.Result!.Month);
            Assert.Equal(month == "2026-09" ? 12 : 0, result.Result.Totals.Net);
        }
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetAsync(scope,"2026-09",null,cancelled.Token));
    }
}
