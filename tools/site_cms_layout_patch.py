from pathlib import Path

path = Path('src/DeLong.Web/Pages/Shared/_Layout.cshtml')
s = path.read_text()

def repl(old, new):
    global s
    if old not in s:
        raise SystemExit(f'anchor not found: {old[:120]!r}')
    s = s.replace(old, new, 1)

repl('@inject Microsoft.AspNetCore.Antiforgery.IAntiforgery Antiforgery\n', '@inject Microsoft.AspNetCore.Antiforgery.IAntiforgery Antiforgery\n@inject DeLong.Web.Features.Site.SiteContentService SiteContentService\n')
repl('    var isAdmin = Context.Request.Path.StartsWithSegments("/Admin");\n', '''    var isAdmin = Context.Request.Path.StartsWithSegments("/Admin");
    var publicSite = !isAdmin ? await SiteContentService.GetPublicAsync(Context.RequestAborted) : null;
    var publicSettings = publicSite?.Settings;
    var publicSiteName = string.IsNullOrWhiteSpace(publicSettings?.SiteName) ? "De Long Homestay" : publicSettings.SiteName;
    var publicTagline = string.IsNullOrWhiteSpace(publicSettings?.Tagline) ? "Long Thành · Đồng Nai" : publicSettings.Tagline;
''')
repl('    var canManageStaff = User.IsInRole("Admin");\n', '    var canManageStaff = User.IsInRole("Admin");\n    var canManageProperties = User.IsInRole("Admin");\n    var canManageSite = User.IsInRole("Admin") || User.IsInRole("Manager");\n')
repl('    <title>@ViewData["Title"] - De Long Homestay</title>\n', '''    @{
        var requestedTitle = ViewData["Title"]?.ToString();
        var documentTitle = isAdmin
            ? $"{requestedTitle} - De Long Homestay"
            : (Context.Request.Path == "/" ? (string.IsNullOrWhiteSpace(requestedTitle) ? publicSiteName : requestedTitle) : $"{requestedTitle} - {publicSiteName}");
        var metaDescription = ViewData["MetaDescription"]?.ToString() ?? publicSettings?.MetaDescription;
        var canonical = !isAdmin && !string.IsNullOrWhiteSpace(publicSettings?.CanonicalBaseUrl)
            ? $"{publicSettings.CanonicalBaseUrl.TrimEnd('/')}{Context.Request.Path}"
            : null;
    }
    <title>@documentTitle</title>
    @if (!isAdmin)
    {
        @if (!string.IsNullOrWhiteSpace(metaDescription)) { <meta name="description" content="@metaDescription" /> }
        <meta name="robots" content="@(publicSettings?.RobotsIndex == false ? "noindex,nofollow" : "index,follow")" />
        @if (!string.IsNullOrWhiteSpace(canonical)) { <link rel="canonical" href="@canonical" /> }
        @if (!string.IsNullOrWhiteSpace(publicSettings?.FaviconUrl)) { <link rel="icon" href="@publicSettings.FaviconUrl" type="image/png" /> }
        @if (!string.IsNullOrWhiteSpace(publicSettings?.GoogleSiteVerification)) { <meta name="google-site-verification" content="@publicSettings.GoogleSiteVerification" /> }
        <meta property="og:title" content="@(string.IsNullOrWhiteSpace(publicSettings?.OgTitle) ? documentTitle : publicSettings.OgTitle)" />
        @if (!string.IsNullOrWhiteSpace(publicSettings?.OgDescription ?? metaDescription)) { <meta property="og:description" content="@(publicSettings?.OgDescription ?? metaDescription)" /> }
        @if (!string.IsNullOrWhiteSpace(publicSettings?.OgImageUrl)) { <meta property="og:image" content="@publicSettings.OgImageUrl" /> }
        @if (!string.IsNullOrWhiteSpace(canonical)) { <meta property="og:url" content="@canonical" /> }
        <meta property="og:type" content="website" />
    }
''')
repl('    <link rel="stylesheet" href="~/css/public.css" asp-append-version="true" />\n', '    <link rel="stylesheet" href="~/css/public.css" asp-append-version="true" />\n    @if (!isAdmin) { <link rel="stylesheet" href="/site/custom.css" /> }\n')
repl('                @if (canViewRooms || canManageSettings || canManageStaff)\n', '                @if (canViewRooms || canManageSettings || canManageStaff || canManageProperties || canManageSite)\n')
settings_anchor = '''                        @if (canManageSettings)
                        {
                            <a class="@NavClass("/Admin/Settings")" asp-page="/Admin/Settings/Index" asp-route-propertyId="@propertyId"><svg><use href="#i-settings" /></svg><span>Cấu hình</span></a>
                        }
'''
settings_new = settings_anchor + '''                        @if (canManageSite)
                        {
                            <a class="@NavClass("/Admin/Site")" asp-page="/Admin/Site/Index" asp-route-propertyId="@propertyId"><svg><use href="#i-settings" /></svg><span>Website</span></a>
                        }
                        @if (canManageProperties)
                        {
                            <a class="@NavClass("/Admin/Properties")" asp-page="/Admin/Properties/Index"><svg><use href="#i-bed" /></svg><span>Cơ sở</span></a>
                        }
'''
repl(settings_anchor, settings_new)
repl('            <a class="public-site-brand" asp-page="/Index"><span class="brand-mark">DL</span><span><strong>De Long Homestay</strong><small>Long Thành · Đồng Nai</small></span></a>\n', '''            <a class="public-site-brand" asp-page="/Index">
                @if (!string.IsNullOrWhiteSpace(publicSettings?.LogoUrl)) { <img class="public-site-brand-logo" src="@publicSettings.LogoUrl" alt="@publicSiteName" /> }
                else { <span class="brand-mark">DL</span> }
                <span><strong>@publicSiteName</strong><small>@publicTagline</small></span>
            </a>
''')
repl('                <a asp-page="/Booking/Index">Đặt phòng</a>\n', '                <a asp-page="/Booking/Index">Đặt phòng</a>\n                <a asp-page="/Booking/Lookup">Tra cứu</a>\n')
repl('@await RenderSectionAsync("Scripts", required: false)\n</body>', '@await RenderSectionAsync("Scripts", required: false)\n@if (!isAdmin) { <script src="/site/custom.js"></script> }\n</body>')
path.write_text(s)

css = Path('src/DeLong.Web/wwwroot/css/public.css')
cs = css.read_text()
marker = '.public-site-brand-logo'
if marker not in cs:
    cs += '\n.public-site-brand-logo{width:46px;height:46px;display:block;object-fit:contain;border-radius:12px}.public-site-brand:has(.public-site-brand-logo) .brand-mark{display:none}\n'
css.write_text(cs)
