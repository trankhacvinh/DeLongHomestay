using ClosedXML.Excel;

namespace DeLong.Web.Features.Reports;

public sealed record ReportExcelFile(byte[] Content, string FileName);

public sealed class ReportExcelExportService
{
    private const string CurrencyFormat = "#,##0\" đ\"";
    private const string NumberFormat = "#,##0";
    private const string DecimalFormat = "#,##0.0";
    private const string PercentFormat = "0.0\"%\"";
    private static readonly XLColor Primary = XLColor.FromHtml("#155D59");
    private static readonly XLColor PrimaryLight = XLColor.FromHtml("#E8F2F0");
    private static readonly XLColor Muted = XLColor.FromHtml("#667A76");

    public ReportExcelFile Create(ReportSnapshotDto report, string scopeName, DateOnly month)
    {
        using var workbook = new XLWorkbook();
        workbook.Properties.Title = $"Báo cáo kinh doanh {scopeName} {month:MM-yyyy}";
        workbook.Properties.Subject = "Giá trị booking, dòng tiền, công suất, chi phí và công nợ";
        workbook.Properties.Author = "De Long Homestay";

        AddSummarySheet(workbook, report, scopeName, month);
        AddDailySheet(workbook, report, scopeName, month);
        AddWeeklySheet(workbook, report, scopeName, month);
        AddTrendSheet(workbook, report, scopeName, month);
        AddRoomSheet(workbook, report, scopeName, month);
        AddSourceSheet(workbook, report, scopeName, month);
        AddStatusSheet(workbook, report, scopeName, month);
        AddPaymentSheet(workbook, report, scopeName, month);
        AddExpenseSheet(workbook, report, scopeName, month);
        AddOutstandingSheet(workbook, report, scopeName, month);
        AddWeekdaySheet(workbook, report, scopeName, month);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var safeScope = string.Concat(scopeName.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '-' : character));
        return new ReportExcelFile(stream.ToArray(), $"Bao-cao-kinh-doanh-{safeScope}-{month:yyyy-MM}.xlsx");
    }

    private static void AddSummarySheet(XLWorkbook workbook, ReportSnapshotDto report, string scopeName, DateOnly month)
    {
        var sheet = CreateSheet(workbook, "Tổng quan", "TỔNG QUAN KINH DOANH", scopeName, month, 4);
        var rows = new (string Group, string Metric, double Value, string Format)[]
        {
            ("Booking", "Booking hoạt động", report.BookingCount, NumberFormat),
            ("Booking", "Booking hủy / không đến", report.CancelledBookingCount, NumberFormat),
            ("Booking", "Giá trị booking", (double)report.BookingValue, CurrencyFormat),
            ("Booking", "Trung bình / booking", (double)report.AverageBookingValue, CurrencyFormat),
            ("Vận hành", "Số giờ đã sử dụng", report.BookedHours, DecimalFormat),
            ("Vận hành", "Số phòng hoạt động", report.ActiveRoomCount, NumberFormat),
            ("Vận hành", "Công suất phòng", report.OccupancyRate, PercentFormat),
            ("Vận hành", "Tỷ lệ hủy / không đến", report.CancellationRate, PercentFormat),
            ("Dòng tiền", "Tổng tiền thu", (double)report.GrossReceipts, CurrencyFormat),
            ("Dòng tiền", "Hoàn tiền", (double)report.Refunds, CurrencyFormat),
            ("Dòng tiền", "Thực thu", (double)report.NetReceipts, CurrencyFormat),
            ("Dòng tiền", "Chi phí", (double)report.Expenses, CurrencyFormat),
            ("Dòng tiền", "Dòng tiền ròng", (double)report.NetCashFlow, CurrencyFormat),
            ("Công nợ", "Công nợ hiện tại", (double)report.Outstanding, CurrencyFormat),
            ("So sánh", "Thực thu hôm nay", (double)report.PeriodReceipts.Today, CurrencyFormat),
            ("So sánh", "Thực thu tuần này", (double)report.PeriodReceipts.ThisWeek, CurrencyFormat),
            ("So sánh", "Thực thu tháng trước", (double)report.PeriodReceipts.PreviousMonth, CurrencyFormat)
        };

        WriteHeaders(sheet, 4, ["Nhóm", "Chỉ số", "Giá trị", "Ghi chú"]);
        var row = 5;
        foreach (var item in rows)
        {
            sheet.Cell(row, 1).Value = item.Group;
            sheet.Cell(row, 2).Value = item.Metric;
            sheet.Cell(row, 3).Value = item.Value;
            sheet.Cell(row, 3).Style.NumberFormat.Format = item.Format;
            sheet.Cell(row, 4).Value = item.Metric switch
            {
                "Giá trị booking" => "Theo ngày check-in; không phải tiền thực thu",
                "Thực thu" => "Tiền thu trừ hoàn tiền theo ngày giao dịch",
                "Công suất phòng" => "Giờ phòng đã đặt / tổng giờ phòng hoạt động",
                "Công nợ hiện tại" => "Tất cả booking hoạt động chưa tất toán",
                _ => string.Empty
            };
            row++;
        }
        FinishSheet(sheet, row - 1, 4);
    }

    private static void AddDailySheet(XLWorkbook workbook, ReportSnapshotDto report, string scopeName, DateOnly month)
    {
        var sheet = CreateSheet(workbook, "Theo ngày", "DOANH THU THEO NGÀY", scopeName, month, 7);
        WriteHeaders(sheet, 4, ["Ngày", "Số booking", "Giá trị booking", "Thực thu", "Chi phí", "Dòng tiền ròng", "Ghi chú"]);
        var row = 5;
        foreach (var item in report.Daily)
        {
            sheet.Cell(row, 1).Value = DateTime.ParseExact(item.Date, "yyyy-MM-dd", null);
            sheet.Cell(row, 1).Style.DateFormat.Format = "dd/MM/yyyy";
            sheet.Cell(row, 2).Value = item.BookingCount;
            sheet.Cell(row, 3).Value = item.BookingValue;
            sheet.Cell(row, 4).Value = item.NetReceipts;
            sheet.Cell(row, 5).Value = item.Expenses;
            sheet.Cell(row, 6).Value = item.NetCashFlow;
            sheet.Cell(row, 7).Value = item.NetCashFlow < 0 ? "Dòng tiền âm" : string.Empty;
            sheet.Range(row, 3, row, 6).Style.NumberFormat.Format = CurrencyFormat;
            row++;
        }
        FinishSheet(sheet, row - 1, 7);
    }

    private static void AddWeeklySheet(XLWorkbook workbook, ReportSnapshotDto report, string scopeName, DateOnly month)
    {
        var sheet = CreateSheet(workbook, "Theo tuần", "DOANH THU THEO TUẦN", scopeName, month, 6);
        WriteHeaders(sheet, 4, ["Từ ngày", "Đến ngày", "Giá trị booking", "Thực thu", "Chi phí", "Dòng tiền ròng"]);
        var row = 5;
        foreach (var item in report.Weekly)
        {
            sheet.Cell(row, 1).Value = DateTime.ParseExact(item.WeekStart, "yyyy-MM-dd", null);
            sheet.Cell(row, 2).Value = DateTime.ParseExact(item.WeekEnd, "yyyy-MM-dd", null);
            sheet.Range(row, 1, row, 2).Style.DateFormat.Format = "dd/MM/yyyy";
            sheet.Cell(row, 3).Value = item.BookingValue;
            sheet.Cell(row, 4).Value = item.NetReceipts;
            sheet.Cell(row, 5).Value = item.Expenses;
            sheet.Cell(row, 6).Value = item.NetReceipts - item.Expenses;
            sheet.Range(row, 3, row, 6).Style.NumberFormat.Format = CurrencyFormat;
            row++;
        }
        FinishSheet(sheet, row - 1, 6);
    }

    private static void AddTrendSheet(XLWorkbook workbook, ReportSnapshotDto report, string scopeName, DateOnly month)
    {
        var sheet = CreateSheet(workbook, "Xu hướng 12 tháng", "XU HƯỚNG DÒNG TIỀN 12 THÁNG", scopeName, month, 4);
        WriteHeaders(sheet, 4, ["Tháng", "Thực thu", "Chi phí", "Dòng tiền ròng"]);
        var row = 5;
        foreach (var item in report.Trend)
        {
            sheet.Cell(row, 1).Value = DateTime.ParseExact($"{item.Month}-01", "yyyy-MM-dd", null);
            sheet.Cell(row, 1).Style.DateFormat.Format = "MM/yyyy";
            sheet.Cell(row, 2).Value = item.NetReceipts;
            sheet.Cell(row, 3).Value = item.Expenses;
            sheet.Cell(row, 4).Value = item.NetCashFlow;
            sheet.Range(row, 2, row, 4).Style.NumberFormat.Format = CurrencyFormat;
            row++;
        }
        FinishSheet(sheet, row - 1, 4);
    }

    private static void AddRoomSheet(XLWorkbook workbook, ReportSnapshotDto report, string scopeName, DateOnly month)
    {
        var sheet = CreateSheet(workbook, "Theo phòng", "HIỆU QUẢ THEO PHÒNG", scopeName, month, 5);
        WriteHeaders(sheet, 4, ["Xếp hạng", "Phòng", "Số booking", "Số giờ sử dụng", "Giá trị booking"]);
        var row = 5;
        foreach (var item in report.ByRoom)
        {
            sheet.Cell(row, 1).Value = row - 4;
            sheet.Cell(row, 2).Value = item.RoomName;
            sheet.Cell(row, 3).Value = item.BookingCount;
            sheet.Cell(row, 4).Value = item.BookedHours;
            sheet.Cell(row, 4).Style.NumberFormat.Format = DecimalFormat;
            sheet.Cell(row, 5).Value = item.BookingValue;
            sheet.Cell(row, 5).Style.NumberFormat.Format = CurrencyFormat;
            row++;
        }
        FinishSheet(sheet, row - 1, 5);
    }

    private static void AddSourceSheet(XLWorkbook workbook, ReportSnapshotDto report, string scopeName, DateOnly month)
    {
        var sheet = CreateSheet(workbook, "Nguồn booking", "HIỆU QUẢ NGUỒN BOOKING", scopeName, month, 4);
        WriteHeaders(sheet, 4, ["Xếp hạng", "Nguồn", "Số booking", "Giá trị booking"]);
        var row = 5;
        foreach (var item in report.BySource)
        {
            sheet.Cell(row, 1).Value = row - 4;
            sheet.Cell(row, 2).Value = item.Source;
            sheet.Cell(row, 3).Value = item.BookingCount;
            sheet.Cell(row, 4).Value = item.BookingValue;
            sheet.Cell(row, 4).Style.NumberFormat.Format = CurrencyFormat;
            row++;
        }
        FinishSheet(sheet, row - 1, 4);
    }

    private static void AddStatusSheet(XLWorkbook workbook, ReportSnapshotDto report, string scopeName, DateOnly month)
    {
        var sheet = CreateSheet(workbook, "Trạng thái", "CƠ CẤU TRẠNG THÁI BOOKING", scopeName, month, 3);
        WriteHeaders(sheet, 4, ["Trạng thái", "Số booking", "Giá trị booking"]);
        var row = 5;
        foreach (var item in report.ByStatus)
        {
            sheet.Cell(row, 1).Value = StatusLabel(item.Status);
            sheet.Cell(row, 2).Value = item.BookingCount;
            sheet.Cell(row, 3).Value = item.BookingValue;
            sheet.Cell(row, 3).Style.NumberFormat.Format = CurrencyFormat;
            row++;
        }
        FinishSheet(sheet, row - 1, 3);
    }

    private static void AddPaymentSheet(XLWorkbook workbook, ReportSnapshotDto report, string scopeName, DateOnly month)
    {
        var sheet = CreateSheet(workbook, "Thanh toán", "PHƯƠNG THỨC THANH TOÁN", scopeName, month, 3);
        WriteHeaders(sheet, 4, ["Phương thức", "Số giao dịch thu", "Tổng tiền thu"]);
        var row = 5;
        foreach (var item in report.ByPaymentMethod)
        {
            sheet.Cell(row, 1).Value = PaymentMethodLabel(item.Method);
            sheet.Cell(row, 2).Value = item.TransactionCount;
            sheet.Cell(row, 3).Value = item.GrossReceipts;
            sheet.Cell(row, 3).Style.NumberFormat.Format = CurrencyFormat;
            row++;
        }
        FinishSheet(sheet, row - 1, 3);
    }

    private static void AddExpenseSheet(XLWorkbook workbook, ReportSnapshotDto report, string scopeName, DateOnly month)
    {
        var sheet = CreateSheet(workbook, "Chi phí", "PHÂN TÍCH CHI PHÍ", scopeName, month, 4);
        WriteHeaders(sheet, 4, ["Xếp hạng", "Nhóm chi phí", "Số khoản", "Tổng chi"]);
        var row = 5;
        foreach (var item in report.ByExpenseCategory)
        {
            sheet.Cell(row, 1).Value = row - 4;
            sheet.Cell(row, 2).Value = item.Category;
            sheet.Cell(row, 3).Value = item.TransactionCount;
            sheet.Cell(row, 4).Value = item.Amount;
            sheet.Cell(row, 4).Style.NumberFormat.Format = CurrencyFormat;
            row++;
        }
        FinishSheet(sheet, row - 1, 4);
    }

    private static void AddOutstandingSheet(XLWorkbook workbook, ReportSnapshotDto report, string scopeName, DateOnly month)
    {
        var sheet = CreateSheet(workbook, "Tuổi công nợ", "TUỔI CÔNG NỢ THEO NGÀY CHECK-IN", scopeName, month, 3);
        WriteHeaders(sheet, 4, ["Nhóm", "Số booking", "Số tiền còn nợ"]);
        var row = 5;
        foreach (var item in report.OutstandingBuckets)
        {
            sheet.Cell(row, 1).Value = item.Label;
            sheet.Cell(row, 2).Value = item.BookingCount;
            sheet.Cell(row, 3).Value = item.Amount;
            sheet.Cell(row, 3).Style.NumberFormat.Format = CurrencyFormat;
            row++;
        }
        FinishSheet(sheet, row - 1, 3);
    }

    private static void AddWeekdaySheet(XLWorkbook workbook, ReportSnapshotDto report, string scopeName, DateOnly month)
    {
        var sheet = CreateSheet(workbook, "Theo thứ", "HIỆU QUẢ THEO THỨ TRONG TUẦN", scopeName, month, 5);
        WriteHeaders(sheet, 4, ["Thứ", "Số booking", "Số giờ sử dụng", "Giá trị booking", "Thực thu"]);
        var row = 5;
        foreach (var item in report.ByWeekday)
        {
            sheet.Cell(row, 1).Value = item.Label;
            sheet.Cell(row, 2).Value = item.BookingCount;
            sheet.Cell(row, 3).Value = item.BookedHours;
            sheet.Cell(row, 3).Style.NumberFormat.Format = DecimalFormat;
            sheet.Cell(row, 4).Value = item.BookingValue;
            sheet.Cell(row, 5).Value = item.NetReceipts;
            sheet.Range(row, 4, row, 5).Style.NumberFormat.Format = CurrencyFormat;
            row++;
        }
        FinishSheet(sheet, row - 1, 5);
    }

    private static IXLWorksheet CreateSheet(XLWorkbook workbook, string name, string title, string scopeName, DateOnly month, int columns)
    {
        var sheet = workbook.Worksheets.Add(name);
        sheet.SheetView.FreezeRows(4);
        sheet.Range(1, 1, 1, columns).Merge().Value = title;
        sheet.Range(1, 1, 1, columns).Style
            .Font.SetBold().Font.SetFontSize(16).Font.SetFontColor(XLColor.White)
            .Fill.SetBackgroundColor(Primary)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left)
            .Alignment.SetVertical(XLAlignmentVerticalValues.Center);
        sheet.Row(1).Height = 30;
        sheet.Range(2, 1, 2, columns).Merge().Value = $"Phạm vi: {scopeName} · Kỳ báo cáo: {month:MM/yyyy} · Xuất lúc: {DateTime.Now:dd/MM/yyyy HH:mm}";
        sheet.Range(2, 1, 2, columns).Style.Font.SetFontColor(Muted).Fill.SetBackgroundColor(PrimaryLight);
        sheet.Row(2).Height = 24;
        return sheet;
    }

    private static void WriteHeaders(IXLWorksheet sheet, int row, IReadOnlyList<string> headers)
    {
        for (var column = 0; column < headers.Count; column++)
            sheet.Cell(row, column + 1).Value = headers[column];
        sheet.Range(row, 1, row, headers.Count).Style
            .Font.SetBold().Font.SetFontColor(XLColor.White)
            .Fill.SetBackgroundColor(Primary)
            .Alignment.SetVertical(XLAlignmentVerticalValues.Center);
        sheet.Row(row).Height = 24;
    }

    private static void FinishSheet(IXLWorksheet sheet, int lastRow, int lastColumn)
    {
        if (lastRow >= 5)
        {
            var range = sheet.Range(4, 1, lastRow, lastColumn);
            range.Style.Border.SetBottomBorder(XLBorderStyleValues.Hair).Border.SetBottomBorderColor(XLColor.FromHtml("#D7E3E0"));
            range.CreateTable().Theme = XLTableTheme.TableStyleMedium2;
        }
        sheet.Columns(1, lastColumn).AdjustToContents();
        foreach (var column in sheet.Columns(1, lastColumn))
        {
            if (column.Width > 42) column.Width = 42;
            if (column.Width < 12) column.Width = 12;
        }
        sheet.RowsUsed().Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center);
        sheet.RowsUsed().Style.Font.SetFontName("Aptos");
        sheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        sheet.PageSetup.FitToPages(1, 0);
    }

    private static string StatusLabel(string value) => value switch
    {
        "Requested" => "Yêu cầu",
        "Held" => "Đang giữ",
        "Confirmed" => "Đã xác nhận",
        "CheckedIn" => "Đang ở",
        "Completed" => "Hoàn tất",
        "Cancelled" => "Đã hủy",
        "NoShow" => "Không đến",
        _ => value
    };

    private static string PaymentMethodLabel(string value) => value switch
    {
        "Cash" => "Tiền mặt",
        "BankTransfer" => "Chuyển khoản",
        "Card" => "Thẻ",
        "Pay2S" => "Pay2S",
        "Other" => "Khác",
        _ => value
    };
}
