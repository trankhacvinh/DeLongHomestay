namespace DeLong.Web.Features.Site.Themes;

/// <summary>The theme, colour preset and homepage layout one public request is drawn with.</summary>
public sealed record PublicThemeSelection(
    ThemeDefinition Theme,
    ThemePaletteDefinition Palette,
    IReadOnlyList<ThemeLayoutItem>? Layout,
    bool Standard,
    bool IsPreview)
{
    /// <summary>
    /// Saved choice, unless a signed-in Admin/Manager previews another theme or palette with <c>?theme=</c> / <c>?palette=</c>
    /// (Admin → Giao diện "Xem thử"). Guests always get the saved theme. The legacy "Tùy chỉnh tự do" mode is only kept
    /// for Theme 1 sites that still use it; every other theme, and any preview, is drawn standard.
    /// </summary>
    public static PublicThemeSelection Resolve(PublicThemeDto saved, HttpContext? http)
    {
        var request = http?.Request;
        var user = http?.User;
        var canPreview = user?.Identity?.IsAuthenticated == true && (user.IsInRole("Admin") || user.IsInRole("Manager"));
        var previewTheme = canPreview ? request?.Query["theme"].ToString() : null;
        var previewPalette = canPreview ? request?.Query["palette"].ToString() : null;
        var isPreview = ThemeCatalog.Exists(previewTheme) || !string.IsNullOrWhiteSpace(previewPalette);

        var theme = ThemeCatalog.Exists(previewTheme) ? ThemeCatalog.Get(previewTheme) : ThemeCatalog.Get(saved.ThemeKey);
        var palette = theme.Palette(string.IsNullOrWhiteSpace(previewPalette) ? saved.PaletteFor(theme.Key) : previewPalette);
        var standard = isPreview || theme.Key != ThemeCatalog.DefaultKey || saved.IsStandard;
        return new PublicThemeSelection(theme, palette, saved.LayoutFor(theme.Key), standard, isPreview);
    }

    public bool IsDefaultPalette => ReferenceEquals(Palette, Theme.Palettes[0]);
}
