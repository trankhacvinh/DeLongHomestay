using System.Text.Json;
using System.Text.RegularExpressions;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Site;

public sealed class SavePublicNavigationItemRequest
{
    public string? Id { get; init; }
    public string? Label { get; init; }
    public string? Url { get; init; }
    public bool IsVisible { get; init; } = true;
    public bool OpenInNewTab { get; init; }
}

public sealed class SavePublicShellSettingsRequest
{
    // Legacy fields stay supported so an older visual editor/admin client does not break.
    public string? HomeLabel { get; init; }
    public string? RoomsLabel { get; init; }
    public string? BranchesLabel { get; init; }
    public string? BookingLabel { get; init; }
    public string? LookupLabel { get; init; }
    public string? HeaderCtaText { get; init; }
    public string? HeaderCtaUrl { get; init; }
    public IReadOnlyList<string>? NavigationOrder { get; init; }
    public IReadOnlyList<SavePublicNavigationItemRequest>? NavigationItems { get; init; }
    public bool ShowHome { get; init; } = true;
    public bool ShowRooms { get; init; } = true;
    public bool ShowBranches { get; init; } = true;
    public string? FooterIntro { get; init; }
    public string? FooterBookingText { get; init; }
    public string? FooterBookingUrl { get; init; }
    public string? FooterExploreTitle { get; init; }
    public string? FooterBranchesTitle { get; init; }
    public string? FooterContactTitle { get; init; }
    public string? FooterBottomText { get; init; }
    public bool ShowFooterContact { get; init; } = true;
}

public sealed record PublicNavigationItemDto(
    string Id,
    string Label,
    string Url,
    bool IsVisible,
    bool OpenInNewTab,
    bool IsSystem);

public sealed record PublicShellSettingsDto(
    string HomeLabel,
    string RoomsLabel,
    string BranchesLabel,
    string BookingLabel,
    string LookupLabel,
    string HeaderCtaText,
    string HeaderCtaUrl,
    IReadOnlyList<string> NavigationOrder,
    IReadOnlyList<PublicNavigationItemDto> NavigationItems,
    bool ShowHome,
    bool ShowRooms,
    bool ShowBranches,
    string FooterIntro,
    string FooterBookingText,
    string FooterBookingUrl,
    string FooterExploreTitle,
    string FooterBranchesTitle,
    string FooterContactTitle,
    string FooterBottomText,
    bool ShowFooterContact);

public static class PublicShellSettingsStore
{
    public const string MetadataSectionType = "__PublicShell";
    public static readonly IReadOnlyList<string> DefaultNavigationOrder = ["home", "rooms", "branches", "booking", "lookup"];

    private static readonly HashSet<string> SystemNavigationKeys = new(DefaultNavigationOrder, StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> SystemUrlTokens = new(["@home", "@rooms", "@branches", "@booking", "@lookup"], StringComparer.OrdinalIgnoreCase);
    private static readonly Regex NavigationIdRegex = new("^[a-zA-Z0-9_-]{1,64}$", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed class NavigationItemPayload
    {
        public string? Id { get; init; }
        public string? Label { get; init; }
        public string? Url { get; init; }
        public bool? IsVisible { get; init; }
        public bool? OpenInNewTab { get; init; }
    }

    private sealed class Payload
    {
        public string? HomeLabel { get; init; }
        public string? RoomsLabel { get; init; }
        public string? BranchesLabel { get; init; }
        public string? BookingLabel { get; init; }
        public string? LookupLabel { get; init; }
        public string? HeaderCtaText { get; init; }
        public string? HeaderCtaUrl { get; init; }
        public IReadOnlyList<string>? NavigationOrder { get; init; }
        public IReadOnlyList<NavigationItemPayload>? NavigationItems { get; init; }
        public bool? ShowHome { get; init; }
        public bool? ShowRooms { get; init; }
        public bool? ShowBranches { get; init; }
        public string? FooterIntro { get; init; }
        public string? FooterBookingText { get; init; }
        public string? FooterBookingUrl { get; init; }
        public string? FooterExploreTitle { get; init; }
        public string? FooterBranchesTitle { get; init; }
        public string? FooterContactTitle { get; init; }
        public string? FooterBottomText { get; init; }
        public bool? ShowFooterContact { get; init; }
    }

    public static async Task<PublicShellSettingsDto> ReadAsync(AppDbContext db, Guid? propertyId, CancellationToken ct = default)
    {
        var json = await db.Set<HomeSection>().AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.Type == MetadataSectionType)
            .Select(x => x.ContentJson)
            .SingleOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(json)) return Defaults();
        try
        {
            return Normalize(JsonSerializer.Deserialize<Payload>(json, JsonOptions));
        }
        catch (JsonException)
        {
            return Defaults();
        }
    }

