using ClosedXML.Excel;
using DeLong.Web.Features.Reports;
using Xunit;

namespace DeLong.Tests;

public sealed class ReportsExcelExportServiceTests
{
    [Fact]
    public void Create_builds_a_readable_multi_sheet_business_workbook()
    {
        var report = CreateReport();

        var file = new ReportExcelExportService().Create(report, "De Long Homestay", new DateOnly(2026, 8, 1));

        Assert.NotEmpty(file.Content);
        Assert.Equal("Bao-cao-kinh-doanh-De Long Homestay-2026-08.xlsx", file.FileName);
        using var workbook = new XLWorkbook(new MemoryStream(file.Content));
        Assert.Equal(11, workbook.Worksheets.Count);
        Assert.Equal(
            ["Tổng quan", "Theo ngày", "Theo tuần", "Xu hướng 12 tháng", "Theo phòng", "Nguồn booking", "Trạng thái", "Thanh toán", "Chi phí", "Tuổi công nợ", "Theo thứ"],
            workbook.Worksheets.Select(x => x.Name).ToArray());

        var summary = workbook.Worksheet("Tổng quan");
        Assert.Equal("TỔNG QUAN KINH DOANH", summary.Cell("A1").GetString());
        var netReceiptRow = summary.Column(2).CellsUsed().Single(cell => cell.GetString() == "Thực thu").Address.RowNumber;
        Assert.Equal(9_000_000m, summary.Cell(netReceiptRow, 3).GetValue<decimal>());
        Assert.Contains("Giá trị booking", summary.Column(2).CellsUsed().Select(x => x.GetString()));

        var daily = workbook.Worksheet("Theo ngày");
        Assert.Equal(new DateTime(2026, 8, 1), daily.Cell("A5").GetDateTime());
        Assert.Equal(2_000_000m, daily.Cell("D5").GetValue<decimal>());
        Assert.NotEmpty(daily.Tables);
    }

    private static ReportSnapshotDto CreateReport() => new(
        4,
        1,
        12_000_000m,
        3_000_000m,
        96,
        3,
        43.5,
        20,
        10_000_000m,
        1_000_000m,
        9_000_000m,
        3_000_000m,
        6_000_000m,
        2_000_000m,
        new ReportPeriodReceiptsDto(500_000m, 3_000_000m, 9_000_000m, 8_000_000m, 12.5),
        [new ReportRoomDto("Coco Blue #1", 4, 12_000_000m, 96)],
        [new ReportSourceDto("Website", 4, 12_000_000m)],
        [new ReportStatusDto("Confirmed", 4, 12_000_000m)],
        [new ReportPaymentMethodDto("Pay2S", 4, 10_000_000m)],
        [new ReportExpenseCategoryDto("Điện nước", 2, 3_000_000m)],
        [new ReportWeekdayDto(0, "Thứ Hai", 4, 12_000_000m, 9_000_000m, 96)],
        [new ReportOutstandingBucketDto("current", "Đến hạn / trong 7 ngày", 1, 2_000_000m)],
        [new ReportDailyDto("2026-08-01", 1, 3_000_000m, 2_000_000m, 500_000m, 1_500_000m)],
        [new ReportWeeklyDto("2026-07-27", "2026-08-02", 3_000_000m, 2_000_000m, 500_000m)],
        [new ReportTrendDto("2026-08", 9_000_000m, 3_000_000m, 6_000_000m)]);
}
