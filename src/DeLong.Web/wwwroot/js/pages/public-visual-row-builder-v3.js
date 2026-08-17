(function () {
    if (!document.body?.classList.contains('public-body') || !window.DeLongApi) return;

    const normalizedPath = window.location.pathname.replace(/\/+$/, '') || '/';
    const scopedMatch = normalizedPath.match(/^\/h\/([^/]+)$/i);
    if (!(normalizedPath === '/' || scopedMatch)) return;

    const siteSlug = scopedMatch ? decodeURIComponent(scopedMatch[1]) : '';
    const contextUrl = `/api/admin/site/visual-context${siteSlug ? `?siteSlug=${encodeURIComponent(siteSlug)}` : ''}`;
    const MAX_COLUMNS = 4;
    const MAX_ELEMENTS = 10;
    const ELEMENT_CLIPBOARD = 'delong:pve:elementClipboard';
    const ROW_CLIPBOARD = 'delong:pve:rowClipboard';

    const LAYOUTS = {
        single: { label: '1 cột', count: 1 },
        '2-equal': { label: '2 cột 50 / 50', count: 2 },
        '2-left': { label: '2 cột 33 / 67', count: 2 },
        '2-right': { label: '2 cột 67 / 33', count: 2 },
        '3-equal': { label: '3 cột đều', count: 3 },
        '3-wide-center': { label: '3 cột 25 / 50 / 25', count: 3 },
        '4-equal': { label: '4 cột đều', count: 4 }
    };

    const ELEMENTS = {
        heading: 'Tiêu đề', text: 'Văn bản', image: 'Ảnh', button: 'Nút',
        divider: 'Dòng phân cách', spacer: 'Khoảng cách', html: 'HTML'
    };

    function h(value) {
        return String(value ?? '').replace(/[&<>'"]/g, char => ({ '&':'&amp;','<':'&lt;','>':'&gt;',"'":'&#39;','"':'&quot;' }[char]));
    }
    function clone(value) { return JSON.parse(JSON.stringify(value ?? {})); }
    function json(value) { try { return JSON.parse(value || '{}'); } catch { return {}; } }
    function storageGet(key) { try { return sessionStorage.getItem(key); } catch { return null; } }
    function storageSet(key, value) { try { sessionStorage.setItem(key, value); return true; } catch { return false; } }

    function select(name, value, options, extra) {
        return `<select name="${h(name)}" ${extra || ''}>${options.map(item => {
            const key = Array.isArray(item) ? item[0] : item;
            const label = Array.isArray(item) ? item[1] : item;
            return `<option value="${h(key)}"${String(key) === String(value) ? ' selected' : ''}>${h(label)}</option>`;
        }).join('')}</select>`;
    }

    function elementDefault(kind) {
        if (kind === 'heading') return { kind, text: 'Tiêu đề mới', level: 'h3', align: 'left' };
        if (kind === 'text') return { kind, html: '<p>Nhập nội dung của bạn.</p>' };
        if (kind === 'image') return { kind, imageUrl: '', altText: '', caption: '', linkUrl: '' };
        if (kind === 'button') return { kind, text: 'Xem thêm', url: '#', style: 'primary', align: 'left' };
        if (kind === 'divider') return { kind, style: 'solid', label: '' };
        if (kind === 'spacer') return { kind, size: 'md' };
        return { kind: 'html', html: '<div>Nội dung HTML</div>' };
    }

    function defaultRow() {
        return {
            builderKind: 'row', rowVersion: 3, layout: '2-equal', gap: 'md', align: 'top',
            theme: 'plain', padding: 'md', mobile: 'stack',
            columns: [
                { elements: [elementDefault('heading'), elementDefault('text')] },
                { elements: [elementDefault('image')] }
            ],
            html: ''
        };
    }

    function normalizeElement(element) {
        const kind = ELEMENTS[element?.kind] ? element.kind : 'text';
        return Object.assign(elementDefault(kind), element || {}, { kind });
    }

    function normalizeRow(input) {
        const source = input && input.builderKind === 'row' ? clone(input) : defaultRow();
        source.builderKind = 'row';
        source.rowVersion = 3;
        source.layout = LAYOUTS[source.layout] ? source.layout : '2-equal';
        source.gap = ['none','sm','md','lg'].includes(source.gap) ? source.gap : 'md';
        source.align = ['top','center','bottom','stretch'].includes(source.align) ? source.align : 'top';
        source.theme = ['plain','soft','cream','dark'].includes(source.theme) ? source.theme : 'plain';
        source.padding = ['none','sm','md','lg','xl'].includes(source.padding) ? source.padding : 'md';
        source.mobile = ['stack','reverse'].includes(source.mobile) ? source.mobile : 'stack';
        const oldColumns = Array.isArray(source.columns) ? source.columns.slice(0, MAX_COLUMNS) : [];
        source.columns = Array.from({ length: LAYOUTS[source.layout].count }, (_, index) => {
            const column = oldColumns[index] || { elements: [] };
            const elements = Array.isArray(column.elements) ? column.elements.slice(0, MAX_ELEMENTS) : [];
            return { elements: elements.map(normalizeElement) };
        });
        return source;
    }

    function buildElementHtml(element) {
        if (element.kind === 'heading') {
            const level = ['h2','h3','h4'].includes(element.level) ? element.level : 'h3';
            const align = ['left','center','right'].includes(element.align) ? element.align : 'left';
            return `<${level} class="dl-row-heading dl-align-${align}">${h(element.text || '')}</${level}>`;
        }
        if (element.kind === 'text') return `<div class="dl-row-text">${element.html || ''}</div>`;
        if (element.kind === 'image') {
            const image = element.imageUrl ? `<img src="${h(element.imageUrl)}" alt="${h(element.altText || '')}">` : '<div class="dl-row-image-empty">Chưa có ảnh</div>';
            const media = element.linkUrl ? `<a href="${h(element.linkUrl)}">${image}</a>` : image;
            return `<figure class="dl-row-image">${media}${element.caption ? `<figcaption>${h(element.caption)}</figcaption>` : ''}</figure>`;
        }
        if (element.kind === 'button') {
            const style = ['primary','outline','ghost'].includes(element.style) ? element.style : 'primary';
            const align = ['left','center','right'].includes(element.align) ? element.align : 'left';
            return `<div class="dl-row-button-wrap dl-align-${align}"><a class="dl-row-button dl-row-button-${style}" href="${h(element.url || '#')}">${h(element.text || 'Xem thêm')}</a></div>`;
        }
        if (element.kind === 'divider') {
            const style = ['solid','dashed','soft'].includes(element.style) ? element.style : 'solid';
            return `<div class="dl-row-divider dl-row-divider-${style}">${element.label ? `<span>${h(element.label)}</span>` : ''}</div>`;
        }
        if (element.kind === 'spacer') {
            const size = ['sm','md','lg','xl'].includes(element.size) ? element.size : 'md';
            return `<div class="dl-row-spacer dl-row-spacer-${size}"></div>`;
        }
        return `<div class="dl-row-html">${element.html || ''}</div>`;
    }

    function buildRowHtml(content) {
        const row = normalizeRow(content);
        const columns = row.columns.map((column, index) => `<div class="dl-builder-column dl-builder-column-${index + 1}">${column.elements.map(buildElementHtml).join('') || '<div class="dl-row-column-empty"></div>'}</div>`).join('');
        return `<div class="dl-builder-row dl-row-layout-${h(row.layout)} dl-row-gap-${h(row.gap)} dl-row-align-${h(row.align)} dl-row-theme-${h(row.theme)} dl-row-pad-${h(row.padding)} dl-row-mobile-${h(row.mobile)}">${columns}</div>`;
    }

    function safePreviewHtml(markup) {
        if (window.DeLongRichEditor?.cleanForVisual) return window.DeLongRichEditor.cleanForVisual(markup);
        const parser = new DOMParser();
        const doc = parser.parseFromString(`<div>${markup}</div>`, 'text/html');
        doc.querySelectorAll('script,style,iframe,object,embed,link,meta,form').forEach(node => node.remove());
        doc.querySelectorAll('*').forEach(node => [...node.attributes].forEach(attr => {
            const name = attr.name.toLowerCase(), value = attr.value.trim().toLowerCase();
            if (name.startsWith('on') || ((name === 'href' || name === 'src') && value.startsWith('javascript:'))) node.removeAttribute(attr.name);
        }));
        return doc.body.firstElementChild?.innerHTML || '';
    }

    function setSession(context) {
        const suffix = context.scope === 'global' ? 'global' : context.propertyId;
        try {
            sessionStorage.setItem(`delong:pve:editing:${suffix}`, '1');
            sessionStorage.setItem(`delong:pve:scroll:${suffix}`, String(Math.max(0, window.scrollY || 0)));
        } catch { }
    }

    class RowBuilder {
        constructor(context) {
            this.context = context;
            this.api = context.scope === 'global' ? '/api/admin/site/global' : `/api/admin/properties/${context.propertyId}/site`;
            this.sections = [];
            this.rows = new Map();
            this.drawer = null;
            this.current = null;
            this.state = defaultRow();
            this.history = [];
            this.historyIndex = -1;
            this.historyTimer = null;
            this.previewDevice = 'desktop';
            this.enhanceQueued = false;
            this.domObserver = new MutationObserver(() => this.queueEnhance());
            this.bodyClassObserver = new MutationObserver(() => this.syncToolbarVisibility());
        }

        async mount() {
            await this.refreshSections();
            this.domObserver.observe(document.body, { childList: true, subtree: true });
            this.bodyClassObserver.observe(document.body, { attributes: true, attributeFilter: ['class'] });
            this.enhance();
        }

        queueEnhance() {
            if (this.enhanceQueued) return;
            this.enhanceQueued = true;
            requestAnimationFrame(() => { this.enhanceQueued = false; this.enhance(); });
        }

        async refreshSections() {
            try {
                const data = await DeLongApi.get(`${this.api}/`);
                this.sections = (data?.sections || data?.site?.sections || []).slice();
                this.rows = new Map(this.sections.filter(section => {
                    const content = json(section.contentJson);
                    return section.type === 'RichText' && content.builderKind === 'row';
                }).map(section => [String(section.id), section]));
            } catch { }
        }

        enhance() {
            this.enhanceToolbar();
            this.enhanceCoreDrawer();
            this.enhanceRowSections();
            this.syncToolbarVisibility();
        }

        enhanceToolbar() {
            const actions = document.querySelector('.pve-toolbar-actions');
            if (!actions || actions.querySelector('[data-row-builder-add]')) return;
            const button = document.createElement('button');
            button.type = 'button'; button.dataset.rowBuilderAdd = '1'; button.className = 'pve-row-builder-add'; button.textContent = '＋ Row / cột';
            const add = actions.querySelector('[data-add]');
            if (add) add.after(button); else actions.prepend(button);
            button.addEventListener('click', () => this.open(null));
        }

        syncToolbarVisibility() {
            const button = document.querySelector('[data-row-builder-add]');
            if (button) button.hidden = !document.body.classList.contains('pve-editing');
        }

        enhanceCoreDrawer() {
            const drawer = document.querySelector('.pve-drawer:not(.pve-row-builder-drawer)');
            const body = drawer?.querySelector('[data-editor-form] .pve-drawer-body');
            if (!body || body.querySelector('[data-row-builder-shortcut]')) return;
            const shortcut = document.createElement('button');
            shortcut.type = 'button'; shortcut.dataset.rowBuilderShortcut = '1'; shortcut.className = 'pve-row-builder-shortcut';
            shortcut.innerHTML = '<strong>Row / Column Builder</strong><span>Tạo 1–4 cột, nhiều phần tử và xem trước song song.</span>';
            body.prepend(shortcut);
            shortcut.addEventListener('click', () => { drawer.remove(); this.open(null); });
        }

        enhanceRowSections() {
            document.querySelectorAll('.pve-editable-section[data-pve-section-id]').forEach(sectionEl => {
                const section = this.rows.get(String(sectionEl.dataset.pveSectionId));
                if (!section) return;
                sectionEl.classList.add('pve-row-builder-section');
                const controls = sectionEl.querySelector(':scope > .pve-section-controls');
                if (!controls) return;
                const label = controls.querySelector('span');
                if (label && label.textContent !== 'Row / cột') label.textContent = 'Row / cột';
                const edit = controls.querySelector('[data-action="edit"]');
                if (!edit || edit.dataset.rowBuilderBound === '1') return;
                edit.dataset.rowBuilderBound = '1';
                edit.addEventListener('click', event => {
                    event.preventDefault(); event.stopImmediatePropagation(); this.open(section);
                }, true);
            });
        }

        open(section) {
            this.close();
            this.current = section || null;
            this.state = normalizeRow(section ? json(section.contentJson) : defaultRow());
            this.previewDevice = 'desktop';
            this.history = [clone(this.state)];
            this.historyIndex = 0;

            const drawer = document.createElement('section');
            drawer.className = 'pve-drawer pve-drawer-wide pve-row-builder-drawer';
            drawer.setAttribute('role', 'dialog'); drawer.setAttribute('aria-modal', 'true');
            drawer.innerHTML = `<header><div><small>ROW / COLUMN BUILDER</small><h2>${section ? h(section.name || 'Chỉnh Row') : 'Tạo Row mới'}</h2></div><div class="pve-row-header-tools"><button type="button" data-row-undo title="Hoàn tác">↶</button><button type="button" data-row-redo title="Làm lại">↷</button><button type="button" data-row-copy>Copy Row</button><button type="button" data-row-paste>Dán Row</button><button type="button" data-close aria-label="Đóng">×</button></div></header>
                <form data-row-form><div class="pve-drawer-body">
                    <div class="pve-row-builder-settings">
                        <label class="pve-field"><span>Tên quản trị</span><input name="rowName" value="${h(section?.name || 'Row nội dung')}" maxlength="120"></label>
                        <label class="pve-check pve-check-top"><input type="checkbox" name="rowVisible"${section?.isVisible === false ? '' : ' checked'}><span>Hiển thị Row</span></label>
                        <label class="pve-field"><span>Bố cục cột</span>${select('rowLayout', this.state.layout, Object.entries(LAYOUTS).map(([key, value]) => [key, value.label]))}</label>
                        <label class="pve-field"><span>Khoảng giữa cột</span>${select('rowGap', this.state.gap, [['none','Không'],['sm','Nhỏ'],['md','Vừa'],['lg','Lớn']])}</label>
                        <label class="pve-field"><span>Căn dọc</span>${select('rowAlign', this.state.align, [['top','Trên'],['center','Giữa'],['bottom','Dưới'],['stretch','Căng đều']])}</label>
                        <label class="pve-field"><span>Nền</span>${select('rowTheme', this.state.theme, [['plain','Trong suốt'],['soft','Xanh nhạt'],['cream','Kem'],['dark','Tối']])}</label>
                        <label class="pve-field"><span>Padding</span>${select('rowPadding', this.state.padding, [['none','Không'],['sm','Nhỏ'],['md','Vừa'],['lg','Lớn'],['xl','Rất lớn']])}</label>
                        <label class="pve-field"><span>Mobile</span>${select('rowMobile', this.state.mobile, [['stack','Xếp cột theo thứ tự'],['reverse','Đảo thứ tự cột']])}</label>
                    </div>
                    <div class="pve-row-columns" data-row-columns></div>
                    <section class="pve-row-preview-wrap">
                        <div class="pve-row-preview-head"><div><strong>Xem trước</strong><div class="pve-preview-devices"><button type="button" class="active" data-preview-device="desktop">Desktop</button><button type="button" data-preview-device="tablet">Tablet</button><button type="button" data-preview-device="mobile">Mobile</button></div></div><small>Preview cập nhật khi nhập; server vẫn sanitize khi lưu.</small></div>
                        <div class="pve-row-preview" data-row-preview><div class="pve-row-preview-canvas device-desktop" data-preview-canvas></div></div>
                    </section>
                </div><footer><span>${section ? 'Dùng ↑ / ↓ ngoài trang để đổi vị trí Row.' : 'Row mới được thêm cuối trang; có thể di chuyển sau.'}</span><div><button type="button" data-close>Hủy</button><button type="submit" class="pve-primary">Lưu Row</button></div></footer></form>`;
            document.body.appendChild(drawer); this.drawer = drawer;

            drawer.querySelectorAll('[data-close]').forEach(button => button.addEventListener('click', () => this.close()));
            drawer.querySelector('[data-row-form]').addEventListener('submit', event => { event.preventDefault(); this.save(); });
            drawer.querySelector('[data-row-undo]').addEventListener('click', () => this.undo());
            drawer.querySelector('[data-row-redo]').addEventListener('click', () => this.redo());
            drawer.querySelector('[data-row-copy]').addEventListener('click', () => this.copyRow());
            drawer.querySelector('[data-row-paste]').addEventListener('click', () => this.pasteRow());
            drawer.querySelectorAll('[data-preview-device]').forEach(button => button.addEventListener('click', () => this.setPreviewDevice(button.dataset.previewDevice)));

            drawer.querySelector('[name="rowLayout"]').addEventListener('change', event => this.changeLayout(event.target.value));
            ['rowGap','rowAlign','rowTheme','rowPadding','rowMobile'].forEach(name => drawer.querySelector(`[name="${name}"]`).addEventListener('change', event => {
                const key = name.replace('row','').replace(/^./, c => c.toLowerCase());
                this.state[key] = event.target.value;
                this.pushHistory(); this.updatePreview(); this.updateHistoryButtons();
            }));
            this.renderColumns(); this.updatePreview(); this.updateHistoryButtons(); this.updateClipboardButtons();
        }

        changeLayout(layout) {
            if (!LAYOUTS[layout]) return;
            const old = this.state.columns || [];
            this.state.layout = layout;
            this.state.columns = Array.from({ length: LAYOUTS[layout].count }, (_, index) => old[index] || { elements: [] });
            this.pushHistory(); this.renderColumns(); this.updatePreview(); this.updateHistoryButtons();
        }

        pushHistory() {
            const snapshot = normalizeRow(this.state);
            const serialized = JSON.stringify(snapshot);
            const current = this.history[this.historyIndex] ? JSON.stringify(this.history[this.historyIndex]) : '';
            if (serialized === current) return;
            this.history = this.history.slice(0, this.historyIndex + 1);
            this.history.push(clone(snapshot));
            if (this.history.length > 60) this.history.shift();
            this.historyIndex = this.history.length - 1;
        }

        pushHistoryDebounced() {
            clearTimeout(this.historyTimer);
            this.historyTimer = setTimeout(() => { this.pushHistory(); this.updateHistoryButtons(); }, 450);
        }

        applyHistory(index) {
            if (index < 0 || index >= this.history.length) return;
            this.historyIndex = index;
            this.state = normalizeRow(this.history[index]);
            this.syncSettings(); this.renderColumns(); this.updatePreview(); this.updateHistoryButtons();
        }
        flushHistory() {
            if (this.historyTimer) { clearTimeout(this.historyTimer); this.historyTimer = null; this.pushHistory(); }
        }
        undo() { this.flushHistory(); this.applyHistory(this.historyIndex - 1); }
        redo() { this.flushHistory(); this.applyHistory(this.historyIndex + 1); }

        updateHistoryButtons() {
            if (!this.drawer) return;
            const undo = this.drawer.querySelector('[data-row-undo]'), redo = this.drawer.querySelector('[data-row-redo]');
            if (undo) undo.disabled = this.historyIndex <= 0;
            if (redo) redo.disabled = this.historyIndex >= this.history.length - 1;
        }

        syncSettings() {
            if (!this.drawer) return;
            const map = { rowLayout:'layout', rowGap:'gap', rowAlign:'align', rowTheme:'theme', rowPadding:'padding', rowMobile:'mobile' };
            Object.entries(map).forEach(([name,key]) => { const el = this.drawer.querySelector(`[name="${name}"]`); if (el) el.value = this.state[key]; });
        }

        renderColumns() {
            if (!this.drawer) return;
            const root = this.drawer.querySelector('[data-row-columns]');
            const canPaste = !!this.readElementClipboard();
            root.innerHTML = this.state.columns.map((column, colIndex) => `<article class="pve-row-column" data-column="${colIndex}">
                <header><div><small>CỘT ${colIndex + 1}</small><strong>${column.elements.length} phần tử</strong></div></header>
                <div class="pve-row-elements">${column.elements.map((element, elementIndex) => this.elementCard(element, colIndex, elementIndex)).join('') || '<div class="pve-row-empty">Cột đang trống.</div>'}</div>
                <div class="pve-row-add-element">${select(`addKind${colIndex}`, 'text', Object.entries(ELEMENTS))}<button type="button" data-add-element="${colIndex}">＋ Thêm</button><button type="button" data-paste-element="${colIndex}"${canPaste && column.elements.length < MAX_ELEMENTS ? '' : ' disabled'}>Dán</button></div>
            </article>`).join('');
            this.bindColumnEvents();
        }

        elementCard(element, colIndex, elementIndex) {
            const canLeft = colIndex > 0, canRight = colIndex < this.state.columns.length - 1;
            const actions = `<div><button type="button" data-element-action="up" data-col="${colIndex}" data-index="${elementIndex}" title="Đưa lên">↑</button><button type="button" data-element-action="down" data-col="${colIndex}" data-index="${elementIndex}" title="Đưa xuống">↓</button>${canLeft ? `<button type="button" data-element-action="left" data-col="${colIndex}" data-index="${elementIndex}" title="Sang cột trái">←</button>` : ''}${canRight ? `<button type="button" data-element-action="right" data-col="${colIndex}" data-index="${elementIndex}" title="Sang cột phải">→</button>` : ''}<button type="button" data-element-action="copy" data-col="${colIndex}" data-index="${elementIndex}" title="Copy phần tử">⎘</button><button type="button" data-element-action="duplicate" data-col="${colIndex}" data-index="${elementIndex}" title="Nhân bản">⧉</button><button type="button" class="danger" data-element-action="delete" data-col="${colIndex}" data-index="${elementIndex}" title="Xóa">×</button></div>`;
            const head = `<div class="pve-row-element-head"><div><small>${ELEMENTS[element.kind] || element.kind}</small><strong>Phần tử ${elementIndex + 1}</strong></div>${actions}</div>`;
            let fields = '';
            if (element.kind === 'heading') fields = `<label class="pve-field"><span>Nội dung</span><input data-element-field="text" value="${h(element.text || '')}"></label><div class="pve-grid-2"><label class="pve-field"><span>Cấp</span>${select('level', element.level || 'h3', [['h2','H2'],['h3','H3'],['h4','H4']], 'data-element-field="level"')}</label><label class="pve-field"><span>Căn chữ</span>${select('align', element.align || 'left', [['left','Trái'],['center','Giữa'],['right','Phải']], 'data-element-field="align"')}</label></div>`;
            else if (element.kind === 'text') fields = `<label class="pve-field"><span>Nội dung văn bản</span><textarea rows="8" data-rich-row data-element-field="html">${h(element.html || '')}</textarea></label>`;
            else if (element.kind === 'image') fields = `<div class="pve-field"><span>Ảnh</span><div class="pve-image-row"><input data-element-field="imageUrl" value="${h(element.imageUrl || '')}"><label class="pve-upload">Tải ảnh<input type="file" accept="image/png,image/jpeg,image/webp" data-row-upload data-col="${colIndex}" data-index="${elementIndex}" hidden></label></div></div><label class="pve-field"><span>Alt text</span><input data-element-field="altText" value="${h(element.altText || '')}"></label><label class="pve-field"><span>Chú thích</span><input data-element-field="caption" value="${h(element.caption || '')}"></label><label class="pve-field"><span>Link khi bấm</span><input data-element-field="linkUrl" value="${h(element.linkUrl || '')}"></label>`;
            else if (element.kind === 'button') fields = `<div class="pve-grid-2"><label class="pve-field"><span>Chữ trên nút</span><input data-element-field="text" value="${h(element.text || '')}"></label><label class="pve-field"><span>URL</span><input data-element-field="url" value="${h(element.url || '')}"></label><label class="pve-field"><span>Kiểu</span>${select('style', element.style || 'primary', [['primary','Nút chính'],['outline','Viền'],['ghost','Nhẹ']], 'data-element-field="style"')}</label><label class="pve-field"><span>Căn</span>${select('align', element.align || 'left', [['left','Trái'],['center','Giữa'],['right','Phải']], 'data-element-field="align"')}</label></div>`;
            else if (element.kind === 'divider') fields = `<div class="pve-grid-2"><label class="pve-field"><span>Nhãn</span><input data-element-field="label" value="${h(element.label || '')}"></label><label class="pve-field"><span>Kiểu dòng</span>${select('style', element.style || 'solid', [['solid','Liền'],['dashed','Nét đứt'],['soft','Mảnh']], 'data-element-field="style"')}</label></div>`;
            else if (element.kind === 'spacer') fields = `<label class="pve-field"><span>Khoảng cách</span>${select('size', element.size || 'md', [['sm','Nhỏ'],['md','Vừa'],['lg','Lớn'],['xl','Rất lớn']], 'data-element-field="size"')}</label>`;
            else fields = `<label class="pve-field"><span>HTML tùy chỉnh</span><textarea rows="9" data-rich-row data-element-field="html">${h(element.html || '')}</textarea></label>`;
            return `<section class="pve-row-element" data-col="${colIndex}" data-index="${elementIndex}">${head}${fields}</section>`;
        }

        bindColumnEvents() {
            if (!this.drawer) return;
            this.drawer.querySelectorAll('[data-add-element]').forEach(button => button.addEventListener('click', () => {
                const col = Number(button.dataset.addElement), list = this.state.columns[col]?.elements;
                if (!list || list.length >= MAX_ELEMENTS) return this.toast(`Mỗi cột tối đa ${MAX_ELEMENTS} phần tử.`, true);
                list.push(elementDefault(this.drawer.querySelector(`[name="addKind${col}"]`)?.value || 'text'));
                this.pushHistory(); this.renderColumns(); this.updatePreview(); this.updateHistoryButtons();
            }));
            this.drawer.querySelectorAll('[data-paste-element]').forEach(button => button.addEventListener('click', () => this.pasteElement(Number(button.dataset.pasteElement))));

            this.drawer.querySelectorAll('[data-element-action]').forEach(button => button.addEventListener('click', () => {
                const col = Number(button.dataset.col), index = Number(button.dataset.index), list = this.state.columns[col]?.elements;
                if (!list?.[index]) return;
                const action = button.dataset.elementAction;
                if (action === 'copy') return this.copyElement(list[index]);
                if (action === 'delete') list.splice(index, 1);
                else if (action === 'duplicate' && list.length < MAX_ELEMENTS) list.splice(index + 1, 0, clone(list[index]));
                else if (action === 'up' && index > 0) [list[index - 1], list[index]] = [list[index], list[index - 1]];
                else if (action === 'down' && index < list.length - 1) [list[index + 1], list[index]] = [list[index], list[index + 1]];
                else if (action === 'left' || action === 'right') {
                    const targetCol = col + (action === 'left' ? -1 : 1), target = this.state.columns[targetCol]?.elements;
                    if (!target || target.length >= MAX_ELEMENTS) return this.toast('Cột đích đã đầy.', true);
                    target.push(list.splice(index, 1)[0]);
                }
                this.pushHistory(); this.renderColumns(); this.updatePreview(); this.updateHistoryButtons();
            }));

            this.drawer.querySelectorAll('.pve-row-element').forEach(card => {
                const col = Number(card.dataset.col), index = Number(card.dataset.index);
                card.querySelectorAll('[data-element-field]').forEach(input => {
                    const update = () => {
                        const element = this.state.columns[col]?.elements[index];
                        if (!element) return;
                        element[input.dataset.elementField] = input.value;
                        this.updatePreview(); this.pushHistoryDebounced();
                    };
                    input.addEventListener(input.tagName === 'SELECT' ? 'change' : 'input', update);
                });
                card.querySelector('[data-row-upload]')?.addEventListener('change', event => this.upload(event));
            });

            this.drawer.querySelectorAll('textarea[data-rich-row]').forEach(textarea => {
                window.DeLongRichEditor?.enhance(textarea, { helpText: 'Soạn trực quan hoặc chuyển sang HTML khi cần.' });
            });
            this.updateClipboardButtons();
        }

        copyElement(element) {
            if (storageSet(ELEMENT_CLIPBOARD, JSON.stringify(normalizeElement(element)))) {
                this.toast('Đã copy phần tử. Có thể dán sang cột khác.'); this.updateClipboardButtons();
            }
        }
        readElementClipboard() {
            const raw = storageGet(ELEMENT_CLIPBOARD); if (!raw) return null;
            try { return normalizeElement(JSON.parse(raw)); } catch { return null; }
        }
        pasteElement(col) {
            const item = this.readElementClipboard(), list = this.state.columns[col]?.elements;
            if (!item || !list) return this.toast('Chưa có phần tử đã copy.', true);
            if (list.length >= MAX_ELEMENTS) return this.toast('Cột đích đã đầy.', true);
            list.push(clone(item)); this.pushHistory(); this.renderColumns(); this.updatePreview(); this.updateHistoryButtons();
        }

        copyRow() {
            if (storageSet(ROW_CLIPBOARD, JSON.stringify(normalizeRow(this.state)))) {
                this.toast('Đã copy Row.'); this.updateClipboardButtons();
            }
        }
        pasteRow() {
            const raw = storageGet(ROW_CLIPBOARD); if (!raw) return this.toast('Chưa có Row đã copy.', true);
            try {
                const next = normalizeRow(JSON.parse(raw));
                if (!confirm('Thay nội dung Row hiện tại bằng Row đã copy? Bạn có thể Hoàn tác sau đó.')) return;
                this.state = next; this.pushHistory(); this.syncSettings(); this.renderColumns(); this.updatePreview(); this.updateHistoryButtons();
            } catch { this.toast('Dữ liệu Row đã copy không hợp lệ.', true); }
        }
        updateClipboardButtons() {
            if (!this.drawer) return;
            const pasteRow = this.drawer.querySelector('[data-row-paste]');
            if (pasteRow) pasteRow.disabled = !storageGet(ROW_CLIPBOARD);
            this.drawer.querySelectorAll('[data-paste-element]').forEach(button => {
                const col = Number(button.dataset.pasteElement), list = this.state.columns[col]?.elements;
                button.disabled = !this.readElementClipboard() || !list || list.length >= MAX_ELEMENTS;
            });
        }

        async upload(event) {
            const file = event.target.files?.[0], col = Number(event.target.dataset.col), index = Number(event.target.dataset.index);
            event.target.value = '';
            if (!file || !this.state.columns[col]?.elements[index]) return;
            try {
                const form = new FormData(); form.append('file', file);
                const asset = await DeLongApi.postForm(`${this.api}/assets/section`, form);
                this.state.columns[col].elements[index].imageUrl = asset.url || '';
                this.pushHistory(); this.renderColumns(); this.updatePreview(); this.updateHistoryButtons(); this.toast('Đã tải ảnh. Bấm Lưu Row để áp dụng.');
            } catch (error) { this.toast(error.message || 'Không thể tải ảnh.', true); }
        }

        setPreviewDevice(device) {
            if (!['desktop','tablet','mobile'].includes(device)) return;
            this.previewDevice = device;
            this.drawer?.querySelectorAll('[data-preview-device]').forEach(button => button.classList.toggle('active', button.dataset.previewDevice === device));
            const canvas = this.drawer?.querySelector('[data-preview-canvas]');
            if (canvas) canvas.className = `pve-row-preview-canvas device-${device}`;
        }

        updatePreview() {
            const canvas = this.drawer?.querySelector('[data-preview-canvas]');
            if (canvas) canvas.innerHTML = safePreviewHtml(buildRowHtml(this.state));
        }

        async save() {
            if (!this.drawer) return;
            const form = this.drawer.querySelector('[data-row-form]'), submit = form.querySelector('button[type="submit"]');
            submit.disabled = true; submit.textContent = 'Đang lưu…';
            const content = normalizeRow(this.state); content.html = buildRowHtml(content);
            const payload = { type:'RichText', name:form.elements.rowName.value.trim() || 'Row nội dung', variant:'wide', isVisible:form.elements.rowVisible.checked, contentJson:JSON.stringify(content) };
            if (payload.contentJson.length > 28000) {
                submit.disabled = false; submit.textContent = 'Lưu Row'; return this.toast('Row quá lớn. Hãy tách thành hai Row.', true);
            }
            try {
                if (this.current) await DeLongApi.put(`${this.api}/sections/${this.current.id}`, payload);
                else await DeLongApi.post(`${this.api}/sections`, payload);
                setSession(this.context); window.location.reload();
            } catch (error) {
                submit.disabled = false; submit.textContent = 'Lưu Row'; this.toast(error.message || 'Không thể lưu Row.', true);
            }
        }

        close() {
            clearTimeout(this.historyTimer);
            this.drawer?.remove(); this.drawer = null; this.current = null;
        }

        toast(message, error) {
            let node = document.querySelector('.pve-toast');
            if (!node) { node = document.createElement('div'); node.className = 'pve-toast'; document.body.appendChild(node); }
            node.className = `pve-toast ${error ? 'error' : 'success'}`; node.textContent = message; node.hidden = false;
            clearTimeout(node._rowTimer); node._rowTimer = setTimeout(() => { node.hidden = true; }, 3000);
        }
    }

    DeLongApi.get(contextUrl).then(context => { if (context?.canEdit) new RowBuilder(context).mount(); }).catch(() => {});
})();
