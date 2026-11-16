using System.Text.Json;
using DeLong.Web.Data;
using DeLong.Web.Features.Site;
using DeLong.Web.Features.Site.Themes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Admin.Site;

/// <summary>Admin → Giao diện: pick a theme and colour preset, switch homepage blocks on/off and reorder them.</summary>
[Authorize(Roles = "Admin")]
public sealed class ThemeModel(SiteContentService siteContentService, AppDbContext db) : PageModel
{
    public string PageDataJson { get; private set; } = "{}";

    public async Task OnGetAsync(CancellationToken ct)
    {
        var saved = await PublicThemeStore.ReadAsync(db, ct);
        var sections = await siteContentService.GetGlobalPublicSectionsAsync(ct);
        PageDataJson = JsonSerializer.Serialize(new
        {
            activeTheme = saved.ThemeKey,
            legacyCustom = !saved.IsStandard,
            themes = ThemeCatalog.All.Select(theme => new
            {
                key = theme.Key,
                name = theme.Name,
                description = theme.Description,
                previewImageUrl = theme.PreviewImageUrl,
                palette = theme.Palette(saved.PaletteFor(theme.Key)).Key,
                palettes = theme.Palettes.Select(x => new { key = x.Key, name = x.Name, swatches = x.Swatches }),
                customised = saved.LayoutFor(theme.Key) is not null,
                blocks = CurrentLayout(theme, saved.LayoutFor(theme.Key), sections).Select(item => new
                {
                    type = item.Type,
                    label = theme.Slot(item.Type)!.Label,
                    visible = item.Visible,
                    hasContent = sections.Any(x => string.Equals(x.Type, item.Type, StringComparison.OrdinalIgnoreCase))
                })
            })
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    /// <summary>
    /// The block list as the homepage draws it today: the saved layout, else Theme 1's stored blocks (visible ones in
    /// their order, then the rest switched off), else the theme's defaults.
    /// </summary>
    public static IReadOnlyList<ThemeLayoutItem> CurrentLayout(
        ThemeDefinition theme,
        IReadOnlyList<ThemeLayoutItem>? saved,
        IReadOnlyList<HomeSectionDto> sections)
    {
        if (saved is not null) return PublicThemeStore.CleanLayout(theme, saved);
        if (theme.Key != ThemeCatalog.DefaultKey)
            return theme.Slots.Select(x => new ThemeLayoutItem(x.Type, x.DefaultVisible)).ToList();
        var shown = sections.Where(x => x.IsVisible).OrderBy(x => x.SortOrder)
            .Select(x => new ThemeLayoutItem(x.Type, true));
        return PublicThemeStore.CleanLayout(theme, shown);
    }
}
