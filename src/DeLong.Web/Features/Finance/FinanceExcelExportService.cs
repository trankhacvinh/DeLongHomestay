using ClosedXML.Excel;
using DeLong.Web.Domain.Enums;

namespace DeLong.Web.Features.Finance;

public sealed record FinanceExcelFile(byte[] Content, string FileName);

public sealed class FinanceExcelExportService
{
    private const string CurrencyFormat = "#,##0\" đ\";[Red]-#,##0\" đ\"";
    private static readonly XLColor Primary = XLColor.FromHtml("#155D59");
    private static readonly XLColor PrimaryLight = XLColor.FromHtml("#E8F2F0");
    private static readonly XLColor Muted = XLColor.FromHtml("#667A76");

    public FinanceExcelFile Create(
        FinanceSnapshotDto snapshot,
        string scopeName,
        FinancePeriodRange period,
        IReadOnlyDictionary<Guid, string> propertyNames,
        IReadOnlyDictionary<Guid, TimeZoneInfo> propertyTimeZones)
    {
        using var workbook = new XLWorkbook();
        workbook.Properties.Title = $"Sổ thu chi {scopeName} {period.Start:dd-MM-yyyy}";
        workbook.Properties.Subject = "Thu booking, hoàn tiền, chi phí và dòng tiền ròng";
        workbook.Properties.Author = "De Long Homestay";

        AddSummarySheet(workbook, snapshot.Summary, scopeName, period);
        AddLedgerSheet(workbook, snapshot, scopeName, period, propertyNames, propertyTimeZones);
        AddPaymentsSheet(workbook, snapshot.Payments, scopeName, period, propertyNames, propertyTimeZones);
        AddExpensesSheet(workbook, snapshot.Expenses, scopeName, period, propertyNames, propertyTimeZones);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var safeScope = string.Concat(scopeName.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '-' : character));
        return new FinanceExcelFile(stream.ToArray(), $"So-thu-chi-{safeScope}-{period.Period}-{period.Start:yyyy-MM-dd}.xlsx");
    }

    private static void AddSummarySheet(
        XLWorkbook workbook,
        FinanceSummaryDto summary,
        string scopeName,
        FinancePeriodRange period)
    {
        var sheet = CreateSheet(workbook, "Tổng quan", "TỔNG QUAN THU CHI", scopeName, period, 3);
        WriteHeaders(sheet, 4, ["Chỉ số", "Số tiền", "Cách tính"]);
        var rows = new (string Label, decimal Value, string Description)[]
        {
            ("Tổng tiền thu", summary.Receipts, "Tổng Receipt hợp lệ trong kỳ"),
            ("Hoàn tiền", summary.Refunds, "Tổng Refund hợp lệ trong kỳ"),
            ("Thực thu", summary.NetReceipts, "Tổng tiền thu − hoàn tiền"),
            ("Chi phí", summary.Expenses, "Chi phí vận hành hợp lệ trong kỳ"),
            ("Dòng tiền ròng", summary.NetCashFlow, "Thực thu − chi phí"),
            ("Công nợ hiện tại", summary.Outstanding, "Tất cả booking hoạt động chưa tất toán")
        };
        var row = 5;
        foreach (var item in rows)
        {
            sheet.Cell(row, 1).Value = item.Label;
            sheet.Cell(row, 2).Value = item.Value;
            sheet.Cell(row, 2).Style.NumberFormat.Format = CurrencyFormat;
            sheet.Cell(row, 3).Value = item.Description;
            row++;
        }
        FinishSheet(sheet, row - 1, 3);
    }

    private static void AddLedgerSheet(
        XLWorkbook workbook,
        FinanceSnapshotDto snapshot,
        string scopeName,
        FinancePeriodRange period,
        IReadOnlyDictionary<Guid, string> propertyNames,
        IReadOnlyDictionary<Guid, TimeZoneInfo> propertyTimeZones)
    {
        var sheet = CreateSheet(workbook, "Sổ thu chi", "SỔ THU CHI CHI TIẾT", scopeName, period, 11);
        WriteHeaders(sheet, 4, ["Thời gian", "Cơ sở", "Loại", "Nhóm / Booking", "Nội dung / Khách", "Phương thức", "Tham chiếu", "Tiền thu", "Tiền chi", "Trạng thái", "Ghi chú"]);
        var rows = BuildLedgerRows(snapshot, propertyNames, propertyTimeZones).OrderBy(x => x.OccurredAt).ToList();
        var row = 5;
        foreach (var item in rows)
        {
            sheet.Cell(row, 1).Value = item.OccurredAt;
            sheet.Cell(row, 1).Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
            sheet.Cell(row, 2).Value = item.PropertyName;
            sheet.Cell(row, 3).Value = item.Type;
            sheet.Cell(row, 4).Value = item.Category;
            sheet.Cell(row, 5).Value = item.Description;
            sheet.Cell(row, 6).Value = item.Method;
            sheet.Cell(row, 7).Value = item.Reference;
            sheet.Cell(row, 8).Value = item.Receipt;
            sheet.Cell(row, 9).Value = item.Outflow;
            sheet.Range(row, 8, row, 9).Style.NumberFormat.Format = CurrencyFormat;
            sheet.Cell(row, 10).Value = item.IsVoided ? "Đã void" : "Hợp lệ";
            sheet.Cell(row, 11).Value = item.Note;
            if (item.IsVoided) sheet.Range(row, 1, row, 11).Style.Font.SetFontColor(Muted).Font.SetStrikethrough();
            row++;
        }
        FinishSheet(sheet, row - 1, 11);
    }

    private static void AddPaymentsSheet(
        XLWorkbook workbook,
        IReadOnlyList<FinancePaymentDto> payments,
        string scopeName,
        FinancePeriodRange period,
        IReadOnlyDictionary<Guid, string> propertyNames,
        IReadOnlyDictionary<Guid, TimeZoneInfo> propertyTimeZones)
    {
        var sheet = CreateSheet(workbook, "Thu booking", "THANH TOÁN BOOKING", scopeName, period, 10);
        WriteHeaders(sheet, 4, ["Thời gian", "Cơ sở", "Booking", "Khách", "Loại", "Phương thức", "Số tiền", "Tham chiếu", "Trạng thái", "Số tiền hiệu lực"]);
        var row = 5;
        foreach (var item in payments.OrderBy(x => x.OccurredAtUtc))
        {
            sheet.Cell(row, 1).Value = LocalTime(item.OccurredAtUtc, item.PropertyId, propertyTimeZones);
            sheet.Cell(row, 1).Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
            sheet.Cell(row, 2).Value = PropertyName(item.PropertyId, propertyNames);
            sheet.Cell(row, 3).Value = item.BookingCode;
            sheet.Cell(row, 4).Value = item.CustomerName;
            sheet.Cell(row, 5).Value = item.Type == PaymentType.Receipt ? "Thu" : "Hoàn tiền";
            sheet.Cell(row, 6).Value = PaymentMethodLabel(item.Method);
            sheet.Cell(row, 7).Value = item.Amount;
            sheet.Cell(row, 8).Value = item.Reference;
            sheet.Cell(row, 9).Value = item.IsVoided ? "Đã void" : "Hợp lệ";
            sheet.Cell(row, 10).Value = item.IsVoided ? 0 : item.Type == PaymentType.Receipt ? item.Amount : -item.Amount;
            sheet.Range(row, 7, row, 7).Style.NumberFormat.Format = CurrencyFormat;
            sheet.Cell(row, 10).Style.NumberFormat.Format = CurrencyFormat;
            row++;
        }
        FinishSheet(sheet, row - 1, 10);
    }

    private static void AddExpensesSheet(
        XLWorkbook workbook,
        IReadOnlyList<Features.Expenses.ExpenseDto> expenses,
        string scopeName,
        FinancePeriodRange period,
        IReadOnlyDictionary<Guid, string> propertyNames,
        IReadOnlyDictionary<Guid, TimeZoneInfo> propertyTimeZones)
    {
        var sheet = CreateSheet(workbook, "Chi phí", "CHI PHÍ VẬN HÀNH", scopeName, period, 12);
        WriteHeaders(sheet, 4, ["Thời gian", "Cơ sở", "Nhóm", "Nội dung", "Nhà cung cấp", "Phương thức", "Số tiền", "Tham chiếu", "Trạng thái", "Số tiền hiệu lực", "Lý do void", "Ghi chú"]);
        var row = 5;
        foreach (var item in expenses.OrderBy(x => x.OccurredAtUtc))
        {
            sheet.Cell(row, 1).Value = LocalTime(item.OccurredAtUtc, item.PropertyId, propertyTimeZones);
            sheet.Cell(row, 1).Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
            sheet.Cell(row, 2).Value = PropertyName(item.PropertyId, propertyNames);
            sheet.Cell(row, 3).Value = item.Category;
            sheet.Cell(row, 4).Value = item.Description;
            sheet.Cell(row, 5).Value = item.Vendor;
            sheet.Cell(row, 6).Value = PaymentMethodLabel(item.Method);
            sheet.Cell(row, 7).Value = item.Amount;
            sheet.Cell(row, 8).Value = item.Reference;
            sheet.Cell(row, 9).Value = item.IsVoided ? "Đã void" : "Hợp lệ";
            sheet.Cell(row, 10).Value = item.IsVoided ? 0 : -item.Amount;
            sheet.Cell(row, 11).Value = item.VoidReason;
            sheet.Cell(row, 12).Value = item.Note;
            sheet.Range(row, 7, row, 7).Style.NumberFormat.Format = CurrencyFormat;
            sheet.Cell(row, 10).Style.NumberFormat.Format = CurrencyFormat;
            row++;
        }
        FinishSheet(sheet, row - 1, 12);
    }

    private static IEnumerable<LedgerRow> BuildLedgerRows(
        FinanceSnapshotDto snapshot,
        IReadOnlyDictionary<Guid, string> propertyNames,
        IReadOnlyDictionary<Guid, TimeZoneInfo> propertyTimeZones)
    {
        foreach (var payment in snapshot.Payments)
        {
            var effectiveAmount = payment.IsVoided ? 0 : payment.Amount;
            yield return new LedgerRow(
                LocalTime(payment.OccurredAtUtc, payment.PropertyId, propertyTimeZones),
                PropertyName(payment.PropertyId, propertyNames),
                payment.Type == PaymentType.Receipt ? "Thu booking" : "Hoàn tiền",
                payment.BookingCode,
                payment.CustomerName,
                PaymentMethodLabel(payment.Method),
                payment.Reference,
                payment.Type == PaymentType.Receipt ? effectiveAmount : 0,
                payment.Type == PaymentType.Refund ? effectiveAmount : 0,
                payment.IsVoided,
                null);
        }

        foreach (var expense in snapshot.Expenses)
        {
            yield return new LedgerRow(
                LocalTime(expense.OccurredAtUtc, expense.PropertyId, propertyTimeZones),
                PropertyName(expense.PropertyId, propertyNames),
                "Chi phí",
                expense.Category,
                expense.Description,
                PaymentMethodLabel(expense.Method),
                expense.Reference,
                0,
                expense.IsVoided ? 0 : expense.Amount,
                expense.IsVoided,
                expense.IsVoided ? expense.VoidReason : expense.Note);
        }
    }

    private static IXLWorksheet CreateSheet(XLWorkbook workbook, string name, string title, string scopeName, FinancePeriodRange period, int columns)
    {
        var sheet = workbook.Worksheets.Add(name);
        sheet.SheetView.FreezeRows(4);
        sheet.Range(1, 1, 1, columns).Merge().Value = title;
        sheet.Range(1, 1, 1, columns).Style.Font.SetBold().Font.SetFontSize(16).Font.SetFontColor(XLColor.White)
            .Fill.SetBackgroundColor(Primary).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left).Alignment.SetVertical(XLAlignmentVerticalValues.Center);
        sheet.Row(1).Height = 30;
        sheet.Range(2, 1, 2, columns).Merge().Value = $"Phạm vi: {scopeName} · Kỳ: {PeriodLabel(period)} · Xuất lúc: {DateTime.Now:dd/MM/yyyy HH:mm}";
        sheet.Range(2, 1, 2, columns).Style.Font.SetFontColor(Muted).Fill.SetBackgroundColor(PrimaryLight);
        sheet.Row(2).Height = 24;
        return sheet;
    }

    private static void WriteHeaders(IXLWorksheet sheet, int row, IReadOnlyList<string> headers)
    {
        for (var index = 0; index < headers.Count; index++) sheet.Cell(row, index + 1).Value = headers[index];
        sheet.Range(row, 1, row, headers.Count).Style.Font.SetBold().Font.SetFontColor(XLColor.White)
            .Fill.SetBackgroundColor(Primary).Alignment.SetVertical(XLAlignmentVerticalValues.Center);
        sheet.Row(row).Height = 24;
    }

    private static void FinishSheet(IXLWorksheet sheet, int lastRow, int lastColumn)
    {
        if (lastRow >= 5) sheet.Range(4, 1, lastRow, lastColumn).CreateTable().Theme = XLTableTheme.TableStyleMedium2;
        sheet.Columns(1, lastColumn).AdjustToContents();
        foreach (var column in sheet.Columns(1, lastColumn))
        {
            if (column.Width > 38) column.Width = 38;
            if (column.Width < 12) column.Width = 12;
        }
        sheet.RowsUsed().Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center);
        sheet.RowsUsed().Style.Font.SetFontName("Aptos");
        sheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        sheet.PageSetup.FitToPages(1, 0);
    }

    private static DateTime LocalTime(DateTime utc, Guid propertyId, IReadOnlyDictionary<Guid, TimeZoneInfo> timeZones) =>
        TimeZoneInfo.ConvertTimeFromUtc(utc, timeZones.TryGetValue(propertyId, out var zone) ? zone : TimeZoneInfo.Utc);
    private static string PropertyName(Guid propertyId, IReadOnlyDictionary<Guid, string> names) =>
        names.TryGetValue(propertyId, out var name) ? name : propertyId.ToString();
    private static string PeriodLabel(FinancePeriodRange period) => period.Period switch
    {
        "day" => period.Start.ToString("dd/MM/yyyy"),
        "week" => $"{period.Start:dd/MM/yyyy} – {period.EndExclusive.AddDays(-1):dd/MM/yyyy}",
        "quarter" => $"Quý {(period.Start.Month - 1) / 3 + 1}/{period.Start.Year}",
        _ => period.Start.ToString("MM/yyyy")
    };
    private static string PaymentMethodLabel(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "Tiền mặt",
        PaymentMethod.BankTransfer => "Chuyển khoản",
        PaymentMethod.Card => "Thẻ",
        PaymentMethod.Pay2S => "Pay2S",
        _ => "Khác"
    };

    private sealed record LedgerRow(
        DateTime OccurredAt,
        string PropertyName,
        string Type,
        string Category,
        string Description,
        string Method,
        string? Reference,
        decimal Receipt,
        decimal Outflow,
        bool IsVoided,
        string? Note);
}
