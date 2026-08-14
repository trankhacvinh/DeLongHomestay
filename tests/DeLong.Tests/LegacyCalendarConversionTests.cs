using ClosedXML.Excel;
using DeLong.Web.Features.Imports;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace DeLong.Tests;

public sealed class LegacyCalendarConversionTests
{
    [Fact]
    public async Task Colored_calendar_slot_is_converted_to_structured_draft()
    {
        using var sourceWorkbook = new XLWorkbook();
        var source = sourceWorkbook.Worksheets.Add("Lịch Book phòng");
        source.Cell("A7").Value = "Tên phòng";
        source.Cell("C7").Value = "Coco Blue #1\nCOCO-01";
        source.Cell("C8").Value = "10:30 - 13:30 (250k)";
        source.Cell("B9").Value = new DateTime(2026, 8, 20);
        source.Cell("C9").Value = "Check in 10h30";
        source.Cell("C9").Style.Fill.PatternType = XLFillPatternValues.Solid;
        source.Cell("C9").Style.Fill.BackgroundColor = XLColor.FromHtml("#F9CB9C");

        using var sourceStream = new MemoryStream();
        sourceWorkbook.SaveAs(sourceStream);
        var bytes = sourceStream.ToArray();
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "calendar.xlsx")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        };

        var service = new LegacyCalendarConversionService();
        var (result, error) = await service.ConvertAsync(file);

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal(1, result!.OccupiedSlots);

        using var converted = new XLWorkbook(new MemoryStream(result.FileBytes));
        var sheet = converted.Worksheet("Đặt phòng");
        Assert.Equal("Coco Blue #1", sheet.Cell("C2").GetString());
        Assert.Equal(string.Empty, sheet.Cell("E2").GetString());
        Assert.Equal(string.Empty, sheet.Cell("F2").GetString());
        Assert.Equal(new DateTime(2026, 8, 20, 10, 30, 0), sheet.Cell("J2").GetDateTime());
        Assert.Equal(new DateTime(2026, 8, 20, 13, 30, 0), sheet.Cell("K2").GetDateTime());
        Assert.Equal(250000d, sheet.Cell("L2").GetDouble());
        Assert.Contains("C9", sheet.Cell("O2").GetString());
        Assert.True(sheet.Cell("E2").Style.Fill.PatternType != XLFillPatternValues.None);
        Assert.True(sheet.Cell("F2").Style.Fill.PatternType != XLFillPatternValues.None);
    }
}
