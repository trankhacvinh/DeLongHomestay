from pathlib import Path

program = Path('src/DeLong.Web/Program.cs')
s = program.read_text()
anchor = 'app.MapSiteContentEndpoints();\n'
if 'app.MapPublicSeoEndpoints();' not in s:
    if anchor not in s:
        raise SystemExit('Program SEO anchor not found')
    s = s.replace(anchor, anchor + 'app.MapPublicSeoEndpoints();\n', 1)
program.write_text(s)

layout = Path('src/DeLong.Web/Pages/Shared/_Layout.cshtml')
s = layout.read_text()
anchor = '        <meta property="og:type" content="website" />\n'
if 'application/ld+json' not in s:
    if anchor not in s:
        raise SystemExit('Layout JSON-LD anchor not found')
    insert = '''        <meta property="og:type" content="website" />
        @{
            var structuredData = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["@context"] = "https://schema.org",
                ["@type"] = "LodgingBusiness",
                ["name"] = publicSiteName,
                ["description"] = metaDescription,
                ["telephone"] = publicSettings?.Phone,
                ["email"] = publicSettings?.Email,
                ["address"] = publicSettings?.Address,
                ["url"] = publicSettings?.CanonicalBaseUrl,
                ["logo"] = publicSettings?.LogoUrl,
                ["image"] = publicSettings?.OgImageUrl
            });
        }
        <script type="application/ld+json">@Html.Raw(structuredData)</script>
'''
    s = s.replace(anchor, insert, 1)
layout.write_text(s)
