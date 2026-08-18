(function (global) {
    const R = global.DeLongRowInline;
    const U = global.DeLongRowInlineUI;
    if (!R || !U) return;

    const KNOWN = new Set(Object.keys(R.LABELS || {}));
    const esc = value => String(value ?? '').replace(/[&<>"']/g, char => ({
        '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
    }[char]));

    function responsive() {
        return {
            desktop: { visible: true, size: 'auto', align: 'inherit' },
            tablet: { visible: true, size: 'auto', align: 'inherit' },
            mobile: { visible: true, size: 'auto', align: 'inherit' }
        };
    }

    function elementDefault(kind) {
        const r = responsive();
        if (kind === 'heading') return { kind, text: 'Tiêu đề mới', level: 'h3', align: 'left', responsive: r };
        if (kind === 'text') return { kind, html: '<p>Nhập nội dung của bạn.</p>', responsive: r };
        if (kind === 'image') return { kind, imageUrl: '', altText: '', caption: '', linkUrl: '', responsive: r };
        if (kind === 'button') return { kind, text: 'Xem thêm', url: '#', style: 'primary', align: 'left', responsive: r };
        if (kind === 'divider') return { kind, style: 'solid', label: '', responsive: r };
        if (kind === 'spacer') return { kind, size: 'md', responsive: r };
        return { kind: 'html', html: '<div>Nội dung HTML</div>', responsive: r };
    }

    function normalizeMalformed(content) {
        let changed = false;
        if (!content?.columns) return changed;
        content.columns.forEach(column => (column?.elements || []).forEach(element => {
            if (!element?.kind && KNOWN.has(element?.k)) {
                element.kind = element.k;
                delete element.k;
                changed = true;
            }
        }));
        return changed;
    }

    const originalContent = R.content;
    R.content = function (section) {
        const content = originalContent(section);
        if (content) normalizeMalformed(content);
        return content;
    };
    R.elementDefault = elementDefault;

    R.add = async function (id, col, kind) {
        const section = R.state.sections.get(String(id));
        const before = R.clone(R.content(section));
        const next = R.clone(before);
        const list = next?.columns?.[col]?.elements;
        const column = R.columnEl(id, col);
        if (!section || !list || !column || !KNOWN.has(kind)) return null;
        if (list.length >= R.MAX) {
            R.toast(`Mỗi cột tối đa ${R.MAX} phần tử.`, true);
            return null;
        }

        const item = elementDefault(kind);
        list.push(item);
        column.querySelector(':scope > .dl-row-column-empty')?.remove();
        const holder = document.createElement('div');
        holder.innerHTML = R.renderElement(item);
        const add = column.querySelector(':scope > [data-row-inline-add]');
        column.insertBefore(holder.firstElementChild, add || null);

        const saved = await R.save(section, next, R.rowEl(id));
        if (!saved) {
            R.restore(section, before);
            return null;
        }
        return { index: list.length - 1, item };
    };

    const SIZE_LABELS = {
        sm: 'Nhỏ · 20px',
        md: 'Vừa · 42px',
        lg: 'Lớn · 70px',
        xl: 'Rất lớn · 108px'
    };

    function ensureBasicInspector() {
        if (U.basicInspector) return U.basicInspector;
        const node = document.createElement('div');
        node.className = 'pve-row-inline-inspector pve-row-basic-inspector';
        node.dataset.rowInlineUi = '1';
        node.hidden = true;
        document.body.appendChild(node);
        U.basicInspector = node;
        return node;
    }

    function previewMarkup(type, element) {
        return `<div class="pve-row-basic-preview">${R.renderElement(element)}</div>`;
    }

    function formMarkup(type, element) {
        if (type === 'divider') {
            return `<div class="pve-row-inline-panel-head"><strong>Phân cách</strong><button type="button" data-ri-basic-close>×</button></div>
                <div class="pve-row-inline-inspector-body">
                    <label><span>Kiểu đường</span><select name="style">
                        <option value="solid"${element.style === 'solid' ? ' selected' : ''}>Liền</option>
                        <option value="dashed"${element.style === 'dashed' ? ' selected' : ''}>Nét đứt</option>
                        <option value="soft"${element.style === 'soft' ? ' selected' : ''}>Nhẹ</option>
                    </select></label>
                    <label><span>Nhãn ở giữa (tùy chọn)</span><input name="label" value="${esc(element.label || '')}" placeholder="Ví dụ: Tiện ích"></label>
                    <small class="pve-row-basic-help">Dùng để tách hai nhóm nội dung. Để trống nhãn nếu chỉ cần một đường phân cách.</small>
                    ${previewMarkup(type, element)}
                </div>
                <div class="pve-row-inline-panel-foot"><button type="button" data-ri-basic-builder>Nâng cao</button><button type="button" data-ri-basic-cancel>Hủy</button><button type="button" class="primary" data-ri-basic-save>Lưu</button></div>`;
        }

        return `<div class="pve-row-inline-panel-head"><strong>Khoảng cách</strong><button type="button" data-ri-basic-close>×</button></div>
            <div class="pve-row-inline-inspector-body">
                <label><span>Chiều cao khoảng trống</span><select name="size">
                    ${Object.entries(SIZE_LABELS).map(([value, label]) => `<option value="${value}"${element.size === value ? ' selected' : ''}>${label}</option>`).join('')}
                </select></label>
                <small class="pve-row-basic-help">Khoảng cách là vùng trống có chủ đích giữa các element, không phải một khối văn bản.</small>
                ${previewMarkup(type, element)}
            </div>
            <div class="pve-row-inline-panel-foot"><button type="button" data-ri-basic-builder>Nâng cao</button><button type="button" data-ri-basic-cancel>Hủy</button><button type="button" class="primary" data-ri-basic-save>Lưu</button></div>`;
    }

    function closeBasicInspector() {
        const node = U.basicInspector;
        if (!node) return;
        node.hidden = true;
        node._model = null;
    }

    function openBasicInspector(id, col, index, shell) {
        U.closeFloat?.();
        closeBasicInspector();
        const section = R.state.sections.get(String(id));
        const content = R.content(section);
        const element = content?.columns?.[col]?.elements?.[index];
        if (!section || !['divider', 'spacer'].includes(element?.kind)) return;

        const node = ensureBasicInspector();
        node._model = { id: String(id), col, index, shell, content: R.clone(content), type: element.kind };
        node.innerHTML = formMarkup(element.kind, element);
        node.hidden = false;
        U.position(node, shell);
    }

    function readBasic(node, model) {
        const element = model.content?.columns?.[model.col]?.elements?.[model.index];
        if (!element) return null;
        if (model.type === 'divider') {
            element.style = node.querySelector('[name="style"]')?.value || 'solid';
            element.label = node.querySelector('[name="label"]')?.value.trim() || '';
        } else {
            element.size = node.querySelector('[name="size"]')?.value || 'md';
        }
        return element;
    }

    function updateBasicPreview() {
        const node = U.basicInspector, model = node?._model;
        if (!node || !model) return;
        const draft = R.clone(model.content);
        const tempModel = { ...model, content: draft };
        const element = readBasic(node, tempModel);
        const preview = node.querySelector('.pve-row-basic-preview');
        if (preview && element) preview.innerHTML = R.renderElement(element);
    }

    async function saveBasicInspector() {
        const node = U.basicInspector, model = node?._model;
        if (!node || !model) return;
        const section = R.state.sections.get(model.id);
        const before = R.clone(R.content(section));
        const element = readBasic(node, model);
        if (!section || !element) return closeBasicInspector();

        model.shell.outerHTML = R.renderElement(element);
        const button = node.querySelector('[data-ri-basic-save]');
        if (button) { button.disabled = true; button.textContent = 'Đang lưu…'; }
        const saved = await R.save(section, model.content, R.rowEl(model.id));
        if (button) { button.disabled = false; button.textContent = 'Lưu'; }
        if (!saved) {
            R.restore(section, before);
            global.dispatchEvent(new Event('delong:row-inline-ui-refresh'));
            return;
        }
        closeBasicInspector();
        global.dispatchEvent(new Event('delong:row-inline-ui-refresh'));
    }

    function basicModelFromShell(shell) {
        if (!shell) return null;
        const id = shell.dataset.rowInlineSection || shell.closest('[data-pve-section-id]')?.dataset.pveSectionId;
        const col = Number(shell.dataset.rowInlineCol);
        const index = Number(shell.dataset.rowInlineIndex);
        const element = R.content(R.state.sections.get(String(id)))?.columns?.[col]?.elements?.[index];
        return ['divider', 'spacer'].includes(element?.kind) ? { id, col, index, element } : null;
    }

    function decorateBasicElements() {
        if (!document.body.classList.contains('pve-editing')) return;
        document.querySelectorAll('.pve-row-inline-element').forEach(shell => {
            const model = basicModelFromShell(shell);
            shell.classList.toggle('pve-row-basic-divider', model?.element?.kind === 'divider');
            shell.classList.toggle('pve-row-basic-spacer', model?.element?.kind === 'spacer');
            const spacer = shell.querySelector('.dl-row-spacer');
            if (spacer && model?.element?.kind === 'spacer') {
                spacer.dataset.rowInlineSpacerLabel = `Khoảng cách · ${SIZE_LABELS[model.element.size] || SIZE_LABELS.md}`;
            }
        });
    }

    let decorateQueued = false;
    function queueDecorate() {
        if (decorateQueued) return;
        decorateQueued = true;
        requestAnimationFrame(() => { decorateQueued = false; decorateBasicElements(); });
    }

    document.addEventListener('click', event => {
        const basicButton = event.target.closest('[data-ri-basic-close],[data-ri-basic-cancel],[data-ri-basic-save],[data-ri-basic-builder]');
        if (basicButton) {
            event.preventDefault();
            event.stopImmediatePropagation();
            if (basicButton.hasAttribute('data-ri-basic-save')) saveBasicInspector();
            else if (basicButton.hasAttribute('data-ri-basic-builder')) {
                const id = U.basicInspector?._model?.id;
                closeBasicInspector();
                if (id) U.openBuilder(id);
            } else closeBasicInspector();
            return;
        }

        if (!document.body.classList.contains('pve-editing')) return;
        const action = event.target.closest('[data-ri-action="edit"]');
        const shell = action?.closest('.pve-row-inline-element') || event.target.closest('.pve-row-inline-element');
        const model = basicModelFromShell(shell);
        if (!model) return;
        if (!action && !event.target.closest('.dl-row-divider,.dl-row-spacer')) return;

        event.preventDefault();
        event.stopPropagation();
        event.stopImmediatePropagation();
        openBasicInspector(model.id, model.col, model.index, shell);
    }, true);

    document.addEventListener('input', event => {
        if (event.target.closest('.pve-row-basic-inspector')) updateBasicPreview();
    });
    document.addEventListener('change', event => {
        if (event.target.closest('.pve-row-basic-inspector')) updateBasicPreview();
    });
    document.addEventListener('pointerdown', event => {
        const node = U.basicInspector;
        if (node && !node.hidden && !node.contains(event.target) && !event.target.closest('.pve-row-basic-divider,.pve-row-basic-spacer')) closeBasicInspector();
    });
    document.addEventListener('keydown', event => {
        if (event.key === 'Escape' && U.basicInspector && !U.basicInspector.hidden) {
            event.preventDefault();
            closeBasicInspector();
        }
    });

    new MutationObserver(queueDecorate).observe(document.body, { childList: true, subtree: true, attributes: true, attributeFilter: ['class'] });
    global.addEventListener('delong:row-inline-changed', queueDecorate);
    global.addEventListener('delong:row-inline-ui-refresh', queueDecorate);

    async function repairMalformedRows() {
        const canEdit = await R.ready;
        if (!canEdit) return;
        for (const section of R.state.sections.values()) {
            const raw = originalContent(section);
            if (!raw || !normalizeMalformed(raw)) continue;

            raw.columns.forEach((column, col) => {
                const host = R.columnEl(section.id, col);
                const shells = R.shells(host);
                (column.elements || []).forEach((element, index) => {
                    if (shells[index]) shells[index].outerHTML = R.renderElement(element);
                });
            });
            await R.save(section, raw, R.rowEl(section.id), { message: false });
        }
        queueDecorate();
    }

    repairMalformedRows().catch(() => {});
})(window);
