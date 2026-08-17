using System.Text.Json;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Site;

public sealed class SavePublicShellSettingsRequest
{
    public string? HomeLabel { get; init; }
    public string? RoomsLabel { get; init; }
    public string? BranchesLabel { get; init; }
    public string? BookingLabel { get; init; }
    public string? LookupLabel { get; init; }
    public string? HeaderCtaText { get; init; }
    public IReadOnlyList<string>? NavigationOrder { get; init; }
    public bool ShowHome { get; init; } = true;
    public bool ShowRooms { get; init; } = true;
    public bool ShowBranches { get; init; } = true;
    public string? FooterIntro { get; init; }
    public string? FooterBookingText { get; init; }
    public string? FooterExploreTitle { get; init; }
    public string? FooterBranchesTitle { get; init; }
    public string? FooterContactTitle { get; init; }
    public string? FooterBottomText { get; init; }
    public bool ShowFooterContact { get; init; } = true;
}

public sealed record PublicShellSettingsDto(
    string HomeLabel,
    string RoomsLabel,
    string BranchesLabel,
    string BookingLabel,
    string LookupLabel,
    string HeaderCtaText,
    IReadOnlyList<string> NavigationOrder,
    bool ShowHome,
    bool ShowRooms,
    bool ShowBranches,
    string FooterIntro,
    string FooterBookingText,
    string FooterExploreTitle,
    string FooterBranchesTitle,
    string FooterContactTitle,
    string FooterBottomText,
    bool ShowFooterContact);

public static class PublicShellSettingsStore
{
    public const string MetadataSectionType = "__PublicShell";
    public static readonly IReadOnlyList<string> DefaultNavigationOrder = ["home", "rooms", "branches", "booking", "lookup"];
    private static readonly HashSet<string> AllowedNavigationKeys = new(DefaultNavigationOrder, StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed class Payload
    {
        public string? HomeLabel { get; init; }
        public string? RoomsLabel { get; init; }
        public string? BranchesLabel { get; init; }
        public string? BookingLabel { get; init; }
        public string? LookupLabel { get; init; }
        public string? HeaderCtaText { get; init; }
        public IReadOnlyList<string>? NavigationOrder { get; init; }
        public bool? ShowHome { get; init; }
        public bool? ShowRooms { get; init; }
        public bool? ShowBranches { get; init; }
        public string? FooterIntro { get; init; }
        public string? FooterBookingText { get; init; }
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

        var order = NormalizeOrder(request.NavigationOrder);
        var payload = new Payload
        {
            HomeLabel = CleanOrNull(request.HomeLabel),
            RoomsLabel = CleanOrNull(request.RoomsLabel),
            BranchesLabel = CleanOrNull(request.BranchesLabel),
            BookingLabel = CleanOrNull(request.BookingLabel),
            LookupLabel = CleanOrNull(request.LookupLabel),
            HeaderCtaText = CleanOrNull(request.HeaderCtaText),
            NavigationOrder = order,
            ShowHome = request.ShowHome,
            ShowRooms = request.ShowRooms,
            ShowBranches = request.ShowBranches,
            FooterIntro = CleanOrNull(request.FooterIntro),
            FooterBookingText = CleanOrNull(request.FooterBookingText),
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
        return new PublicShellSettingsDto(
            First(payload.HomeLabel, "Trang chủ"),
            First(payload.RoomsLabel, "Phòng"),
            First(payload.BranchesLabel, "Cơ sở"),
            First(payload.BookingLabel, "Đặt phòng"),
            First(payload.LookupLabel, "Tra cứu"),
            First(payload.HeaderCtaText, "Đặt phòng"),
            NormalizeOrder(payload.NavigationOrder),
            payload.ShowHome ?? true,
            payload.ShowRooms ?? true,
            payload.ShowBranches ?? true,
            First(payload.FooterIntro, "Một nơi để nghỉ chậm lại, chọn phòng rõ ràng và gửi yêu cầu đặt chỗ trực tiếp."),
            First(payload.FooterBookingText, "Đặt phòng →"),
            First(payload.FooterExploreTitle, "Khám phá"),
            First(payload.FooterBranchesTitle, "Cơ sở"),
            First(payload.FooterContactTitle, "Liên hệ"),
            First(payload.FooterBottomText, "Đặt phòng trực tiếp · Giá rõ ràng · Xác nhận bởi nhân viên"),
            payload.ShowFooterContact ?? true);
    }

    private static PublicShellSettingsDto Defaults() => Normalize(null);

    private static IReadOnlyList<string> NormalizeOrder(IReadOnlyList<string>? order)
    {
        var normalized = new List<string>();
        foreach (var key in order ?? [])
        {
            var clean = key?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(clean) || !AllowedNavigationKeys.Contains(clean) || normalized.Contains(clean, StringComparer.OrdinalIgnoreCase)) continue;
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
        if (request.NavigationOrder is { Count: > 10 }) return "Thứ tự menu không hợp lệ.";
        if (request.NavigationOrder is not null && request.NavigationOrder.Any(x => !AllowedNavigationKeys.Contains((x ?? string.Empty).Trim())))
            return "Menu chứa mục không được hỗ trợ.";
        return null;
    }

    private static string First(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    private static string? CleanOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static int Length(string? value) => value?.Trim().Length ?? 0;
}
