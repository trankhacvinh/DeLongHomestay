(function () {
    if (!document.body?.classList.contains('public-body') || !window.DeLongApi) return;

    const normalizedPath = window.location.pathname.replace(/\/+$/, '') || '/';
    const scopedMatch = normalizedPath.match(/^\/h\/([^/]+)$/i);
    const isHome = normalizedPath === '/' || !!scopedMatch;
    if (!isHome) return;

    const siteSlug = scopedMatch ? decodeURIComponent(scopedMatch[1]) : '';
    const contextUrl = `/api/admin/site/visual-context${siteSlug ? `?siteSlug=${encodeURIComponent(siteSlug)}` : ''}`;
    const MAX_COLUMNS = 4;
    const MAX_ELEMENTS = 10;

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
        heading: 'Tiêu đề',
        text: 'Văn bản',
        image: 'Ảnh',
        button: 'Nút',
        divider: 'Dòng phân cách',
        spacer: 'Khoảng cách',
        html: 'HTML'
    };

    function h(value) {
        return String(value ?? '').replace(/[&<>'"]/g, char => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[char]));
    }

    function clone(value) {
        return JSON.parse(JSON.stringify(value ?? {}));
    }

    function json(value) {
        try { return JSON.parse(value || '{}'); } catch { return {}; }
    }

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
            builderKind: 'row',
            rowVersion: 1,
            layout: '2-equal',
            gap: 'md',
            align: 'top',
            theme: 'plain',
            padding: 'md',
            mobile: 'stack',
            columns: [
                { elements: [elementDefault('heading'), elementDefault('text')] },
                { elements: [elementDefault('image')] }
            ],
            html: ''
        };
    }

    function normalizeRow(input) {
        const source = input && input.builderKind === 'row' ? clone(input) : defaultRow();
        source.builderKind = 'row';
        source.rowVersion = 1;
        source.layout = LAYOUTS[source.layout] ? source.layout : '2-equal';
        source.gap = ['none', 'sm', 'md', 'lg'].includes(source.gap) ? source.gap : 'md';
        source.align = ['top', 'center', 'bottom', 'stretch'].includes(source.align) ? source.align : 'top';
        source.theme = ['plain', 'soft', 'cream', 'dark'].includes(source.theme) ? source.theme : 'plain';
        source.padding = ['none', 'sm', 'md', 'lg', 'xl'].includes(source.padding) ? source.padding : 'md';
        source.mobile = ['stack', 'reverse'].includes(source.mobile) ? source.mobile : 'stack';
        const count = LAYOUTS[source.layout].count;
        const oldColumns = Array.isArray(source.columns) ? source.columns.slice(0, MAX_COLUMNS) : [];
        source.columns = Array.from({ length: count }, (_, index) => {
            const column = oldColumns[index] || { elements: [] };
            const elements = Array.isArray(column.elements) ? column.elements.slice(0, MAX_ELEMENTS) : [];
            return { elements: elements.map(element => normalizeElement(element)) };
        });
        return source;
    }

    function normalizeElement(element) {
        const kind = ELEMENTS[element?.kind] ? element.kind : 'text';
        return Object.assign(elementDefault(kind), element || {}, { kind });
    }

    function buildElementHtml(element) {
        if (element.kind === 'heading') {
            const level = ['h2', 'h3', 'h4'].includes(element.level) ? element.level : 'h3';
            const align = ['left', 'center', 'right'].includes(element.align) ? element.align : 'left';
            return `<${level} class="dl-row-heading dl-align-${align}">${h(element.text || '')}</${level}>`;
        }
        if (element.kind === 'text') return `<div class="dl-row-text">${element.html || ''}</div>`;
        if (element.kind === 'image') {
            const image = element.imageUrl
                ? `<img src="${h(element.imageUrl)}" alt="${h(element.altText || '')}">`
                : '<div class="dl-row-image-empty">Chưa có ảnh</div>';
            const media = element.linkUrl ? `<a href="${h(element.linkUrl)}">${image}</a>` : image;
            const caption = element.caption ? `<figcaption>${h(element.caption)}</figcaption>` : '';
            return `<figure class="dl-row-image">${media}${caption}</figure>`;
        }
        if (element.kind === 'button') {
            const style = ['primary', 'outline', 'ghost'].includes(element.style) ? element.style : 'primary';
            const align = ['left', 'center', 'right'].includes(element.align) ? element.align : 'left';
            return `<div class="dl-row-button-wrap dl-align-${align}"><a class="dl-row-button dl-row-button-${style}" href="${h(element.url || '#')}">${h(element.text || 'Xem thêm')}</a></div>`;
        }
        if (element.kind === 'divider') {
            const style = ['solid', 'dashed', 'soft'].includes(element.style) ? element.style : 'solid';
            return `<div class="dl-row-divider dl-row-divider-${style}">${element.label ? `<span>${h(element.label)}</span>` : ''}</div>`;
        }
        if (element.kind === 'spacer') {
            const size = ['sm', 'md', 'lg', 'xl'].includes(element.size) ? element.size : 'md';
            return `<div class="dl-row-spacer dl-row-spacer-${size}"></div>`;
        }
        return `<div class="dl-row-html">${element.html || ''}</div>`;
    }

    function buildRowHtml(content) {
        const row = normalizeRow(content);
        const columns = row.columns.map((column, index) => {
            const elements = column.elements.map(buildElementHtml).join('');
            return `<div class="dl-builder-column dl-builder-column-${index + 1}">${elements || '<div class="dl-row-column-empty"></div>'}</div>`;
        }).join('');
        return `<div class="dl-builder-row dl-row-layout-${h(row.layout)} dl-row-gap-${h(row.gap)} dl-row-align-${h(row.align)} dl-row-theme-${h(row.theme)} dl-row-pad-${h(row.padding)} dl-row-mobile-${h(row.mobile)}">${columns}</div>`;
    }

    function safePreviewHtml(markup) {
        const parser = new DOMParser();
        const doc = parser.parseFromString(`<div>${markup}</div>`, 'text/html');
        doc.querySelectorAll('script,style,iframe,object,embed,link,meta,form').forEach(node => node.remove());
        doc.querySelectorAll('*').forEach(node => {
            [...node.attributes].forEach(attr => {
                const name = attr.name.toLowerCase();
                const value = attr.value.trim().toLowerCase();
                if (name.startsWith('on') || ((name === 'href' || name === 'src') && value.startsWith('javascript:'))) node.removeAttribute(attr.name);
            });
        });
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
            this.observer = new MutationObserver(() => this.enhance());
        }

        async mount() {
            await this.refreshSections();
            this.observer.observe(document.body, { childList: true, subtree: true, attributes: true, attributeFilter: ['class'] });
            this.enhance();
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
        }

        enhanceToolbar() {
            const actions = document.querySelector('.pve-toolbar-actions');
            if (!actions || actions.querySelector('[data-row-builder-add]')) return;
            const button = document.createElement('button');
            button.type = 'button';
            button.dataset.rowBuilderAdd = '1';
            button.className = 'pve-row-builder-add';
            button.textContent = '＋ Row / cột';
            button.hidden = !document.body.classList.contains('pve-editing');
            const add = actions.querySelector('[data-add]');
            if (add) add.after(button); else actions.prepend(button);
            button.addEventListener('click', () => this.open(null));

            const sync = () => { button.hidden = !document.body.classList.contains('pve-editing'); };
            const bodyObserver = new MutationObserver(sync);
            bodyObserver.observe(document.body, { attributes: true, attributeFilter: ['class'] });
        }

        enhanceCoreDrawer() {
            const drawer = document.querySelector('.pve-drawer:not(.pve-row-builder-drawer)');
            const form = drawer?.querySelector('[data-editor-form]');
            const body = form?.querySelector('.pve-drawer-body');
            if (!body || body.querySelector('[data-row-builder-shortcut]')) return;
            const shortcut = document.createElement('button');
            shortcut.type = 'button';
            shortcut.dataset.rowBuilderShortcut = '1';
            shortcut.className = 'pve-row-builder-shortcut';
            shortcut.innerHTML = '<strong>Row / Column Builder</strong><span>Tạo 1–4 cột và thêm nhiều phần tử bên trong như Flatsome.</span>';
            body.prepend(shortcut);
            shortcut.addEventListener('click', () => {
                drawer.remove();
                this.open(null);
            });
        }

        enhanceRowSections() {
            document.querySelectorAll('.pve-editable-section[data-pve-section-id]').forEach(sectionEl => {
                const section = this.rows.get(String(sectionEl.dataset.pveSectionId));
                if (!section) return;
                sectionEl.classList.add('pve-row-builder-section');
                const controls = sectionEl.querySelector(':scope > .pve-section-controls');
                if (!controls) return;
                const label = controls.querySelector('span');
                if (label) label.textContent = 'Row / cột';
                const edit = controls.querySelector('[data-action="edit"]');
                if (!edit || edit.dataset.rowBuilderBound === '1') return;
                edit.dataset.rowBuilderBound = '1';
                edit.addEventListener('click', event => {
                    event.preventDefault();
                    event.stopPropagation();
                    this.open(section);
                }, true);
            });
        }

        open(section) {
            this.close();
            this.current = section || null;
            this.state = normalizeRow(section ? json(section.contentJson) : defaultRow());
            const drawer = document.createElement('section');
            drawer.className = 'pve-drawer pve-drawer-wide pve-row-builder-drawer';
            drawer.setAttribute('role', 'dialog');
            drawer.setAttribute('aria-modal', 'true');
            drawer.innerHTML = `
                <header>
                    <div><small>ROW / COLUMN BUILDER</small><h2>${section ? h(section.name || 'Chỉnh Row') : 'Tạo Row mới'}</h2></div>
                    <button type="button" data-close aria-label="Đóng">×</button>
                </header>
                <form data-row-form>
                    <div class="pve-drawer-body">
                        <div class="pve-row-builder-settings">
                            <label class="pve-field"><span>Tên quản trị</span><input name="rowName" value="${h(section?.name || 'Row nội dung')}" maxlength="120"></label>
                            <label class="pve-check pve-check-top"><input type="checkbox" name="rowVisible"${section?.isVisible === false ? '' : ' checked'}><span>Hiển thị Row</span></label>
                            <label class="pve-field"><span>Bố cục cột</span>${select('rowLayout', this.state.layout, Object.entries(LAYOUTS).map(([key, value]) => [key, value.label]))}</label>
                            <label class="pve-field"><span>Khoảng giữa cột</span>${select('rowGap', this.state.gap, [['none', 'Không'], ['sm', 'Nhỏ'], ['md', 'Vừa'], ['lg', 'Lớn']])}</label>
                            <label class="pve-field"><span>Căn dọc</span>${select('rowAlign', this.state.align, [['top', 'Trên'], ['center', 'Giữa'], ['bottom', 'Dưới'], ['stretch', 'Căng đều']])}</label>
                            <label class="pve-field"><span>Nền</span>${select('rowTheme', this.state.theme, [['plain', 'Trong suốt'], ['soft', 'Xanh nhạt'], ['cream', 'Kem'], ['dark', 'Tối']])}</label>
                            <label class="pve-field"><span>Padding</span>${select('rowPadding', this.state.padding, [['none', 'Không'], ['sm', 'Nhỏ'], ['md', 'Vừa'], ['lg', 'Lớn'], ['xl', 'Rất lớn']])}</label>
                            <label class="pve-field"><span>Mobile</span>${select('rowMobile', this.state.mobile, [['stack', 'Xếp cột theo thứ tự'], ['reverse', 'Đảo thứ tự cột']])}</label>
                        </div>
                        <div class="pve-row-columns" data-row-columns></div>
                        <section class="pve-row-preview-wrap">
                            <div class="pve-row-preview-head"><strong>Xem trước nhanh</strong><small>HTML nguy hiểm bị loại khỏi preview; server vẫn sanitize khi lưu.</small></div>
                            <div class="pve-row-preview" data-row-preview></div>
                        </section>
                    </div>
                    <footer>
                        <span>${section ? 'Có thể dùng ↑ / ↓ ngoài trang để đổi vị trí Row.' : 'Row mới sẽ được thêm cuối trang; dùng ↑ / ↓ để di chuyển.'}</span>
                        <div><button type="button" data-close>Hủy</button><button type="submit" class="pve-primary">Lưu Row</button></div>
                    </footer>
                </form>`;
            document.body.appendChild(drawer);
            this.drawer = drawer;
            drawer.querySelectorAll('[data-close]').forEach(button => button.addEventListener('click', () => this.close()));
            drawer.querySelector('[data-row-form]').addEventListener('submit', event => { event.preventDefault(); this.save(); });
            drawer.querySelector('[name="rowLayout"]').addEventListener('change', event => this.changeLayout(event.target.value));
            ['rowGap', 'rowAlign', 'rowTheme', 'rowPadding', 'rowMobile'].forEach(name => {
                drawer.querySelector(`[name="${name}"]`).addEventListener('change', event => {
                    const key = name.replace('row', '').replace(/^./, c => c.toLowerCase());
                    this.state[key] = event.target.value;
                    this.updatePreview();
                });
            });
            this.renderColumns();
            this.updatePreview();
        }

        changeLayout(layout) {
            if (!LAYOUTS[layout]) return;
            const count = LAYOUTS[layout].count;
            const old = this.state.columns || [];
            this.state.layout = layout;
            this.state.columns = Array.from({ length: count }, (_, index) => old[index] || { elements: [] });
            this.renderColumns();
            this.updatePreview();
        }

        renderColumns() {
            if (!this.drawer) return;
            const root = this.drawer.querySelector('[data-row-columns]');
            root.innerHTML = this.state.columns.map((column, colIndex) => `
                <article class="pve-row-column" data-column="${colIndex}">
                    <header><div><small>CỘT ${colIndex + 1}</small><strong>${column.elements.length} phần tử</strong></div></header>
                    <div class="pve-row-elements">${column.elements.map((element, elementIndex) => this.elementCard(element, colIndex, elementIndex)).join('') || '<div class="pve-row-empty">Cột đang trống. Thêm phần tử bên dưới.</div>'}</div>
                    <div class="pve-row-add-element">
                        ${select(`addKind${colIndex}`, 'text', Object.entries(ELEMENTS))}
                        <button type="button" data-add-element="${colIndex}">＋ Thêm phần tử</button>
                    </div>
                </article>`).join('');
            this.bindColumnEvents();
        }

        elementCard(element, colIndex, elementIndex) {
            const commonHead = `<div class="pve-row-element-head"><div><small>${ELEMENTS[element.kind] || element.kind}</small><strong>Phần tử ${elementIndex + 1}</strong></div><div><button type="button" data-element-action="up" data-col="${colIndex}" data-index="${elementIndex}" title="Đưa lên">↑</button><button type="button" data-element-action="down" data-col="${colIndex}" data-index="${elementIndex}" title="Đưa xuống">↓</button><button type="button" data-element-action="duplicate" data-col="${colIndex}" data-index="${elementIndex}" title="Nhân bản">⧉</button><button type="button" class="danger" data-element-action="delete" data-col="${colIndex}" data-index="${elementIndex}" title="Xóa">×</button></div></div>`;
            let fields = '';
            if (element.kind === 'heading') fields = `<label class="pve-field"><span>Nội dung</span><input data-element-field="text" value="${h(element.text || '')}"></label><div class="pve-grid-2"><label class="pve-field"><span>Cấp</span>${select('level', element.level || 'h3', [['h2', 'H2'], ['h3', 'H3'], ['h4', 'H4']], 'data-element-field="level"')}</label><label class="pve-field"><span>Căn chữ</span>${select('align', element.align || 'left', [['left', 'Trái'], ['center', 'Giữa'], ['right', 'Phải']], 'data-element-field="align"')}</label></div>`;
            else if (element.kind === 'text') fields = `<label class="pve-field"><span>HTML văn bản</span><textarea rows="7" data-element-field="html">${h(element.html || '')}</textarea></label>`;
            else if (element.kind === 'image') fields = `<div class="pve-field"><span>Ảnh</span><div class="pve-image-row"><input data-element-field="imageUrl" value="${h(element.imageUrl || '')}"><label class="pve-upload">Tải ảnh<input type="file" accept="image/png,image/jpeg,image/webp" data-row-upload data-col="${colIndex}" data-index="${elementIndex}" hidden></label></div></div><label class="pve-field"><span>Alt text</span><input data-element-field="altText" value="${h(element.altText || '')}"></label><label class="pve-field"><span>Chú thích</span><input data-element-field="caption" value="${h(element.caption || '')}"></label><label class="pve-field"><span>Link khi bấm</span><input data-element-field="linkUrl" value="${h(element.linkUrl || '')}"></label>`;
            else if (element.kind === 'button') fields = `<div class="pve-grid-2"><label class="pve-field"><span>Chữ trên nút</span><input data-element-field="text" value="${h(element.text || '')}"></label><label class="pve-field"><span>URL</span><input data-element-field="url" value="${h(element.url || '')}"></label><label class="pve-field"><span>Kiểu</span>${select('style', element.style || 'primary', [['primary', 'Nút chính'], ['outline', 'Viền'], ['ghost', 'Nhẹ']], 'data-element-field="style"')}</label><label class="pve-field"><span>Căn</span>${select('align', element.align || 'left', [['left', 'Trái'], ['center', 'Giữa'], ['right', 'Phải']], 'data-element-field="align"')}</label></div>`;
            else if (element.kind === 'divider') fields = `<div class="pve-grid-2"><label class="pve-field"><span>Nhãn</span><input data-element-field="label" value="${h(element.label || '')}"></label><label class="pve-field"><span>Kiểu dòng</span>${select('style', element.style || 'solid', [['solid', 'Liền'], ['dashed', 'Nét đứt'], ['soft', 'Mảnh']], 'data-element-field="style"')}</label></div>`;
            else if (element.kind === 'spacer') fields = `<label class="pve-field"><span>Khoảng cách</span>${select('size', element.size || 'md', [['sm', 'Nhỏ'], ['md', 'Vừa'], ['lg', 'Lớn'], ['xl', 'Rất lớn']], 'data-element-field="size"')}</label>`;
            else fields = `<label class="pve-field"><span>HTML</span><textarea rows="8" data-element-field="html">${h(element.html || '')}</textarea></label>`;
            return `<section class="pve-row-element" data-col="${colIndex}" data-index="${elementIndex}">${commonHead}${fields}</section>`;
        }

        bindColumnEvents() {
            if (!this.drawer) return;
            this.drawer.querySelectorAll('[data-add-element]').forEach(button => button.addEventListener('click', () => {
                const col = Number(button.dataset.addElement);
                const selectEl = this.drawer.querySelector(`[name="addKind${col}"]`);
                if (!this.state.columns[col] || this.state.columns[col].elements.length >= MAX_ELEMENTS) {
                    this.toast(`Mỗi cột tối đa ${MAX_ELEMENTS} phần tử.`, true);
                    return;
                }
                this.state.columns[col].elements.push(elementDefault(selectEl?.value || 'text'));
                this.renderColumns();
                this.updatePreview();
            }));

            this.drawer.querySelectorAll('[data-element-action]').forEach(button => button.addEventListener('click', () => {
                const col = Number(button.dataset.col);
                const index = Number(button.dataset.index);
                const list = this.state.columns[col]?.elements;
                if (!list?.[index]) return;
                const action = button.dataset.elementAction;
                if (action === 'delete') list.splice(index, 1);
                if (action === 'duplicate' && list.length < MAX_ELEMENTS) list.splice(index + 1, 0, clone(list[index]));
                if (action === 'up' && index > 0) [list[index - 1], list[index]] = [list[index], list[index - 1]];
                if (action === 'down' && index < list.length - 1) [list[index + 1], list[index]] = [list[index], list[index + 1]];
                this.renderColumns();
                this.updatePreview();
            }));

            this.drawer.querySelectorAll('.pve-row-element').forEach(card => {
                const col = Number(card.dataset.col);
                const index = Number(card.dataset.index);
                card.querySelectorAll('[data-element-field]').forEach(input => {
                    const update = () => {
                        const element = this.state.columns[col]?.elements[index];
                        if (!element) return;
                        element[input.dataset.elementField] = input.value;
                        this.updatePreview();
                    };
                    input.addEventListener(input.tagName === 'SELECT' ? 'change' : 'input', update);
                });
                card.querySelector('[data-row-upload]')?.addEventListener('change', event => this.upload(event));
            });
        }

        async upload(event) {
            const file = event.target.files?.[0];
            event.target.value = '';
            if (!file) return;
            const col = Number(event.target.dataset.col);
            const index = Number(event.target.dataset.index);
            const element = this.state.columns[col]?.elements[index];
            if (!element) return;
            const label = event.target.closest('.pve-upload');
            const text = label?.childNodes?.[0];
            const old = text?.textContent || 'Tải ảnh';
            if (text) text.textContent = 'Đang tải…';
            try {
                const form = new FormData();
                form.append('file', file);
                const asset = await DeLongApi.postForm(`${this.api}/assets/section`, form);
                element.imageUrl = asset.url || '';
                this.renderColumns();
                this.updatePreview();
                this.toast('Đã tải ảnh. Bấm Lưu Row để áp dụng.');
            } catch (error) {
                this.toast(error.message || 'Không thể tải ảnh.', true);
            } finally {
                if (text) text.textContent = old;
            }
        }

        updatePreview() {
            if (!this.drawer) return;
            const preview = this.drawer.querySelector('[data-row-preview]');
            if (preview) preview.innerHTML = safePreviewHtml(buildRowHtml(this.state));
        }

        async save() {
            if (!this.drawer) return;
            const form = this.drawer.querySelector('[data-row-form]');
            const submit = form.querySelector('button[type="submit"]');
            submit.disabled = true;
            submit.textContent = 'Đang lưu…';
            const content = normalizeRow(this.state);
            content.html = buildRowHtml(content);
            const payload = {
                type: 'RichText',
                name: form.elements.rowName.value.trim() || 'Row nội dung',
                variant: 'wide',
                isVisible: form.elements.rowVisible.checked,
                contentJson: JSON.stringify(content)
            };
            if (payload.contentJson.length > 28000) {
                submit.disabled = false;
                submit.textContent = 'Lưu Row';
                this.toast('Row quá lớn. Hãy tách thành hai Row để website nhẹ và dễ chỉnh hơn.', true);
                return;
            }
            try {
                if (this.current) await DeLongApi.put(`${this.api}/sections/${this.current.id}`, payload);
                else await DeLongApi.post(`${this.api}/sections`, payload);
                setSession(this.context);
                window.location.reload();
            } catch (error) {
                submit.disabled = false;
                submit.textContent = 'Lưu Row';
                this.toast(error.message || 'Không thể lưu Row.', true);
            }
        }

        close() {
            this.drawer?.remove();
            this.drawer = null;
            this.current = null;
        }

        toast(message, error) {
            let node = document.querySelector('.pve-toast');
            if (!node) {
                node = document.createElement('div');
                node.className = 'pve-toast';
                document.body.appendChild(node);
            }
            node.className = `pve-toast ${error ? 'error' : 'success'}`;
            node.textContent = message;
            node.hidden = false;
            clearTimeout(node._rowTimer);
            node._rowTimer = setTimeout(() => { node.hidden = true; }, 3000);
        }
    }

    DeLongApi.get(contextUrl)
        .then(context => { if (context?.canEdit) new RowBuilder(context).mount(); })
        .catch(() => {});
})();
