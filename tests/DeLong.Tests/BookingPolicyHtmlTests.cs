using System.Reflection;
using DeLong.Web.Features.PublicBooking;
using Xunit;

namespace DeLong.Tests;

public sealed class BookingPolicyHtmlTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Policy_html_keeps_editor_formatting_and_removes_unsafe_markup()
    {
        var normalize = typeof(BookingPolicyStore).GetMethod(
            "NormalizePolicyHtml",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(normalize);
        var html = Assert.IsType<string>(normalize.Invoke(null, ["<h2>Nội quy</h2><script>alert(1)</script><p><strong>Giữ gìn tài sản</strong></p><a href=\"javascript:alert(2)\">Xem</a>"]));

        Assert.Contains("<h2>Nội quy</h2>", html, StringComparison.Ordinal);
        Assert.Contains("<strong>Giữ gìn tài sản</strong>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Legacy_plain_text_policy_is_normalized_for_the_rich_editor()
    {
        var normalize = typeof(BookingPolicyStore).GetMethod(
            "NormalizePolicyHtml",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(normalize);
        var html = Assert.IsType<string>(normalize.Invoke(null, ["Dòng một\nDòng hai"]));

        Assert.Equal("<p>Dòng một<br>Dòng hai</p>", html);
    }
}
