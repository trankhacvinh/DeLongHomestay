(function () {
    if (!document.body?.classList.contains('public-body') || !window.DeLongApi) return;

    const normalizedPath = window.location.pathname.replace(/\/+$/, '') || '/';
    const scoped = normalizedPath.match(/^\/h\/([^/]+)(?:\/|$)/i);
    const siteSlug = scoped ? decodeURIComponent(scoped[1]) : '';
    const scopedHome = siteSlug ? `/h/${encodeURIComponent(siteSlug)}` : '';
    const customPageId = document.querySelector('meta[name="delong-custom-page-id"]')?.content || '';
    const customPageSlug = document.querySelector('meta[name="delong-custom-page-slug"]')?.content || '';
    const isCustomPage = !!customPageId && !!customPageSlug;
    const isHome = normalizedPath === '/' || (!!scopedHome && normalizedPath === scopedHome);
    if (!isHome && !isCustomPage) return;

    const params = new URLSearchParams();
    if (siteSlug) params.set('siteSlug', siteSlug);
    if (isCustomPage) params.set('pageSlug', customPageSlug);
    const contextUrl = `/api/admin/site/visual-context${params.size ? `?${params}` : ''}`;
    const state = { context: null, api: '', panel: null, target: null, breakpoint: 'desktop', queued: false };

    const ALIGN = [['auto','Mặc định'],['left','Trái'],['center','Giữa'],['right','Phải']];
    const TEXT_SIZE = [['auto','Mặc định'],['xs','Rất nhỏ'],['sm','Nhỏ'],['md','Vừa'],['lg','Lớn'],['xl','Rất lớn'],['hero','Hero']];
    const TEXT_WIDTH = [['auto','Mặc định'],['narrow','Gọn'],['content','Vừa'],['wide','Rộng'],['full','Full']];
    const IMAGE_WIDTH = [['auto','Mặc định'],['sm','Gọn'],['md','Vừa'],['lg','Rộng'],['full','Full khối']];
    const SPACE = [['auto','Mặc định'],['none','Không khoảng'],['xs','Rất nhỏ'],['sm','Nhỏ'],['md','Vừa'],['lg','Lớn'],['xl','Rất lớn']];
    const BUTTON_SIZE = [['auto','Mặc định'],['sm','Nhỏ'],['md','Vừa'],['lg','Lớn']];
    const IMAGE_RADIUS = [['auto','Theo giao diện'],['none','Vuông'],['sm','Bo nhẹ'],['md','Bo vừa'],['lg','Bo lớn'],['pill','Bo tròn']];
    const BREAKPOINTS = [['desktop','Desktop'],['tablet','Tablet'],['mobile','Mobile']];

    function parseJson(value) { try { return JSON.parse(value || '{}'); } catch { return {}; } }
    function clone(value) { return JSON.parse(JSON.stringify(value ?? {})); }
    function h(value) { return String(value ?? '').replace(/[&<>"']/g, ch => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[ch])); }
    function getAt(object, path) { return String(path || '').split('.').filter(Boolean).reduce((value, part) => value == null ? undefined : value[part], object); }
    function setAt(object, path, value) {
        const parts = String(path || '').split('.').filter(Boolean); if (!parts.length) return;
        let cursor = object;
        for (let i = 0; i < parts.length - 1; i += 1) {
            const key = parts[i];
            if (!cursor[key] || typeof cursor[key] !== 'object' || Array.isArray(cursor[key])) cursor[key] = {};
            cursor = cursor[key];
        }
        cursor[parts[parts.length - 1]] = value;
    }
    function allowed(value, items, fallback) { return items.some(item => item[0] === value) ? value : fallback; }

    function cleanBase(style, target) {
        const result = {
            align: allowed(style?.align, ALIGN, 'auto'),
            width: allowed(style?.width, target.image ? IMAGE_WIDTH : TEXT_WIDTH, 'auto'),
            space: allowed(style?.space, SPACE, 'auto')
        };
        if (target.image) result.radius = allowed(style?.radius, IMAGE_RADIUS, 'auto');
        else {
            result.size = allowed(style?.size, TEXT_SIZE, 'auto');
            if (target.button) result.buttonSize = allowed(style?.buttonSize, BUTTON_SIZE, 'auto');
        }
        return result;
    }

    function overrideItems(items) { return [['inherit','Kế thừa Desktop'], ...items.filter(item => item[0] !== 'auto')]; }
    function cleanOverride(style, target) {
        const result = {
            align: allowed(style?.align, overrideItems(ALIGN), 'inherit'),
            width: allowed(style?.width, overrideItems(target.image ? IMAGE_WIDTH : TEXT_WIDTH), 'inherit'),
            space: allowed(style?.space, overrideItems(SPACE), 'inherit')
        };
        if (target.image) result.radius = allowed(style?.radius, overrideItems(IMAGE_RADIUS), 'inherit');
        else {
            result.size = allowed(style?.size, overrideItems(TEXT_SIZE), 'inherit');
            if (target.button) result.buttonSize = allowed(style?.buttonSize, overrideItems(BUTTON_SIZE), 'inherit');
        }
        return result;
    }

    function visualDraft(content, key, target) {
        const raw = getAt(content?._visual || {}, key) || {};
        return {
            desktop: cleanBase(raw, target),
            tablet: cleanOverride(raw.tablet || {}, target),
            mobile: cleanOverride(raw.mobile || {}, target)
        };
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
        clearTimeout(node._elementStyleTimer); node._elementStyleTimer = setTimeout(() => { node.hidden = true; }, 3200);
    }

    function allStyleClasses(target) {
        const base = [
            'cp-align-left','cp-align-center','cp-align-right',
            'cp-width-narrow','cp-width-content','cp-width-wide','cp-width-full',
            'cp-size-xs','cp-size-sm','cp-size-md','cp-size-lg','cp-size-xl','cp-size-hero',
            'cp-space-none','cp-space-xs','cp-space-sm','cp-space-md','cp-space-lg','cp-space-xl',
            'cp-button-size-sm','cp-button-size-md','cp-button-size-lg',
            'cp-image-align-left','cp-image-align-center','cp-image-align-right',
            'cp-image-width-sm','cp-image-width-md','cp-image-width-lg','cp-image-width-full',
            'cp-image-radius-none','cp-image-radius-sm','cp-image-radius-md','cp-image-radius-lg','cp-image-radius-pill'
        ];
        ['tablet','mobile'].forEach(bp => {
            base.push(
                `cp-${bp}-align-left`,`cp-${bp}-align-center`,`cp-${bp}-align-right`,
                `cp-${bp}-width-narrow`,`cp-${bp}-width-content`,`cp-${bp}-width-wide`,`cp-${bp}-width-full`,
                `cp-${bp}-size-xs`,`cp-${bp}-size-sm`,`cp-${bp}-size-md`,`cp-${bp}-size-lg`,`cp-${bp}-size-xl`,`cp-${bp}-size-hero`,
                `cp-${bp}-space-none`,`cp-${bp}-space-xs`,`cp-${bp}-space-sm`,`cp-${bp}-space-md`,`cp-${bp}-space-lg`,`cp-${bp}-space-xl`,
                `cp-${bp}-button-size-sm`,`cp-${bp}-button-size-md`,`cp-${bp}-button-size-lg`,
                `cp-${bp}-image-align-left`,`cp-${bp}-image-align-center`,`cp-${bp}-image-align-right`,
                `cp-${bp}-image-width-sm`,`cp-${bp}-image-width-md`,`cp-${bp}-image-width-lg`,`cp-${bp}-image-width-full`,
                `cp-${bp}-image-radius-none`,`cp-${bp}-image-radius-sm`,`cp-${bp}-image-radius-md`,`cp-${bp}-image-radius-lg`,`cp-${bp}-image-radius-pill`
            );
        });
        return base;
    }

    function classesForStyle(style, target, prefix) {
        const result = [];
        const p = prefix ? `cp-${prefix}-` : 'cp-';
        if (style.align && !['auto','inherit'].includes(style.align)) result.push(`${p}${target.image ? 'image-align' : 'align'}-${style.align}`);
        if (style.width && !['auto','inherit'].includes(style.width)) result.push(`${p}${target.image ? 'image-width' : 'width'}-${style.width}`);
        if (style.space && !['auto','inherit'].includes(style.space)) result.push(`${p}space-${style.space}`);
        if (target.image) {
            if (style.radius && !['auto','inherit'].includes(style.radius)) result.push(`${p}image-radius-${style.radius}`);
        } else {
            if (style.size && !['auto','inherit'].includes(style.size)) result.push(`${p}size-${style.size}`);
            if (target.button && style.buttonSize && !['auto','inherit'].includes(style.buttonSize)) result.push(`${p}button-size-${style.buttonSize}`);
        }
        return result;
    }

    function applyPreview(target, draft, previewBreakpoint) {
        const element = target?.element; if (!element) return;
        element.classList.remove(...allStyleClasses(target), 'cp-style-preview-tablet', 'cp-style-preview-mobile');
        element.classList.add(...classesForStyle(draft.desktop, target, ''));
        element.classList.add(...classesForStyle(draft.tablet, target, 'tablet'));
        element.classList.add(...classesForStyle(draft.mobile, target, 'mobile'));
        if (previewBreakpoint === 'tablet') element.classList.add('cp-style-preview-tablet');
        if (previewBreakpoint === 'mobile') element.classList.add('cp-style-preview-mobile');
    }

    function options(items, value) {
        return items.map(([key,label]) => `<option value="${h(key)}"${key === value ? ' selected' : ''}>${h(label)}</option>`).join('');
    }

    function ensurePanel() {
        if (state.panel?.isConnected) return state.panel;
        const panel = document.createElement('section');
        panel.className = 'cp-element-style-panel'; panel.hidden = true;
        panel.setAttribute('role', 'dialog'); panel.setAttribute('aria-label', 'Kiểu phần tử');
        document.body.appendChild(panel);
        panel.addEventListener('pointerdown', event => event.stopPropagation());
        panel.addEventListener('change', event => {
            if (!state.target || !event.target.closest('[data-cp-style-fields]')) return;
            writeCurrentFields();
            applyPreview(state.target, state.target.draft, state.breakpoint);
        });
        panel.addEventListener('click', event => {
            const tab = event.target.closest('[data-cp-breakpoint]');
            if (tab) { writeCurrentFields(); state.breakpoint = tab.dataset.cpBreakpoint || 'desktop'; renderPanelBody(); return; }
            if (event.target.closest('[data-cp-style-close]')) closePanel(true);
            else if (event.target.closest('[data-cp-style-reset]')) resetCurrent();
            else if (event.target.closest('[data-cp-style-save]')) savePanel();
        });
        state.panel = panel;
        return panel;
    }

    function currentDraft() { return state.target?.draft?.[state.breakpoint] || {}; }
    function fieldItems(base) { return state.breakpoint === 'desktop' ? base : overrideItems(base); }

    function renderPanelBody() {
        const panel = state.panel; const target = state.target; if (!panel || !target) return;
        const style = currentDraft();
        panel.innerHTML = `<header><div><small>KIỂU PHẦN TỬ</small><strong>${h(target.label || (target.image ? 'Ảnh' : 'Nội dung'))}</strong></div><button type="button" data-cp-style-close aria-label="Đóng">×</button></header>
            <nav class="cp-element-style-tabs" aria-label="Thiết bị">${BREAKPOINTS.map(([key,label]) => `<button type="button" data-cp-breakpoint="${key}" class="${state.breakpoint === key ? 'active' : ''}">${label}</button>`).join('')}</nav>
            <div class="cp-element-style-fields" data-cp-style-fields>
                <label><span>Căn</span><select name="cpAlign">${options(fieldItems(ALIGN), style.align)}</select></label>
                ${target.image ? '' : `<label><span>Cỡ chữ</span><select name="cpSize">${options(fieldItems(TEXT_SIZE), style.size)}</select></label>`}
                <label><span>${target.image ? 'Kích thước ảnh' : 'Độ rộng'}</span><select name="cpWidth">${options(fieldItems(target.image ? IMAGE_WIDTH : TEXT_WIDTH), style.width)}</select></label>
                <label><span>Khoảng cách dọc</span><select name="cpSpace">${options(fieldItems(SPACE), style.space)}</select></label>
                ${target.image ? `<label><span>Bo góc ảnh</span><select name="cpRadius">${options(fieldItems(IMAGE_RADIUS), style.radius)}</select></label>` : ''}
                ${target.button ? `<label><span>Cỡ nút</span><select name="cpButtonSize">${options(fieldItems(BUTTON_SIZE), style.buttonSize)}</select></label>` : ''}
            </div>
            <div class="cp-element-style-hint"><strong>${state.breakpoint === 'desktop' ? 'Desktop là style gốc.' : `${state.breakpoint === 'tablet' ? 'Tablet' : 'Mobile'} mặc định kế thừa Desktop.`}</strong><span>Thay đổi được xem trước ngay. Lưu kiểu không reload trang nên không làm mất vị trí đang chỉnh.</span></div>
            <footer><button type="button" data-cp-style-reset>↺ ${state.breakpoint === 'desktop' ? 'Mặc định' : 'Kế thừa Desktop'}</button><div><button type="button" data-cp-style-close>Hủy</button><button type="button" class="primary" data-cp-style-save>Lưu kiểu</button></div></footer>`;
        applyPreview(target, target.draft, state.breakpoint);
        requestAnimationFrame(() => placePanel(target.anchor || target.element));
    }

    function writeCurrentFields() {
        if (!state.panel || !state.target) return;
        const style = currentDraft();
        const value = name => state.panel.querySelector(`[name="${name}"]`)?.value;
        if (value('cpAlign')) style.align = value('cpAlign');
        if (value('cpWidth')) style.width = value('cpWidth');
        if (value('cpSpace')) style.space = value('cpSpace');
        if (value('cpSize')) style.size = value('cpSize');
        if (value('cpRadius')) style.radius = value('cpRadius');
        if (value('cpButtonSize')) style.buttonSize = value('cpButtonSize');
    }

    function resetCurrent() {
        if (!state.target) return;
        state.target.draft[state.breakpoint] = state.breakpoint === 'desktop'
            ? cleanBase({}, state.target)
            : cleanOverride({}, state.target);
        renderPanelBody();
    }

    function placePanel(anchor) {
        const panel = state.panel; if (!panel || !anchor || panel.hidden) return;
        const rect = anchor.getBoundingClientRect();
        const width = Math.min(panel.offsetWidth || 430, window.innerWidth - 20);
        const left = Math.max(10, Math.min(rect.left, window.innerWidth - width - 10));
        let top = rect.bottom + 8;
        const height = panel.offsetHeight || 360;
        if (top + height > window.innerHeight - 10) top = Math.max(10, rect.top - height - 8);
        panel.style.left = `${Math.round(left)}px`; panel.style.top = `${Math.round(top)}px`;
    }

    async function openPanel(target) {
        try {
            const section = await freshSection(target.sectionId);
            if (!section) throw new Error('Không tìm thấy khối đang chỉnh.');
            const content = parseJson(section.contentJson);
            target.button = !target.image && !!target.element?.matches?.('.public-btn,.dl-row-button');
            const originalClasses = allStyleClasses(target).filter(name => target.element?.classList.contains(name));
            state.target = Object.assign({}, target, { section, content, draft: visualDraft(content, target.key, target), originalClasses });
            state.breakpoint = 'desktop';
            const panel = ensurePanel(); panel.hidden = false; renderPanelBody();
        } catch (error) { toast(error.message || 'Không thể mở chỉnh kiểu phần tử.', true); }
    }

    function closePanel(restore) {
        if (!state.panel || state.panel.hidden) return;
        if (state.target?.element) {
            state.target.element.classList.remove('cp-style-preview-tablet','cp-style-preview-mobile');
            if (restore) {
                state.target.element.classList.remove(...allStyleClasses(state.target));
                state.target.element.classList.add(...(state.target.originalClasses || []));
            }
        }
        state.panel.hidden = true; state.target = null;
    }

    function serializedStyle(target) {
        const compact = value => Object.fromEntries(Object.entries(value).filter(([,v]) => v && v !== 'inherit'));
        const base = compact(target.draft.desktop);
        const tablet = compact(target.draft.tablet);
        const mobile = compact(target.draft.mobile);
        if (Object.keys(tablet).length) base.tablet = tablet;
        if (Object.keys(mobile).length) base.mobile = mobile;
        return base;
    }

    async function savePanel() {
        if (!state.target || !state.panel) return;
        writeCurrentFields();
        const save = state.panel.querySelector('[data-cp-style-save]'); save.disabled = true; save.textContent = 'Đang lưu…';
        try {
            const section = await freshSection(state.target.sectionId);
            if (!section) throw new Error('Không tìm thấy khối đang chỉnh.');
            const content = clone(parseJson(section.contentJson));
            if (!content._visual || typeof content._visual !== 'object' || Array.isArray(content._visual)) content._visual = {};
            setAt(content._visual, state.target.key, serializedStyle(state.target));
            await DeLongApi.put(`${state.api}/sections/${section.id}`, {
                type: section.type,
                name: section.name || '',
                variant: section.variant || 'wide',
                isVisible: section.isVisible !== false,
                contentJson: JSON.stringify(content)
            });
            state.target.element?.classList.remove('cp-style-preview-tablet','cp-style-preview-mobile');
            state.target.originalClasses = allStyleClasses(state.target).filter(name => state.target.element?.classList.contains(name));
            closePanel(false); toast('Đã lưu kiểu phần tử.');
        } catch (error) {
            save.disabled = false; save.textContent = 'Lưu kiểu';
            toast(error.message || 'Không thể lưu kiểu phần tử.', true);
        }
    }

    async function openFromInlineBar(button) {
        const active = document.querySelector('.pve-inline-field.pve-inline-active'); if (!active) return;
        const target = { sectionId: active.dataset.pieSectionId, key: active.dataset.pieKey, label: active.dataset.pieLabel || 'Nội dung', image: false, element: active, anchor: button };
        const save = document.querySelector('.pve-inline-bar [data-pie-save]');
        if (save && !save.disabled) {
            save.click(); let tries = 0;
            while (document.querySelector('.pve-inline-field.pve-inline-active') && tries < 40) { await new Promise(resolve => setTimeout(resolve, 75)); tries += 1; }
            if (document.querySelector('.pve-inline-field.pve-inline-active')) return toast('Hãy lưu nội dung trước khi chỉnh kiểu.', true);
        }
        openPanel(target);
    }

    function enhanceBar() {
        const bar = document.querySelector('.pve-inline-bar'); const active = document.querySelector('.pve-inline-field.pve-inline-active');
        if (!bar || bar.hidden || !active) return;
        const actions = bar.querySelector('.pve-inline-bar-actions'); if (!actions) return;
        let button = actions.querySelector('[data-cp-element-style]');
        if (!button) {
            button = document.createElement('button'); button.type = 'button'; button.dataset.cpElementStyle = '1'; button.textContent = '↔ Kiểu';
            const advanced = actions.querySelector('[data-pie-advanced]'); if (advanced) advanced.before(button); else actions.prepend(button);
            button.addEventListener('click', () => openFromInlineBar(button));
        }
        button.hidden = false;
    }

    function enhanceImages() {
        document.querySelectorAll('[data-pie-image]').forEach(image => {
            const host = image.closest('.pve-inline-image-host'); if (!host || host.querySelector('[data-cp-image-style]')) return;
            const button = document.createElement('button'); button.type = 'button'; button.className = 'pve-inline-image-style-button'; button.dataset.cpImageStyle = '1'; button.innerHTML = '<span>↔</span> Kiểu ảnh';
            host.appendChild(button);
            button.addEventListener('click', event => {
                event.preventDefault(); event.stopPropagation();
                openPanel({ sectionId: image.dataset.pieSectionId, key: image.dataset.pieImage, label: 'Ảnh', image: true, element: host, anchor: button });
            });
        });
    }

    function sync() {
        if (!document.body.classList.contains('pve-editing')) {
            closePanel(true); document.querySelectorAll('[data-cp-image-style]').forEach(node => node.remove()); return;
        }
        enhanceBar(); enhanceImages();
    }
    function queue() { if (state.queued) return; state.queued = true; requestAnimationFrame(() => { state.queued = false; sync(); }); }

    document.addEventListener('keydown', event => {
        if (event.key === 'Escape' && state.panel && !state.panel.hidden) { event.preventDefault(); event.stopPropagation(); closePanel(true); }
    }, true);
    document.addEventListener('pointerdown', event => {
        if (state.panel && !state.panel.hidden && !event.target.closest('.cp-element-style-panel') && !event.target.closest('[data-cp-element-style],[data-cp-image-style]')) closePanel(true);
    });
    window.addEventListener('scroll', () => { if (state.panel && !state.panel.hidden && state.target) placePanel(state.target.anchor || state.target.element); }, { passive: true });
    window.addEventListener('resize', () => { if (state.panel && !state.panel.hidden && state.target) placePanel(state.target.anchor || state.target.element); });
    new MutationObserver(queue).observe(document.body, { childList: true, subtree: true, attributes: true, attributeFilter: ['class','hidden','data-pie-image'] });
    queue();
})();
