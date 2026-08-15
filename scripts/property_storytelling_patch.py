from pathlib import Path


def patch(path, old, new):
    p = Path(path)
    text = p.read_text()
    if old not in text:
        raise SystemExit(f"Patch anchor not found: {path}\n{old[:160]}")
    p.write_text(text.replace(old, new, 1))


# CMS backend: allow storytelling block types and validate location URLs.
path = "src/DeLong.Web/Features/Site/SiteContentService.cs"
patch(
    path,
    '["Hero", "AvailabilitySearch", "BranchGrid", "RoomGrid", "FeatureGrid", "RichText", "Cta"];',
    '["Hero", "AvailabilitySearch", "BranchGrid", "RoomGrid", "FeatureGrid", "Faq", "Location", "PolicyGrid", "RichText", "Cta"];',
)
patch(
    path,
    '        if (request.Type == "RichText" && json["html"] is JsonValue value && value.TryGetValue<string>(out var html))\n',
    '        if (request.Type == "Location")\n        {\n            var mapUrl = json["mapUrl"]?.GetValue<string>();\n            var embedUrl = json["embedUrl"]?.GetValue<string>();\n            if (!IsOptionalHttpUrl(mapUrl) || !IsOptionalHttpUrl(embedUrl))\n                return (null, new("validation", "Đường dẫn bản đồ phải là URL http hoặc https hợp lệ."));\n        }\n        if (request.Type == "RichText" && json["html"] is JsonValue value && value.TryGetValue<string>(out var html))\n',
)
patch(
    path,
    '        "FeatureGrid" => "Điểm nổi bật",\n        "Cta" => "Kêu gọi hành động",\n',
    '        "FeatureGrid" => "Điểm nổi bật",\n        "Faq" => "Câu hỏi thường gặp",\n        "Location" => "Vị trí & chỉ đường",\n        "PolicyGrid" => "Quy định lưu trú",\n        "Cta" => "Kêu gọi hành động",\n',
)

