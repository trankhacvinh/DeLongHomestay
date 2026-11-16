namespace DeLong.Web.Features.Site.Themes;

/// <summary>One homepage block a theme offers: its section type and whether it is on by default.</summary>
public sealed record ThemeSlotDefinition(string Type, string Label, bool DefaultVisible = true);

/// <summary>A colour preset of a theme. <c>Swatches</c> are only for the admin picker; the colours themselves live in the theme CSS
/// (<see cref="ThemeDefinition.PaletteStylesheet"/>) under <c>body[data-palette="key"]</c>; the first palette is the theme's own
/// colours and loads no extra CSS.</summary>
public sealed record ThemePaletteDefinition(string Key, string Name, IReadOnlyList<string> Swatches);

/// <summary>
/// A public-site theme. Themes are code (Razor partials + CSS + optional JS), not data: owners pick one and only switch
/// homepage blocks on/off and reorder them. The booking calendar, booking modal and payment flow are shared by every theme.
/// </summary>
public sealed record ThemeDefinition(
    string Key,
    string Name,
    string Description,
    string PreviewImageUrl,
    IReadOnlyList<ThemeSlotDefinition> Slots,
    IReadOnlyList<ThemePaletteDefinition> Palettes,
    IReadOnlySet<string> OwnPartials,
    IReadOnlyList<string> Stylesheets,
    IReadOnlyList<string> Scripts,
    string? PaletteStylesheet = null)
{
    public ThemeSlotDefinition? Slot(string type) =>
        Slots.FirstOrDefault(x => string.Equals(x.Type, type, StringComparison.OrdinalIgnoreCase));

    public ThemePaletteDefinition Palette(string? key) =>
        Palettes.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase)) ?? Palettes[0];

    /// <summary>Razor partial that renders a homepage block. A theme that does not draw a block itself uses Theme 1's.</summary>
    public string SectionPartial(string type) =>
        $"/Pages/Themes/{(OwnPartials.Contains(type) ? Key : ThemeCatalog.DefaultKey)}/_{type}.cshtml";
}

public static class ThemeCatalog
{
    public const string DefaultKey = "theme1";

