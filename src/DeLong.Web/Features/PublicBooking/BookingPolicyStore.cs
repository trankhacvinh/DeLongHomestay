using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using DeLong.Web.Common.Operations;
using Ganss.Xss;

namespace DeLong.Web.Features.PublicBooking;

public sealed record BookingPolicyDto(
    int PublicMaxNights,
    int PublicMaxConsecutiveSlotDays,
    IReadOnlyList<MultiSlotDiscountTierDto> MultiSlotDiscountTiers,
    int IncludedGuests,
    decimal ExtraGuestFeePerPerson,
    bool RequireIdentityDocuments,
    string PolicyTitle,
    string PolicyText,
    int PolicyVersion,
    int PublicHoldMinutes,
    bool IdentityEncryptionConfigured);

public sealed record MultiSlotDiscountTierDto(int MinimumSlots, decimal DiscountPercent);

public sealed class UpdateBookingPolicyRequest
{
    public int PublicMaxNights { get; init; } = 3;
    public int PublicMaxConsecutiveSlotDays { get; init; } = 3;
    public IReadOnlyList<MultiSlotDiscountTierDto> MultiSlotDiscountTiers { get; init; } = [];
    public int IncludedGuests { get; init; } = 2;
    public decimal ExtraGuestFeePerPerson { get; init; } = 100_000m;
    public bool RequireIdentityDocuments { get; init; } = true;
    public string? PolicyTitle { get; init; }
    public string? PolicyText { get; init; }
}

public sealed class BookingPolicyStore(StoragePaths paths, IConfiguration configuration)
{
    public const int HoldMinutes = 0;
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> Gates = new();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly HtmlSanitizer PolicySanitizer = CreatePolicySanitizer();
    private readonly string root = Path.Combine(paths.DataRoot, "booking-settings");