# Admin CMS JS: friendly editors for FAQ, location and policy blocks.
path = "src/DeLong.Web/wwwroot/js/pages/admin-site-cms.js"
patch(
    path,
    "        { value: 'FeatureGrid', label: 'Nội dung + điểm nổi bật' },\n        { value: 'RichText', label: 'Nội dung tự do' },",
    "        { value: 'FeatureGrid', label: 'Nội dung + điểm nổi bật' },\n        { value: 'Faq', label: 'Câu hỏi thường gặp' },\n        { value: 'Location', label: 'Vị trí & chỉ đường' },\n        { value: 'PolicyGrid', label: 'Quy định lưu trú' },\n        { value: 'RichText', label: 'Nội dung tự do' },",
)
patch(
    path,
    "        if (type === 'FeatureGrid') return { eyebrow: '', title: '', body: '', items: [], imageUrl: '' };\n        if (type === 'Cta') return { title: '', body: '', buttonText: 'Đặt phòng', buttonUrl: '/booking' };",
    "        if (type === 'FeatureGrid') return { eyebrow: '', title: '', body: '', items: [], imageUrl: '' };\n        if (type === 'Faq') return { eyebrow: 'THÔNG TIN', title: 'Câu hỏi thường gặp', items: [{ question: '', answer: '' }] };\n        if (type === 'Location') return { eyebrow: 'VỊ TRÍ', title: 'Tìm đường đến cơ sở', body: '', address: '', mapUrl: '', embedUrl: '', nearby: [] };\n        if (type === 'PolicyGrid') return { eyebrow: 'LƯU TRÚ', title: 'Quy định lưu trú', items: [{ title: '', body: '' }] };\n        if (type === 'Cta') return { title: '', body: '', buttonText: 'Đặt phòng', buttonUrl: '/booking' };",
)
patch(
    path,
    "                sectionForm: { type: 'Hero', name: '', variant: 'split', isVisible: true, content: defaultContent('Hero'), itemsText: '' },",
    "                sectionForm: { type: 'Hero', name: '', variant: 'split', isVisible: true, content: defaultContent('Hero'), itemsText: '', nearbyText: '' },",
)
patch(
    path,
    "                if (type === 'FeatureGrid') return ['split', 'stacked', 'icon-grid', 'dark-band', 'editorial'];\n                if (type === 'RichText') return ['narrow', 'wide', 'editorial'];",
    "                if (type === 'FeatureGrid') return ['split', 'stacked', 'icon-grid', 'dark-band', 'editorial'];\n                if (type === 'Faq') return ['accordion', 'two-column'];\n                if (type === 'Location') return ['split', 'card'];\n                if (type === 'PolicyGrid') return ['grid-3', 'list'];\n                if (type === 'RichText') return ['narrow', 'wide', 'editorial'];",
)
patch(
    path,
    "                this.sectionForm = { type, name: '', variant: this.variantsFor(type)[0], isVisible: true, content: defaultContent(type), itemsText: '' };",
    "                this.sectionForm = { type, name: '', variant: this.variantsFor(type)[0], isVisible: true, content: defaultContent(type), itemsText: '', nearbyText: '' };",
)
patch(
    path,
    "                    content: Object.assign(defaultContent(section.type), content),\n                    itemsText: Array.isArray(content.items) ? content.items.join('\\n') : ''\n",
    "                    content: Object.assign(defaultContent(section.type), content),\n                    itemsText: section.type === 'FeatureGrid' && Array.isArray(content.items) ? content.items.join('\\n') : '',\n                    nearbyText: Array.isArray(content.nearby) ? content.nearby.join('\\n') : ''\n",
)
patch(
    path,
    "                this.sectionForm.itemsText = '';\n            },\n            sectionPayload() {\n                const content = Object.assign({}, this.sectionForm.content);\n                if (this.sectionForm.type === 'FeatureGrid') content.items = this.sectionForm.itemsText.split('\\n').map(x => x.trim()).filter(Boolean);\n",
    "                this.sectionForm.itemsText = '';\n                this.sectionForm.nearbyText = '';\n            },\n            addFaqItem() { this.sectionForm.content.items.push({ question: '', answer: '' }); },\n            removeFaqItem(index) { if (this.sectionForm.content.items.length > 1) this.sectionForm.content.items.splice(index, 1); },\n            addPolicyItem() { this.sectionForm.content.items.push({ title: '', body: '' }); },\n            removePolicyItem(index) { if (this.sectionForm.content.items.length > 1) this.sectionForm.content.items.splice(index, 1); },\n            sectionPayload() {\n                const content = Object.assign({}, this.sectionForm.content);\n                if (this.sectionForm.type === 'FeatureGrid') content.items = this.sectionForm.itemsText.split('\\n').map(x => x.trim()).filter(Boolean);\n                if (this.sectionForm.type === 'Location') content.nearby = this.sectionForm.nearbyText.split('\\n').map(x => x.trim()).filter(Boolean);\n",
)

# Admin CMS markup.
path = "src/DeLong.Web/Pages/Admin/Site/Index.cshtml"
patch(
    path,
    '    <link rel="stylesheet" href="~/css/site-cms.css" asp-append-version="true" />\n',
    '    <link rel="stylesheet" href="~/css/site-cms.css" asp-append-version="true" />\n    <link rel="stylesheet" href="~/css/storytelling-admin.css" asp-append-version="true" />\n',
)
anchor = '''                <div class="home-content-fields" v-else-if="sectionForm.type === 'FeatureGrid'"><div class="field"><label>Eyebrow</label><input v-model="sectionForm.content.eyebrow" /></div><div class="field"><label>Tiêu đề</label><textarea v-model="sectionForm.content.title" rows="2"></textarea></div><div class="field"><label>Mô tả</label><textarea v-model="sectionForm.content.body" rows="4"></textarea></div><div class="field"><label>Các mục (mỗi dòng một mục)</label><textarea v-model="sectionForm.itemsText" rows="5"></textarea></div><div class="field"><label>Ảnh kể chuyện</label><div class="asset-input-row"><input v-model="sectionForm.content.imageUrl" placeholder="Để trống = ảnh cover cơ sở" /><label class="btn btn-light btn-sm file-btn">Tải ảnh<input type="file" accept="image/png,image/jpeg,image/webp" v-on:change="uploadSectionImage($event)" /></label></div><small>Layout <strong>split</strong> hoặc <strong>editorial</strong> sẽ đặt ảnh cạnh nội dung.</small></div></div>
                <div class="home-content-fields" v-else-if="sectionForm.type === 'RichText'">'''
