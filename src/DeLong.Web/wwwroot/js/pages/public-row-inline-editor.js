(function () {
    if (!document.body?.classList.contains('public-body') || !window.DeLongApi) return;

    const normalizedPath = window.location.pathname.replace(/\/+$/, '') || '/';
    const scopedMatch = normalizedPath.match(/^\/h\/([^/]+)$/i);
    if (!(normalizedPath === '/' || scopedMatch)) return;

    const siteSlug = scopedMatch ? decodeURIComponent(scopedMatch[1]) : '';
    const scopedHome = siteSlug ? `/h/${encodeURIComponent(siteSlug)}` : '';
    const contextUrl = `/api/admin/site/visual-context${siteSlug ? `?siteSlug=${encodeURIComponent(siteSlug)}` : ''}`;
    const MAX_ELEMENTS = 10;
    const DEVICES = ['desktop', 'tablet', 'mobile'];
    const ELEMENT_LABELS = {
        heading: 'Tiêu đề', text: 'Văn bản', image: 'Ảnh', button: 'Nút',
        divider: 'Phân cách', spacer: 'Khoảng cách', html: 'HTML'
    };

    const state = {
        context: null,
        api: '',
        sections: new Map(),
        histories: new Map(),
        active: null,
        dragged: null,
        bar: null,
        linkPanel: null,
        inspector: null,
        addMenu: null,
        imageInput: null,
        queued: false,
        refreshing: false
    };

    function h(value) {
        return String(value ?? '').replace(/[&<>'"]/g, char => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[char]));
    }
    function clone(value) { return JSON.parse(JSON.stringify(value ?? {})); }
    function json(value) { try { return JSON.parse(value || '{}'); } catch { return {}; } }
    function allowed(value, values, fallback) { return values.includes(value) ? value : fallback; }

    function toast(message, error) {
        let node = document.querySelector('.pve-toast');
        if (!node) { node = document.createElement('div'); node.className = 'pve-toast'; document.body.appendChild(node); }
        node.className = `pve-toast ${error ? 'error' : 'success'}`;
        node.textContent = message;
        node.hidden = false;
        clearTimeout(node._rowInlineTimer);
        node._rowInlineTimer = setTimeout(() => { node.hidden = true; }, 3000);
    }

    function responsiveDefault() {
        return {
            desktop: { visible: true, size: 'auto', align: 'inherit' },
            tablet: { visible: true, size: 'auto', align: 'inherit' },
            mobile: { visible: true, size: 'auto', align: 'inherit' }
        };
    }

    function elementDefault(kind) {
        const responsive = responsiveDefault();
        if (kind === 'heading') return { kind, text: 'Tiêu đề mới', level: 'h3', align: 'left', responsive };
        if (kind === 'text') return { kind, html: '<p>Nhập nội dung của bạn.</p>', responsive };
        if (kind === 'image') return { kind, imageUrl: '', altText: '', caption: '', linkUrl: '', responsive };
        if (kind === 'button') return { kind, text: 'Xem thêm', url: '#', style: 'primary', align: 'left', responsive };
        if (kind === 'divider') return { kind, style: 'solid', label: '', responsive };
        if (kind === 'spacer') return { kind, size: 'md', responsive };
        return { kind: 'html', html: '<div>Nội dung HTML</div>', responsive };
    }

    function visibilityClasses(responsive) {
        return DEVICES.filter(device => responsive?.[device]?.visible === false).map(device => `dl-hide-${device}`).join(' ');
    }

    function elementResponsiveClasses(element) {
        const classes = [visibilityClasses(element.responsive)];
        DEVICES.forEach(device => {
            const item = element.responsive?.[device] || {};
            if (item.size && item.size !== 'auto') classes.push(`dl-heading-${device}-size-${item.size}`);
            if (item.align && item.align !== 'inherit') classes.push(`dl-element-${device}-align-${item.align}`);
        });
        return classes.filter(Boolean).join(' ');
    }

    function renderElement(element) {
        const kind = ELEMENT_LABELS[element?.kind] ? element.kind : 'text';
        const current = Object.assign(elementDefault(kind), clone(element || {}), { kind });
        const wrapClass = `dl-row-element-shell ${elementResponsiveClasses(current)}`.trim();
        let inner = '';
        if (kind === 'heading') {
            const level = allowed(current.level, ['h2', 'h3', 'h4'], 'h3');
            const align = allowed(current.align, ['left', 'center', 'right'], 'left');
            inner = `<${level} class="dl-row-heading dl-align-${align}">${h(current.text || '')}</${level}>`;
        } else if (kind === 'text') {
            inner = `<div class="dl-row-text">${current.html || ''}</div>`;
        } else if (kind === 'image') {
            const image = current.imageUrl ? `<img src="${h(current.imageUrl)}" alt="${h(current.altText || '')}">` : '<div class="dl-row-image-empty">Chưa có ảnh</div>';
            const media = current.linkUrl ? `<a href="${h(current.linkUrl)}">${image}</a>` : image;
            inner = `<figure class="dl-row-image">${media}${current.caption ? `<figcaption>${h(current.caption)}</figcaption>` : ''}</figure>`;
        } else if (kind === 'button') {
            const style = allowed(current.style, ['primary', 'outline', 'ghost'], 'primary');
            const align = allowed(current.align, ['left', 'center', 'right'], 'left');
            inner = `<div class="dl-row-button-wrap dl-align-${align}"><a class="dl-row-button dl-row-button-${style}" href="${h(current.url || '#')}">${h(current.text || 'Xem thêm')}</a></div>`;
        } else if (kind === 'divider') {
            const style = allowed(current.style, ['solid', 'dashed', 'soft'], 'solid');
            inner = `<div class="dl-row-divider dl-row-divider-${style}">${current.label ? `<span>${h(current.label)}</span>` : ''}</div>`;
        } else if (kind === 'spacer') {
            const size = allowed(current.size, ['sm', 'md', 'lg', 'xl'], 'md');
            inner = `<div class="dl-row-spacer dl-row-spacer-${size}"></div>`;
        } else {
            inner = `<div class="dl-row-html">${current.html || ''}</div>`;
        }
        return `<div class="${h(wrapClass)}">${inner}</div>`;
    }

    function rowContent(section) {
        const content = json(section?.contentJson);
        return content?.builderKind === 'row' && Array.isArray(content.columns) ? content : null;
    }

    async function refreshSections() {
        if (!state.api || state.refreshing) return;
        state.refreshing = true;
        try {
            const data = await DeLongApi.get(`${state.api}/`);
            const all = (data?.sections || data?.site?.sections || []).slice();
            state.sections = new Map(all.filter(section => section.type === 'RichText' && rowContent(section)).map(section => [String(section.id), section]));
            state.sections.forEach(section => ensureHistory(section));
        } catch { }
        finally { state.refreshing = false; }
    }

    function ensureHistory(section) {
        const id = String(section.id);
        if (state.histories.has(id)) return state.histories.get(id);
        const content = rowContent(section);
        const history = { items: content ? [clone(content)] : [], index: content ? 0 : -1 };
        state.histories.set(id, history);
        return history;
    }

    function pushHistory(section, content) {
        const history = ensureHistory(section);
        const serialized = JSON.stringify(content);
        if (history.index >= 0 && JSON.stringify(history.items[history.index]) === serialized) return;
        history.items = history.items.slice(0, history.index + 1);
        history.items.push(clone(content));
        if (history.items.length > 40) history.items.shift();
        history.index = history.items.length - 1;
        updateRowHistoryButtons(String(section.id));
    }

    function cleanRowHtml(row) {
        const copy = row.cloneNode(true);
        copy.querySelectorAll('[data-row-inline-ui]').forEach(node => node.remove());
        [copy, ...copy.querySelectorAll('*')].forEach(node => {
            node.classList?.remove('pve-row-inline-row', 'pve-row-inline-column', 'pve-row-inline-element', 'pve-row-inline-active', 'pve-row-inline-dragging', 'pve-row-inline-drop');
            node.removeAttribute?.('contenteditable');
            node.removeAttribute?.('spellcheck');
            node.removeAttribute?.('draggable');
            [...(node.attributes || [])].forEach(attr => {
                if (attr.name.startsWith('data-row-inline')) node.removeAttribute(attr.name);
            });
        });
        return copy.outerHTML;
    }

    function payloadFor(section, content) {
        return {
            type: 'RichText',
            name: section.name || 'Row nội dung',
            variant: section.variant || 'wide',
            isVisible: section.isVisible !== false,
            contentJson: JSON.stringify(content)
        };
    }

    async function saveContent(section, content, row, options) {
        const next = clone(content);
        if (row) next.html = cleanRowHtml(row);
        const payload = payloadFor(section, next);
        if (payload.contentJson.length > 42000) {
            toast('Row quá lớn. Hãy tách thành hai Row trước khi lưu.', true);
            return null;
        }
        try {
            await DeLongApi.put(`${state.api}/sections/${section.id}`, payload);
            section.contentJson = payload.contentJson;
            if (!options?.skipHistory) pushHistory(section, next);
            if (options?.message !== false) toast(options?.message || 'Đã lưu Row trực tiếp.');
            return next;
        } catch (error) {
            toast(error.message || 'Không thể lưu Row.', true);
            return null;
        }
    }

    function rowSectionElement(sectionId) {
        return document.querySelector(`.pve-editable-section[data-pve-section-id="${CSS.escape(String(sectionId))}"]`);
    }

    function rowElement(sectionId) {
        return rowSectionElement(sectionId)?.querySelector('.dl-builder-row') || null;
    }

    function replaceRowFromSnapshot(section, snapshot) {
        const sectionEl = rowSectionElement(section.id);
        const holder = sectionEl?.querySelector('.public-cms-rich-inner');
        if (!holder || !snapshot?.html) return null;
        holder.innerHTML = snapshot.html;
        return holder.querySelector('.dl-builder-row');
    }

    async function historyMove(sectionId, delta) {
        const section = state.sections.get(String(sectionId));
        if (!section) return;
        const history = ensureHistory(section);
        const targetIndex = history.index + delta;
        if (targetIndex < 0 || targetIndex >= history.items.length) return;
        cancelActive(); closeInspector(); closeAddMenu();
        const snapshot = clone(history.items[targetIndex]);
        const row = replaceRowFromSnapshot(section, snapshot);
        if (!row) return;
        const saved = await saveContent(section, snapshot, row, { skipHistory: true, message: delta < 0 ? 'Đã hoàn tác thay đổi Row.' : 'Đã làm lại thay đổi Row.' });
        if (!saved) return;
        history.index = targetIndex;
        history.items[targetIndex] = clone(saved);
        queueDecorate();
    }

    function updateRowHistoryButtons(sectionId) {
        const history = state.histories.get(String(sectionId));
        const root = rowSectionElement(sectionId);
        const undo = root?.querySelector('[data-row-inline-undo]');
        const redo = root?.querySelector('[data-row-inline-redo]');
        if (undo) undo.disabled = !history || history.index <= 0;
        if (redo) redo.disabled = !history || history.index >= history.items.length - 1;
    }

    function openAdvanced(sectionId) {
        cancelActive(); closeInspector(); closeAddMenu();
        const button = rowSectionElement(sectionId)?.querySelector(':scope > .pve-section-controls [data-action="edit"]');
        if (button) button.click();
        else toast('Chưa thể mở Row Builder. Hãy thử lại sau một giây.', true);
    }

    function ensureBar() {
        if (state.bar) return state.bar;
        const bar = document.createElement('div');
        bar.className = 'pve-row-inline-bar';
        bar.dataset.rowInlineUi = '1';
        bar.hidden = true;
        bar.innerHTML = `<div><strong data-row-inline-bar-title>Chỉnh phần tử</strong><small data-row-inline-bar-help></small></div><div class="pve-row-inline-bar-actions"><button type="button" data-row-inline-link hidden>Liên kết</button><button type="button" data-row-inline-advanced>Nâng cao</button><button type="button" data-row-inline-cancel>Hủy</button><button type="button" class="primary" data-row-inline-save>Lưu</button></div>`;
        document.body.appendChild(bar);
        bar.querySelector('[data-row-inline-save]').addEventListener('click', saveActive);
        bar.querySelector('[data-row-inline-cancel]').addEventListener('click', cancelActive);
        bar.querySelector('[data-row-inline-link]').addEventListener('click', toggleLinkPanel);
        bar.querySelector('[data-row-inline-advanced]').addEventListener('click', () => state.active && openAdvanced(state.active.sectionId));
        state.bar = bar;
        return bar;
    }

    function positionFloating(node, target, offset) {
        if (!node || !target) return;
        const rect = target.getBoundingClientRect();
        const width = node.offsetWidth || 420;
        const left = Math.max(8, Math.min(window.innerWidth - width - 8, rect.left));
        const desiredTop = rect.top - (node.offsetHeight || 48) - (offset || 9);
        const top = desiredTop >= 54 ? desiredTop : Math.min(window.innerHeight - (node.offsetHeight || 48) - 8, rect.bottom + (offset || 9));
        node.style.left = `${left}px`;
        node.style.top = `${Math.max(54, top)}px`;
    }

    function startActive(sectionId, col, index, target, field, multiline) {
        const section = state.sections.get(String(sectionId));
        const content = rowContent(section);
        const element = content?.columns?.[col]?.elements?.[index];
        if (!section || !element || !target) return;
        if (state.active?.target === target) return;
        cancelActive(); closeInspector(); closeAddMenu();

        const originalHtml = target.innerHTML;
        state.active = { sectionId: String(sectionId), col, index, field, target, content: clone(content), originalHtml, multiline: !!multiline };
        target.classList.add('pve-row-inline-active');
        target.setAttribute('contenteditable', 'true');
        target.setAttribute('spellcheck', 'true');
        target.focus({ preventScroll: true });
        try {
            const range = document.createRange(); range.selectNodeContents(target); range.collapse(false);
            const selection = window.getSelection(); selection.removeAllRanges(); selection.addRange(range);
        } catch { }

        const bar = ensureBar();
        const kind = element.kind;
        bar.querySelector('[data-row-inline-bar-title]').textContent = `Row · ${ELEMENT_LABELS[kind] || kind}`;
        bar.querySelector('[data-row-inline-bar-help]').textContent = multiline ? 'Ctrl/Cmd + Enter để lưu · Esc để hủy' : 'Enter để lưu · Esc để hủy';
        bar.querySelector('[data-row-inline-link]').hidden = kind !== 'button';
        bar.hidden = false;
        positionFloating(bar, target);
    }

    function cancelActive() {
        const active = state.active;
        if (!active) return;
        active.target.innerHTML = active.originalHtml;
        active.target.removeAttribute('contenteditable');
        active.target.removeAttribute('spellcheck');
        active.target.classList.remove('pve-row-inline-active', 'pve-row-inline-saving');
        state.active = null;
        if (state.bar) state.bar.hidden = true;
        closeLinkPanel();
    }

    async function saveActive() {
        const active = state.active;
        if (!active) return;
        const section = state.sections.get(active.sectionId);
        const element = active.content?.columns?.[active.col]?.elements?.[active.index];
        const row = rowElement(active.sectionId);
        if (!section || !element || !row) return cancelActive();

        if (active.field === 'html') element.html = active.target.innerHTML;
        else element[active.field] = active.target.textContent.trim();
        if (element.kind === 'button') {
            const href = active.target.getAttribute('href');
            if (href != null) element.url = href;
        }

        active.target.classList.add('pve-row-inline-saving');
        const saveButton = state.bar?.querySelector('[data-row-inline-save]');
        if (saveButton) { saveButton.disabled = true; saveButton.textContent = 'Đang lưu…'; }
        const saved = await saveContent(section, active.content, row);
        if (saveButton) { saveButton.disabled = false; saveButton.textContent = 'Lưu'; }
        active.target.classList.remove('pve-row-inline-saving');
        if (!saved) return;
        active.target.removeAttribute('contenteditable');
        active.target.removeAttribute('spellcheck');
        active.target.classList.remove('pve-row-inline-active');
        state.active = null;
        if (state.bar) state.bar.hidden = true;
        closeLinkPanel();
        queueDecorate();
    }

    function systemLinks() {
        const home = scopedHome || '/';
        return [
            ['Trang chủ', home],
            ['Phòng', scopedHome ? `${scopedHome}/rooms` : '/rooms'],
            ['Đặt phòng', scopedHome ? `${scopedHome}/booking` : '/booking'],
            ['Tra cứu', scopedHome ? `${scopedHome}/booking/lookup` : '/booking/lookup'],
            ['Blog', scopedHome ? `${scopedHome}/blog` : '/blog'],
            ['Gallery', `${home === '/' ? '/' : home}#gallery`]
        ];
    }

    function ensureLinkPanel() {
        if (state.linkPanel) return state.linkPanel;
        const panel = document.createElement('div');
        panel.className = 'pve-row-inline-link-panel'; panel.dataset.rowInlineUi = '1'; panel.hidden = true;
        panel.innerHTML = `<div class="pve-row-inline-panel-head"><strong>Liên kết nút</strong><button type="button" data-row-inline-link-close aria-label="Đóng">×</button></div><input type="text" data-row-inline-link-input placeholder="/booking hoặc https://..."><div class="pve-row-inline-link-suggestions"><span>Chọn nhanh</span>${systemLinks().map(([label, url]) => `<button type="button" data-row-inline-link-value="${h(url)}">${h(label)}</button>`).join('')}</div><div class="pve-row-inline-panel-foot"><button type="button" data-row-inline-link-test>Mở thử</button><button type="button" class="primary" data-row-inline-link-apply>Áp dụng</button></div>`;
        document.body.appendChild(panel);
        panel.querySelector('[data-row-inline-link-close]').addEventListener('click', closeLinkPanel);
        panel.querySelectorAll('[data-row-inline-link-value]').forEach(button => button.addEventListener('click', () => { panel.querySelector('[data-row-inline-link-input]').value = button.dataset.rowInlineLinkValue; }));
        panel.querySelector('[data-row-inline-link-test]').addEventListener('click', () => {
            const value = panel.querySelector('[data-row-inline-link-input]').value.trim();
            if (!value || /^(javascript|data|vbscript):/i.test(value)) return;
            try { window.open(new URL(value, window.location.origin).href, '_blank', 'noopener,noreferrer'); } catch { }
        });
        panel.querySelector('[data-row-inline-link-apply]').addEventListener('click', () => {
            const active = state.active;
            if (!active) return closeLinkPanel();
            const value = panel.querySelector('[data-row-inline-link-input]').value.trim() || '#';
            if (/^(javascript|data|vbscript):/i.test(value)) return toast('Link này không an toàn.', true);
            active.target.setAttribute('href', value);
            const element = active.content?.columns?.[active.col]?.elements?.[active.index];
            if (element) element.url = value;
            closeLinkPanel();
            positionFloating(state.bar, active.target);
        });
        state.linkPanel = panel;
        return panel;
    }

    function toggleLinkPanel() {
        const active = state.active;
        if (!active) return;
        const panel = ensureLinkPanel();
        if (!panel.hidden) return closeLinkPanel();
        panel.querySelector('[data-row-inline-link-input]').value = active.target.getAttribute('href') || '#';
        panel.hidden = false;
        positionFloating(panel, state.bar || active.target, 7);
        panel.querySelector('[data-row-inline-link-input]').focus();
    }
    function closeLinkPanel() { if (state.linkPanel) state.linkPanel.hidden = true; }

    function ensureInspector() {
        if (state.inspector) return state.inspector;
        const panel = document.createElement('div');
        panel.className = 'pve-row-inline-inspector'; panel.dataset.rowInlineUi = '1'; panel.hidden = true;
        panel.innerHTML = `<div class="pve-row-inline-panel-head"><strong data-row-inline-inspector-title>Chỉnh ảnh</strong><button type="button" data-row-inline-inspector-close aria-label="Đóng">×</button></div><div class="pve-row-inline-inspector-body"><label><span>Ảnh</span><div><input name="imageUrl" type="text"><button type="button" data-row-inline-upload>＋ Tải ảnh</button></div></label><label><span>Alt text</span><input name="altText" type="text"></label><label><span>Chú thích</span><input name="caption" type="text"></label><label><span>Link khi bấm ảnh</span><input name="linkUrl" type="text"></label><div class="pve-row-inline-link-suggestions"><span>Link nhanh</span>${systemLinks().map(([label, url]) => `<button type="button" data-row-inline-image-link="${h(url)}">${h(label)}</button>`).join('')}</div></div><div class="pve-row-inline-panel-foot"><button type="button" data-row-inline-inspector-advanced>Nâng cao</button><button type="button" data-row-inline-inspector-cancel>Hủy</button><button type="button" class="primary" data-row-inline-inspector-save>Lưu</button></div>`;
        document.body.appendChild(panel);
        panel.querySelector('[data-row-inline-inspector-close]').addEventListener('click', closeInspector);
        panel.querySelector('[data-row-inline-inspector-cancel]').addEventListener('click', closeInspector);
        panel.querySelector('[data-row-inline-inspector-advanced]').addEventListener('click', () => panel._model && openAdvanced(panel._model.sectionId));
        panel.querySelectorAll('[data-row-inline-image-link]').forEach(button => button.addEventListener('click', () => { panel.querySelector('[name="linkUrl"]').value = button.dataset.rowInlineImageLink; }));
        panel.querySelector('[data-row-inline-upload]').addEventListener('click', () => {
            if (!state.imageInput) {
                const input = document.createElement('input'); input.type = 'file'; input.accept = 'image/png,image/jpeg,image/webp'; input.hidden = true; input.dataset.rowInlineUi = '1'; document.body.appendChild(input);
                input.addEventListener('change', uploadInspectorImage); state.imageInput = input;
            }
            state.imageInput.click();
        });
        panel.querySelector('[data-row-inline-inspector-save]').addEventListener('click', saveInspector);
        state.inspector = panel;
        return panel;
    }

    function openImageInspector(sectionId, col, index, shell) {
        cancelActive(); closeAddMenu(); closeLinkPanel();
        const section = state.sections.get(String(sectionId));
        const content = rowContent(section);
        const element = content?.columns?.[col]?.elements?.[index];
        if (!section || element?.kind !== 'image') return;
        const panel = ensureInspector();
        panel._model = { sectionId: String(sectionId), col, index, shell, content: clone(content) };
        panel.querySelector('[name="imageUrl"]').value = element.imageUrl || '';
        panel.querySelector('[name="altText"]').value = element.altText || '';
        panel.querySelector('[name="caption"]').value = element.caption || '';
        panel.querySelector('[name="linkUrl"]').value = element.linkUrl || '';
        panel.hidden = false;
        positionFloating(panel, shell, 8);
    }

    function closeInspector() {
        if (!state.inspector) return;
        state.inspector.hidden = true; state.inspector._model = null;
    }

    async function uploadInspectorImage(event) {
        const file = event.target.files?.[0]; event.target.value = '';
        const model = state.inspector?._model;
        if (!file || !model) return;
        const button = state.inspector.querySelector('[data-row-inline-upload]');
        button.disabled = true; button.textContent = 'Đang tải…';
        try {
            const form = new FormData(); form.append('file', file);
            const asset = await DeLongApi.postForm(`${state.api}/assets/section`, form);
            state.inspector.querySelector('[name="imageUrl"]').value = asset.url || '';
            const alt = state.inspector.querySelector('[name="altText"]');
            if (!alt.value.trim()) alt.value = file.name.replace(/\.[^.]+$/, '').replace(/[-_]+/g, ' ').trim();
            toast('Đã tải ảnh. Bấm Lưu để áp dụng vào Row.');
        } catch (error) { toast(error.message || 'Không thể tải ảnh.', true); }
        finally { button.disabled = false; button.textContent = '＋ Tải ảnh'; }
    }

    async function saveInspector() {
        const panel = state.inspector, model = panel?._model;
        if (!panel || !model) return;
        const section = state.sections.get(model.sectionId);
        const content = model.content;
        const element = content?.columns?.[model.col]?.elements?.[model.index];
        if (!section || element?.kind !== 'image') return closeInspector();
        element.imageUrl = panel.querySelector('[name="imageUrl"]').value.trim();
        element.altText = panel.querySelector('[name="altText"]').value.trim();
        element.caption = panel.querySelector('[name="caption"]').value.trim();
        element.linkUrl = panel.querySelector('[name="linkUrl"]').value.trim();
        if (/^(javascript|data|vbscript):/i.test(element.linkUrl)) return toast('Link ảnh không an toàn.', true);

        const oldHtml = model.shell.outerHTML;
        model.shell.outerHTML = renderElement(element);
        const row = rowElement(model.sectionId);
        const save = panel.querySelector('[data-row-inline-inspector-save]'); save.disabled = true; save.textContent = 'Đang lưu…';
        const saved = await saveContent(section, content, row);
        save.disabled = false; save.textContent = 'Lưu';
        if (!saved) {
            const current = rowElement(model.sectionId)?.querySelector(`.dl-builder-column:nth-child(${model.col + 1}) > .dl-row-element-shell:nth-of-type(${model.index + 1})`);
            if (current) current.outerHTML = oldHtml;
            return;
        }
        closeInspector(); queueDecorate();
    }

    function ensureAddMenu() {
        if (state.addMenu) return state.addMenu;
        const menu = document.createElement('div');
        menu.className = 'pve-row-inline-add-menu'; menu.dataset.rowInlineUi = '1'; menu.hidden = true;
        menu.innerHTML = `<strong>Thêm phần tử</strong><div>${Object.entries(ELEMENT_LABELS).map(([kind, label]) => `<button type="button" data-row-inline-add-kind="${h(kind)}">${h(label)}</button>`).join('')}</div>`;
        document.body.appendChild(menu);
        menu.querySelectorAll('[data-row-inline-add-kind]').forEach(button => button.addEventListener('click', () => addElement(button.dataset.rowInlineAddKind)));
        state.addMenu = menu;
        return menu;
    }

    function openAddMenu(sectionId, col, button) {
        cancelActive(); closeInspector(); closeLinkPanel();
        const menu = ensureAddMenu();
        menu._model = { sectionId: String(sectionId), col: Number(col) };
        menu.hidden = false;
        positionFloating(menu, button, 7);
    }
    function closeAddMenu() { if (state.addMenu) { state.addMenu.hidden = true; state.addMenu._model = null; } }

    async function addElement(kind) {
        const model = state.addMenu?._model;
        if (!model || !ELEMENT_LABELS[kind]) return;
        const section = state.sections.get(model.sectionId);
        const content = clone(rowContent(section));
        const list = content?.columns?.[model.col]?.elements;
        const column = rowElement(model.sectionId)?.querySelector(`:scope > .dl-builder-column:nth-child(${model.col + 1})`);
        if (!section || !list || !column) return closeAddMenu();
        if (list.length >= MAX_ELEMENTS) return toast(`Mỗi cột tối đa ${MAX_ELEMENTS} phần tử.`, true);
        const element = elementDefault(kind); list.push(element);
        const holder = document.createElement('div'); holder.innerHTML = renderElement(element);
        const shell = holder.firstElementChild;
        const addButton = column.querySelector(':scope > [data-row-inline-add]');
        column.insertBefore(shell, addButton || null);
        closeAddMenu();
        const saved = await saveContent(section, content, rowElement(model.sectionId));
        if (!saved) return replaceRowFromSnapshot(section, rowContent(section));
        queueDecorate();
        setTimeout(() => {
            const fresh = rowElement(model.sectionId)?.querySelector(`:scope > .dl-builder-column:nth-child(${model.col + 1}) > .dl-row-element-shell:nth-of-type(${list.length})`);
            if (kind === 'heading') startActive(model.sectionId, model.col, list.length - 1, fresh?.querySelector('.dl-row-heading'), 'text', false);
            else if (kind === 'text') startActive(model.sectionId, model.col, list.length - 1, fresh?.querySelector('.dl-row-text'), 'html', true);
            else if (kind === 'button') startActive(model.sectionId, model.col, list.length - 1, fresh?.querySelector('.dl-row-button'), 'text', false);
            else if (kind === 'image') openImageInspector(model.sectionId, model.col, list.length - 1, fresh);
            else if (kind === 'html') openAdvanced(model.sectionId);
        }, 60);
    }

    async function mutateElement(sectionId, col, index, action) {
        cancelActive(); closeInspector(); closeAddMenu(); closeLinkPanel();
        const section = state.sections.get(String(sectionId));
        const content = clone(rowContent(section));
        const list = content?.columns?.[col]?.elements;
        const row = rowElement(sectionId);
        const column = row?.querySelector(`:scope > .dl-builder-column:nth-child(${col + 1})`);
        const shells = column ? [...column.querySelectorAll(':scope > .dl-row-element-shell')] : [];
        const shell = shells[index];
        if (!section || !list?.[index] || !shell || !row) return;
        const before = clone(rowContent(section));

        if (action === 'delete') {
            if (!confirm(`Xóa ${ELEMENT_LABELS[list[index].kind] || 'phần tử'} này khỏi Row?`)) return;
            list.splice(index, 1); shell.remove();
        } else if (action === 'duplicate') {
            if (list.length >= MAX_ELEMENTS) return toast(`Mỗi cột tối đa ${MAX_ELEMENTS} phần tử.`, true);
            const copied = clone(list[index]); list.splice(index + 1, 0, copied);
            const holder = document.createElement('div'); holder.innerHTML = renderElement(copied); shell.after(holder.firstElementChild);
        } else if (action === 'up' && index > 0) {
            [list[index - 1], list[index]] = [list[index], list[index - 1]]; shells[index - 1].before(shell);
        } else if (action === 'down' && index < list.length - 1) {
            [list[index + 1], list[index]] = [list[index], list[index + 1]]; shells[index + 1].after(shell);
        } else if (action === 'left' || action === 'right') {
            const targetCol = col + (action === 'left' ? -1 : 1);
            const targetList = content?.columns?.[targetCol]?.elements;
            const targetColumn = row.querySelector(`:scope > .dl-builder-column:nth-child(${targetCol + 1})`);
            if (!targetList || !targetColumn) return;
            if (targetList.length >= MAX_ELEMENTS) return toast('Cột đích đã đầy.', true);
            targetList.push(list.splice(index, 1)[0]);
            const addButton = targetColumn.querySelector(':scope > [data-row-inline-add]');
            targetColumn.insertBefore(shell, addButton || null);
        } else return;

        const saved = await saveContent(section, content, row);
        if (!saved) replaceRowFromSnapshot(section, before);
        queueDecorate();
    }

    function dragStart(event, sectionId, col, index, shell) {
        state.dragged = { sectionId: String(sectionId), col, index, shell };
        shell.classList.add('pve-row-inline-dragging');
        event.dataTransfer.effectAllowed = 'move';
        event.dataTransfer.setData('text/plain', `${sectionId}:${col}:${index}`);
    }

    function dragEnd() {
        state.dragged?.shell?.classList.remove('pve-row-inline-dragging');
        document.querySelectorAll('.pve-row-inline-drop').forEach(node => node.classList.remove('pve-row-inline-drop'));
        state.dragged = null;
    }

    async function dropElement(event, sectionId, targetCol, column) {
        const dragged = state.dragged;
        if (!dragged || dragged.sectionId !== String(sectionId)) return;
        event.preventDefault();
        const section = state.sections.get(String(sectionId));
        const content = clone(rowContent(section));
        const source = content?.columns?.[dragged.col]?.elements;
        const target = content?.columns?.[targetCol]?.elements;
        const row = rowElement(sectionId);
        if (!section || !source?.[dragged.index] || !target || !row) return dragEnd();
        if (dragged.col !== targetCol && target.length >= MAX_ELEMENTS) { dragEnd(); return toast('Cột đích đã đầy.', true); }

        const targetShell = event.target.closest('.dl-row-element-shell');
        let targetIndex = targetShell && targetShell.closest('.dl-builder-column') === column ? Number(targetShell.dataset.rowInlineIndex) : target.length;
        let after = false;
        if (targetShell) {
            const rect = targetShell.getBoundingClientRect(); after = event.clientY > rect.top + rect.height / 2;
            if (after) targetIndex += 1;
        }

        const moved = source.splice(dragged.index, 1)[0];
        if (dragged.col === targetCol && targetIndex > dragged.index) targetIndex -= 1;
        targetIndex = Math.max(0, Math.min(target.length, targetIndex));
        target.splice(targetIndex, 0, moved);

        if (targetShell && targetShell !== dragged.shell) {
            if (after) targetShell.after(dragged.shell); else targetShell.before(dragged.shell);
        } else if (!targetShell) {
            const addButton = column.querySelector(':scope > [data-row-inline-add]');
            column.insertBefore(dragged.shell, addButton || null);
        }

        const before = rowContent(section);
        const saved = await saveContent(section, content, row, { message: 'Đã di chuyển phần tử.' });
        if (!saved && before) replaceRowFromSnapshot(section, before);
        dragEnd(); queueDecorate();
    }

    function decorateElement(sectionId, colIndex, index, shell, element) {
        shell.classList.add('pve-row-inline-element');
        shell.dataset.rowInlineSection = String(sectionId);
        shell.dataset.rowInlineCol = String(colIndex);
        shell.dataset.rowInlineIndex = String(index);
        shell.querySelector(':scope > [data-row-inline-tools]')?.remove();

        const tools = document.createElement('div');
        tools.className = 'pve-row-inline-tools'; tools.dataset.rowInlineUi = '1'; tools.dataset.rowInlineTools = '1';
        const canLeft = colIndex > 0, contentColumnCount(sectionId) > colIndex + 1;
        tools.innerHTML = `<span>${h(ELEMENT_LABELS[element.kind] || element.kind)}</span><button type="button" class="drag" draggable="true" data-row-inline-drag title="Kéo sang vị trí khác">⋮⋮</button><button type="button" data-row-inline-action="up" title="Đưa lên">↑</button><button type="button" data-row-inline-action="down" title="Đưa xuống">↓</button>${canLeft ? '<button type="button" data-row-inline-action="left" title="Sang cột trái">←</button>' : ''}${canRight ? '<button type="button" data-row-inline-action="right" title="Sang cột phải">→</button>' : ''}<button type="button" data-row-inline-action="edit">Sửa</button><button type="button" data-row-inline-action="duplicate" title="Nhân bản">⧉</button><button type="button" class="danger" data-row-inline-action="delete" title="Xóa">×</button>`;
        shell.appendChild(tools);

        tools.querySelector('[data-row-inline-drag]').addEventListener('dragstart', event => dragStart(event, sectionId, colIndex, index, shell));
        tools.querySelector('[data-row-inline-drag]').addEventListener('dragend', dragEnd);
        tools.querySelectorAll('[data-row-inline-action]').forEach(button => button.addEventListener('click', event => {
            event.preventDefault(); event.stopPropagation();
            const action = button.dataset.rowInlineAction;
            if (action === 'edit') {
                if (element.kind === 'heading') startActive(sectionId, colIndex, index, shell.querySelector('.dl-row-heading'), 'text', false);
                else if (element.kind === 'text') startActive(sectionId, colIndex, index, shell.querySelector('.dl-row-text'), 'html', true);
                else if (element.kind === 'button') startActive(sectionId, colIndex, index, shell.querySelector('.dl-row-button'), 'text', false);
                else if (element.kind === 'image') openImageInspector(sectionId, colIndex, index, shell);
                else openAdvanced(sectionId);
            } else mutateElement(sectionId, colIndex, index, action);
        }));
    }

    function contentColumnCount(sectionId) {
        return rowContent(state.sections.get(String(sectionId)))?.columns?.length || 0;
    }

    function decorateRow(sectionEl, section) {
        const content = rowContent(section);
        const row = sectionEl.querySelector('.dl-builder-row');
        if (!content || !row) return;
        row.classList.add('pve-row-inline-row');

        let rowTools = sectionEl.querySelector(':scope > [data-row-inline-row-tools]');
        if (!rowTools) {
            rowTools = document.createElement('div');
            rowTools.className = 'pve-row-inline-row-tools'; rowTools.dataset.rowInlineUi = '1'; rowTools.dataset.rowInlineRowTools = '1';
            rowTools.innerHTML = `<strong>Row · sửa trực tiếp</strong><button type="button" data-row-inline-undo title="Hoàn tác">↶</button><button type="button" data-row-inline-redo title="Làm lại">↷</button><button type="button" data-row-inline-open-builder>Builder</button>`;
            sectionEl.appendChild(rowTools);
            rowTools.querySelector('[data-row-inline-undo]').addEventListener('click', event => { event.preventDefault(); event.stopPropagation(); historyMove(section.id, -1); });
            rowTools.querySelector('[data-row-inline-redo]').addEventListener('click', event => { event.preventDefault(); event.stopPropagation(); historyMove(section.id, 1); });
            rowTools.querySelector('[data-row-inline-open-builder]').addEventListener('click', event => { event.preventDefault(); event.stopPropagation(); openAdvanced(section.id); });
        }
        updateRowHistoryButtons(String(section.id));

        const columns = [...row.querySelectorAll(':scope > .dl-builder-column')];
        columns.forEach((column, colIndex) => {
            column.classList.add('pve-row-inline-column');
            column.dataset.rowInlineCol = String(colIndex);
            const shells = [...column.querySelectorAll(':scope > .dl-row-element-shell')];
            shells.forEach((shell, index) => {
                const element = content.columns?.[colIndex]?.elements?.[index];
                if (element) decorateElement(section.id, colIndex, index, shell, element);
            });
            let add = column.querySelector(':scope > [data-row-inline-add]');
            if (!add) {
                add = document.createElement('button'); add.type = 'button'; add.className = 'pve-row-inline-add'; add.dataset.rowInlineUi = '1'; add.dataset.rowInlineAdd = '1'; add.textContent = '＋ Thêm phần tử';
                column.appendChild(add);
                add.addEventListener('click', event => { event.preventDefault(); event.stopPropagation(); openAddMenu(section.id, colIndex, add); });
            }
            column.ondragover = event => {
                if (!state.dragged || state.dragged.sectionId !== String(section.id)) return;
                event.preventDefault(); column.classList.add('pve-row-inline-drop');
            };
            column.ondragleave = event => { if (!column.contains(event.relatedTarget)) column.classList.remove('pve-row-inline-drop'); };
            column.ondrop = event => { column.classList.remove('pve-row-inline-drop'); dropElement(event, section.id, colIndex, column); };
        });
    }

    function cleanup() {
        cancelActive(); closeInspector(); closeLinkPanel(); closeAddMenu();
        document.querySelectorAll('[data-row-inline-ui]').forEach(node => {
            if ([state.bar, state.linkPanel, state.inspector, state.addMenu, state.imageInput].includes(node)) return;
            node.remove();
        });
        document.querySelectorAll('.pve-row-inline-row,.pve-row-inline-column,.pve-row-inline-element,.pve-row-inline-drop,.pve-row-inline-dragging').forEach(node => {
            node.classList.remove('pve-row-inline-row', 'pve-row-inline-column', 'pve-row-inline-element', 'pve-row-inline-drop', 'pve-row-inline-dragging');
            [...node.attributes].forEach(attr => { if (attr.name.startsWith('data-row-inline')) node.removeAttribute(attr.name); });
        });
        if (state.bar) state.bar.hidden = true;
        if (state.linkPanel) state.linkPanel.hidden = true;
        if (state.inspector) state.inspector.hidden = true;
        if (state.addMenu) state.addMenu.hidden = true;
    }

    async function decorate() {
        if (!document.body.classList.contains('pve-editing')) return cleanup();
        const sectionEls = [...document.querySelectorAll('.pve-editable-section[data-pve-section-id]')];
        let missingRow = false;
        sectionEls.forEach(sectionEl => {
            if (!sectionEl.querySelector('.dl-builder-row')) return;
            const section = state.sections.get(String(sectionEl.dataset.pveSectionId));
            if (section) decorateRow(sectionEl, section); else missingRow = true;
        });
        if (missingRow && !state.refreshing) { await refreshSections(); sectionEls.forEach(sectionEl => { const section = state.sections.get(String(sectionEl.dataset.pveSectionId)); if (section) decorateRow(sectionEl, section); }); }
    }

    function queueDecorate() {
        if (state.queued) return;
        state.queued = true;
        requestAnimationFrame(() => { state.queued = false; decorate(); });
    }

    document.addEventListener('click', event => {
        if (!document.body.classList.contains('pve-editing')) return;
        const shell = event.target.closest('.pve-row-inline-element');
        if (!shell || event.target.closest('[data-row-inline-ui]')) return;
        const sectionId = shell.dataset.rowInlineSection;
        const col = Number(shell.dataset.rowInlineCol), index = Number(shell.dataset.rowInlineIndex);
        const element = rowContent(state.sections.get(String(sectionId)))?.columns?.[col]?.elements?.[index];
        if (!element) return;
        if (event.target.closest('a')) { event.preventDefault(); event.stopPropagation(); }
        if (element.kind === 'heading' && event.target.closest('.dl-row-heading')) startActive(sectionId, col, index, shell.querySelector('.dl-row-heading'), 'text', false);
        else if (element.kind === 'text' && event.target.closest('.dl-row-text')) startActive(sectionId, col, index, shell.querySelector('.dl-row-text'), 'html', true);
        else if (element.kind === 'button' && event.target.closest('.dl-row-button')) startActive(sectionId, col, index, shell.querySelector('.dl-row-button'), 'text', false);
        else if (element.kind === 'image' && event.target.closest('.dl-row-image')) openImageInspector(sectionId, col, index, shell);
    }, true);

    document.addEventListener('keydown', event => {
        if (!state.active) return;
        if (event.key === 'Escape') { event.preventDefault(); cancelActive(); return; }
        if (event.key === 'Enter' && (!state.active.multiline || event.ctrlKey || event.metaKey)) {
            event.preventDefault(); saveActive();
        }
    });

    document.addEventListener('pointerdown', event => {
        if (state.addMenu && !state.addMenu.hidden && !state.addMenu.contains(event.target) && !event.target.closest('[data-row-inline-add]')) closeAddMenu();
        if (state.linkPanel && !state.linkPanel.hidden && !state.linkPanel.contains(event.target) && !event.target.closest('[data-row-inline-link]')) closeLinkPanel();
    });

    window.addEventListener('scroll', () => {
        if (state.active && state.bar && !state.bar.hidden) positionFloating(state.bar, state.active.target);
        if (state.inspector?._model && !state.inspector.hidden) positionFloating(state.inspector, state.inspector._model.shell);
    }, { passive: true });
    window.addEventListener('resize', queueDecorate);

    const observer = new MutationObserver(queueDecorate);
    observer.observe(document.body, { childList: true, subtree: true, attributes: true, attributeFilter: ['class'] });

    DeLongApi.get(contextUrl).then(async context => {
        if (!context?.canEdit) return;
        state.context = context;
        state.api = context.scope === 'global' ? '/api/admin/site/global' : `/api/admin/properties/${context.propertyId}/site`;
        await refreshSections();
        queueDecorate();
    }).catch(() => {});
})();