    public static async Task<(PublicShellSettingsDto? Settings, string? Error)> SaveAsync(
        AppDbContext db,
        Guid? propertyId,
        SavePublicShellSettingsRequest request,
        CancellationToken ct = default)
    {
        var validation = Validate(request);
        if (validation is not null) return (null, validation);

        var navigationItems = request.NavigationItems is null
            ? BuildLegacyItems(request)
            : request.NavigationItems.Select(x => new NavigationItemPayload
            {
                Id = NormalizeId(x.Id),
                Label = CleanOrNull(x.Label),
                Url = CleanOrNull(x.Url),
                IsVisible = x.IsVisible,
                OpenInNewTab = x.OpenInNewTab
            }).ToList();

        var normalizedItems = NormalizeItems(navigationItems);
        var payload = new Payload
        {
            HomeLabel = SystemLabel(normalizedItems, "home", request.HomeLabel),
            RoomsLabel = SystemLabel(normalizedItems, "rooms", request.RoomsLabel),
            BranchesLabel = SystemLabel(normalizedItems, "branches", request.BranchesLabel),
            BookingLabel = SystemLabel(normalizedItems, "booking", request.BookingLabel),
            LookupLabel = SystemLabel(normalizedItems, "lookup", request.LookupLabel),
            HeaderCtaText = CleanOrNull(request.HeaderCtaText),
            HeaderCtaUrl = CleanOrNull(request.HeaderCtaUrl),
            NavigationOrder = normalizedItems.Select(x => x.Id).ToList(),
            NavigationItems = normalizedItems.Select(x => new NavigationItemPayload
            {
                Id = x.Id,
                Label = x.Label,
                Url = x.Url,
                IsVisible = x.IsVisible,
                OpenInNewTab = x.OpenInNewTab
            }).ToList(),
            ShowHome = SystemVisible(normalizedItems, "home"),
            ShowRooms = SystemVisible(normalizedItems, "rooms"),
            ShowBranches = SystemVisible(normalizedItems, "branches"),
            FooterIntro = CleanOrNull(request.FooterIntro),
            FooterBookingText = CleanOrNull(request.FooterBookingText),
            FooterBookingUrl = CleanOrNull(request.FooterBookingUrl),
            FooterExploreTitle = CleanOrNull(request.FooterExploreTitle),
            FooterBranchesTitle = CleanOrNull(request.FooterBranchesTitle),
            FooterContactTitle = CleanOrNull(request.FooterContactTitle),
            FooterBottomText = CleanOrNull(request.FooterBottomText),
            ShowFooterContact = request.ShowFooterContact
        };

        var row = await db.Set<HomeSection>()
            .SingleOrDefaultAsync(x => x.PropertyId == propertyId && x.Type == MetadataSectionType, ct);
        if (row is null)
        {
            row = new HomeSection
            {
                PropertyId = propertyId,
                Type = MetadataSectionType,
                Name = "Cấu hình Header & Footer",
                Variant = "metadata",
                ContentJson = JsonSerializer.Serialize(payload, JsonOptions),
                SortOrder = int.MinValue + 10,
                IsVisible = false
            };
            db.Set<HomeSection>().Add(row);
        }
        else
        {
            row.ContentJson = JsonSerializer.Serialize(payload, JsonOptions);
            row.SortOrder = int.MinValue + 10;
            row.IsVisible = false;
        }

        await db.SaveChangesAsync(ct);
        return (Normalize(payload), null);
    }