replacement = '''                <div class="home-content-fields" v-else-if="sectionForm.type === 'FeatureGrid'"><div class="field"><label>Eyebrow</label><input v-model="sectionForm.content.eyebrow" /></div><div class="field"><label>Tiêu đề</label><textarea v-model="sectionForm.content.title" rows="2"></textarea></div><div class="field"><label>Mô tả</label><textarea v-model="sectionForm.content.body" rows="4"></textarea></div><div class="field"><label>Các mục (mỗi dòng một mục)</label><textarea v-model="sectionForm.itemsText" rows="5"></textarea></div><div class="field"><label>Ảnh kể chuyện</label><div class="asset-input-row"><input v-model="sectionForm.content.imageUrl" placeholder="Để trống = ảnh cover cơ sở" /><label class="btn btn-light btn-sm file-btn">Tải ảnh<input type="file" accept="image/png,image/jpeg,image/webp" v-on:change="uploadSectionImage($event)" /></label></div><small>Layout <strong>split</strong> hoặc <strong>editorial</strong> sẽ đặt ảnh cạnh nội dung.</small></div></div>
                <div class="home-content-fields" v-else-if="sectionForm.type === 'Faq'">
                    <div class="field"><label>Eyebrow</label><input v-model="sectionForm.content.eyebrow" /></div><div class="field"><label>Tiêu đề</label><input v-model="sectionForm.content.title" /></div>
                    <div class="storytelling-repeat-list"><div class="storytelling-repeat-item" v-for="(item, index) in sectionForm.content.items" v-bind:key="index"><div class="field"><label>Câu hỏi {{ index + 1 }}</label><input v-model.trim="item.question" maxlength="300" /></div><div class="field"><label>Trả lời</label><textarea v-model.trim="item.answer" rows="3" maxlength="1600"></textarea></div><button class="btn btn-light btn-sm" type="button" v-on:click="removeFaqItem(index)" v-bind:disabled="sectionForm.content.items.length <= 1">Xóa câu</button></div></div>
                    <button class="btn btn-light btn-sm storytelling-add" type="button" v-on:click="addFaqItem">+ Thêm câu hỏi</button>
                </div>
                <div class="home-content-fields" v-else-if="sectionForm.type === 'Location'">
                    <div class="form-grid"><div class="field"><label>Eyebrow</label><input v-model="sectionForm.content.eyebrow" /></div><div class="field"><label>Tiêu đề</label><input v-model="sectionForm.content.title" /></div></div>
                    <div class="field"><label>Mô tả</label><textarea v-model="sectionForm.content.body" rows="3"></textarea></div>
                    <div class="field"><label>Địa chỉ</label><input v-model="sectionForm.content.address" v-bind:placeholder="settings.address || 'Dùng địa chỉ trong Thông tin & SEO'" /><small>Để trống sẽ dùng địa chỉ của cơ sở.</small></div>
                    <div class="field"><label>Link Google Maps</label><input v-model="sectionForm.content.mapUrl" v-bind:placeholder="settings.googleMapsUrl || 'https://maps.google.com/…'" /><small>Dùng cho nút Mở Google Maps. Để trống sẽ dùng link trong cấu hình cơ sở.</small></div>
                    <div class="field"><label>Google Maps embed URL (tùy chọn)</label><input v-model="sectionForm.content.embedUrl" placeholder="https://www.google.com/maps/embed?pb=…" /><small>Dán URL trong thuộc tính <code>src</code> của mã nhúng Google Maps.</small></div>
                    <div class="field"><label>Điểm gần đây (mỗi dòng một mục)</label><textarea v-model="sectionForm.nearbyText" rows="5" placeholder="Sân bay Long Thành · 15 phút&#10;Trung tâm Long Thành · 8 phút"></textarea></div>
                </div>
                <div class="home-content-fields" v-else-if="sectionForm.type === 'PolicyGrid'">
                    <div class="field"><label>Eyebrow</label><input v-model="sectionForm.content.eyebrow" /></div><div class="field"><label>Tiêu đề</label><input v-model="sectionForm.content.title" /></div>
                    <div class="storytelling-repeat-list"><div class="storytelling-repeat-item" v-for="(item, index) in sectionForm.content.items" v-bind:key="index"><div class="field"><label>Tiêu đề mục {{ index + 1 }}</label><input v-model.trim="item.title" maxlength="200" placeholder="Nhận phòng / Trả phòng" /></div><div class="field"><label>Nội dung</label><textarea v-model.trim="item.body" rows="3" maxlength="1200"></textarea></div><button class="btn btn-light btn-sm" type="button" v-on:click="removePolicyItem(index)" v-bind:disabled="sectionForm.content.items.length <= 1">Xóa mục</button></div></div>
                    <button class="btn btn-light btn-sm storytelling-add" type="button" v-on:click="addPolicyItem">+ Thêm quy định</button>
                </div>
                <div class="home-content-fields" v-else-if="sectionForm.type === 'RichText'">'''
