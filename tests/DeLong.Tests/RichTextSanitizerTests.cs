using DeLong.Web.Common.Security;
using Xunit;

namespace DeLong.Tests;

public sealed class RichTextSanitizerTests
{
    [Fact]
    public void Keeps_links_and_youtube_embed_when_saved()
    {
        var result = RichTextSanitizer.Sanitize("<p><a href=\"https://example.com/booking\">Đặt phòng</a></p><iframe class=\"ql-video\" src=\"https://www.youtube.com/embed/dQw4w9WgXcQ\"></iframe>");
        Assert.Contains("href=\"https://example.com/booking\"", result);
        Assert.Contains("https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ", result);
        Assert.Contains("allowfullscreen", result);
    }

    [Theory]
    [InlineData("https://evil.example/embed/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com.evil.example/embed/dQw4w9WgXcQ")]
    [InlineData("javascript:alert(1)")]
    public void Removes_untrusted_frames(string url)
    {
        var result = RichTextSanitizer.Sanitize($"<p>Nội dung</p><iframe src=\"{url}\"></iframe>");
        Assert.DoesNotContain("iframe", result);
        Assert.Contains("Nội dung", result);
    }
}
