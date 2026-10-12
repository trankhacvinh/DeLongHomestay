using System.Reflection;
using DeLong.Web.Features.PublicBooking;
using Xunit;

namespace DeLong.Tests;

public sealed class BookingPolicyHtmlTests
{
    [Fact]
    public void Policy_images_keep_safe_sources_and_drop_executable_attributes()
    {
        var normalize = typeof(BookingPolicyStore).GetMethod("NormalizePolicyHtml", BindingFlags.NonPublic | BindingFlags.Static)!;
        var html = Assert.IsType<string>(normalize.Invoke(null, ["<p>Nội quy</p><img src='/uploads/site/rules.webp' alt='Nội quy' onerror='alert(1)'><img src='javascript:alert(2)'>"]));
        Assert.Contains("/uploads/site/rules.webp", html);
        Assert.Contains("alt=\"Nội quy\"", html);
        Assert.DoesNotContain("onerror", html);
        Assert.DoesNotContain("javascript:", html);
    }

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