patch(path, anchor, replacement)

# Public property page: render FAQ, location, policy blocks and load styling.
path = "src/DeLong.Web/Pages/Index.cshtml"
patch(
    path,
    '    <link rel="stylesheet" href="~/css/hospitality-editorial.css" asp-append-version="true" />\n',
    '    <link rel="stylesheet" href="~/css/hospitality-editorial.css" asp-append-version="true" />\n    <link rel="stylesheet" href="~/css/property-storytelling.css" asp-append-version="true" />\n',
)
anchor = '''                case "RichText":
                {
                    <section class="public-section public-cms-rich variant-@(block.Variant)"><div class="public-container"><div class="public-cms-rich-inner">@Html.Raw(Text(block.Content, "html"))</div></div></section>
                    break;
                }
                case "Cta":'''
replacement = '''                case "Faq":
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
                    <section class="public-section public-story-policies variant-@(block.Variant)"><div class="public-container"><div class="public-section-head"><div><span class="public-eyebrow">@Text(block.Content, "eyebrow", "LƯU TRÚ")</span><h2>@Text(block.Content, "title", "Quy định lưu trú")</h2></div></div>@if (policies is not null) { <div class="public-policy-grid">@foreach (var item in policies.OfType<JsonObject>()) { var policyTitle = Text(item, "title"); var policyBody = Text(item, "body"); if (!string.IsNullOrWhiteSpace(policyTitle) || !string.IsNullOrWhiteSpace(policyBody)) { <article><span>@(policies.IndexOf(item) + 1).ToString("00")</span><h3>@policyTitle</h3><p>@policyBody</p></article> } }</div> }</div></section>
                    break;
                }
                case "RichText":
                {
                    <section class="public-section public-cms-rich variant-@(block.Variant)"><div class="public-container"><div class="public-cms-rich-inner">@Html.Raw(Text(block.Content, "html"))</div></div></section>
                    break;
                }
                case "Cta":'''
p = Path(path)
text = p.read_text()
first = text.find(anchor)
second = text.find(anchor, first + len(anchor))
if second < 0:
    raise SystemExit("Property switch anchor not found")
p.write_text(text[:second] + text[second:].replace(anchor, replacement, 1))

# Similar rooms in room detail.
path = "src/DeLong.Web/Pages/Rooms/Details.cshtml.cs"
patch(
    path,
    '    public string? SiteSlug { get; private set; }\n',
    '    public string? SiteSlug { get; private set; }\n    public IReadOnlyList<PublicRoomCardDto> SimilarRooms { get; private set; } = [];\n',
)
patch(
    path,
    '        Room = room;\n        return Page();\n',
    '        Room = room;\n        var catalog = await publicRoomContentService.GetCatalogAsync(property.Id, cancellationToken);\n        SimilarRooms = catalog.Rooms\n            .Where(x => x.Id != room.Id)\n            .OrderByDescending(x => x.Amenities.Intersect(room.Amenities, StringComparer.OrdinalIgnoreCase).Count())\n            .ThenBy(x => Math.Abs(x.Capacity - room.Capacity))\n            .ThenBy(x => Math.Abs(x.QuickFromPrice - room.QuickFromPrice))\n            .Take(3)\n            .ToList();\n        return Page();\n',
)

