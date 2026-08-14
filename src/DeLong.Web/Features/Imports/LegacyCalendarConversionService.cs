using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace DeLong.Web.Features.Imports;

public sealed record LegacyCalendarConversion(byte[] FileBytes, int OccupiedSlots, string FileName);

public sealed class LegacyCalendarConversionService
{
    private const long MaxFileBytes = 10L * 1024 * 1024;
    private static readonly Regex SlotTimeRegex = new(@"(?<start>\d{1,2}:\d{2})\s*[-–→]\s*(?<end>\d{1,2}:\d{2})", RegexOptions.Compiled);

    public async Task<(LegacyCalendarConversion? Result, BookingImportError? Error)> ConvertAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length <= 0) return (null, new("file_empty", "Vui lòng chọn file lịch Excel."));
        if (file.Length > MaxFileBytes) return (null, new("file_too_large", "File Excel tối đa 10 MB."));
        var extension = Path.GetExtension(file.FileName);
        if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase) && !string.Equals(extension, ".xlsm", StringComparison.OrdinalIgnoreCase))
            return (null, new("file_type", "Chỉ hỗ trợ file .xlsx hoặc .xlsm."));

        await using var upload = file.OpenReadStream();
        using var input = new MemoryStream();
        await upload.CopyToAsync(input, cancellationToken);
        input.Position = 0;

        XLWorkbook source;
        try { source = new XLWorkbook(input); }
        catch { return (null, new("file_invalid", "Không thể đọc workbook Excel này.")); }

        using (source)
        {
            var slots = ExtractOccupiedSlots(source);
            if (slots.Count == 0)
                return (null, new("no_legacy_slots", "Không tìm thấy ô đặt phòng có màu trong lịch. File có thể không phải lịch màu De Long hoặc tháng này chưa có booking."));

            using var outputWorkbook = new XLWorkbook();
            var sheet = outputWorkbook.Worksheets.Add("Đặt phòng");
            var headers = new[]
            {
                "ID", "Tên cơ sở", "Mã phòng", "Ngày tạo", "Tên khách hàng", "Số điện thoại", "Nguồn", "Nhân viên",
                "Hình thức phòng", "Ngày giờ checkin", "Ngày giờ checkout", "Đơn giá", "Phụ phí", "Tổng doanh thu", "Ghi chú"
            };
            for (var i = 0; i < headers.Length; i++) sheet.Cell(1, i + 1).Value = headers[i];
            sheet.Range(1, 1, 1, headers.Length).Style.Font.SetBold().Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(XLColor.FromHtml("#155E63"));
            sheet.SheetView.FreezeRows(1);

            for (var index = 0; index < slots.Count; index++)
            {
                var item = slots[index];
                var row = index + 2;
                sheet.Cell(row, 1).Value = $"CAL-{item.SheetName}-{item.CellAddress}";
                sheet.Cell(row, 2).Value = "De Long Homestay";
                sheet.Cell(row, 3).Value = item.RoomName;
                sheet.Cell(row, 4).Value = item.CheckIn.Date;
                sheet.Cell(row, 5).Value = string.Empty;
                sheet.Cell(row, 6).Value = string.Empty;
                sheet.Cell(row, 7).Value = "Lịch màu Excel";
                sheet.Cell(row, 9).Value = item.IsOvernight ? "Qua đêm" : "Combo";
                sheet.Cell(row, 10).Value = item.CheckIn;
                sheet.Cell(row, 11).Value = item.CheckOut;
                sheet.Cell(row, 12).Value = item.Price;
                sheet.Cell(row, 13).Value = 0;
                sheet.Cell(row, 14).Value = item.Price;
                sheet.Cell(row, 15).Value = BuildNote(item);
                sheet.Cell(row, 5).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF1B8");
                sheet.Cell(row, 6).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF1B8");
            }

            sheet.Column(1).Width = 23;
            sheet.Column(3).Width = 20;
            sheet.Column(5).Width = 24;
            sheet.Column(6).Width = 17;
            sheet.Columns(9, 11).Width = 22;
            sheet.Columns(12, 14).Width = 16;
            sheet.Column(15).Width = 46;
            sheet.Range($"D2:D{slots.Count + 1}").Style.DateFormat.Format = "dd/MM/yyyy";
            sheet.Range($"J2:K{slots.Count + 1}").Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
            sheet.Range($"L2:N{slots.Count + 1}").Style.NumberFormat.Format = "#,##0";
            sheet.RangeUsed()!.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
            sheet.RangeUsed()!.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            sheet.RangeUsed()!.Style.Border.InsideBorder = XLBorderStyleValues.Hair;

            var guide = outputWorkbook.Worksheets.Add("Cần hoàn thiện");
            guide.Cell("A1").Value = "Đã chuyển lịch màu sang danh sách booking nháp";
            guide.Cell("A1").Style.Font.SetBold().Font.SetFontSize(16);
            guide.Cell("A3").Value = "Việc cần làm";
            guide.Cell("B3").Value = "Điền Tên khách hàng và Số điện thoại ở các ô màu vàng. Kiểm tra các booking nhiều khung và gộp/chỉnh thời gian nếu cần.";
            guide.Cell("A4").Value = "Vì sao không import thẳng?";
            guide.Cell("B4").Value = "Lịch màu cũ không chứa tên khách/SĐT theo từng ô, và một khách có thể chiếm nhiều khung. Hệ thống không tự bịa dữ liệu.";
            guide.Cell("A5").Value = "Sau khi sửa";
            guide.Cell("B5").Value = "Lưu file rồi vào Nhập dữ liệu → Xem trước. Các dòng lỗi/trùng sẽ được báo trước khi ghi PostgreSQL.";
            guide.Cell("A6").Value = "Số ô đã nhận diện";
            guide.Cell("B6").Value = slots.Count;
            guide.Columns(1, 2).AdjustToContents();
            guide.Column(2).Width = Math.Min(guide.Column(2).Width, 90);
            guide.Column(2).Style.Alignment.WrapText = true;

            using var output = new MemoryStream();
            outputWorkbook.SaveAs(output);
            return (new LegacyCalendarConversion(output.ToArray(), slots.Count, $"DeLong-calendar-converted-{DateTime.Today:yyyyMMdd}.xlsx"), null);
        }
    }

    private static List<LegacyOccupiedSlot> ExtractOccupiedSlots(XLWorkbook workbook)
    {
        var result = new List<LegacyOccupiedSlot>();
        foreach (var sheet in workbook.Worksheets)
        {
            var used = sheet.RangeUsed();
            if (used is null) continue;
            var lastRow = used.LastRow().RowNumber();
            var lastColumn = used.LastColumn().ColumnNumber();
            var roomHeaderRows = new List<int>();
            for (var row = 1; row <= lastRow; row++)
            {
                if (string.Equals(sheet.Cell(row, 1).GetString().Trim(), "Tên phòng", StringComparison.OrdinalIgnoreCase)) roomHeaderRows.Add(row);
            }

            for (var sectionIndex = 0; sectionIndex < roomHeaderRows.Count; sectionIndex++)
            {
                var roomHeaderRow = roomHeaderRows[sectionIndex];
                var slotHeaderRow = roomHeaderRow + 1;
                var dataStart = slotHeaderRow + 1;
                var dataEnd = sectionIndex + 1 < roomHeaderRows.Count ? roomHeaderRows[sectionIndex + 1] - 1 : lastRow;
                var roomStarts = new List<(int Column, string RoomName)>();
                for (var column = 3; column <= lastColumn; column++)
                {
                    var text = sheet.Cell(roomHeaderRow, column).GetString().Trim();
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    var name = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(name)) roomStarts.Add((column, name));
                }
                if (roomStarts.Count == 0) continue;

                for (var roomIndex = 0; roomIndex < roomStarts.Count; roomIndex++)
                {
                    var roomStart = roomStarts[roomIndex];
                    var roomEnd = roomIndex + 1 < roomStarts.Count ? roomStarts[roomIndex + 1].Column - 1 : lastColumn;
                    for (var column = roomStart.Column; column <= roomEnd; column++)
                    {
                        var header = sheet.Cell(slotHeaderRow, column).GetString();
                        var match = SlotTimeRegex.Match(header);
                        if (!match.Success) continue;
                        if (!TimeOnly.TryParseExact(match.Groups["start"].Value, "H:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start)) continue;
                        if (!TimeOnly.TryParseExact(match.Groups["end"].Value, "H:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var end)) continue;
                        var price = ParseHeaderPrice(header);

                        for (var row = dataStart; row <= dataEnd; row++)
                        {
                            var dateCell = sheet.Cell(row, 2);
                            if (!dateCell.TryGetValue<DateTime>(out var date)) continue;
                            var bookingCell = sheet.Cell(row, column);
                            if (bookingCell.Style.Fill.PatternType == XLFillPatternValues.None) continue;

                            var checkIn = date.Date.Add(start.ToTimeSpan());
                            var checkOut = date.Date.Add(end.ToTimeSpan());
                            var overnight = end <= start;
                            if (overnight) checkOut = checkOut.AddDays(1);
                            result.Add(new LegacyOccupiedSlot(
                                sheet.Name,
                                bookingCell.Address.ToStringRelative(),
                                roomStart.RoomName,
                                checkIn,
                                checkOut,
                                price,
                                overnight,
                                bookingCell.GetString().Trim()));
                        }
                    }
                }
            }
        }
        return result
            .GroupBy(x => new { x.SheetName, x.CellAddress })
            .Select(x => x.First())
            .OrderBy(x => x.CheckIn)
            .ThenBy(x => x.RoomName)
            .ToList();
    }

    private static decimal ParseHeaderPrice(string header)
    {
        var priceMatch = Regex.Match(header, @"\((?<price>[\d.,]+)\s*k\)", RegexOptions.IgnoreCase);
        if (!priceMatch.Success) return 0;
        var raw = priceMatch.Groups["price"].Value.Replace(',', '.');
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value * 1000 : 0;
    }

    private static string BuildNote(LegacyOccupiedSlot item)
    {
        var note = $"Chuyển từ {item.SheetName}!{item.CellAddress}";
        if (!string.IsNullOrWhiteSpace(item.CellNote)) note += $" · Ghi chú cũ: {item.CellNote}";
        return note;
    }

    private sealed record LegacyOccupiedSlot(
        string SheetName,
        string CellAddress,
        string RoomName,
        DateTime CheckIn,
        DateTime CheckOut,
        decimal Price,
        bool IsOvernight,
        string CellNote);
}
