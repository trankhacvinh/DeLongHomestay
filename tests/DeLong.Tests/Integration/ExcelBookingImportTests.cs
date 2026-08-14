using ClosedXML.Excel;
using DeLong.Web.Common.Auditing;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Imports;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeLong.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class ExcelBookingImportTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Structured_excel_is_previewed_imported_and_then_detected_as_duplicate()
    {
        var connectionString = Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var property = new Property
        {
            Code = $"IMP-{suffix}",
            Name = $"Import Test {suffix}",
            TimeZoneId = "Asia/Ho_Chi_Minh",
            IsActive = true
        };
        var room = new Room
        {
            Property = property,
            Code = $"R-{suffix}",
            Name = $"Import Room {suffix}",
            Capacity = 2,
            IsActive = true,
            IsPublished = true
        };
        db.Properties.Add(property);
        db.Rooms.Add(room);
        await db.SaveChangesAsync();

        var bytes = BuildWorkbook(room.Code, new DateTime(2030, 8, 20, 14, 0, 0), new DateTime(2030, 8, 20, 17, 0, 0));
        var service = new ExcelBookingImportService(db, new AuditService(db));

        var (preview, previewError) = await service.PreviewAsync(property.Id, CreateFile(bytes));
        Assert.Null(previewError);
        Assert.NotNull(preview);
        Assert.Equal("structured-bookings", preview!.Format);
        Assert.Equal(1, preview.TotalRows);
        Assert.Equal(1, preview.ReadyRows);
        Assert.Equal(0, preview.ErrorRows);
        Assert.Equal("0987654321", preview.Rows[0].CustomerPhone);

        var (result, importError) = await service.ImportAsync(property.Id, CreateFile(bytes), null);
        Assert.Null(importError);
        Assert.NotNull(result);
        Assert.Equal(1, result!.ImportedRows);

        var imported = await db.Bookings.AsNoTracking().Include(x => x.Customer)
            .SingleAsync(x => x.PropertyId == property.Id);
        Assert.Equal(room.Id, imported.RoomId);
        Assert.Equal(BookingStatus.Confirmed, imported.Status);
        Assert.Equal(BookingType.TimeSlot, imported.Type);
        Assert.Equal(250000m, imported.RoomAmount);
        Assert.Equal("0987654321", imported.Customer.NormalizedPhone);
        Assert.Contains("Excel", imported.Source);

        var (duplicatePreview, duplicateError) = await service.PreviewAsync(property.Id, CreateFile(bytes));
        Assert.Null(duplicateError);
        Assert.NotNull(duplicatePreview);
        Assert.Equal(0, duplicatePreview!.ReadyRows);
        Assert.Equal(1, duplicatePreview.DuplicateRows);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Multi_day_import_requires_nightly_rate_and_keeps_snapshot()
    {
        var connectionString = Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var property = new Property { Code = $"IMPN-{suffix}", Name = "Import Nightly", TimeZoneId = "Asia/Ho_Chi_Minh", IsActive = true };
        var room = new Room { Property = property, Code = $"N-{suffix}", Name = $"Nightly Room {suffix}", Capacity = 2, IsActive = true, IsPublished = true };
        var nightly = new RoomRate
        {
            Room = room,
            Name = "Lưu trú theo đêm",
            StartTime = new TimeOnly(14, 0),
            EndTime = new TimeOnly(12, 0),
            Type = RoomRateType.Nightly,
            Price = 500000,
            IsActive = true
        };
        db.AddRange(property, room, nightly);
        await db.SaveChangesAsync();

        var bytes = BuildWorkbook(room.Name, new DateTime(2030, 8, 20, 14, 0, 0), new DateTime(2030, 8, 23, 12, 0, 0), 1500000);
        var service = new ExcelBookingImportService(db, new AuditService(db));
        var (preview, error) = await service.PreviewAsync(property.Id, CreateFile(bytes));
        Assert.Null(error);
        Assert.Equal(1, preview!.ReadyRows);
        Assert.Contains("3 đêm", preview.Rows[0].Message);

        var (result, importError) = await service.ImportAsync(property.Id, CreateFile(bytes), null);
        Assert.Null(importError);
        Assert.Equal(1, result!.ImportedRows);

        var booking = await db.Bookings.AsNoTracking().SingleAsync(x => x.PropertyId == property.Id);
        Assert.Equal(BookingType.MultiDay, booking.Type);
        Assert.Equal(nightly.Id, booking.RoomRateId);
        Assert.Equal(3, booking.NightCount);
        Assert.Equal(500000m, booking.UnitPrice);
        Assert.Equal(1500000m, booking.RoomAmount);
    }

    private static byte[] BuildWorkbook(string room, DateTime checkIn, DateTime checkOut, decimal price = 250000)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Đặt phòng");
        var headers = new[] { "ID", "Tên cơ sở", "Mã phòng", "Ngày tạo", "Tên khách hàng", "Số điện thoại", "Nguồn", "Nhân viên", "Hình thức phòng", "Ngày giờ checkin", "Ngày giờ checkout", "Đơn giá", "Phụ phí", "Tổng doanh thu", "Ghi chú" };
        for (var i = 0; i < headers.Length; i++) sheet.Cell(1, i + 1).Value = headers[i];
        sheet.Cell(2, 1).Value = "LEGACY-1";
        sheet.Cell(2, 2).Value = "De Long Homestay";
        sheet.Cell(2, 3).Value = room;
        sheet.Cell(2, 4).Value = new DateTime(2030, 8, 1);
        sheet.Cell(2, 5).Value = "Khách Import";
        sheet.Cell(2, 6).Value = 987654321d; // Legacy Excel frequently drops the leading 0.
        sheet.Cell(2, 7).Value = "Zalo";
        sheet.Cell(2, 9).Value = (checkOut.Date - checkIn.Date).Days >= 2 ? "Cả ngày" : "Combo";
        sheet.Cell(2, 10).Value = checkIn;
        sheet.Cell(2, 11).Value = checkOut;
        sheet.Cell(2, 12).Value = price;
        sheet.Cell(2, 13).Value = 0;
        sheet.Cell(2, 14).Value = price;
        sheet.Cell(2, 15).Value = "Import test";
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static IFormFile CreateFile(byte[] bytes)
    {
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", "bookings.xlsx")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        };
    }
}