path = "src/DeLong.Web/Pages/Rooms/Details.cshtml"
patch(
    path,
    '    <link rel="stylesheet" href="~/css/hospitality-room.css" asp-append-version="true" />\n',
    '    <link rel="stylesheet" href="~/css/hospitality-room.css" asp-append-version="true" />\n    <link rel="stylesheet" href="~/css/property-storytelling.css" asp-append-version="true" />\n',
)
patch(
    path,
    '    </section>\n</main>\n\n@section Scripts {',
    '''    </section>
    @if (Model.SimilarRooms.Count > 0)
    {
        <section class="public-section public-similar-rooms"><div class="public-container"><div class="public-section-head"><div><span class="public-eyebrow">PHÒNG TƯƠNG TỰ</span><h2>Bạn cũng có thể thích</h2></div><a class="public-text-link" href="@PublicUrlBuilder.Rooms(Model.SiteSlug)">Xem tất cả phòng →</a></div><div class="public-room-grid">@foreach (var room in Model.SimilarRooms) { <article class="public-room-card public-room-card-content"><a class="public-room-art" href="@PublicUrlBuilder.Room(Model.SiteSlug!, room.Slug)">@if (!string.IsNullOrWhiteSpace(room.CoverCardUrl)) { <img class="public-room-cover-image" src="@room.CoverCardUrl" alt="@room.Name" loading="lazy" /><span class="public-room-image-scrim"></span> } else { <span class="public-room-poster"><small>@Model.PropertyName · @room.Code</small><strong>@room.Name</strong></span> }<span class="public-room-image-title"><small>@Model.PropertyName</small><strong>@room.Name</strong></span></a><div class="public-room-body"><div><h3><a href="@PublicUrlBuilder.Room(Model.SiteSlug!, room.Slug)">@room.Name</a></h3><p>@(string.IsNullOrWhiteSpace(room.ShortDescription) ? $"{room.Capacity} người" : room.ShortDescription)</p></div><div class="public-room-conversion"><div class="public-room-price"><small>Giá từ</small><strong>@room.QuickFromPrice.ToString("N0")đ</strong></div><a class="public-room-book-now" href="@PublicUrlBuilder.Booking(Model.SiteSlug!, room: room.Code)">Đặt ngay →</a></div></div></article> }</div></div></section>
    }
</main>

@section Scripts {''',
)

# Regression coverage for the new block types.
path = "tests/DeLong.Tests/Integration/SiteCmsAndLookupTests.cs"
patch(
    path,
    '        Assert.DoesNotContain("<script", sanitizedHtml, StringComparison.OrdinalIgnoreCase);\n\n        var (updated, updateError)',
    '''        Assert.DoesNotContain("<script", sanitizedHtml, StringComparison.OrdinalIgnoreCase);

        foreach (var request in new[]
        {
            new SaveHomeSectionRequest { Type = "Faq", Name = "FAQ", Variant = "accordion", ContentJson = "{\\"title\\":\\"FAQ\\",\\"items\\":[{\\"question\\":\\"Có chỗ đậu xe?\\",\\"answer\\":\\"Vui lòng liên hệ cơ sở để xác nhận.\\"}]}" },
            new SaveHomeSectionRequest { Type = "Location", Name = "Vị trí", Variant = "split", ContentJson = "{\\"title\\":\\"Tìm đường\\",\\"mapUrl\\":\\"https://maps.google.com/\\",\\"nearby\\":[\\"Trung tâm · 5 phút\\"]}" },
            new SaveHomeSectionRequest { Type = "PolicyGrid", Name = "Quy định", Variant = "grid-3", ContentJson = "{\\"title\\":\\"Quy định lưu trú\\",\\"items\\":[{\\"title\\":\\"Nhận phòng\\",\\"body\\":\\"Theo giờ đã xác nhận.\\"}]}" }
        })
        {
            var (storyBlock, storyError) = await siteService.CreateSectionAsync(created.Id, request);
            Assert.Null(storyError);
            Assert.NotNull(storyBlock);
        }

        var (updated, updateError)''',
)

Path("src/DeLong.Web/wwwroot/css/storytelling-admin.css").write_text(
    ".storytelling-repeat-list{display:grid;gap:14px;margin-top:14px}.storytelling-repeat-item{display:grid;gap:12px;padding:16px;border:1px solid var(--border);border-radius:16px;background:var(--surface-soft,#f7faf9)}.storytelling-repeat-item .btn{justify-self:start}.storytelling-add{margin-top:12px}.home-content-fields code{font-size:.86em}\n"
)