    private static PublicShellSettingsDto Normalize(Payload? payload)
    {
        payload ??= new Payload();
        var items = payload.NavigationItems is null
            ? NormalizeItems(BuildLegacyItems(payload))
            : NormalizeItems(payload.NavigationItems);

        return new PublicShellSettingsDto(
            SystemLabel(items, "home", payload.HomeLabel) ?? "Trang chủ",
            SystemLabel(items, "rooms", payload.RoomsLabel) ?? "Phòng",
            SystemLabel(items, "branches", payload.BranchesLabel) ?? "Cơ sở",
            SystemLabel(items, "booking", payload.BookingLabel) ?? "Đặt phòng",
            SystemLabel(items, "lookup", payload.LookupLabel) ?? "Tra cứu",
            First(payload.HeaderCtaText, "Đặt phòng"),
            First(payload.HeaderCtaUrl, "@booking"),
            items.Select(x => x.Id).ToList(),
            items,
            SystemVisible(items, "home"),
            SystemVisible(items, "rooms"),
            SystemVisible(items, "branches"),
            First(payload.FooterIntro, "Một nơi để nghỉ chậm lại, chọn phòng rõ ràng và gửi yêu cầu đặt chỗ trực tiếp."),
            First(payload.FooterBookingText, "Đặt phòng →"),
            First(payload.FooterBookingUrl, "@booking"),
            First(payload.FooterExploreTitle, "Khám phá"),
            First(payload.FooterBranchesTitle, "Cơ sở"),
            First(payload.FooterContactTitle, "Liên hệ"),
            First(payload.FooterBottomText, "Đặt phòng trực tiếp · Giá rõ ràng · Xác nhận bởi nhân viên"),
            payload.ShowFooterContact ?? true);
    }

    private static PublicShellSettingsDto Defaults() => Normalize(null);

    private static IReadOnlyList<NavigationItemPayload> BuildLegacyItems(Payload payload)
    {
        var order = NormalizeLegacyOrder(payload.NavigationOrder);
        var map = new Dictionary<string, NavigationItemPayload>(StringComparer.OrdinalIgnoreCase)
        {
            ["home"] = new() { Id = "home", Label = First(payload.HomeLabel, "Trang chủ"), Url = "@home", IsVisible = payload.ShowHome ?? true },
            ["rooms"] = new() { Id = "rooms", Label = First(payload.RoomsLabel, "Phòng"), Url = "@rooms", IsVisible = payload.ShowRooms ?? true },
            ["branches"] = new() { Id = "branches", Label = First(payload.BranchesLabel, "Cơ sở"), Url = "@branches", IsVisible = payload.ShowBranches ?? true },
            ["booking"] = new() { Id = "booking", Label = First(payload.BookingLabel, "Đặt phòng"), Url = "@booking", IsVisible = true },
            ["lookup"] = new() { Id = "lookup", Label = First(payload.LookupLabel, "Tra cứu"), Url = "@lookup", IsVisible = true }
        };
        return order.Select(key => map[key]).ToList();
    }

    private static IReadOnlyList<NavigationItemPayload> BuildLegacyItems(SavePublicShellSettingsRequest request)
    {
        var order = NormalizeLegacyOrder(request.NavigationOrder);
        var map = new Dictionary<string, NavigationItemPayload>(StringComparer.OrdinalIgnoreCase)
        {
            ["home"] = new() { Id = "home", Label = First(request.HomeLabel, "Trang chủ"), Url = "@home", IsVisible = request.ShowHome },
            ["rooms"] = new() { Id = "rooms", Label = First(request.RoomsLabel, "Phòng"), Url = "@rooms", IsVisible = request.ShowRooms },
            ["branches"] = new() { Id = "branches", Label = First(request.BranchesLabel, "Cơ sở"), Url = "@branches", IsVisible = request.ShowBranches },
            ["booking"] = new() { Id = "booking", Label = First(request.BookingLabel, "Đặt phòng"), Url = "@booking", IsVisible = true },
            ["lookup"] = new() { Id = "lookup", Label = First(request.LookupLabel, "Tra cứu"), Url = "@lookup", IsVisible = true }
        };
        return order.Select(key => map[key]).ToList();
    }

