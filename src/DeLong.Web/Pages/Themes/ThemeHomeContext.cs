using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using DeLong.Web.Features.PublicRooms;
using DeLong.Web.Features.Site;
using DeLong.Web.Features.Site.Themes;

namespace DeLong.Web.Pages.Themes;

/// <summary>Model of one homepage block partial (<c>/Pages/Themes/&lt;theme&gt;/_&lt;Type&gt;.cshtml</c>).</summary>
public sealed record ThemeSectionModel(IndexModel Page, PublicHomeSectionVm Block, ThemeHomeContext Home);

/// <summary>
/// Values and helpers shared by every block of one homepage render. Blocks are rendered one partial at a time, so
/// "render once" flags (calendar anchor, booking steps band) live here.
/// </summary>
public sealed class ThemeHomeContext
{
    private static readonly HtmlEncoder Enc = HtmlEncoder.Default;

    public ThemeHomeContext(IndexModel page, ThemeDefinition theme, string siteName)
    {
        Page = page;
        Theme = theme;
        SiteName = siteName;
        RoomCount = page.GlobalCatalog.Rooms.Count;
        FromPrice = page.GlobalCatalog.Rooms.Select(x => x.Room)
            .Where(x => x.QuickFromPrice > 0).Select(x => x.QuickFromPrice).DefaultIfEmpty(0).Min();
        HeroPriceShort = FromPrice <= 0 ? string.Empty : FromPrice >= 1000 ? $"{FromPrice / 1000:0.#}K" : $"{FromPrice:N0}đ";
        RoomMarqueeText = string.Join(" ✦ ", page.GlobalCatalog.Rooms.Select(x => x.Room.Name).Distinct().Take(12));
    }

    public IndexModel Page { get; }
    public ThemeDefinition Theme { get; }
    public string SiteName { get; }
    public int RoomCount { get; }
    public decimal FromPrice { get; }
    public string HeroPriceShort { get; }
    public string RoomMarqueeText { get; }
    public bool BookingCalendarAnchorRendered { get; set; }
    public bool BookingStepsRendered { get; set; }

    public ThemeSectionModel For(PublicHomeSectionVm block) => new(Page, block, this);

    /// <summary>Gallery + Blog showcase drawn under the blocks; not a block owners switch on or off here.</summary>
    public const string EditorialType = "Editorial";
    public ThemeSectionModel Editorial() => For(new PublicHomeSectionVm(Guid.Empty, EditorialType, EditorialType, "default", new JsonObject()));

    public static string Text(JsonObject content, string key, string fallback = "") => content[key]?.GetValue<string>() ?? fallback;

    /// <summary>Visual-editor classes; only the legacy "Tùy chỉnh tự do" mode renders them.</summary>
    public string V(JsonObject content, string path, bool image = false) =>
        Page.ThemeStandard ? string.Empty : CustomPageModel.VisualClass(content, path, image);

    /// <summary>Standard theme renders every block in its default layout so the built-in design stays consistent.</summary>
    public string Variant(string variant) => Page.ThemeStandard ? "standard" : variant;

    /// <summary>Room card colour (Admin › Trang chủ chung › Màu thẻ phòng); null leaves the default palette from CSS.</summary>
    public string? RoomTone(Guid roomId) => Page.ThemeStandard && Page.Theme.RoomColor(roomId) is { } tone ? $"--dl-tone:{tone}" : null;

    public static string RoomPhotos(PublicRoomCardDto room) =>
        JsonSerializer.Serialize((room.GalleryLargeUrls?.Count > 0 ? room.GalleryLargeUrls : room.GalleryCardUrls) ?? []);

