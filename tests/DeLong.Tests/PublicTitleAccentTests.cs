using DeLong.Web.Features.Site;
using Xunit;

namespace DeLong.Tests;

public sealed class PublicTitleAccentTests
{
    private static string Render(string? title, int words = 2)
    {
        using var writer = new StringWriter();
        PublicTitleAccent.Render(title, words).WriteTo(writer, System.Text.Encodings.Web.HtmlEncoder.Default);
        return writer.ToString();
    }

    [Fact]
    public void Wraps_the_last_words_in_em_and_keeps_vietnamese_text_readable()
    {
        Assert.Equal("Xem phòng và khung giờ <em>còn trống</em>", Render("Xem phòng và khung giờ còn trống"));
    }

    [Fact]
    public void Encodes_markup_in_cms_titles()
    {
        Assert.Equal("&lt;b&gt;Một vài lựa <em>chọn &lt;/b&gt;</em>", Render("<b>Một vài lựa chọn </b>"));
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("Câu hỏi", "Câu hỏi")]
    public void Short_or_empty_titles_have_no_accent(string? title, string expected)
    {
        Assert.Equal(expected, Render(title));
    }
}
