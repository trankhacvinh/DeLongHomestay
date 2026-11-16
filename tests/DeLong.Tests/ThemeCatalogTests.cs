using DeLong.Web.Features.Site;
using DeLong.Web.Features.Site.Themes;
using DeLong.Web.Pages;
using ThemeModel = DeLong.Web.Pages.Admin.Site.ThemeModel;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Xunit;

namespace DeLong.Tests;

public sealed class ThemeCatalogTests
{
    private static HomeSectionDto Row(string type, int order, bool visible, string json = "{}") =>
        new(Guid.NewGuid(), type, type, "split", json, order, visible);

    [Fact]
    [Trait("Category", "Unit")]
    public void Theme_1_without_a_saved_layout_keeps_the_stored_blocks_exactly()
    {
        HomeSectionDto[] rows = [Row("Hero", 0, true), Row("Faq", 1, false), Row("RoomGrid", 2, true), Row("RichText", 3, true), Row("RichText", 4, true)];
        var sections = IndexModel.ThemeSections(ThemeCatalog.Theme1, null, rows);
        Assert.Equal(new[] { "Hero", "RoomGrid", "RichText", "RichText" }, sections.Select(x => x.Type));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Saved_layout_sets_order_and_visibility_and_reuses_hidden_content()
    {
        HomeSectionDto[] rows = [Row("Hero", 0, true, """{"title":"Xin chào"}"""), Row("Faq", 1, false, """{"title":"Hỏi"}""")];
        ThemeLayoutItem[] layout = [new("Faq", true), new("RoomGrid", false), new("Hero", true), new("Cta", true)];
        var sections = IndexModel.ThemeSections(ThemeCatalog.Theme1, layout, rows);
        Assert.Equal(new[] { "Faq", "Hero", "Cta" }, sections.Select(x => x.Type));
        Assert.Equal("Hỏi", sections[0].Content["title"]!.GetValue<string>());
        Assert.Equal(Guid.Empty, sections[2].Id); // no stored Cta: the theme draws its built-in defaults
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Clean_layout_drops_unknown_and_duplicate_blocks_and_appends_missing_ones_switched_off()
    {
        var layout = PublicThemeStore.CleanLayout(ThemeCatalog.Theme1,
            [new("Cta", true), new("BranchGrid", true), new("cta", false), new("Hero", true)]);
        Assert.Equal("Cta", layout[0].Type);
        Assert.True(layout[0].Visible);
        Assert.Equal("Hero", layout[1].Type);
        Assert.DoesNotContain(layout, x => x.Type == "BranchGrid");
        Assert.Equal(ThemeCatalog.Theme1.Slots.Count, layout.Count);
        Assert.All(layout.Skip(2), x => Assert.False(x.Visible));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Admin_block_list_starts_from_the_blocks_guests_see_today()
    {
        HomeSectionDto[] rows = [Row("RoomGrid", 0, true), Row("Hero", 1, true), Row("Faq", 2, false)];
        var layout = ThemeModel.CurrentLayout(ThemeCatalog.Theme1, null, rows);
        Assert.Equal(new[] { "RoomGrid", "Hero" }, layout.Where(x => x.Visible).Select(x => x.Type));
        Assert.Contains(layout, x => x.Type == "Faq" && !x.Visible);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Only_signed_in_admins_can_preview_another_theme_or_palette()
    {
        var saved = new PublicThemeDto(PublicThemeStore.StandardMode, ThemeKey: "theme1", Palettes: new Dictionary<string, string> { ["theme1"] = "forest" });
        var guest = new DefaultHttpContext();
        guest.Request.QueryString = new QueryString("?palette=ink");
        var forGuest = PublicThemeSelection.Resolve(saved, guest);
        Assert.Equal("forest", forGuest.Palette.Key);
        Assert.False(forGuest.IsPreview);

        var admin = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Admin")], "test")) };
        admin.Request.QueryString = new QueryString("?palette=ink");
        var forAdmin = PublicThemeSelection.Resolve(saved, admin);
        Assert.Equal("ink", forAdmin.Palette.Key);
        Assert.True(forAdmin.IsPreview);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Legacy_free_form_mode_only_applies_to_theme_1_outside_previews()
    {
        var legacy = new PublicThemeDto(PublicThemeStore.CustomMode);
        Assert.False(PublicThemeSelection.Resolve(legacy, new DefaultHttpContext()).Standard);
        Assert.True(PublicThemeSelection.Resolve(legacy, new DefaultHttpContext()).IsDefaultPalette);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Every_theme_draws_each_of_its_blocks_with_an_existing_partial()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DeLongHomestay.sln"))) directory = directory.Parent;
        foreach (var theme in ThemeCatalog.All)
        {
            Assert.NotEmpty(theme.Palettes);
            foreach (var type in theme.Slots.Select(x => x.Type).Append("Editorial"))
            {
                var partial = theme.SectionPartial(type).TrimStart('/');
                Assert.True(File.Exists(Path.Combine(directory!.FullName, "src/DeLong.Web", partial)), $"{theme.Key}: missing {partial}");
            }
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Every_theme_asset_ships_with_the_site()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DeLongHomestay.sln"))) directory = directory.Parent;
        var wwwroot = Path.Combine(directory!.FullName, "src/DeLong.Web/wwwroot");
        foreach (var theme in ThemeCatalog.All)
        {
            var assets = theme.Stylesheets.Concat(theme.Scripts).Append(theme.PreviewImageUrl);
            if (theme.PaletteStylesheet is { } palettes) assets = assets.Append(palettes);
            foreach (var asset in assets)
                Assert.True(File.Exists(Path.Combine(wwwroot, asset.TrimStart('/'))), $"{theme.Key}: missing {asset}");
            Assert.Equal(theme.Slots.Count, theme.Slots.Select(x => x.Type).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }
        Assert.True(File.Exists(Path.Combine(wwwroot, "vendor/three/0.160.1/three.module.min.js")));
        Assert.True(File.Exists(Path.Combine(wwwroot, "vendor/three/0.160.1/LICENSE")));
        Assert.True(File.Exists(Path.Combine(wwwroot, "vendor/gsap/3.13.0/LICENSE.txt")));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Theme2_starts_with_its_own_block_order_without_touching_theme1()
    {
        var sections = new List<HomeSectionDto>();
        var layout = ThemeModel.CurrentLayout(ThemeCatalog.Theme2, null, sections);
        Assert.Equal(["Hero", "RoomGrid", "AvailabilityCalendar", "FeatureGrid", "Faq"], layout.Where(x => x.Visible).Take(5).Select(x => x.Type));
        Assert.Equal("/Pages/Themes/theme2/_Hero.cshtml", ThemeCatalog.Theme2.SectionPartial("Hero"));
        Assert.Equal("/Pages/Themes/theme1/_Faq.cshtml", ThemeCatalog.Theme2.SectionPartial("Faq"));
        Assert.Equal("dusk", ThemeCatalog.Theme2.Palette("nope").Key);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Theme4_puts_the_date_search_right_under_its_landscape()
    {
        var layout = ThemeModel.CurrentLayout(ThemeCatalog.Theme4, null, new List<HomeSectionDto>());
        Assert.Equal(["Hero", "AvailabilitySearch", "RoomGrid"], layout.Take(3).Select(x => x.Type));
        Assert.All(layout.Take(3), x => Assert.True(x.Visible));
        Assert.Equal("/Pages/Themes/theme4/_AvailabilitySearch.cshtml", ThemeCatalog.Theme4.SectionPartial("AvailabilitySearch"));
        Assert.Equal("/Pages/Themes/theme1/_AvailabilityCalendar.cshtml", ThemeCatalog.Theme4.SectionPartial("AvailabilityCalendar"));
        Assert.Equal("lake", ThemeCatalog.Theme4.Palettes[0].Key);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Theme5_opens_on_its_pixel_world_then_the_draggable_rooms()
    {
        var layout = ThemeModel.CurrentLayout(ThemeCatalog.Theme5, null, new List<HomeSectionDto>());
        Assert.Equal(["Hero", "RoomGrid", "FeatureGrid"], layout.Take(3).Select(x => x.Type));
        Assert.False(layout.Single(x => x.Type == "AvailabilitySearch").Visible);
        Assert.Equal("/Pages/Themes/theme5/_RoomGrid.cshtml", ThemeCatalog.Theme5.SectionPartial("RoomGrid"));
        Assert.Equal("/Pages/Themes/theme1/_Faq.cshtml", ThemeCatalog.Theme5.SectionPartial("Faq"));
        Assert.Equal("sunset", ThemeCatalog.Theme5.Palettes[0].Key);
        Assert.DoesNotContain(ThemeCatalog.Theme5.Scripts, x => x.Contains("three", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Theme3_tells_its_story_before_the_rooms_and_loads_scrolltrigger_first()
    {
        var layout = ThemeModel.CurrentLayout(ThemeCatalog.Theme3, null, new List<HomeSectionDto>());
        Assert.Equal(["Hero", "FeatureGrid", "RoomGrid", "AvailabilityCalendar"], layout.Take(4).Select(x => x.Type));
        Assert.Equal(["theme1", "theme2", "theme3", "theme4", "theme5"], ThemeCatalog.All.Select(x => x.Key));
        var scripts = ThemeCatalog.Theme3.Scripts.ToList();
        Assert.True(scripts.IndexOf("/vendor/gsap/3.13.0/ScrollTrigger.min.js") < scripts.IndexOf("/themes/theme3/theme.js"));
    }
}
