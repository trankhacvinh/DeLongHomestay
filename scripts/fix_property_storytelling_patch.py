from pathlib import Path

p = Path('scripts/property_storytelling_patch.py')
text = p.read_text()
start = text.index('# Public property page: render FAQ, location, policy blocks and load styling.')
end = text.index('# Similar rooms in room detail.')
replacement = r"""# Public property page: render FAQ, location, policy blocks and load styling.
path = "src/DeLong.Web/Pages/Index.cshtml"
patch(
    path,
    '    <link rel="stylesheet" href="~/css/hospitality-editorial.css" asp-append-version="true" />\n',
    '    <link rel="stylesheet" href="~/css/hospitality-editorial.css" asp-append-version="true" />\n    <link rel="stylesheet" href="~/css/property-storytelling.css" asp-append-version="true" />\n',
)
storytelling_cases = '''                case "Faq":
                {
                    var faqItems = block.Content["items"] as JsonArray;
                    <section class="public-section public-story-faq variant-@(block.Variant)"><div class="public-container"><div class="public-section-head"><div><span class="public-eyebrow">@Text(block.Content, "eyebrow", "THÔNG TIN")</span><h2>@Text(block.Content, "title", "Câu hỏi thường gặp")</h2></div></div>@if (faqItems is not null) { <div class="public-faq-list">@foreach (var item in faqItems.OfType<JsonObject>()) { var question = Text(item, "question"); var answer = Text(item, "answer"); if (!string.IsNullOrWhiteSpace(question) && !string.IsNullOrWhiteSpace(answer)) { <details><summary>@question<span>+</span></summary><p>@answer</p></details> } }</div> }</div></section>
                    break;
                }
                case "Location":
                {
                    var nearby = block.Content["nearby"] as JsonArray;
                    var address = Text(block.Content, "address", Model.SiteSettings?.Address ?? string.Empty);
                    var mapUrl = Text(block.Content, "mapUrl", Model.SiteSettings?.GoogleMapsUrl ?? string.Empty);
                    var embedUrl = Text(block.Content, "embedUrl");
                    <section class="public-section public-story-location variant-@(block.Variant)"><div class="public-container"><div class="public-location-shell"><div class="public-location-copy"><span class="public-eyebrow">@Text(block.Content, "eyebrow", "VỊ TRÍ")</span><h2>@Text(block.Content, "title", $"Tìm đường đến {siteName}")</h2>@if (!string.IsNullOrWhiteSpace(Text(block.Content, "body"))) { <p>@Text(block.Content, "body")</p> }@if (!string.IsNullOrWhiteSpace(address)) { <address>@address</address> }@if (nearby is not null) { <div class="public-location-nearby">@foreach (var item in nearby) { if (!string.IsNullOrWhiteSpace(item?.ToString())) { <span>@item</span> } }</div> }@if (!string.IsNullOrWhiteSpace(mapUrl)) { <a class="public-btn public-btn-primary" href="@mapUrl" target="_blank" rel="noopener">Mở Google Maps ↗</a> }</div><div class="public-location-map">@if (!string.IsNullOrWhiteSpace(embedUrl)) { <iframe src="@embedUrl" loading="lazy" referrerpolicy="no-referrer-when-downgrade" title="Bản đồ @siteName"></iframe> } else { <div class="public-location-map-fallback"><span>VỊ TRÍ</span><strong>@siteName</strong><small>@(string.IsNullOrWhiteSpace(address) ? "Thêm địa chỉ hoặc bản đồ trong CMS" : address)</small></div> }</div></div></div></section>
                    break;
                }
                case "PolicyGrid":
                {
                    var policies = block.Content["items"] as JsonArray;
                    var policyIndex = 0;
                    <section class="public-section public-story-policies variant-@(block.Variant)"><div class="public-container"><div class="public-section-head"><div><span class="public-eyebrow">@Text(block.Content, "eyebrow", "LƯU TRÚ")</span><h2>@Text(block.Content, "title", "Quy định lưu trú")</h2></div></div>@if (policies is not null) { <div class="public-policy-grid">@foreach (var item in policies.OfType<JsonObject>()) { var policyTitle = Text(item, "title"); var policyBody = Text(item, "body"); if (!string.IsNullOrWhiteSpace(policyTitle) || !string.IsNullOrWhiteSpace(policyBody)) { policyIndex++; <article><span>@policyIndex.ToString("00")</span><h3>@policyTitle</h3><p>@policyBody</p></article> } }</div> }</div></section>
                    break;
                }
'''
p = Path(path)
page = p.read_text()
property_marker = 'alt="Không gian tại @siteName"'
property_region = page.find(property_marker)
if property_region < 0:
    raise SystemExit('Property FeatureGrid marker not found')
insert_at = page.find('                case "RichText":', property_region)
if insert_at < 0:
    raise SystemExit('Property RichText case not found')
page = page[:insert_at] + storytelling_cases + page[insert_at:]
p.write_text(page)

"""
p.write_text(text[:start] + replacement + text[end:])
