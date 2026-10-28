using ClosedXML.Excel;

namespace DeLong.Web.Features.Finance;

public sealed record OperationalSheet(string Name, string[] Headers, IReadOnlyList<object?[]> Rows);

public static class OperationalExcelExport
{
    public static byte[] Create(params OperationalSheet[] sheets)
    {
        using var book = new XLWorkbook();
        foreach (var source in sheets) {
            var sheet = book.Worksheets.Add(source.Name);
            for (var column = 0; column < source.Headers.Length; column++) sheet.Cell(1, column + 1).Value = source.Headers[column];
            for (var row = 0; row < source.Rows.Count; row++)
            for (var column = 0; column < source.Rows[row].Length; column++) {
                var cell = sheet.Cell(row + 2, column + 1);
                switch (source.Rows[row][column]) {
                    case decimal number: cell.Value = (double)number; cell.Style.NumberFormat.Format = "#,##0.###"; break;
                    case DateTime date: cell.Value = date; cell.Style.NumberFormat.Format = "dd/MM/yyyy HH:mm"; break;
                    case bool flag: cell.Value = flag ? "Có" : "Không"; break;
                    default: cell.Value = source.Rows[row][column]?.ToString() ?? ""; break;
                }
            }
            sheet.Range(1, 1, 1, source.Headers.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#155D59");
            sheet.Range(1, 1, 1, source.Headers.Length).Style.Font.FontColor = XLColor.White;
            sheet.Range(1, 1, 1, source.Headers.Length).Style.Font.Bold = true;
            sheet.SheetView.FreezeRows(1);
            sheet.Range(1, 1, Math.Max(1, source.Rows.Count + 1), source.Headers.Length).SetAutoFilter();
            sheet.Columns().AdjustToContents(10, 55);
        }
        using var stream = new MemoryStream(); book.SaveAs(stream); return stream.ToArray();
    }
}