    private static IReadOnlyList<PublicNavigationItemDto> NormalizeItems(IReadOnlyList<NavigationItemPayload>? items)
    {
        var result = new List<PublicNavigationItemDto>();
        foreach (var item in items ?? [])
        {
            var id = NormalizeId(item.Id);
            var label = CleanOrNull(item.Label);
            var url = CleanOrNull(item.Url);
            if (id is null || label is null || url is null || result.Any(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase))) continue;
            result.Add(new PublicNavigationItemDto(
                id,
                label,
                url,
                item.IsVisible ?? true,
                item.OpenInNewTab ?? false,
                SystemNavigationKeys.Contains(id)));
        }
        return result;
    }

    private static IReadOnlyList<string> NormalizeLegacyOrder(IReadOnlyList<string>? order)
    {
        var normalized = new List<string>();
        foreach (var key in order ?? [])
        {
            var clean = key?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(clean) || !SystemNavigationKeys.Contains(clean) || normalized.Contains(clean, StringComparer.OrdinalIgnoreCase)) continue;
            normalized.Add(clean);
        }
        foreach (var key in DefaultNavigationOrder)
        {
            if (!normalized.Contains(key, StringComparer.OrdinalIgnoreCase)) normalized.Add(key);
        }
        return normalized;
    }

    private static string? Validate(SavePublicShellSettingsRequest request)
    {
        var shortFields = new[]
        {
            request.HomeLabel, request.RoomsLabel, request.BranchesLabel, request.BookingLabel,
            request.LookupLabel, request.HeaderCtaText, request.FooterExploreTitle,
            request.FooterBranchesTitle, request.FooterContactTitle, request.FooterBookingText
        };
        if (shortFields.Any(x => Length(x) > 80)) return "Tên menu/CTA/Footer tối đa 80 ký tự.";
        if (Length(request.FooterIntro) > 700) return "Mô tả Footer tối đa 700 ký tự.";
        if (Length(request.FooterBottomText) > 300) return "Dòng cuối Footer tối đa 300 ký tự.";
        if (!IsSafeUrl(request.HeaderCtaUrl)) return "Link CTA Header không hợp lệ.";
        if (!IsSafeUrl(request.FooterBookingUrl)) return "Link đặt phòng ở Footer không hợp lệ.";

        if (request.NavigationItems is not null)
        {
            if (request.NavigationItems.Count > 20) return "Menu tối đa 20 mục.";
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in request.NavigationItems)
            {
                var id = NormalizeId(item.Id);
                if (id is null || !ids.Add(id)) return "Mỗi mục menu cần ID hợp lệ và không được trùng.";
                if (string.IsNullOrWhiteSpace(item.Label) || Length(item.Label) > 80) return "Tên mỗi mục menu phải từ 1 đến 80 ký tự.";
                if (string.IsNullOrWhiteSpace(item.Url) || Length(item.Url) > 1500 || !IsSafeUrl(item.Url)) return $"Link của mục ‘{item.Label?.Trim()}’ không hợp lệ.";
            }
            return null;
        }

        if (request.NavigationOrder is { Count: > 10 }) return "Thứ tự menu không hợp lệ.";
        if (request.NavigationOrder is not null && request.NavigationOrder.Any(x => !SystemNavigationKeys.Contains((x ?? string.Empty).Trim())))
            return "Menu chứa mục không được hỗ trợ.";
        return null;
    }

    private static bool IsSafeUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        var url = value.Trim();
        if (SystemUrlTokens.Contains(url)) return true;
        if (url.StartsWith("/", StringComparison.Ordinal) && !url.StartsWith("//", StringComparison.Ordinal)) return true;
        if (url.StartsWith("#", StringComparison.Ordinal) || url.StartsWith("?", StringComparison.Ordinal)) return true;
        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute))
            return absolute.Scheme is "http" or "https" or "mailto" or "tel";
        return Uri.TryCreate(url, UriKind.Relative, out _) && !url.Contains(':');
    }

    private static string? SystemLabel(IReadOnlyList<PublicNavigationItemDto> items, string id, string? fallback) =>
        items.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase))?.Label ?? CleanOrNull(fallback);

    private static bool SystemVisible(IReadOnlyList<PublicNavigationItemDto> items, string id) =>
        items.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase))?.IsVisible ?? false;

    private static string? NormalizeId(string? value)
    {
        var id = value?.Trim();
        return !string.IsNullOrWhiteSpace(id) && NavigationIdRegex.IsMatch(id) ? id : null;
    }

    private static string First(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    private static string? CleanOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static int Length(string? value) => value?.Trim().Length ?? 0;
}
