namespace DeLong.Web.Features.Site;

public sealed record PublicShellNavLink(string Id, string Label, string Href, bool OpenInNewTab, bool IsCurrent);

/// <summary>
/// Builds the guest header menu on the server from the saved menu settings (Admin → menu editor), so the page
/// arrives with the real menu and the right item marked current. public-shell-runtime.js used to swap the default
/// menu for the saved one after an API call, which made the menu visibly jump on every page load.
/// The URL tokens mirror resolveUrl() in wwwroot/js/pages/public-shell-runtime.js.
/// </summary>
public static class PublicShellNav
{
    public static string ResolveUrl(string? value, string? siteSlug)
    {
        var url = (value ?? string.Empty).Trim();
        if (!url.StartsWith('@')) return string.IsNullOrEmpty(url) ? "#" : url;
        var prefix = PublicPropertyResolver.ScopePrefix(siteSlug);
        return url.ToLowerInvariant() switch
        {
            "@home" => string.IsNullOrEmpty(prefix) ? "/" : prefix,
            "@rooms" => $"{prefix}/rooms",
            "@branches" => "/#co-so",
            "@booking" => $"{prefix}/booking",
            "@policies" => "/chinh-sach-dat-phong",
            "@lookup" => $"{prefix}/booking/lookup",
            _ => "#"
        };
    }

    /// <param name="currentKey">home, booking, policies or lookup for the page being shown; null otherwise.</param>
    /// <param name="branchesAvailable">The "Cơ sở" item points at the home page's branch list, so it is shown only when that list exists.</param>
    public static IReadOnlyList<PublicShellNavLink> Build(
        PublicShellSettingsDto settings,
        string? siteSlug,
        string? currentKey,
        string currentPath,
        bool branchesAvailable)
    {
        var path = NormalizePath(currentPath);
        var links = new List<PublicShellNavLink>();
        foreach (var item in settings.NavigationItems)
        {
            if (!item.IsVisible) continue;
            var token = (item.Url ?? string.Empty).Trim();
            if (string.Equals(item.Id, "branches", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(token, "@branches", StringComparison.OrdinalIgnoreCase) &&
                !branchesAvailable) continue;
            var href = ResolveUrl(token, siteSlug);
            var key = token.StartsWith('@') ? token[1..].ToLowerInvariant() : null;
            var isCurrent = key is not null
                ? key == currentKey
                : href.StartsWith('/') && !href.Contains('#') && string.Equals(NormalizePath(href.Split('?')[0]), path, StringComparison.OrdinalIgnoreCase);
            links.Add(new PublicShellNavLink(
                item.Id,
                string.IsNullOrWhiteSpace(item.Label) ? "Liên kết" : item.Label,
                href,
                item.OpenInNewTab,
                isCurrent));
        }
        return links;
    }

    private static string NormalizePath(string value)
    {
        var trimmed = (value ?? "/").TrimEnd('/');
        return trimmed.Length == 0 ? "/" : trimmed;
    }
}
