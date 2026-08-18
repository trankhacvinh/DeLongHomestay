(function () {
    if (!document.body?.classList.contains('public-body') || !window.DeLongApi) return;

    const pageId = document.querySelector('meta[name="delong-custom-page-id"]')?.content || '';
    const pageSlug = document.querySelector('meta[name="delong-custom-page-slug"]')?.content || '';
    if (!pageId || !pageSlug) return;

    const normalizedPath = window.location.pathname.replace(/\/+$/, '') || '/';
    const scoped = normalizedPath.match(/^\/h\/([^/]+)(?:\/|$)/i);
    const siteSlug = scoped ? decodeURIComponent(scoped[1]) : '';
    const params = new URLSearchParams();
    if (siteSlug) params.set('siteSlug', siteSlug);
    params.set('pageSlug', pageSlug);
    const contextUrl = `/api/admin/site/visual-context?${params}`;
    const state = { context: null, api: '', panel: null, target: null, queued: false };

    const ALIGN = [['auto','Tự động'],['left','Trái'],['center','Giữa'],['right','Phải']];
    const TEXT_SIZE = [['auto','Mặc định'],['xs','Rất nhỏ'],['sm','Nhỏ'],['md','Vừa'],['lg','Lớn'],['xl','Rất lớn'],['hero','Hero']];
    const TEXT_WIDTH = [['auto','Mặc định'],['narrow','Gọn'],['content','Vừa'],['wide','Rộng'],['full','Full']];
    const IMAGE_WIDTH = [['auto','Mặc định'],['sm','Gọn'],['md','Vừa'],['lg','Rộng'],['full','Full khối']];

    function parseJson(value) { try { return JSON.parse(value || '{}'); } catch { return {}; } }
    function clone(value) { return JSON.parse(JSON.stringify(value ?? {})); }
    function h(value) { return String(value ?? '').replace(/[&<>"']/g, ch => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[ch])); }

    function getAt(object, path) {
        return String(path || '').split('.').filter(Boolean).reduce((value, part) => value == null ? undefined : value[part], object);
    }

    function setAt(object, path, value) {
        const parts = String(path || '').split('.').filter(Boolean);
        if (!parts.length) return;
        let cursor = object;
        for (let index = 0; index < parts.length - 1; index += 1) {
            const key = parts[index];
            if (!cursor[key] || typeof cursor[key] !== 'object' || Array.isArray(cursor[key])) cursor[key] = {};
            cursor = cursor[key];
        }
        cursor[parts[parts.length - 1]] = value;
    }

    function cleanStyle(style, image) {
        const result = {
            align: ALIGN.some(item => item[0] === style?.align) ? style.align : 'auto',
            width: (image ? IMAGE_WIDTH : TEXT_WIDTH).some(item => item[0] === style?.width) ? style.width : 'auto'
        };
        if (!image) result.size = TEXT_SIZE.some(item => item[0] === style?.size) ? style.size : 'auto';
        return result;
    }

    function visualStyle(content, key, image) {
        return cleanStyle(getAt(content?._visual || {}, key) || {}, image);
    }

    async function ensureContext() {
        if (state.context) return state.context;
        state.context = await DeLongApi.get(contextUrl);
        if (!state.context?.canEdit) throw new Error('Bạn không có quyền chỉnh trang này.');
        state.api = state.context.sectionApi || state.context.siteApi || '';
        if (!state.api) throw new Error('Không xác định được API của trang.');
        return state.context;
    }

    async function freshSection(sectionId) {
        await ensureContext();
        const data = await DeLongApi.get(`${state.api}/`);
        const sections = data?.sections || data?.site?.sections || [];
        return sections.find(section => String(section.id) === String(sectionId)) || null;
    }

    function toast(message, error) {
        let node = document.querySelector('.pve-toast');
        if (!node) { node = document.createElement('div'); node.className = 'pve-toast'; document.body.appendChild(node); }
        node.className = `pve-toast ${error ? 'error' : 'success'}`;
        node.textContent = message; node.hidden = false;
        clearTimeout(node._cpStyleTimer); node._cpStyleTimer = setTimeout(() => { node.hidden = true; }, 3200);
    }

    function styleClasses(image) {
        return image
            ? ['cp-image-align-auto','cp-image-align-left','cp-image-align-center','cp-image-align-right','cp-image-width-auto','cp-image-width-sm','cp-image-width-md','cp-image-width-lg','cp-image-width-full']
            : ['cp-align-auto','cp-align-left','cp-align-center','cp-align-right','cp-size-auto','cp-size-xs','cp-size-sm','cp-size-md','cp-size-lg','cp-size-xl','cp-size-hero','cp-width-auto','cp-width-narrow','cp-width-content','cp-width-wide','cp-width-full'];
    }

    function applyPreview(target, style, image) {
        if (!target) return;
        target.classList.remove(...styleClasses(image));
        const normalized = cleanStyle(style, image);
        if (normalized.align !== 'auto') target.classList.add(`${image ? 'cp-image-align' : 'cp-align'}-${normalized.align}`);
        if (normalized.width !== 'auto') target.classList.add(`${image ? 'cp-image-width' : 'cp-width'}-${normalized.width}`);
        if (!image && normalized.size !== 'auto') target.classList.add(`cp-size-${normalized.size}`);
    }

    function options(items, value) {
        return items.map(([key,label]) => `<option value="${h(key)}"${key === value ? ' selected' : ''}>${h(label)}</option>`).join('');
    }

    function ensurePanel() {
        if (state.panel?.isConnected) return state.panel;
        const panel = document.createElement('section');
        panel.className = 'cp-element-style-panel';
        panel.hidden = true;
        panel.setAttribute('role', 'dialog');
        panel.setAttribute('aria-label', 'Kiểu phần tử');
        document.body.appendChild(panel);
        panel.addEventListener('pointerdown', event => event.stopPropagation());
        panel.addEventListener('change', () => {
            if (!state.target) return;
            const style = readPanelStyle();
            state.target.style = style;
            applyPreview(state.target.element, style, state.target.image);
        });
        panel.addEventListener('click', event => {
            if (event.target.closest('[data-cp-style-close]')) closePanel(true);
            else if (event.target.closest('[data-cp-style-reset]')) resetPanel();
            else if (event.target.closest('[data-cp-style-save]')) savePanel();
        });
        state.panel = panel;
        return panel;
    }

    function readPanelStyle() {
        const panel = state.panel;
        if (!panel || !state.target) return {};
        const result = {
            align: panel.querySelector('[name="cpAlign"]')?.value || 'auto',
            width: panel.querySelector('[name="cpWidth"]')?.value || 'auto'
        };
        if (!state.target.image) result.size = panel.querySelector('[name="cpSize"]')?.value || 'auto';
        return result;
    }

    function placePanel(anchor) {
        const panel = state.panel;
        if (!panel || !anchor) return;
        const rect = anchor.getBoundingClientRect();
        const width = Math.min(panel.offsetWidth || 390, window.innerWidth - 20);
        let left = Math.max(10, Math.min(rect.left, window.innerWidth - width - 10));
        let top = rect.bottom + 8;
        const height = panel.offsetHeight || 280;
        if (top + height > window.innerHeight - 10) top = Math.max(10, rect.top - height - 8);
        panel.style.left = `${Math.round(left)}px`;
        panel.style.top = `${Math.round(top)}px`;
    }

    async function openPanel(target) {
        try {
            const section = await freshSection(target.sectionId);
            if (!section) throw new Error('Không tìm thấy khối đang chỉnh.');
            const content = parseJson(section.contentJson);
            const style = visualStyle(content, target.key, target.image);
            const element = target.element;
            const originalClasses = styleClasses(target.image).filter(name => element?.classList.contains(name));
            state.target = Object.assign({}, target, { section, content, style, originalClasses });
            const panel = ensurePanel();
            panel.innerHTML = `<header><div><small>KIỂU PHẦN TỬ</small><strong>${h(target.label || (target.image ? 'Ảnh' : 'Nội dung'))}</strong></div><button type="button" data-cp-style-close aria-label="Đóng">×</button></header>
                <div class="cp-element-style-fields">
                    <label><span>Căn</span><select name="cpAlign">${options(ALIGN, style.align)}</select></label>
                    ${target.image ? '' : `<label><span>Cỡ chữ</span><select name="cpSize">${options(TEXT_SIZE, style.size)}</select></label>`}
                    <label><span>${target.image ? 'Kích thước ảnh' : 'Độ rộng'}</span><select name="cpWidth">${options(target.image ? IMAGE_WIDTH : TEXT_WIDTH, style.width)}</select></label>
                </div>
                <div class="cp-element-style-hint">${target.image ? 'Full khối cho phép ảnh chiếm toàn bộ hàng của Hero/Feature.' : 'Căn, cỡ chữ và độ rộng được lưu riêng cho phần tử này.'}</div>
                <footer><button type="button" data-cp-style-reset>↺ Mặc định</button><div><button type="button" data-cp-style-close>Hủy</button><button type="button" class="primary" data-cp-style-save>Lưu kiểu</button></div></footer>`;
            panel.hidden = false;
            applyPreview(element, style, target.image);
            requestAnimationFrame(() => placePanel(target.anchor || element));
        } catch (error) {
            toast(error.message || 'Không thể mở chỉnh kiểu phần tử.', true);
        }
    }

    function resetPanel() {
        if (!state.panel || !state.target) return;
        state.panel.querySelector('[name="cpAlign"]').value = 'auto';
        state.panel.querySelector('[name="cpWidth"]').value = 'auto';
        const size = state.panel.querySelector('[name="cpSize"]');
        if (size) size.value = 'auto';
        const style = readPanelStyle();
        state.target.style = style;
        applyPreview(state.target.element, style, state.target.image);
    }

    function closePanel(restore) {
        if (!state.panel || state.panel.hidden) return;
        if (restore && state.target?.element) {
            state.target.element.classList.remove(...styleClasses(state.target.image));
            state.target.element.classList.add(...(state.target.originalClasses || []));
        }
        state.panel.hidden = true;
        state.target = null;
    }

    async function savePanel() {
        if (!state.target || !state.panel) return;
        const save = state.panel.querySelector('[data-cp-style-save]');
        save.disabled = true; save.textContent = 'Đang lưu…';
        try {
            const section = await freshSection(state.target.sectionId);
            if (!section) throw new Error('Không tìm thấy khối đang chỉnh.');
            const content = clone(parseJson(section.contentJson));
            if (!content._visual || typeof content._visual !== 'object' || Array.isArray(content._visual)) content._visual = {};
            const style = cleanStyle(readPanelStyle(), state.target.image);
            setAt(content._visual, state.target.key, style);
            const payload = {
                type: section.type,
                name: section.name || '',
                variant: section.variant || 'wide',
                isVisible: section.isVisible !== false,
                contentJson: JSON.stringify(content)
            };
            await DeLongApi.put(`${state.api}/sections/${section.id}`, payload);
            try {
                sessionStorage.setItem(`delong:pve:editing:page:${pageId}`, '1');
                sessionStorage.setItem(`delong:pve:scroll:page:${pageId}`, String(Math.max(0, window.scrollY || 0)));
            } catch { }
            closePanel(false);
            window.location.reload();
        } catch (error) {
            save.disabled = false; save.textContent = 'Lưu kiểu';
            toast(error.message || 'Không thể lưu kiểu phần tử.', true);
        }
    }

    async function openFromInlineBar(button) {
        const active = document.querySelector('.pve-inline-field.pve-inline-active');
        if (!active) return;
        const target = {
            sectionId: active.dataset.pieSectionId,
            key: active.dataset.pieKey,
            label: active.dataset.pieLabel || 'Nội dung',
            image: false,
            element: active,
            anchor: button
        };
        const save = document.querySelector('.pve-inline-bar [data-pie-save]');
        if (save && !save.disabled) {
            save.click();
            let tries = 0;
            while (document.querySelector('.pve-inline-field.pve-inline-active') && tries < 40) {
                await new Promise(resolve => setTimeout(resolve, 75));
                tries += 1;
            }
            if (document.querySelector('.pve-inline-field.pve-inline-active')) return toast('Hãy lưu nội dung trước khi chỉnh kiểu.', true);
        }
        openPanel(target);
    }

    function enhanceBar() {
        const bar = document.querySelector('.pve-inline-bar');
        const active = document.querySelector('.pve-inline-field.pve-inline-active');
        if (!bar || bar.hidden || !active) return;
        const actions = bar.querySelector('.pve-inline-bar-actions');
        if (!actions) return;
        let button = actions.querySelector('[data-cp-element-style]');
        if (!button) {
            button = document.createElement('button');
            button.type = 'button'; button.dataset.cpElementStyle = '1'; button.textContent = '↔ Kiểu';
            const advanced = actions.querySelector('[data-pie-advanced]');
            if (advanced) advanced.before(button); else actions.prepend(button);
            button.addEventListener('click', () => openFromInlineBar(button));
        }
        button.hidden = false;
    }

    function enhanceImages() {
        document.querySelectorAll('[data-pie-image]').forEach(image => {
            const host = image.closest('.pve-inline-image-host');
            if (!host || host.querySelector('[data-cp-image-style]')) return;
            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'pve-inline-image-style-button';
            button.dataset.cpImageStyle = '1';
            button.innerHTML = '<span>↔</span> Kiểu ảnh';
            host.appendChild(button);
            button.addEventListener('click', event => {
                event.preventDefault(); event.stopPropagation();
                openPanel({
                    sectionId: image.dataset.pieSectionId,
                    key: image.dataset.pieImage,
                    label: 'Ảnh', image: true, element: host, anchor: button
                });
            });
        });
    }

    function sync() {
        if (!document.body.classList.contains('pve-editing')) {
            closePanel(true);
            document.querySelectorAll('[data-cp-image-style]').forEach(node => node.remove());
            return;
        }
        enhanceBar();
        enhanceImages();
    }

    function queue() {
        if (state.queued) return;
        state.queued = true;
        requestAnimationFrame(() => { state.queued = false; sync(); });
    }

    document.addEventListener('keydown', event => {
        if (event.key === 'Escape' && state.panel && !state.panel.hidden) {
            event.preventDefault(); event.stopPropagation(); closePanel(true);
        }
    }, true);
    document.addEventListener('pointerdown', event => {
        if (state.panel && !state.panel.hidden && !event.target.closest('.cp-element-style-panel') && !event.target.closest('[data-cp-element-style],[data-cp-image-style]')) closePanel(true);
    });
    window.addEventListener('scroll', () => { if (state.panel && !state.panel.hidden && state.target) placePanel(state.target.anchor || state.target.element); }, { passive: true });
    window.addEventListener('resize', () => { if (state.panel && !state.panel.hidden && state.target) placePanel(state.target.anchor || state.target.element); });

    const observer = new MutationObserver(queue);
    observer.observe(document.body, { childList: true, subtree: true, attributes: true, attributeFilter: ['class','hidden','data-pie-image'] });
    queue();
})();