    public string ScopedUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return url;
        if (string.Equals(url, "/booking", StringComparison.OrdinalIgnoreCase)) return PublicUrlBuilder.BookingHome(Page.SiteSlug);
        if (string.Equals(url, "/rooms", StringComparison.OrdinalIgnoreCase)) return PublicUrlBuilder.BookingHome(Page.SiteSlug);
        if (Page.IsGlobalHome || string.IsNullOrWhiteSpace(Page.SiteSlug)) return url;
        if (url.StartsWith("/h/", StringComparison.OrdinalIgnoreCase)) return url;
        if (string.Equals(url, "/", StringComparison.Ordinal)) return PublicUrlBuilder.PropertyHome(Page.SiteSlug);
        return url.StartsWith('/') ? $"{Page.ScopePrefix}{url}" : url;
    }

    /// <summary>
    /// Theme 1 hero: a three-photo collage (big arch, top arch, small card). The photos are chosen in the Hero block
    /// (imageUrl, imageUrl2, imageUrl3); an empty slot becomes a soft colour tile.
    /// </summary>
    public string HeroCollage(JsonObject content, string fallbackMainImage, string caption, string title)
    {
        var mainImage = Text(content, "imageUrl");
        if (string.IsNullOrWhiteSpace(mainImage)) mainImage = fallbackMainImage;
        var photos = new[] { mainImage, Text(content, "imageUrl2"), Text(content, "imageUrl3") };
        string Tile(int index, string css)
        {
            if (!string.IsNullOrWhiteSpace(photos[index]))
                return $"<div class=\"public-hero-tile {css}\"><img src=\"{Enc.Encode(photos[index])}\" alt=\"{Enc.Encode(index == 0 ? title : $"Không gian tại {SiteName}")}\" {(index == 0 ? "fetchpriority=\"high\"" : "loading=\"lazy\"")} decoding=\"async\" /></div>";
            return $"<div class=\"public-hero-tile {css} is-empty\" aria-hidden=\"true\"></div>";
        }
        var html = new StringBuilder("<div class=\"public-hero-collage\">");
        html.Append(Tile(0, "is-main"));
        html.Append(Tile(1, "is-top"));
        html.Append(Tile(2, "is-bottom"));
        if (!string.IsNullOrEmpty(HeroPriceShort))
            html.Append("<span class=\"public-hero-price-sticker\" aria-hidden=\"true\"><small>đặt nhanh từ</small><strong>").Append(Enc.Encode(HeroPriceShort)).Append("</strong><small>mỗi khung giờ</small></span>");
        if (!string.IsNullOrWhiteSpace(caption))
            html.Append("<span class=\"public-hero-collage-label\"><small>").Append(Enc.Encode(caption)).Append("</small><strong>").Append(Enc.Encode(title)).Append("</strong></span>");
        html.Append("</div>");
        return html.ToString();
    }

    /// <summary>Theme 1 renders FeatureGrid as the "ways to book" card band. Items written as "Title — description" become a card title and its text.</summary>
    public static string HowBand(JsonObject content, string fallbackEyebrow)
    {
        var html = new StringBuilder();
        html.Append("<section class=\"public-section public-how-band\"><div class=\"public-container\"><div class=\"public-section-head\"><div>");
        html.Append("<span class=\"public-eyebrow\">").Append(Enc.Encode(Text(content, "eyebrow", fallbackEyebrow))).Append("</span>");
        html.Append("<h2>").Append(Enc.Encode(Text(content, "title"))).Append("</h2></div>");
        var body = Text(content, "body");
        if (!string.IsNullOrWhiteSpace(body)) html.Append("<p class=\"public-how-lead\">").Append(Enc.Encode(body)).Append("</p>");
        html.Append("</div><div class=\"public-how-grid\">");
        var index = 0;
        foreach (var item in (content["items"] as JsonArray ?? new JsonArray()).Select(x => x?.ToString()).Where(x => !string.IsNullOrWhiteSpace(x)).Take(8))
        {
            var parts = item!.Split(new[] { " — ", " – ", " - ", ": " }, 2, StringSplitOptions.TrimEntries);
            index++;
            html.Append("<article class=\"public-how-card\"><span class=\"public-how-index\">").Append(index.ToString("00")).Append("</span>");
            html.Append("<h3>").Append(Enc.Encode(parts[0])).Append("</h3>");
            if (parts.Length > 1) html.Append("<p>").Append(Enc.Encode(parts[1])).Append("</p>");
            html.Append("</article>");
        }
        html.Append("</div></div></section>");
        return html.ToString();
    }
}
