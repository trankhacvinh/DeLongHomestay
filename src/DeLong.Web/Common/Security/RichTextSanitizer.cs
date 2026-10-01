using AngleSharp.Html.Parser;
using Ganss.Xss;

namespace DeLong.Web.Common.Security;

public static class RichTextSanitizer
{
    public static string Sanitize(string html)
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Add("iframe");
        foreach (var attribute in new[] { "class", "allowfullscreen", "frameborder", "referrerpolicy" })
            sanitizer.AllowedAttributes.Add(attribute);
        sanitizer.AllowedSchemes.Add("tel");
        var document = new HtmlParser().ParseDocument(sanitizer.Sanitize(html));
        foreach (var frame in document.QuerySelectorAll("iframe"))
        {
            var raw = frame.GetAttribute("src");
            if (!Uri.TryCreate(raw, UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps ||
                url.Host is not ("www.youtube.com" or "youtube.com" or "www.youtube-nocookie.com" or "youtube-nocookie.com") ||
                !System.Text.RegularExpressions.Regex.IsMatch(url.AbsolutePath, "^/embed/[A-Za-z0-9_-]{11}$"))
            {
                frame.Remove();
                continue;
            }
            frame.SetAttribute("src", $"https://www.youtube-nocookie.com{url.AbsolutePath}");
            frame.SetAttribute("allowfullscreen", "");
            frame.SetAttribute("referrerpolicy", "strict-origin-when-cross-origin");
        }
        return document.Body?.InnerHtml ?? string.Empty;
    }
}
