using ClosedXML.Excel;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Expenses;
using DeLong.Web.Features.Finance;
using Xunit;

namespace DeLong.Tests;

public sealed class FinanceExcelExportServiceTests
{
    [Fact]
    public void Create_builds_a_four_sheet_ledger_with_effective_amounts()
    {
        var propertyId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var snapshot = new FinanceSnapshotDto(
            new FinanceSummaryDto(2_000_000m, 200_000m, 1_800_000m, 500_000m, 1_300_000m, 700_000m),
            [
                new FinancePaymentDto(Guid.NewGuid(), propertyId, bookingId, "BK-001", "Nguyễn Văn A", PaymentType.Receipt, PaymentMethod.Pay2S, 2_000_000m, new DateTime(2026, 8, 29, 3, 0, 0, DateTimeKind.Utc), false, "P2S-01"),
                new FinancePaymentDto(Guid.NewGuid(), propertyId, bookingId, "BK-001", "Nguyễn Văn A", PaymentType.Refund, PaymentMethod.Pay2S, 200_000m, new DateTime(2026, 8, 29, 4, 0, 0, DateTimeKind.Utc), false, "P2S-02")
            ],
            [new ExpenseDto(Guid.NewGuid(), propertyId, new DateTime(2026, 8, 29, 5, 0, 0, DateTimeKind.Utc), "Điện nước", "Tiền điện", 500_000m, PaymentMethod.BankTransfer, "EVN", "EXP-01", null, null, false, null, null, null)]);
        var range = FinancePeriodResolver.Resolve("day", "2026-08-29", null, new DateOnly(2026, 8, 29));

        var file = new FinanceExcelExportService().Create(
            snapshot,
            "De Long",
            range,
            new Dictionary<Guid, string> { [propertyId] = "De Long" },
            new Dictionary<Guid, TimeZoneInfo> { [propertyId] = TimeZoneInfo.Utc });

        Assert.NotEmpty(file.Content);
        using var workbook = new XLWorkbook(new MemoryStream(file.Content));
        Assert.Equal(["Tổng quan", "Sổ thu chi", "Thu booking", "Chi phí"], workbook.Worksheets.Select(x => x.Name).ToArray());
        var ledger = workbook.Worksheet("Sổ thu chi");
        Assert.Equal(2_000_000m, ledger.Cell("H5").GetValue<decimal>());
        Assert.Equal(200_000m, ledger.Cell("I6").GetValue<decimal>());
        Assert.Equal(500_000m, ledger.Cell("I7").GetValue<decimal>());
        Assert.NotEmpty(ledger.Tables);
        Assert.Equal("Pay2S", workbook.Worksheet("Thu booking").Cell("F5").GetString());
    }
}
