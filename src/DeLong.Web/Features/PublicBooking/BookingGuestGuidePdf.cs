using System.Net;
using System.Text.RegularExpressions;
using SkiaSharp;

namespace DeLong.Web.Features.PublicBooking;

public static partial class BookingGuestGuidePdf
{
    private const float PageWidth = 595f;
    private const float PageHeight = 842f;
    private const float Margin = 48f;

    public static byte[] Create(PublicBookingGuideDto guide)
    {
        using var stream = new MemoryStream();
        using var document = SKDocument.CreatePdf(stream);
        using var regularTypeface = SKTypeface.FromFamilyName("Arial") ?? SKTypeface.Default;
        using var boldTypeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) ?? SKTypeface.Default;
        using var regularFont = new SKFont(regularTypeface, 12.5f);
        using var mutedFont = new SKFont(regularTypeface, 10.5f);
        using var headingFont = new SKFont(boldTypeface, 22f);
        using var sectionFont = new SKFont(boldTypeface, 14f);
        using var regular = new SKPaint { IsAntialias = true, Color = SKColors.Black };
        using var muted = new SKPaint { IsAntialias = true, Color = SKColor.Parse("#61716E") };
        using var heading = new SKPaint { IsAntialias = true, Color = SKColor.Parse("#143C3A") };
        using var section = new SKPaint { IsAntialias = true, Color = SKColor.Parse("#176861") };

        SKCanvas? canvas = null;
        var y = 0f;
        void NewPage()
        {
            if (canvas is not null) document.EndPage();
            canvas = document.BeginPage(PageWidth, PageHeight);
            y = Margin;
            canvas.DrawText("DE LONG HOMESTAY", Margin, y, SKTextAlign.Left, mutedFont, muted);
            y += 32;
        }

        void EnsureSpace(float needed)
        {
            if (canvas is null || y + needed > PageHeight - Margin) NewPage();
        }

        void DrawWrapped(string text, SKFont font, SKPaint paint, float lineHeight, float before = 0, float after = 0)
        {
            var lines = Wrap(text, font, paint, PageWidth - Margin * 2);
            EnsureSpace(before + lines.Count * lineHeight + after);
            y += before;
            foreach (var line in lines)
            {
                if (y + lineHeight > PageHeight - Margin) NewPage();
                canvas!.DrawText(line, Margin, y, SKTextAlign.Left, font, paint);
                y += lineHeight;
            }
            y += after;
        }

        NewPage();
        DrawWrapped("Hướng dẫn sử dụng phòng", headingFont, heading, 29, after: 8);
        DrawWrapped($"Mã đặt phòng: {guide.Code}", mutedFont, muted, 16);
        DrawWrapped($"Phòng: {guide.RoomName}", mutedFont, muted, 16, after: 18);
        canvas!.DrawLine(Margin, y, PageWidth - Margin, y, new SKPaint { Color = SKColor.Parse("#D6E2DF"), StrokeWidth = 1 });
        y += 24;

        var paragraphs = HtmlToParagraphs(guide.GuestGuideHtml);
        if (paragraphs.Count == 0)
        {
            DrawWrapped("Phòng chưa có nội dung hướng dẫn. Vui lòng liên hệ cơ sở để được hỗ trợ.", regularFont, regular, 19);
        }
        else
        {
            foreach (var paragraph in paragraphs)
            {
                var isHeading = paragraph.StartsWith("## ", StringComparison.Ordinal);
                DrawWrapped(isHeading ? paragraph[3..] : paragraph, isHeading ? sectionFont : regularFont, isHeading ? section : regular, isHeading ? 22 : 19, before: isHeading ? 10 : 0, after: 7);
            }
        }

        EnsureSpace(42);
        y += 18;
        DrawWrapped("Tài liệu dành riêng cho khách có booking còn hiệu lực. Không chia sẻ mã đặt phòng cho người khác.", mutedFont, muted, 16);
        if (canvas is not null) document.EndPage();
        document.Close();
        return stream.ToArray();
    }

    private static IReadOnlyList<string> HtmlToParagraphs(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return [];
        var value = HeadingRegex().Replace(html, match => $"\n## {match.Groups[1].Value}\n");
        value = ListItemRegex().Replace(value, match => $"\n• {match.Groups[1].Value}");
        value = BlockEndRegex().Replace(value, "\n");
        value = BreakRegex().Replace(value, "\n");
        value = TagRegex().Replace(value, string.Empty);
        value = WebUtility.HtmlDecode(value).Replace('\u00A0', ' ');
        return value.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(x => WhitespaceRegex().Replace(x, " ").Trim())
            .Where(x => x.Length > 0)
            .ToList();
    }

    private static IReadOnlyList<string> Wrap(string text, SKFont font, SKPaint paint, float maxWidth)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return [string.Empty];
        var lines = new List<string>();
        var current = words[0];
        foreach (var word in words.Skip(1))
        {
            var candidate = $"{current} {word}";
            if (font.MeasureText(candidate, paint) <= maxWidth) current = candidate;
            else { lines.Add(current); current = word; }
        }
        lines.Add(current);
        return lines;
    }

    [GeneratedRegex("<h[1-3][^>]*>(.*?)</h[1-3]>", RegexOptions.IgnoreCase | RegexOptions.Singleline)] private static partial Regex HeadingRegex();
    [GeneratedRegex("<li[^>]*>(.*?)</li>", RegexOptions.IgnoreCase | RegexOptions.Singleline)] private static partial Regex ListItemRegex();
    [GeneratedRegex("</(?:p|div|blockquote|ul|ol)>", RegexOptions.IgnoreCase)] private static partial Regex BlockEndRegex();
    [GeneratedRegex("<br\\s*/?>", RegexOptions.IgnoreCase)] private static partial Regex BreakRegex();
    [GeneratedRegex("<[^>]+>", RegexOptions.Singleline)] private static partial Regex TagRegex();
    [GeneratedRegex("\\s+")] private static partial Regex WhitespaceRegex();
}