    public async Task<BookingPolicyDto> GetAsync(Guid propertyId, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(root);
        var path = PathFor(propertyId);
        if (!File.Exists(path)) return Defaults();

        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var stored = await JsonSerializer.DeserializeAsync<StoredBookingPolicy>(stream, JsonOptions, cancellationToken);
            return Normalize(stored);
        }
        catch (JsonException)
        {
            return Defaults();
        }
    }

    public async Task<(BookingPolicyDto? Policy, string? Error)> SaveAsync(Guid propertyId, UpdateBookingPolicyRequest request, CancellationToken cancellationToken = default)
    {
        if (request.PublicMaxNights is < 1 or > 14) return (null, "Số đêm tối đa cho khách online phải từ 1 đến 14.");
        if (request.PublicMaxConsecutiveSlotDays is < 1 or > 14) return (null, "Số ngày chọn khung liên tiếp phải từ 1 đến 14.");
        var tiers = NormalizeTiers(request.MultiSlotDiscountTiers);
        if (tiers is null) return (null, "Mức giảm nhiều khung phải có số khung từ 2 trở lên, phần trăm từ 0 đến 100 và không trùng số khung.");
        if (request.IncludedGuests is < 1 or > 50) return (null, "Số khách đã gồm trong giá phải từ 1 đến 50.");
        if (request.ExtraGuestFeePerPerson is < 0 or > 10_000_000m) return (null, "Phụ thu mỗi khách không hợp lệ.");
        if (!IdentityEncryptionConfigured())
            return (null, "Kho CCCD chưa thể tạo hoặc đọc khóa mã hóa trong DataRoot. Hãy kiểm tra quyền ghi DataRoot/security hoặc khôi phục DataRoot đầy đủ từ bản sao lưu.");

        var title = Clean(request.PolicyTitle, 200) ?? "Nội quy & Chính sách";
        if ((request.PolicyText?.Length ?? 0) > 20_000) return (null, "Nội dung Nội quy & Chính sách tối đa 20.000 ký tự.");
        var text = NormalizePolicyHtml(request.PolicyText);
        var gate = Gates.GetOrAdd(propertyId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            var current = await GetAsync(propertyId, cancellationToken);
            var policyChanged = !string.Equals(current.PolicyTitle, title, StringComparison.Ordinal) ||
                                !string.Equals(current.PolicyText, text, StringComparison.Ordinal);
            var version = policyChanged ? checked(Math.Max(1, current.PolicyVersion) + 1) : Math.Max(1, current.PolicyVersion);
            var stored = new StoredBookingPolicy
            {
                PublicMaxNights = request.PublicMaxNights,
                PublicMaxConsecutiveSlotDays = request.PublicMaxConsecutiveSlotDays,
                MultiSlotDiscountTiers = tiers,
                IncludedGuests = request.IncludedGuests,
                ExtraGuestFeePerPerson = request.ExtraGuestFeePerPerson,
                RequireIdentityDocuments = true,
                PolicyTitle = title,
                PolicyText = text,
                PolicyVersion = version
            };

            Directory.CreateDirectory(root);
            var path = PathFor(propertyId);
            var temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                await using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
                    await JsonSerializer.SerializeAsync(stream, stored, JsonOptions, cancellationToken);
                File.Move(temp, path, true);
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }

            return (Normalize(stored), null);
        }
        finally
        {
            gate.Release();
        }
    }

    private BookingPolicyDto Defaults()
    {
        var encryptionReady = IdentityEncryptionConfigured();
        return new BookingPolicyDto(
            3,
            3,
            [],
            2,
            100_000m,
            true,
            "Nội quy & Chính sách",
            NormalizePolicyHtml(DefaultPolicyText),
            1,
            HoldMinutes,
            encryptionReady);
    }

    private BookingPolicyDto Normalize(StoredBookingPolicy? stored)
    {
        if (stored is null) return Defaults();
        var encryptionReady = IdentityEncryptionConfigured();
        return new BookingPolicyDto(
            Math.Clamp(stored.PublicMaxNights <= 0 ? 3 : stored.PublicMaxNights, 1, 14),
            Math.Clamp(stored.PublicMaxConsecutiveSlotDays <= 0 ? 3 : stored.PublicMaxConsecutiveSlotDays, 1, 14),
            NormalizeTiers(stored.MultiSlotDiscountTiers) ?? [],
            Math.Clamp(stored.IncludedGuests <= 0 ? 2 : stored.IncludedGuests, 1, 50),
            Math.Clamp(stored.ExtraGuestFeePerPerson, 0m, 10_000_000m),
            true,
            Clean(stored.PolicyTitle, 200) ?? "Nội quy & Chính sách",
            NormalizePolicyHtml(stored.PolicyText),
            Math.Max(1, stored.PolicyVersion),
            HoldMinutes,
            encryptionReady);
    }

    private bool IdentityEncryptionConfigured() =>
        new IdentityDocumentStorage(paths, configuration).IsConfigured;

    private string PathFor(Guid propertyId) => Path.Combine(root, propertyId.ToString("N") + ".json");

    private static IReadOnlyList<MultiSlotDiscountTierDto>? NormalizeTiers(IReadOnlyList<MultiSlotDiscountTierDto>? source)
    {
        if (source is null || source.Count == 0) return [];
        if (source.Any(x => x.MinimumSlots < 2 || x.MinimumSlots > 100 || x.DiscountPercent < 0 || x.DiscountPercent > 100) ||
            source.GroupBy(x => x.MinimumSlots).Any(x => x.Count() > 1)) return null;
        return source.OrderBy(x => x.MinimumSlots)
            .Select(x => new MultiSlotDiscountTierDto(x.MinimumSlots, decimal.Round(x.DiscountPercent, 2)))
            .ToList();
    }

    private static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var clean = value.Trim();
        return clean.Length <= maxLength ? clean : clean[..maxLength];
    }

    internal static string NormalizePolicyHtml(string? value)
    {
        var source = Clean(value, 20_000) ?? DefaultPolicyText;
        if (!source.Contains('<', StringComparison.Ordinal))
        {
            var encoded = WebUtility.HtmlEncode(source)
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace("\r", "\n", StringComparison.Ordinal)
                .Replace("\n", "<br>", StringComparison.Ordinal);
            source = $"<p>{encoded}</p>";
        }

        var sanitized = PolicySanitizer.Sanitize(source).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? $"<p>{WebUtility.HtmlEncode(DefaultPolicyText)}</p>" : sanitized;
    }

    private static HtmlSanitizer CreatePolicySanitizer()
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        foreach (var tag in new[] { "p", "br", "strong", "b", "em", "i", "u", "h2", "h3", "ul", "ol", "li", "a", "blockquote" })
            sanitizer.AllowedTags.Add(tag);
        sanitizer.AllowedAttributes.Clear();
        foreach (var attribute in new[] { "href", "target", "rel", "title" })
            sanitizer.AllowedAttributes.Add(attribute);
        sanitizer.AllowedSchemes.Clear();
        foreach (var scheme in new[] { "http", "https", "mailto", "tel" }) sanitizer.AllowedSchemes.Add(scheme);
        return sanitizer;
    }

    private const string DefaultPolicyText = "Khách vui lòng cung cấp thông tin đặt phòng chính xác, giữ gìn tài sản và tuân thủ hướng dẫn nhận/trả phòng của cơ sở. Vui lòng liên hệ cơ sở nếu cần thay đổi giờ hoặc số lượng khách.";

    private sealed class StoredBookingPolicy
    {
        public int PublicMaxNights { get; init; } = 3;
        public int PublicMaxConsecutiveSlotDays { get; init; } = 3;
        public IReadOnlyList<MultiSlotDiscountTierDto> MultiSlotDiscountTiers { get; init; } = [];
        public int IncludedGuests { get; init; } = 2;
        public decimal ExtraGuestFeePerPerson { get; init; } = 100_000m;
        public bool RequireIdentityDocuments { get; init; } = true;
        public string? PolicyTitle { get; init; }
        public string? PolicyText { get; init; }
        public int PolicyVersion { get; init; } = 1;
    }
}
