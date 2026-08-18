(function (global) {
    const TYPES = {
        icon: { label: 'Icon + nội dung', icon: '✦' },
        video: { label: 'Video', icon: '▶' },
        map: { label: 'Bản đồ', icon: '⌖' },
        accordion: { label: 'Accordion', icon: '☰' },
        testimonial: { label: 'Đánh giá khách', icon: '❝' },
        promo: { label: 'Ưu đãi / Giá', icon: '₫' }
    };

    const esc = value => String(value ?? '').replace(/[&<>"']/g, char => ({
        '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
    }[char]));
    const text = (root, selector) => root?.querySelector(selector)?.textContent?.trim() || '';
    const attr = (root, selector, name) => root?.querySelector(selector)?.getAttribute(name) || '';
    const unsafeUrl = value => /^(?:javascript|data|vbscript):/i.test(String(value || '').trim());
    const safeUrl = value => {
        const url = String(value || '').trim();
        return url && !unsafeUrl(url) ? url : '';
    };
    const nl2br = value => esc(value).replace(/\r?\n/g, '<br>');
    const clamp = (value, min, max, fallback) => {
        const number = Number(value);
        return Number.isFinite(number) ? Math.max(min, Math.min(max, number)) : fallback;
    };

    function defaultModel(type) {
        if (type === 'icon') return { icon: '✦', title: 'Điểm nổi bật', body: 'Mô tả ngắn gọn về tiện ích hoặc giá trị nổi bật.', linkText: '', url: '' };
        if (type === 'video') return { title: 'Video giới thiệu', url: '', caption: '', posterUrl: '' };
        if (type === 'map') return { title: 'Vị trí của chúng tôi', address: 'Nhập địa chỉ', buttonText: 'Mở Google Maps', url: '' };
        if (type === 'accordion') return { openFirst: true, items: [
            { title: 'Nhận phòng lúc mấy giờ?', body: 'Bạn có thể chỉnh câu trả lời ngay tại đây.' },
            { title: 'Có chỗ để xe không?', body: 'Thêm các câu hỏi thường gặp phù hợp với cơ sở.' }
        ] };
        if (type === 'testimonial') return { quote: 'Không gian sạch sẽ, thuận tiện và hỗ trợ rất nhanh.', name: 'Khách lưu trú', role: 'Đánh giá gần đây', imageUrl: '', rating: 5 };
        return { badge: 'Ưu đãi', title: 'Gói lưu trú đặc biệt', body: 'Thêm mô tả ngắn cho ưu đãi hoặc mức giá nổi bật.', price: '690.000đ', suffix: '/ đêm', buttonText: 'Đặt ngay', url: '', tone: 'soft' };
    }

    function render(type, model) {
        const m = Object.assign(defaultModel(type), model || {});
        if (type === 'icon') {
            const url = safeUrl(m.url), link = url && m.linkText ? `<a class="dl-advanced-icon-link" href="${esc(url)}">${esc(m.linkText)}</a>` : '';
            return `<div class="dl-advanced-icon"><div class="dl-advanced-icon-symbol">${esc(m.icon || '✦')}</div><div class="dl-advanced-icon-copy"><strong>${esc(m.title)}</strong><p>${nl2br(m.body)}</p>${link}</div></div>`;
        }
        if (type === 'video') {
            const url = safeUrl(m.url), poster = safeUrl(m.posterUrl), direct = /\.(mp4|webm|ogg)(?:[?#].*)?$/i.test(url);
            const media = direct ? `<video class="dl-advanced-video-player" src="${esc(url)}"${poster ? ` poster="${esc(poster)}"` : ''} controls preload="metadata" playsinline></video>` : `<a class="dl-advanced-video-link" href="${esc(url || '#')}">${poster ? `<img class="dl-advanced-video-poster" src="${esc(poster)}" alt="${esc(m.title || 'Video')}">` : ''}<span class="dl-advanced-video-play">▶</span><span class="dl-advanced-video-copy"><strong>${esc(m.title || 'Video')}</strong><span>${url ? 'Mở video' : 'Thêm đường dẫn video'}</span></span></a>`;
            return `<div class="dl-advanced-video">${media}${direct ? `<strong class="dl-advanced-video-title">${esc(m.title || 'Video')}</strong>` : ''}${m.caption ? `<p class="dl-advanced-video-caption">${nl2br(m.caption)}</p>` : ''}</div>`;
        }
        if (type === 'map') {
            const url = safeUrl(m.url);
            return `<div class="dl-advanced-map"><div class="dl-advanced-map-canvas"><span>⌖</span></div><div class="dl-advanced-map-copy"><strong>${esc(m.title || 'Vị trí')}</strong><p class="dl-advanced-map-address">${nl2br(m.address)}</p>${url ? `<a class="dl-advanced-map-link" href="${esc(url)}">${esc(m.buttonText || 'Mở bản đồ')}</a>` : ''}</div></div>`;
        }
        if (type === 'accordion') {
            const items = (Array.isArray(m.items) ? m.items : []).filter(item => String(item?.title || item?.body || '').trim()).slice(0, 8);
            return `<div class="dl-advanced-accordion">${items.map((item, index) => `<details class="dl-advanced-accordion-item"${m.openFirst && index === 0 ? ' open' : ''}><summary class="dl-advanced-accordion-question"><strong>${esc(item.title || `Mục ${index + 1}`)}</strong><span>＋</span></summary><div class="dl-advanced-accordion-answer"><p>${nl2br(item.body)}</p></div></details>`).join('')}</div>`;
        }
        if (type === 'testimonial') {
            const image = safeUrl(m.imageUrl), rating = clamp(m.rating, 1, 5, 5);
            return `<blockquote class="dl-advanced-testimonial"><div class="dl-advanced-testimonial-stars">${'★'.repeat(rating)}${'☆'.repeat(5 - rating)}</div><p class="dl-advanced-testimonial-quote">${nl2br(m.quote)}</p><footer>${image ? `<img src="${esc(image)}" alt="${esc(m.name || 'Khách lưu trú')}">` : '<span class="dl-advanced-testimonial-avatar">☺</span>'}<span><strong>${esc(m.name || 'Khách lưu trú')}</strong>${m.role ? `<small>${esc(m.role)}</small>` : ''}</span></footer></blockquote>`;
        }
        const url = safeUrl(m.url), tone = ['soft', 'cream', 'dark', 'outline'].includes(m.tone) ? m.tone : 'soft';
        return `<div class="dl-advanced-promo dl-advanced-promo-${tone}">${m.badge ? `<span class="dl-advanced-promo-badge">${esc(m.badge)}</span>` : ''}<strong class="dl-advanced-promo-title">${esc(m.title || 'Ưu đãi')}</strong>${m.body ? `<p class="dl-advanced-promo-body">${nl2br(m.body)}</p>` : ''}<div class="dl-advanced-promo-price"><strong>${esc(m.price || '')}</strong>${m.suffix ? `<span>${esc(m.suffix)}</span>` : ''}</div>${url && m.buttonText ? `<a class="dl-row-button dl-row-button-primary dl-advanced-promo-button" href="${esc(url)}">${esc(m.buttonText)}</a>` : ''}</div>`;
    }

    function detect(markup) {
        if (!String(markup || '').includes('dl-advanced-')) return null;
        const doc = new DOMParser().parseFromString(String(markup || ''), 'text/html');
        const root = doc.body.firstElementChild;
        if (!root) return null;
        const type = Object.keys(TYPES).find(key => root.classList.contains(`dl-advanced-${key}`));
        if (!type) return null;
        let model;
        if (type === 'icon') model = { icon: text(root, '.dl-advanced-icon-symbol'), title: text(root, '.dl-advanced-icon-copy > strong'), body: text(root, '.dl-advanced-icon-copy > p'), linkText: text(root, '.dl-advanced-icon-link'), url: attr(root, '.dl-advanced-icon-link', 'href') };
        else if (type === 'video') model = { title: text(root, '.dl-advanced-video-title') || text(root, '.dl-advanced-video-copy > strong') || 'Video giới thiệu', url: (attr(root, '.dl-advanced-video-player', 'src') || attr(root, '.dl-advanced-video-link', 'href')).replace(/^#$/, ''), caption: text(root, '.dl-advanced-video-caption'), posterUrl: attr(root, '.dl-advanced-video-player', 'poster') || attr(root, '.dl-advanced-video-poster', 'src') };
        else if (type === 'map') model = { title: text(root, '.dl-advanced-map-copy > strong'), address: text(root, '.dl-advanced-map-address'), buttonText: text(root, '.dl-advanced-map-link') || 'Mở Google Maps', url: attr(root, '.dl-advanced-map-link', 'href') };
        else if (type === 'accordion') model = { openFirst: !!root.querySelector('.dl-advanced-accordion-item:first-child[open]'), items: [...root.querySelectorAll('.dl-advanced-accordion-item')].map(item => ({ title: text(item, '.dl-advanced-accordion-question strong'), body: text(item, '.dl-advanced-accordion-answer p') })) };
        else if (type === 'testimonial') model = { quote: text(root, '.dl-advanced-testimonial-quote'), name: text(root, 'footer strong'), role: text(root, 'footer small'), imageUrl: attr(root, 'footer img', 'src'), rating: Math.max(1, (text(root, '.dl-advanced-testimonial-stars').match(/★/g) || []).length || 5) };
        else model = { badge: text(root, '.dl-advanced-promo-badge'), title: text(root, '.dl-advanced-promo-title'), body: text(root, '.dl-advanced-promo-body'), price: text(root, '.dl-advanced-promo-price > strong'), suffix: text(root, '.dl-advanced-promo-price > span'), buttonText: text(root, '.dl-advanced-promo-button'), url: attr(root, '.dl-advanced-promo-button', 'href'), tone: ['soft', 'cream', 'dark', 'outline'].find(value => root.classList.contains(`dl-advanced-promo-${value}`)) || 'soft' };
        return { type, model: Object.assign(defaultModel(type), model) };
    }

    function field(name, label, value, options) {
        if (options) return `<label><span>${esc(label)}</span><select data-ae-field="${name}">${options.map(([key, title]) => `<option value="${esc(key)}"${String(key) === String(value) ? ' selected' : ''}>${esc(title)}</option>`).join('')}</select></label>`;
        return `<label><span>${esc(label)}</span><input data-ae-field="${name}" value="${esc(value || '')}"></label>`;
    }
    function area(name, label, value, rows) { return `<label class="wide"><span>${esc(label)}</span><textarea data-ae-field="${name}" rows="${rows || 3}">${esc(value || '')}</textarea></label>`; }

    function formHtml(type, model, compact) {
        const m = Object.assign(defaultModel(type), model || {});
        let fields = '';
        if (type === 'icon') fields = `${field('icon', 'Icon / ký hiệu', m.icon)}${field('title', 'Tiêu đề', m.title)}${area('body', 'Mô tả', m.body, 3)}${field('linkText', 'Chữ liên kết', m.linkText)}${field('url', 'Link', m.url)}`;
        else if (type === 'video') fields = `${field('title', 'Tiêu đề video', m.title)}${field('url', 'YouTube / Vimeo / MP4', m.url)}${field('posterUrl', 'Ảnh poster', m.posterUrl)}<button type="button" class="pve-ae-upload" data-ae-upload="posterUrl">＋ Tải poster</button>${area('caption', 'Chú thích', m.caption, 2)}`;
        else if (type === 'map') fields = `${field('title', 'Tiêu đề', m.title)}${area('address', 'Địa chỉ', m.address, 2)}${field('buttonText', 'Chữ nút', m.buttonText)}${field('url', 'Link Google Maps', m.url)}`;
        else if (type === 'accordion') {
            const lines = (m.items || []).map(item => `${String(item.title || '').replace(/\|/g, '／')} | ${String(item.body || '').replace(/\r?\n/g, ' ↵ ').replace(/\|/g, '／')}`).join('\n');
            fields = `${area('items', 'Mỗi dòng: Câu hỏi | Câu trả lời', lines, 7)}<label class="pve-ae-check wide"><input type="checkbox" data-ae-field="openFirst"${m.openFirst ? ' checked' : ''}><span>Mở sẵn mục đầu tiên</span></label>`;
        } else if (type === 'testimonial') fields = `${area('quote', 'Nội dung đánh giá', m.quote, 4)}${field('name', 'Tên khách', m.name)}${field('role', 'Dòng phụ', m.role)}${field('imageUrl', 'Ảnh đại diện', m.imageUrl)}<button type="button" class="pve-ae-upload" data-ae-upload="imageUrl">＋ Tải ảnh</button>${field('rating', 'Số sao', m.rating, [['5','5 sao'],['4','4 sao'],['3','3 sao'],['2','2 sao'],['1','1 sao']])}`;
        else fields = `${field('badge', 'Nhãn', m.badge)}${field('title', 'Tiêu đề', m.title)}${area('body', 'Mô tả', m.body, 3)}${field('price', 'Giá / số nổi bật', m.price)}${field('suffix', 'Hậu tố', m.suffix)}${field('buttonText', 'Chữ nút', m.buttonText)}${field('url', 'Link nút', m.url)}${field('tone', 'Kiểu nền', m.tone, [['soft','Nhẹ'],['cream','Kem'],['dark','Tối'],['outline','Viền']])}`;
        return `<div class="pve-ae-form${compact ? ' compact' : ''}" data-ae-form data-ae-type="${type}"><div class="pve-ae-form-title"><span>${TYPES[type].icon}</span><strong>${TYPES[type].label}</strong></div><div class="pve-ae-grid">${fields}</div>${['icon','map','promo'].includes(type) ? '<div class="pve-ae-link-help">Có thể dùng link nội bộ như <code>/booking</code> hoặc URL https://…</div>' : ''}</div>`;
    }

    function readForm(form, type) {
        const get = name => {
            const input = form.querySelector(`[data-ae-field="${name}"]`);
            if (!input) return '';
            return input.type === 'checkbox' ? input.checked : input.value.trim();
        };
        if (type === 'accordion') {
            const items = String(get('items') || '').split(/\r?\n/).map(line => {
                const parts = line.split('|');
                return { title: (parts.shift() || '').trim(), body: parts.join('|').trim().replace(/ ↵ /g, '\n') };
            }).filter(item => item.title || item.body).slice(0, 8);
            return { items, openFirst: !!get('openFirst') };
        }
        const model = {};
        form.querySelectorAll('[data-ae-field]').forEach(input => { model[input.dataset.aeField] = input.type === 'checkbox' ? input.checked : input.value.trim(); });
        if (type === 'testimonial') model.rating = clamp(model.rating, 1, 5, 5);
        return model;
    }

    const R = global.DeLongRowInline;
    const U = global.DeLongRowInlineUI;
    if (!R || !U) return;

    let editor = null;
    let fileInput = null;
    function ensureEditor() {
        if (editor) return editor;
        editor = document.createElement('div'); editor.className = 'pve-ae-editor'; editor.hidden = true; editor.dataset.rowInlineUi = '1';
        editor.innerHTML = '<div class="pve-ae-editor-head"><strong data-ae-editor-title>Phần tử nâng cao</strong><button type="button" data-ae-close>×</button></div><div data-ae-editor-body></div><div class="pve-ae-editor-foot"><button type="button" data-ae-builder>Builder</button><button type="button" data-ae-cancel>Hủy</button><button type="button" class="primary" data-ae-save>Lưu</button></div>';
        document.body.appendChild(editor); return editor;
    }
    function closeEditor() { if (editor) { editor.hidden = true; editor._model = null; } }
    function locate(sectionId, col, index) {
        const section = R.state.sections.get(String(sectionId)), content = R.content(section), element = content?.columns?.[col]?.elements?.[index], shell = R.shells(R.columnEl(sectionId, col))[index];
        return { section, content, element, shell };
    }
    function openEditor(sectionId, col, index, shell) {
        const current = locate(sectionId, col, index), found = current.element?.kind === 'html' ? detect(current.element.html) : null;
        if (!found || !current.section || !current.shell) return;
        U.closeFloat();
        const node = ensureEditor(); node._model = { sectionId: String(sectionId), col, index, shell: shell || current.shell, content: R.clone(current.content), type: found.type };
        node.querySelector('[data-ae-editor-title]').textContent = TYPES[found.type].label;
        node.querySelector('[data-ae-editor-body]').innerHTML = formHtml(found.type, found.model, true);
        node.hidden = false; U.position(node, shell || current.shell, 8);
    }
    async function saveEditor() {
        const node = ensureEditor(), m = node._model; if (!m) return;
        const section = R.state.sections.get(m.sectionId), next = R.clone(m.content), element = next?.columns?.[m.col]?.elements?.[m.index];
        if (!section || element?.kind !== 'html') return closeEditor();
        const form = node.querySelector('[data-ae-form]'), model = readForm(form, m.type);
        element.html = render(m.type, model);
        const button = node.querySelector('[data-ae-save]'); button.disabled = true; button.textContent = 'Đang lưu…';
        m.shell.outerHTML = R.renderElement(element);
        const saved = await R.save(section, next, R.rowEl(m.sectionId)); button.disabled = false; button.textContent = 'Lưu';
        if (!saved) return;
        closeEditor(); dispatchEvent(new Event('delong:row-inline-ui-refresh'));
    }
    async function uploadIntoForm(fieldName, form) {
        if (!fileInput) { fileInput = document.createElement('input'); fileInput.type = 'file'; fileInput.accept = 'image/png,image/jpeg,image/webp'; fileInput.hidden = true; document.body.appendChild(fileInput); }
        fileInput.onchange = async event => {
            const file = event.target.files?.[0]; event.target.value = ''; if (!file) return;
            try { const asset = await R.upload(file); const input = form.querySelector(`[data-ae-field="${fieldName}"]`); if (input) input.value = asset.url || ''; R.toast('Đã tải ảnh. Bấm Lưu để áp dụng.'); }
            catch (error) { R.toast(error.message || 'Không thể tải ảnh.', true); }
        };
        fileInput.click();
    }

    async function addAdvanced(type) {
        const model = U.add?._model; if (!model || !TYPES[type]) return;
        U.closeAdd();
        const result = await R.add(model.id, model.col, 'html'); if (!result) return;
        const section = R.state.sections.get(model.id), content = R.clone(R.content(section)), element = content?.columns?.[model.col]?.elements?.[result.index];
        if (!section || element?.kind !== 'html') return;
        element.html = render(type, defaultModel(type));
        const shell = R.shells(R.columnEl(model.id, model.col))[result.index]; if (shell) shell.outerHTML = R.renderElement(element);
        await R.save(section, content, R.rowEl(model.id), { message: `Đã thêm ${TYPES[type].label}.` });
        dispatchEvent(new Event('delong:row-inline-ui-refresh'));
        setTimeout(() => openEditor(model.id, model.col, result.index, R.shells(R.columnEl(model.id, model.col))[result.index]), 80);
    }

    function decorateInlineAdd() {
        const menu = U.add; if (!menu) return;
        const body = menu.querySelector(':scope > div'); if (!body || body.querySelector('[data-ae-add]')) return;
        const separator = document.createElement('span'); separator.className = 'pve-ae-add-label'; separator.textContent = 'Nâng cao'; body.appendChild(separator);
        Object.entries(TYPES).forEach(([type, info]) => { const button = document.createElement('button'); button.type = 'button'; button.dataset.aeAdd = type; button.innerHTML = `<span>${info.icon}</span>${info.label}`; body.appendChild(button); });
    }

    function advancedFromShell(shell) {
        const wrapper = shell?.querySelector('.dl-row-html > [class*="dl-advanced-"]');
        if (!wrapper) return null;
        const html = shell.closest('.pve-row-inline-element') ? R.content(R.state.sections.get(shell.dataset.rowInlineSection))?.columns?.[Number(shell.dataset.rowInlineCol)]?.elements?.[Number(shell.dataset.rowInlineIndex)]?.html : shell.querySelector('.dl-row-html')?.innerHTML;
        return detect(html || wrapper.outerHTML);
    }

    function decorateInline() {
        document.querySelectorAll('.pve-row-inline-element').forEach(shell => {
            const found = advancedFromShell(shell); if (!found) return;
            shell.classList.add('pve-ae-inline-element'); shell.dataset.aeType = found.type;
            const tools = shell.querySelector(':scope > [data-row-inline-tools]'); if (!tools) return;
            const label = tools.querySelector('[data-ri-kind]'); if (label) label.textContent = TYPES[found.type].label;
            const edit = tools.querySelector('[data-ri-action="edit"]');
            if (edit) { edit.removeAttribute('data-ri-action'); edit.dataset.aeEdit = '1'; edit.textContent = 'Sửa'; }
        });
        decorateInlineAdd();
    }

    function enhanceBuilder() {
        document.querySelectorAll('.pve-row-builder-drawer .pve-row-column').forEach(column => {
            const select = column.querySelector('.pve-row-add-element select');
            if (select) Object.entries(TYPES).forEach(([type, info]) => { if (!select.querySelector(`option[value="ae:${type}"]`)) { const option = document.createElement('option'); option.value = `ae:${type}`; option.textContent = `${info.icon} ${info.label}`; select.appendChild(option); } });
        });
        document.querySelectorAll('.pve-row-builder-drawer .pve-row-element').forEach(card => {
            const textarea = card.querySelector('textarea[data-element-field="html"]'); if (!textarea) return;
            const found = detect(textarea.value); if (!found) { card.querySelector(':scope > [data-ae-builder-editor]')?.remove(); return; }
            card.classList.add('pve-ae-builder-card');
            const small = card.querySelector('.pve-row-element-head small'); if (small) small.textContent = TYPES[found.type].label;
            let panel = card.querySelector(':scope > [data-ae-builder-editor]');
            if (!panel) { panel = document.createElement('div'); panel.dataset.aeBuilderEditor = '1'; textarea.closest('.pve-field')?.after(panel); }
            if (panel.dataset.aeSource === textarea.value) return;
            panel.dataset.aeSource = textarea.value; panel.innerHTML = formHtml(found.type, found.model, false);
            const sourceField = textarea.closest('.pve-field'); if (sourceField) sourceField.hidden = true;
        });
    }

    function syncBuilderForm(form) {
        const card = form.closest('.pve-row-element'), textarea = card?.querySelector('textarea[data-element-field="html"]'); if (!textarea) return;
        const type = form.dataset.aeType, model = readForm(form, type), markup = render(type, model);
        textarea.value = markup; textarea.dispatchEvent(new Event('input', { bubbles: true }));
        const panel = form.closest('[data-ae-builder-editor]'); if (panel) panel.dataset.aeSource = markup;
    }

    document.addEventListener('click', event => {
        const add = event.target.closest('[data-ae-add]'); if (add) { event.preventDefault(); event.stopPropagation(); addAdvanced(add.dataset.aeAdd); return; }
        const edit = event.target.closest('[data-ae-edit]'); if (edit) { const shell = edit.closest('.pve-row-inline-element'); if (!shell) return; event.preventDefault(); event.stopPropagation(); openEditor(shell.dataset.rowInlineSection, Number(shell.dataset.rowInlineCol), Number(shell.dataset.rowInlineIndex), shell); return; }
        if (event.target.closest('[data-ae-close],[data-ae-cancel]')) { closeEditor(); return; }
        if (event.target.closest('[data-ae-save]')) { saveEditor(); return; }
        if (event.target.closest('[data-ae-builder]')) { const m = editor?._model; if (m) U.openBuilder(m.sectionId); return; }
        const upload = event.target.closest('[data-ae-upload]'); if (upload) { event.preventDefault(); const form = upload.closest('[data-ae-form]'); if (form) uploadIntoForm(upload.dataset.aeUpload, form); return; }

        const builderAdd = event.target.closest('.pve-row-builder-drawer [data-add-element]');
        if (builderAdd) {
            const col = Number(builderAdd.dataset.addElement), column = builderAdd.closest('.pve-row-column'), select = column?.querySelector('.pve-row-add-element select'), value = select?.value || '';
            if (value.startsWith('ae:')) {
                const type = value.slice(3), before = column.querySelectorAll('.pve-row-element').length;
                setTimeout(() => {
                    const freshColumn = document.querySelector(`.pve-row-builder-drawer .pve-row-column[data-column="${col}"]`), cards = freshColumn ? [...freshColumn.querySelectorAll('.pve-row-element')] : [];
                    if (!TYPES[type] || cards.length <= before) return;
                    const textarea = cards[cards.length - 1].querySelector('textarea[data-element-field="html"]'); if (!textarea) return;
                    textarea.value = render(type, defaultModel(type)); textarea.dispatchEvent(new Event('input', { bubbles: true })); enhanceBuilder();
                }, 20);
            }
        }
    }, true);

    document.addEventListener('input', event => { const form = event.target.closest?.('.pve-row-builder-drawer [data-ae-form]'); if (form) syncBuilderForm(form); });
    document.addEventListener('change', event => { const form = event.target.closest?.('.pve-row-builder-drawer [data-ae-form]'); if (form) syncBuilderForm(form); });
    document.addEventListener('click', event => {
        if (!document.body.classList.contains('pve-editing') || event.target.closest('[data-row-inline-ui],.pve-section-controls')) return;
        const shell = event.target.closest('.pve-ae-inline-element'); if (!shell) return;
        const found = advancedFromShell(shell); if (!found) return;
        if (event.target.closest('a,.dl-advanced-accordion-question')) return;
        event.preventDefault(); event.stopPropagation(); openEditor(shell.dataset.rowInlineSection, Number(shell.dataset.rowInlineCol), Number(shell.dataset.rowInlineIndex), shell);
    }, true);

    const observer = new MutationObserver(() => { enhanceBuilder(); decorateInline(); });
    observer.observe(document.body, { childList: true, subtree: true });
    addEventListener('delong:row-inline-ui-refresh', () => setTimeout(decorateInline, 20));
    addEventListener('resize', () => { if (editor?._model && !editor.hidden) U.position(editor, editor._model.shell, 8); });
    addEventListener('scroll', () => { if (editor?._model && !editor.hidden) U.position(editor, editor._model.shell, 8); }, { passive: true });
    setTimeout(() => { enhanceBuilder(); decorateInline(); }, 80);
})(window);
