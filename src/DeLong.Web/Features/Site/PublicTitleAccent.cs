using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.Html;

namespace DeLong.Web.Features.Site;

/// <summary>
/// Public headings: the last words of a CMS title are set in the italic accent style of the 2026-11 design.
/// The title text stays exactly as stored; only an &lt;em&gt; wrapper is added, so the inline editor
/// (which reads innerText) keeps saving the same plain text.
/// </summary>
public static class PublicTitleAccent
{
    private static readonly HtmlEncoder Encoder = HtmlEncoder.Create(UnicodeRanges.All);

    public static IHtmlContent Render(string? title, int accentWords = 2)
    {
        var text = (title ?? string.Empty).Trim();
        if (text.Length == 0) return HtmlString.Empty;
        var encoder = Encoder;
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        // Short titles read better without a split accent.
        if (words.Length < 3 || accentWords <= 0) return new HtmlString(encoder.Encode(text));
        var count = Math.Min(accentWords, words.Length - 1);
        var head = string.Join(' ', words[..^count]);
        var tail = string.Join(' ', words[^count..]);
        return new HtmlString($"{encoder.Encode(head)} <em>{encoder.Encode(tail)}</em>");
    }
}