Path("src/DeLong.Web/wwwroot/css/property-storytelling.css").write_text(
    """.public-story-faq .public-section-head,.public-story-policies .public-section-head{margin-bottom:28px}.public-faq-list{max-width:920px;border-top:1px solid var(--public-border,#dbe3df)}.public-faq-list details{border-bottom:1px solid var(--public-border,#dbe3df)}.public-faq-list summary{display:flex;justify-content:space-between;gap:24px;align-items:center;padding:20px 0;font-size:clamp(1.05rem,1.5vw,1.25rem);font-weight:750;cursor:pointer;list-style:none}.public-faq-list summary::-webkit-details-marker{display:none}.public-faq-list summary span{font-size:1.5rem;font-weight:400;transition:transform .2s ease}.public-faq-list details[open] summary span{transform:rotate(45deg)}.public-faq-list details p{max-width:760px;margin:0;padding:0 48px 22px 0;color:var(--public-muted,#667874);line-height:1.75}.public-story-faq.variant-two-column .public-faq-list{display:grid;grid-template-columns:1fr 1fr;gap:0 32px;max-width:none;border-top:0}.public-story-faq.variant-two-column .public-faq-list details{border-top:1px solid var(--public-border,#dbe3df)}
.public-location-shell{display:grid;grid-template-columns:minmax(0,.85fr) minmax(420px,1.15fr);gap:48px;align-items:stretch}.public-location-copy{padding:28px 0}.public-location-copy h2{margin:8px 0 16px;font-size:clamp(2rem,4vw,3.6rem);line-height:1.02}.public-location-copy>p{color:var(--public-muted,#667874);line-height:1.75;max-width:620px}.public-location-copy address{margin:24px 0 18px;font-style:normal;font-size:1.08rem;font-weight:700}.public-location-nearby{display:grid;grid-template-columns:1fr 1fr;gap:8px;margin:18px 0 26px}.public-location-nearby span{padding:11px 14px;border:1px solid var(--public-border,#dbe3df);border-radius:12px;background:#fff}.public-location-map{min-height:380px;border-radius:28px;overflow:hidden;background:linear-gradient(135deg,#e8f1ee,#d3e2de);border:1px solid var(--public-border,#dbe3df)}.public-location-map iframe{width:100%;height:100%;min-height:380px;border:0}.public-location-map-fallback{height:100%;min-height:380px;display:flex;flex-direction:column;justify-content:flex-end;padding:34px;background:radial-gradient(circle at 80% 20%,rgba(255,255,255,.65),transparent 30%),linear-gradient(145deg,#e6f0ed,#c9dcd6)}.public-location-map-fallback span{letter-spacing:.18em;font-size:.75rem;font-weight:800;color:#176c69}.public-location-map-fallback strong{font-size:2rem;margin:8px 0}.public-location-map-fallback small{max-width:420px;color:#536864}
.public-policy-grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:18px}.public-policy-grid article{padding:24px;border:1px solid var(--public-border,#dbe3df);border-radius:20px;background:#fff}.public-policy-grid article>span{display:inline-flex;width:38px;height:38px;align-items:center;justify-content:center;border-radius:12px;background:#e8f4f1;color:#176c69;font-weight:800}.public-policy-grid h3{margin:18px 0 8px;font-size:1.2rem}.public-policy-grid p{margin:0;color:var(--public-muted,#667874);line-height:1.65}.public-story-policies.variant-list .public-policy-grid{grid-template-columns:1fr}.public-story-policies.variant-list .public-policy-grid article{display:grid;grid-template-columns:52px minmax(160px,.35fr) 1fr;align-items:start;gap:18px}.public-story-policies.variant-list .public-policy-grid h3{margin:8px 0}.public-story-policies.variant-list .public-policy-grid p{margin:8px 0}
.public-similar-rooms{padding-top:36px;border-top:1px solid var(--public-border,#dbe3df)}.public-similar-rooms .public-room-grid{grid-template-columns:repeat(3,minmax(0,1fr))}
@media(max-width:900px){.public-location-shell{grid-template-columns:1fr;gap:24px}.public-location-map,.public-location-map iframe,.public-location-map-fallback{min-height:300px}.public-policy-grid,.public-similar-rooms .public-room-grid{grid-template-columns:1fr 1fr}.public-story-faq.variant-two-column .public-faq-list{grid-template-columns:1fr}}
@media(max-width:620px){.public-location-nearby,.public-policy-grid,.public-similar-rooms .public-room-grid{grid-template-columns:1fr}.public-story-policies.variant-list .public-policy-grid article{display:block}.public-story-policies.variant-list .public-policy-grid h3{margin-top:16px}}
"""
)