    public static readonly ThemeDefinition Theme1 = new(
        DefaultKey,
        "Theme 1 · Cổ điển",
        "Sang trọng, cổ điển: nền giấy kem, tiêu đề chữ có chân, điểm vàng. Ảnh đầu trang ghép ba ảnh, dải tên phòng chạy ngang.",
        "/themes/theme1/preview.webp",
        [
            new("Hero", "Ảnh đầu trang"),
            new("AvailabilitySearch", "Ô tìm phòng nhanh"),
            new("FeatureGrid", "Điểm nổi bật"),
            new("RoomGrid", "Các phòng (kèm 4 bước đặt phòng)"),
            new("AvailabilityCalendar", "Lịch phòng", DefaultVisible: false),
            new("Faq", "Hỏi đáp", DefaultVisible: false),
            new("RichText", "Đoạn văn tự do", DefaultVisible: false),
            new("Cta", "Kêu gọi đặt phòng")
        ],
        [
            new("teal", "Xanh cổ vịt", ["#0d2b2e", "#f3ede2", "#e2c48e"]),
            new("forest", "Rừng thông", ["#1f3326", "#f1efe6", "#d9a35b"]),
            new("ink", "Mực đêm", ["#1c2333", "#f2efe9", "#c9a86a"]),
            new("terracotta", "Đất nung", ["#3b2420", "#f6eee6", "#e09a6a"])
        ],
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Hero", "AvailabilitySearch", "AvailabilityCalendar", "FeatureGrid", "RoomGrid", "Faq", "RichText", "Cta", "Editorial"
        },
        [],
        [],
        PaletteStylesheet: "/themes/theme1/palettes.css");

    public static readonly ThemeDefinition Theme2 = new(
        "theme2",
        "Theme 2 · Chill",
        "Trẻ, thư giãn: đầu trang là bầu trời hoàng hôn chuyển động, tiêu đề chữ đậm cỡ lớn, phòng xếp thành dải ảnh dọc kéo ngang. Một phông chữ, ít trang trí.",
        "/themes/theme2/preview.webp",
        [
            new("Hero", "Bầu trời đầu trang"),
            new("RoomGrid", "Dải ảnh các phòng"),
            new("AvailabilityCalendar", "Lịch phòng"),
            new("FeatureGrid", "Điểm nổi bật"),
            new("Faq", "Hỏi đáp"),
            new("AvailabilitySearch", "Ô tìm phòng nhanh", DefaultVisible: false),
            new("RichText", "Đoạn văn tự do", DefaultVisible: false),
            new("Cta", "Lời mời đặt phòng cuối trang")
        ],
        [
            new("dusk", "Hoàng hôn tím", ["#282150", "#efebf7", "#7fe0d6"]),
            new("forest", "Rừng & đất nung", ["#1f3b2d", "#f1f4ee", "#f0a27a"]),
            new("ocean", "Biển đêm", ["#12304a", "#eef3f6", "#ff9c7a"])
        ],
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Hero", "RoomGrid", "FeatureGrid", "Cta" },
        ["/themes/theme2/theme.css"],
        ["/vendor/gsap/3.13.0/gsap.min.js", "/themes/theme2/theme.js"]);

    public static readonly ThemeDefinition Theme3 = new(
        "theme3",
        "Theme 3 · Premium 3D",
        "Sang trọng, nhiều hiệu ứng: đầu trang là căn phòng 3D, cuộn chuột để bước vào phòng, đèn sáng dần. Nét vẽ vàng đồng tự hiện, số liệu đếm chạy, các phòng trượt ngang. Trên điện thoại dùng bản nét vẽ nhẹ hơn.",
        "/themes/theme3/preview.webp",
        [
            new("Hero", "Căn phòng 3D đầu trang"),
            new("FeatureGrid", "Câu chuyện & con số"),
            new("RoomGrid", "Các phòng trượt ngang"),
            new("AvailabilityCalendar", "Lịch phòng"),
            new("Faq", "Hỏi đáp"),
            new("AvailabilitySearch", "Ô tìm phòng nhanh", DefaultVisible: false),
            new("RichText", "Đoạn văn tự do", DefaultVisible: false),
            new("Cta", "Lời mời đặt phòng cuối trang")
        ],
        [
            new("espresso", "Gỗ & đồng", ["#17110e", "#f4efe7", "#c9a46a"]),
            new("emerald", "Ngọc lục", ["#0d1a17", "#f1f2ec", "#c9a46a"]),
            new("sapphire", "Lam ngọc", ["#0d1220", "#f1f1f4", "#d6b57f"])
        ],
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Hero", "FeatureGrid", "RoomGrid", "Cta" },
        ["/themes/theme3/theme.css"],
        ["/vendor/gsap/3.13.0/gsap.min.js", "/vendor/gsap/3.13.0/ScrollTrigger.min.js", "/themes/theme3/theme.js"]);

    public static readonly ThemeDefinition Theme4 = new(
        "theme4",
        "Theme 4 · Ambient",
        "Nhẹ nhàng, thư thái: đầu trang là phong cảnh đồi và hồ trôi chậm, bầu trời đổi theo giờ khách vào web, đom đóm buổi tối. Ô chọn ngày nằm ngay dưới để đặt nhanh.",
        "/themes/theme4/preview.webp",
        [
            new("Hero", "Phong cảnh đầu trang"),
            new("AvailabilitySearch", "Ô chọn ngày đặt nhanh"),
            new("RoomGrid", "Các phòng"),
            new("AvailabilityCalendar", "Lịch phòng"),
            new("FeatureGrid", "Điểm nổi bật"),
            new("Faq", "Hỏi đáp"),
            new("RichText", "Đoạn văn tự do", DefaultVisible: false),
            new("Cta", "Lời mời đặt phòng cuối trang")
        ],
        [
            new("lake", "Hồ sương", ["#233447", "#eef2f1", "#e9b872"]),
            new("tea", "Đồi chè", ["#26392a", "#f0f2ea", "#e2b866"]),
            new("plum", "Hoa mận", ["#3d2e3f", "#f4f0f1", "#f2b880"])
        ],
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Hero", "AvailabilitySearch", "RoomGrid", "FeatureGrid", "Cta" },
        ["/themes/theme4/theme.css"],
        ["/vendor/gsap/3.13.0/gsap.min.js", "/vendor/gsap/3.13.0/ScrollTrigger.min.js", "/themes/theme4/theme.js"]);

    public static readonly IReadOnlyList<ThemeDefinition> All = [Theme1, Theme2, Theme3, Theme4];

    public static ThemeDefinition Get(string? key) =>
        All.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase)) ?? Theme1;

    public static bool Exists(string? key) =>
        All.Any(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
}
