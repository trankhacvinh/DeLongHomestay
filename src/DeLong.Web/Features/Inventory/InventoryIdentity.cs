using System.Text;

namespace DeLong.Web.Features.Inventory;

internal static class InventoryIdentity
{
    internal static string Normalize(string value) => string.Join(' ', value.Normalize(NormalizationForm.FormC).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    internal static string NextCode(IEnumerable<string> existing)
    {
        var codes = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var i = 1; i < int.MaxValue; i++) {
            var code = $"MH{i:D6}";
            if (!codes.Contains(code)) return code;
        }
        throw new InvalidOperationException("Không còn mã mặt hàng khả dụng.");
    }
}
