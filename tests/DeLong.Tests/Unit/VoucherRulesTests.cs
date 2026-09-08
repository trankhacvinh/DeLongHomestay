using System.Net;
using DeLong.Web.Features.Vouchers;
using Xunit;

namespace DeLong.Tests.Unit;

public sealed class VoucherRulesTests
{
    [Theory]
    [InlineData(" welcome10 ", "WELCOME10")]
    [InlineData("Chao Mung-20", "CHAOMUNG-20")]
    [InlineData("  vip_100  ", "VIP_100")]
    public void NormalizeCode_is_case_insensitive_and_ignores_whitespace(string input, string expected) =>
        Assert.Equal(expected, VoucherService.NormalizeCode(input));

    [Fact]
    public void Email_template_renders_html_and_all_voucher_variables()
    {
        var data = new VoucherEmailTemplateData(
            "De Long", "VIP20", "Nguyễn An", 20m,
            new DateTime(2026, 9, 1, 7, 0, 0, DateTimeKind.Unspecified),
            new DateTime(2026, 9, 30, 17, 0, 0, DateTimeKind.Utc),
            "Khung giờ, qua đêm");

        var rendered = VoucherEmailTemplateRenderer.Render(
            "<strong>{{CustomerName}}</strong> · {{VoucherCode}} · {{DiscountPercent}} · {{PropertyName}} · {{EndsAt}} · {{AppliesTo}}",
            string.Empty,
            data,
            true);

        Assert.Contains("<strong>Nguyễn An</strong>", rendered);
        Assert.Contains("VIP20", rendered);
        Assert.Contains("20", rendered);
        Assert.Contains("De Long", rendered);
        Assert.Contains("Khung giờ, qua đêm", WebUtility.HtmlDecode(rendered));
        Assert.DoesNotContain("{{", rendered);
    }
}
