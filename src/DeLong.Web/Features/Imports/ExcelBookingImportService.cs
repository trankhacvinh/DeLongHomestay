using System.Globalization;
using System.Security.Claims;
using System.Text;
using ClosedXML.Excel;
using DeLong.Web.Common.Auditing;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Customers;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DeLong.Web.Features.Imports;

public sealed record BookingImportPreview(
    string Format,
    string FileName,
    int TotalRows,
    int ReadyRows,
    int DuplicateRows,
    int ErrorRows,
    IReadOnlyList<string> Messages,
    IReadOnlyList<BookingImportRowPreview> Rows);

public sealed record BookingImportRowPreview(
    int ExcelRow,
    string? ExternalId,
    string RoomInput,
    Guid? RoomId,
    string? RoomName,
    string CustomerName,
    string CustomerPhone,
    DateTimeOffset? CheckIn,
    DateTimeOffset? CheckOut,
    decimal RoomAmount,
    decimal ExtraAmount,
    decimal DiscountAmount,
    string Source,
    string? Note,
    string State,
    string? Message);

public sealed record BookingImportCommitResult(int ImportedRows, int SkippedDuplicates, IReadOnlyList<string> BookingCodes);
public sealed record BookingImportError(string Code, string Message);

internal sealed record ParsedBookingRow(
    int ExcelRow,
    string? ExternalId,
    string RoomInput,
    string CustomerName,
    string CustomerPhone,
    DateTime LocalCheckIn,
    DateTime LocalCheckOut,
    decimal RoomAmount,
    decimal ExtraAmount,
    decimal DiscountAmount,
    string Source,
    string? Note,
    string? Method);

internal sealed record ResolvedImportRow(
    ParsedBookingRow Parsed,
    Room? Room,
    DateTimeOffset? CheckIn,
    DateTimeOffset? CheckOut,
    BookingType Type,
    RoomRate? NightlyRate,
    int? NightCount,
    string State,
    string? Message);

public sealed class ExcelBookingImportService(AppDbContext db, AuditService auditService)
{
    private const long MaxFileBytes = 10L * 1024 * 1024;
    private static readonly BookingStatus[] LockingStatuses = [BookingStatus.Held, BookingStatus.Confirmed, BookingStatus.CheckedIn];
    private static readonly string[] RequiredStructuredHeaders = ["Mã phòng", "Tên khách hàng", "Số điện thoại", "Ngày giờ checkin", "Ngày giờ checkout"];

    public async Task<(BookingImportPreview? Preview, BookingImportError? Error)> PreviewAsync(
        Guid propertyId,
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        var parsed = await ParseFileAsync(file, cancellationToken);
        if (parsed.Error is not null) return (null, parsed.Error);

        if (parsed.Rows.Count == 0)
        {
            return (new BookingImportPreview(
                parsed.Format,
                file.FileName,
                0,
                0,
                0,
                0,
                parsed.Messages,
                []), null);
        }

        var property = await db.Properties.AsNoTracking().SingleOrDefaultAsync(x => x.Id == propertyId && x.IsActive, cancellationToken);
        if (property is null) return (null, new("property_not_found", "Không tìm thấy cơ sở đang hoạt động."));

        var rooms = await db.Rooms
            .AsNoTracking()
            .Include(x => x.Rates)
            .Where(x => x.PropertyId == propertyId && x.IsActive)
            .ToListAsync(cancellationToken);
        var resolved = new List<ResolvedImportRow>(parsed.Rows.Count);

        foreach (var row in parsed.Rows)
            resolved.Add(await ResolveRowAsync(propertyId, property.TimeZoneId, rooms, row, cancellationToken));

        var previews = resolved.Select(ToPreview).ToList();
        return (new BookingImportPreview(
            parsed.Format,
            file.FileName,
            previews.Count,
            previews.Count(x => x.State == "ready"),
            previews.Count(x => x.State == "duplicate"),
            previews.Count(x => x.State == "error"),
            parsed.Messages,
            previews), null);
    }

