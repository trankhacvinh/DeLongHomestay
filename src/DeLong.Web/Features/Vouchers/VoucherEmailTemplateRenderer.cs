using System.Globalization;
using System.Net;
using DeLong.Web.Features.Notifications;

namespace DeLong.Web.Features.Vouchers;

public sealed record VoucherEmailTemplateData(
    string PropertyName,
    string VoucherCode,
    string CustomerName,
    decimal DiscountPercent,
    DateTime StartsAtLocal,
    DateTime EndsAtLocal,
    string AppliesTo);

public static class VoucherEmailTemplateRenderer
{
    public const string DefaultSubject = "[{{PropertyName}}] Voucher ưu đãi {{VoucherCode}}";
    public const string DefaultBody = """
        <p>Xin chào <strong>{{CustomerName}}</strong>,</p>
        <p>{{PropertyName}} gửi bạn voucher <strong>{{VoucherCode}}</strong> giảm <strong>{{DiscountPercent}}%</strong> tiền phòng.</p>
        <p><strong>Áp dụng:</strong> {{AppliesTo}}<br><strong>Hiệu lực:</strong> {{StartsAt}} - {{EndsAt}}</p>
        <p>Nhập mã trước khi thanh toán trên trang đặt phòng. Voucher tùy thuộc số lượt còn lại tại thời điểm gửi booking.</p>
        """;

    public static string Render(string? template, string fallback, VoucherEmailTemplateData data, bool html)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PropertyName"] = data.PropertyName,
            ["VoucherCode"] = data.VoucherCode,
            ["CustomerName"] = data.CustomerName,
            ["DiscountPercent"] = data.DiscountPercent.ToString("0.##", CultureInfo.InvariantCulture),
            ["StartsAt"] = data.StartsAtLocal.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
            ["EndsAt"] = data.EndsAtLocal.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
            ["AppliesTo"] = data.AppliesTo
        };
        var output = string.IsNullOrWhiteSpace(template) ? fallback : template.Trim();
        if (html)
        {
            output = NotificationEmailTemplateRenderer.SanitizeTemplate(output);
            values = values.ToDictionary(x => x.Key, x => WebUtility.HtmlEncode(x.Value), StringComparer.Ordinal);
        }
        foreach (var (key, value) in values)
            output = output.Replace("{{" + key + "}}", value, StringComparison.Ordinal);
        return output;
    }
}