    public async Task<(BookingImportCommitResult? Result, BookingImportError? Error)> ImportAsync(
        Guid propertyId,
        IFormFile file,
        Guid? actorUserId,
        CancellationToken cancellationToken = default)
    {
        var parsed = await ParseFileAsync(file, cancellationToken);
        if (parsed.Error is not null) return (null, parsed.Error);
        if (!string.Equals(parsed.Format, "structured-bookings", StringComparison.Ordinal))
            return (null, new("format_not_importable", "File này chỉ có lịch màu/ghi chú và không đủ tên khách, số điện thoại để import an toàn. Hãy dùng mẫu “Đặt phòng” có cấu trúc."));
        if (parsed.Rows.Count == 0) return (null, new("no_rows", "Không có dòng đặt phòng nào để import."));

        var property = await db.Properties.SingleOrDefaultAsync(x => x.Id == propertyId && x.IsActive, cancellationToken);
        if (property is null) return (null, new("property_not_found", "Không tìm thấy cơ sở đang hoạt động."));
        var rooms = await db.Rooms.Include(x => x.Rates).Where(x => x.PropertyId == propertyId && x.IsActive).ToListAsync(cancellationToken);

        var resolved = new List<ResolvedImportRow>(parsed.Rows.Count);
        foreach (var row in parsed.Rows)
            resolved.Add(await ResolveRowAsync(propertyId, property.TimeZoneId, rooms, row, cancellationToken));

        var errors = resolved.Where(x => x.State == "error").ToList();
        if (errors.Count > 0)
            return (null, new("preview_has_errors", $"Có {errors.Count} dòng chưa hợp lệ. Hãy sửa file và xem trước lại trước khi import."));

        var ready = resolved.Where(x => x.State == "ready").ToList();
        var duplicateCount = resolved.Count(x => x.State == "duplicate");
        if (ready.Count == 0) return (new BookingImportCommitResult(0, duplicateCount, []), null);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var customerByPhone = await db.Customers
                .Where(x => x.PropertyId == propertyId)
                .ToDictionaryAsync(x => x.NormalizedPhone, StringComparer.Ordinal, cancellationToken);
            var importedCodes = new List<string>(ready.Count);

            foreach (var row in ready)
            {
                var phone = NormalizeLegacyPhone(row.Parsed.CustomerPhone);
                if (!customerByPhone.TryGetValue(phone, out var customer))
                {
                    customer = new Customer
                    {
                        PropertyId = propertyId,
                        Name = row.Parsed.CustomerName.Trim(),
                        Phone = FormatDisplayPhone(row.Parsed.CustomerPhone, phone),
                        NormalizedPhone = phone,
                        IsActive = true,
                        Note = "Tạo từ import Excel"
                    };
                    db.Customers.Add(customer);
                    customerByPhone[phone] = customer;
                }
                else
                {
                    customer.Name = row.Parsed.CustomerName.Trim();
                    if (!customer.IsActive) customer.IsActive = true;
                }

                var checkIn = row.CheckIn!.Value;
                var checkOut = row.CheckOut!.Value;
                var status = checkOut.UtcDateTime <= DateTime.UtcNow ? BookingStatus.Completed : BookingStatus.Confirmed;
                var booking = new Booking
                {
                    PropertyId = propertyId,
                    RoomId = row.Room!.Id,
                    Room = row.Room,
                    Customer = customer,
                    Code = CreateBookingCode(),
                    Type = row.Type,
                    RoomRateId = row.NightlyRate?.Id,
                    RoomRate = row.NightlyRate,
                    RateName = row.Type == BookingType.MultiDay ? row.NightlyRate?.Name ?? "Lưu trú theo đêm (import)" : Clean(row.Parsed.Method),
                    UnitPrice = row.Type == BookingType.MultiDay && row.NightCount is > 0 ? decimal.Round(row.Parsed.RoomAmount / row.NightCount.Value, 0) : null,
                    NightCount = row.NightCount,
                    CheckInUtc = checkIn.UtcDateTime,
                    CheckOutUtc = checkOut.UtcDateTime,
                    Status = status,
                    RoomAmount = row.Parsed.RoomAmount,
                    ExtraAmount = row.Parsed.ExtraAmount,
                    DiscountAmount = row.Parsed.DiscountAmount,
                    Source = string.IsNullOrWhiteSpace(row.Parsed.Source) ? "Excel" : $"Excel · {row.Parsed.Source.Trim()}",
                    Note = BuildImportNote(row.Parsed)
                };
                db.Bookings.Add(booking);
                importedCodes.Add(booking.Code);
                auditService.Add(propertyId, "Booking", booking.Id, "Imported", actorUserId, after: new
                {
                    booking.Code,
                    booking.RoomId,
                    booking.CheckInUtc,
                    booking.CheckOutUtc,
                    Status = booking.Status.ToString(),
                    ExcelRow = row.Parsed.ExcelRow,
                    row.Parsed.ExternalId
                });
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return (new BookingImportCommitResult(ready.Count, duplicateCount, importedCodes), null);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == PostgresErrorCodes.ExclusionViolation)
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            return (null, new("booking_conflict", "Dữ liệu đã thay đổi sau khi xem trước: có lượt đặt trùng phòng/thời gian. Hãy xem trước file lại."));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            throw;
        }
    }

    public byte[] CreateTemplate()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Đặt phòng");
        var headers = new[]
        {
            "ID", "Tên cơ sở", "Mã phòng", "Ngày tạo", "Tên khách hàng", "Số điện thoại", "Nguồn", "Nhân viên",
            "Hình thức phòng", "Ngày giờ checkin", "Ngày giờ checkout", "Đơn giá", "Phụ phí", "Tổng doanh thu", "Ghi chú"
        };
        for (var i = 0; i < headers.Length; i++) sheet.Cell(1, i + 1).Value = headers[i];
        sheet.Range(1, 1, 1, headers.Length).Style
            .Font.SetBold().Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(XLColor.FromHtml("#155E63"));
        sheet.SheetView.FreezeRows(1);
        sheet.Column(1).Width = 16;
        sheet.Column(3).Width = 18;
        sheet.Column(5).Width = 24;
        sheet.Column(6).Width = 17;
        sheet.Columns(9, 11).Width = 22;
        sheet.Columns(12, 14).Width = 16;
        sheet.Column(15).Width = 34;
        sheet.Cell(2, 1).Value = "OLD-001";
        sheet.Cell(2, 2).Value = "De Long Homestay";
        sheet.Cell(2, 3).Value = "COCO-01";
        sheet.Cell(2, 4).Value = DateTime.Today;
        sheet.Cell(2, 5).Value = "Nguyễn Văn A";
        sheet.Cell(2, 6).Value = "0987654321";
        sheet.Cell(2, 7).Value = "Zalo";
        sheet.Cell(2, 9).Value = "Qua đêm";
        sheet.Cell(2, 10).Value = DateTime.Today.AddHours(21);
        sheet.Cell(2, 11).Value = DateTime.Today.AddDays(1).AddHours(9).AddMinutes(30);
        sheet.Cell(2, 12).Value = 360000;
        sheet.Cell(2, 13).Value = 0;
        sheet.Cell(2, 14).Value = 360000;
        sheet.Cell(2, 15).Value = "Dòng ví dụ — xóa trước khi import dữ liệu thật";
        sheet.Range("D2:D200").Style.DateFormat.Format = "dd/MM/yyyy";
        sheet.Range("J2:K200").Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
        sheet.Range("L2:N200").Style.NumberFormat.Format = "#,##0";
        sheet.Range(1, 1, 2, headers.Length).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        sheet.Range(1, 1, 2, headers.Length).Style.Border.InsideBorder = XLBorderStyleValues.Hair;

        var note = workbook.Worksheets.Add("Hướng dẫn");
        note.Cell("A1").Value = "Import booking De Long";
        note.Cell("A1").Style.Font.SetBold().Font.SetFontSize(16);
        note.Cell("A3").Value = "Bắt buộc";
        note.Cell("B3").Value = "Mã phòng, Tên khách hàng, Số điện thoại, Ngày giờ checkin, Ngày giờ checkout";
        note.Cell("A4").Value = "Mã phòng";
        note.Cell("B4").Value = "Có thể dùng mã như COCO-01 hoặc tên phòng như Coco Blue #1.";
        note.Cell("A5").Value = "Dữ liệu trùng";
        note.Cell("B5").Value = "Dòng trùng chính xác với booking đã có sẽ được bỏ qua; xung đột khác sẽ chặn import.";
        note.Cell("A6").Value = "Thanh toán";
        note.Cell("B6").Value = "Import này chỉ tạo khách + booking. Giao dịch thu/hoàn tiền không được suy đoán từ Excel.";
        note.Cell("A7").Value = "An toàn";
        note.Cell("B7").Value = "Luôn dùng Xem trước trước khi Import. Import chạy trong transaction toàn bộ.";
        note.Columns(1, 2).AdjustToContents();
        note.Column(2).Width = Math.Min(note.Column(2).Width, 80);
        note.Column(2).Style.Alignment.WrapText = true;

        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return output.ToArray();
    }

    private async Task<ResolvedImportRow> ResolveRowAsync(
        Guid propertyId,
        string timeZoneId,
        IReadOnlyList<Room> rooms,
        ParsedBookingRow row,
        CancellationToken cancellationToken)
    {
        var room = FindRoom(rooms, row.RoomInput);
        if (room is null) return new(row, null, null, null, BookingType.TimeSlot, null, null, "error", $"Không tìm thấy phòng “{row.RoomInput}”.");
        var phone = NormalizeLegacyPhone(row.CustomerPhone);
        if (string.IsNullOrWhiteSpace(row.CustomerName)) return new(row, room, null, null, BookingType.TimeSlot, null, null, "error", "Thiếu tên khách hàng.");
        if (phone.Length < 8 || phone.Length > 20) return new(row, room, null, null, BookingType.TimeSlot, null, null, "error", "Số điện thoại không hợp lệ.");
        if (row.LocalCheckOut <= row.LocalCheckIn) return new(row, room, null, null, BookingType.TimeSlot, null, null, "error", "Giờ checkout phải sau checkin.");
        if (row.RoomAmount < 0 || row.ExtraAmount < 0 || row.DiscountAmount < 0 || row.RoomAmount + row.ExtraAmount - row.DiscountAmount < 0)
            return new(row, room, null, null, BookingType.TimeSlot, null, null, "error", "Số tiền không hợp lệ.");

        var checkIn = ToPropertyOffset(row.LocalCheckIn, timeZoneId);
        var checkOut = ToPropertyOffset(row.LocalCheckOut, timeZoneId);
        var dateNights = (row.LocalCheckOut.Date - row.LocalCheckIn.Date).Days;
        var type = dateNights >= 2 ? BookingType.MultiDay : BookingType.TimeSlot;
        RoomRate? nightlyRate = null;
        int? nights = null;
        if (type == BookingType.MultiDay)
        {
            nights = dateNights;
            nightlyRate = room.Rates.FirstOrDefault(x => x.IsActive && x.Type == RoomRateType.Nightly);
            if (nightlyRate is null)
                return new(row, room, checkIn, checkOut, type, null, nights, "error", "Phòng chưa có mức giá “Lưu trú theo đêm”; không thể import booking nhiều ngày an toàn.");
        }

        var exactDuplicate = await db.Bookings.AsNoTracking().AnyAsync(x =>
            x.PropertyId == propertyId && x.RoomId == room.Id &&
            x.CheckInUtc == checkIn.UtcDateTime && x.CheckOutUtc == checkOut.UtcDateTime &&
            x.Customer.NormalizedPhone == phone,
            cancellationToken);
        if (exactDuplicate) return new(row, room, checkIn, checkOut, type, nightlyRate, nights, "duplicate", "Đã có booking cùng phòng, thời gian và SĐT — sẽ bỏ qua.");

        var willLock = checkOut.UtcDateTime > DateTime.UtcNow;
        if (willLock)
        {
            var conflict = await db.Bookings.AsNoTracking().AnyAsync(x =>
                x.PropertyId == propertyId && x.RoomId == room.Id && LockingStatuses.Contains(x.Status) &&
                x.CheckInUtc < checkOut.UtcDateTime && checkIn.UtcDateTime < x.CheckOutUtc,
                cancellationToken);
            if (conflict) return new(row, room, checkIn, checkOut, type, nightlyRate, nights, "error", "Phòng đã có booking khóa phòng giao với khoảng thời gian này.");
        }

        return new(row, room, checkIn, checkOut, type, nightlyRate, nights, "ready", type == BookingType.MultiDay ? $"Sẵn sàng · {nights} đêm" : "Sẵn sàng");
    }

    private static BookingImportRowPreview ToPreview(ResolvedImportRow row) => new(
        row.Parsed.ExcelRow,
        row.Parsed.ExternalId,
        row.Parsed.RoomInput,
        row.Room?.Id,
        row.Room?.Name,
        row.Parsed.CustomerName,
        FormatDisplayPhone(row.Parsed.CustomerPhone, NormalizeLegacyPhone(row.Parsed.CustomerPhone)),
        row.CheckIn,
        row.CheckOut,
        row.Parsed.RoomAmount,
        row.Parsed.ExtraAmount,
        row.Parsed.DiscountAmount,
        row.Parsed.Source,
        row.Parsed.Note,
        row.State,
        row.Message);

    private async Task<(string Format, List<ParsedBookingRow> Rows, List<string> Messages, BookingImportError? Error)> ParseFileAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length <= 0) return ("unknown", [], [], new("file_empty", "Vui lòng chọn file Excel."));
        if (file.Length > MaxFileBytes) return ("unknown", [], [], new("file_too_large", "File Excel tối đa 10 MB."));
        var extension = Path.GetExtension(file.FileName);
        if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase) && !string.Equals(extension, ".xlsm", StringComparison.OrdinalIgnoreCase))
            return ("unknown", [], [], new("file_type", "Chỉ hỗ trợ file .xlsx hoặc .xlsm."));

        await using var upload = file.OpenReadStream();
        using var memory = new MemoryStream();
        await upload.CopyToAsync(memory, cancellationToken);
        memory.Position = 0;

        XLWorkbook workbook;
        try { workbook = new XLWorkbook(memory); }
        catch (Exception) { return ("unknown", [], [], new("file_invalid", "Không thể đọc workbook Excel này.")); }
        using (workbook)
        {
            var structured = workbook.Worksheets.FirstOrDefault(x => string.Equals(x.Name.Trim(), "Đặt phòng", StringComparison.OrdinalIgnoreCase));
            if (structured is not null)
            {
                var parse = ParseStructuredSheet(structured);
                if (parse.Error is not null) return ("structured-bookings", [], [], parse.Error);
                var messages = new List<string>();
                if (parse.Rows.Count == 0) messages.Add("Sheet “Đặt phòng” có đúng cấu trúc nhưng hiện chưa có dữ liệu booking.");
                return ("structured-bookings", parse.Rows, messages, null);
            }

            var looksLikeCalendar = workbook.Worksheets.Any(sheet =>
                sheet.CellsUsed().Take(250).Any(cell => cell.GetString().Contains("Tên phòng", StringComparison.OrdinalIgnoreCase)) &&
                sheet.CellsUsed().Take(250).Any(cell => cell.GetString().Contains("Còn trống", StringComparison.OrdinalIgnoreCase)));
            if (looksLikeCalendar)
            {
                return ("legacy-calendar", [], [
                    "Đã nhận diện file lịch màu De Long.",
                    "File lịch màu chỉ thể hiện ô đã đặt/ghi chú giờ nhưng không chứa tên khách + SĐT theo từng lượt, nên hệ thống không tự tạo booking để tránh sinh dữ liệu giả.",
                    "Hãy tải mẫu import có cấu trúc, điền các booking cần giữ rồi dùng Xem trước lại."
                ], null);
            }

            return ("unknown", [], [], new("format_unknown", "Không nhận diện được định dạng. Hãy dùng sheet “Đặt phòng” theo mẫu import của hệ thống."));
        }
    }

    private static (List<ParsedBookingRow> Rows, BookingImportError? Error) ParseStructuredSheet(IXLWorksheet sheet)
    {
        var headerRow = sheet.FirstRowUsed()?.RowNumber() ?? 1;
        var lastColumn = sheet.Row(headerRow).LastCellUsed()?.Address.ColumnNumber ?? 0;
        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var column = 1; column <= lastColumn; column++)
        {
            var text = sheet.Cell(headerRow, column).GetString().Trim();
            if (!string.IsNullOrWhiteSpace(text)) headers[NormalizeHeader(text)] = column;
        }
        foreach (var required in RequiredStructuredHeaders)
            if (!headers.ContainsKey(NormalizeHeader(required)))
                return ([], new("missing_header", $"Sheet “Đặt phòng” thiếu cột bắt buộc “{required}”."));

        var rows = new List<ParsedBookingRow>();
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? headerRow;
        for (var rowNumber = headerRow + 1; rowNumber <= lastRow; rowNumber++)
        {
            var room = GetText(sheet, rowNumber, headers, "Mã phòng");
            var name = GetText(sheet, rowNumber, headers, "Tên khách hàng");
            var phone = GetText(sheet, rowNumber, headers, "Số điện thoại");
            var checkInText = GetText(sheet, rowNumber, headers, "Ngày giờ checkin");
            var checkOutText = GetText(sheet, rowNumber, headers, "Ngày giờ checkout");
            if (string.IsNullOrWhiteSpace(room) && string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(phone) && string.IsNullOrWhiteSpace(checkInText) && string.IsNullOrWhiteSpace(checkOutText)) continue;

            if (!TryGetDateTime(sheet, rowNumber, headers, "Ngày giờ checkin", out var checkIn)) checkIn = DateTime.MinValue;
            if (!TryGetDateTime(sheet, rowNumber, headers, "Ngày giờ checkout", out var checkOut)) checkOut = DateTime.MinValue;
            var unitPrice = GetMoney(sheet, rowNumber, headers, "Đơn giá");
            var extra = GetMoney(sheet, rowNumber, headers, "Phụ phí");
            var total = GetMoney(sheet, rowNumber, headers, "Tổng doanh thu");
            var roomAmount = unitPrice;
            var discount = 0m;
            if (total > 0)
            {
                var computed = roomAmount + extra;
                if (computed > total) discount = computed - total;
                else if (total > computed) extra += total - computed;
            }

            rows.Add(new ParsedBookingRow(
                rowNumber,
                Clean(GetText(sheet, rowNumber, headers, "ID")),
                room.Trim(),
                name.Trim(),
                phone.Trim(),
                checkIn,
                checkOut,
                roomAmount,
                extra,
                discount,
                Clean(GetText(sheet, rowNumber, headers, "Nguồn")) ?? "",
                Clean(GetText(sheet, rowNumber, headers, "Ghi chú")),
                Clean(GetText(sheet, rowNumber, headers, "Hình thức phòng"))));
        }
        return (rows, null);
    }

    private static string GetText(IXLWorksheet sheet, int row, IReadOnlyDictionary<string, int> headers, string header)
    {
        if (!headers.TryGetValue(NormalizeHeader(header), out var column)) return string.Empty;
        var cell = sheet.Cell(row, column);
        if (cell.IsEmpty()) return string.Empty;
        if (cell.TryGetValue<double>(out var number) && NormalizeHeader(header) == NormalizeHeader("Số điện thoại"))
            return number.ToString("0", CultureInfo.InvariantCulture);
        return cell.GetFormattedString(CultureInfo.GetCultureInfo("vi-VN")).Trim();
    }

    private static bool TryGetDateTime(IXLWorksheet sheet, int row, IReadOnlyDictionary<string, int> headers, string header, out DateTime value)
    {
        value = default;
        if (!headers.TryGetValue(NormalizeHeader(header), out var column)) return false;
        var cell = sheet.Cell(row, column);
        if (cell.TryGetValue<DateTime>(out value)) { value = DateTime.SpecifyKind(value, DateTimeKind.Unspecified); return true; }
        var text = cell.GetString().Trim();
        var formats = new[] { "dd/MM/yyyy HH:mm", "d/M/yyyy H:mm", "dd/MM/yyyy H:mm", "M/d/yyyy h:mm tt", "M/d/yyyy H:mm", "yyyy-MM-dd HH:mm" };
        if (DateTime.TryParseExact(text, formats, CultureInfo.GetCultureInfo("vi-VN"), DateTimeStyles.AllowWhiteSpaces, out value) ||
            DateTime.TryParse(text, CultureInfo.GetCultureInfo("vi-VN"), DateTimeStyles.AllowWhiteSpaces, out value) ||
            DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out value))
        {
            value = DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
            return true;
        }
        return false;
    }

    private static decimal GetMoney(IXLWorksheet sheet, int row, IReadOnlyDictionary<string, int> headers, string header)
    {
        if (!headers.TryGetValue(NormalizeHeader(header), out var column)) return 0;
        var cell = sheet.Cell(row, column);
        if (cell.TryGetValue<decimal>(out var direct)) return direct;
        var raw = cell.GetString().Trim().ToLowerInvariant().Replace("đ", "").Replace("vnd", "").Replace(" ", "");
        if (raw.EndsWith("k", StringComparison.Ordinal))
        {
            raw = raw[..^1];
            if (decimal.TryParse(raw.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var thousands)) return thousands * 1000;
        }
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        return decimal.TryParse(digits, out var result) ? result : 0;
    }

    private static Room? FindRoom(IEnumerable<Room> rooms, string input)
    {
        var key = SearchKey(input);
        return rooms.FirstOrDefault(room => SearchKey(room.Code) == key || SearchKey(room.Name) == key)
            ?? rooms.FirstOrDefault(room => SearchKey(room.Name).Contains(key, StringComparison.Ordinal) || key.Contains(SearchKey(room.Name), StringComparison.Ordinal));
    }

    private static DateTimeOffset ToPropertyOffset(DateTime local, string timeZoneId)
    {
        if (local == DateTime.MinValue) return DateTimeOffset.MinValue;
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        return new DateTimeOffset(unspecified, zone.GetUtcOffset(unspecified));
    }

    private static string NormalizeLegacyPhone(string? raw)
    {
        var digits = CustomerService.NormalizePhone(raw);
        if (digits.Length == 9 && digits[0] is '3' or '5' or '7' or '8' or '9') digits = $"0{digits}";
        return digits;
    }

    private static string FormatDisplayPhone(string? raw, string normalized) =>
        string.IsNullOrWhiteSpace(raw) || raw.All(c => char.IsDigit(c) || char.IsWhiteSpace(c)) ? normalized : raw.Trim();

    private static string NormalizeHeader(string value) => SearchKey(value).Replace("-", string.Empty, StringComparison.Ordinal);

    private static string SearchKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c)) builder.Append(char.ToLowerInvariant(c));
        }
        return builder.ToString();
    }

    private static string CreateBookingCode() => $"BK-{DateTime.UtcNow:yyMMdd}-{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";

    private static string? BuildImportNote(ParsedBookingRow row)
    {
        var pieces = new List<string>();
        if (!string.IsNullOrWhiteSpace(row.ExternalId)) pieces.Add($"Mã cũ: {row.ExternalId}");
        if (!string.IsNullOrWhiteSpace(row.Note)) pieces.Add(row.Note.Trim());
        pieces.Add($"Import Excel dòng {row.ExcelRow}");
        return string.Join(" · ", pieces);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
