(function () {
    if (!document.body?.classList.contains('public-body') || !window.DeLongApi) return;

    const normalizedPath = window.location.pathname.replace(/\/+$/, '') || '/';
    const scopedMatch = normalizedPath.match(/^\/h\/([^/]+)(?:\/|$)/i);
    const siteSlug = scopedMatch ? decodeURIComponent(scopedMatch[1]) : '';
    const scopedHome = siteSlug ? `/h/${encodeURIComponent(siteSlug)}` : '';
    const isHome = normalizedPath === '/' || (scopedHome && normalizedPath === scopedHome);
    const customPageId = document.querySelector('meta[name="delong-custom-page-id"]')?.content || '';
    const customPageSlug = document.querySelector('meta[name="delong-custom-page-slug"]')?.content || '';
    const isCustomPage = !!customPageId && !!customPageSlug;
    if (!isHome && !isCustomPage) return;

    const contextParams = new URLSearchParams();
    if (siteSlug) contextParams.set('siteSlug', siteSlug);
    if (isCustomPage) contextParams.set('pageSlug', customPageSlug);
    const contextUrl = `/api/admin/site/visual-context${contextParams.size ? `?${contextParams}` : ''}`;
    const editScopeKey = isCustomPage ? `page:${customPageId}` : (siteSlug || 'global');
    const pendingAdvancedKey = `delong:inline:advanced:${editScopeKey}`;
    const pageBase = isCustomPage ? normalizedPath : (scopedHome || '/');
    const state = {
        context: null,
        api: '',
        siteApi: '',
        sections: new Map(),
        active: null,
        bar: null,
        linkPanel: null,
        imageInput: null,
        imageTarget: null,
        loading: false,
        dirty: false,
        activeRun: 0,
        queued: false
    };

    function parseJson(value) {
        try { return JSON.parse(value || '{}'); } catch { return {}; }
    }

    function clone(value) {
        return JSON.parse(JSON.stringify(value ?? {}));
    }

    function getAt(object, path) {
        return String(path || '').split('.').filter(Boolean).reduce((value, part) => value == null ? undefined : value[part], object);
    }

    function setAt(object, path, value) {
        const parts = String(path || '').split('.').filter(Boolean);
        if (!parts.length) return;
        let cursor = object;
        for (let i = 0; i < parts.length - 1; i += 1) {
            const part = parts[i];
            const next = parts[i + 1];
            if (cursor[part] == null || typeof cursor[part] !== 'object') cursor[part] = /^\d+$/.test(next) ? [] : {};
            cursor = cursor[part];
        }
        cursor[parts[parts.length - 1]] = value;
    }

    function sectionContent(section) {
        return parseJson(section?.contentJson);
    }

    function toast(message, type) {
        let node = document.querySelector('.pve-toast');
        if (!node) {
            node = document.createElement('div');
            node.className = 'pve-toast';
            document.body.appendChild(node);
        }
        node.className = `pve-toast ${type === 'error' ? 'error' : 'success'}`;
        node.textContent = message;
        node.hidden = false;
        clearTimeout(toast.timer);
        toast.timer = setTimeout(() => { node.hidden = true; }, 3000);
    }

    function editStateSuffix() {
        if (isCustomPage) return `page:${customPageId}`;
        return state.context?.scope === 'global' ? 'global' : (state.context?.propertyId || siteSlug || 'property');
    }

    function resolvePreviewUrl(raw) {
        const value = String(raw || '').trim();
        if (!value || /^(javascript|data|vbscript):/i.test(value)) return '';
        if (/^(https?:|mailto:|tel:)/i.test(value)) return value;
        if (value.startsWith('#')) return `${pageBase}${value}`;
        if (!value.startsWith('/')) return value;
        if (!siteSlug || value.startsWith('/h/')) return value;
        if (value === '/') return scopedHome;
        return `${scopedHome}${value}`;
    }

    function normalizedText(element) {
        return String(element.innerText ?? element.textContent ?? '').replace(/\u00a0/g, ' ').trim();
    }

    function visualKind(section) {
        if (section?.type !== 'RichText') return section?.type || '';
        return sectionContent(section).builderKind || 'RichText';
    }

    function markText(element, sectionId, path, label, options) {
        if (!element || !path) return;
        element.dataset.pieSectionId = String(sectionId);
        element.dataset.pieKey = path;
        element.dataset.pieLabel = label || 'Nội dung';
        element.dataset.pieMode = options?.html ? 'html' : 'text';
        element.dataset.pieMultiline = options?.multiline ? '1' : '0';
        if (options?.urlKey) element.dataset.pieUrlKey = options.urlKey;
        else delete element.dataset.pieUrlKey;
        element.classList.add('pve-inline-field');
        if (!element.getAttribute('title')) element.setAttribute('title', `Click để sửa: ${label || 'Nội dung'}`);
    }

    function markImage(image, sectionId, path, label) {
        if (!image || !path) return;
        const host = image.closest('.public-cms-hero-image,.public-story-media,.dl-builder-image') || image.parentElement;
        if (!host) return;
        host.classList.add('pve-inline-image-host');
        let button = [...host.children].find(child => child.matches?.(`[data-pie-image-button][data-pie-key="${CSS.escape(path)}"]`));
        if (!button) {
            button = document.createElement('button');
            button.type = 'button';
            button.className = 'pve-inline-image-button';
            button.dataset.pieImageButton = '1';
            button.dataset.pieKey = path;
            host.appendChild(button);
        }
        button.dataset.pieSectionId = String(sectionId);
        button.dataset.pieLabel = label || 'Ảnh';
        button.innerHTML = '<span>✎</span> Đổi ảnh';
        image.dataset.pieImage = path;
        image.dataset.pieSectionId = String(sectionId);
    }

    function wrapFaqQuestion(summary) {
        if (!summary) return null;
        const existing = summary.querySelector(':scope > [data-pie-generated-question]');
        if (existing) return existing;
        const textNode = [...summary.childNodes].find(node => node.nodeType === Node.TEXT_NODE && node.textContent.trim());
        if (!textNode) return null;
        const span = document.createElement('span');
        span.dataset.pieGeneratedQuestion = '1';
        span.textContent = textNode.textContent.trim();
        textNode.replaceWith(span);
        return span;
    }

    function decorateSection(root, section) {
        const id = section.id;
        const kind = visualKind(section);
        const content = sectionContent(section);
        const q = selector => root.querySelector(selector);

        if (kind === 'Hero') {
            markText(q('.public-hero-copy .public-eyebrow'), id, 'eyebrow', 'Nhãn Hero');
            markText(q('.public-hero-copy h1'), id, 'title', 'Tiêu đề Hero', { multiline: true });
            markText(q('.public-hero-copy > p'), id, 'body', 'Mô tả Hero', { multiline: true });
            const buttons = root.querySelectorAll('.public-hero-actions .public-btn');
            markText(buttons[0], id, 'primaryText', 'Nút chính', { urlKey: 'primaryUrl' });
            markText(buttons[1], id, 'secondaryText', 'Nút phụ', { urlKey: 'secondaryUrl' });
            markImage(q('.public-hotel-hero-main-image,.public-cms-hero-image > img'), id, 'imageUrl', 'Ảnh Hero');
            return;
        }

        if (kind === 'BranchGrid' || kind === 'RoomGrid') {
            markText(q('.public-section-head .public-eyebrow'), id, 'eyebrow', 'Nhãn khối');
            markText(q('.public-section-head h2'), id, 'title', 'Tiêu đề khối', { multiline: true });
            return;
        }

        if (kind === 'AvailabilitySearch') {
            markText(q('.public-check-card h2'), id, 'title', 'Tiêu đề tìm phòng', { multiline: true });
            return;
        }

        if (kind === 'FeatureGrid') {
            markText(q('.public-story-grid .public-eyebrow'), id, 'eyebrow', 'Nhãn nội dung');
            markText(q('.public-story-grid h2'), id, 'title', 'Tiêu đề nội dung', { multiline: true });
            markText(q('.public-story-copy > p'), id, 'body', 'Mô tả', { multiline: true });
            root.querySelectorAll('.public-benefits > span').forEach((item, index) => markText(item, id, `items.${index}`, `Điểm nổi bật ${index + 1}`));
            markImage(q('.public-story-media img'), id, 'imageUrl', 'Ảnh nội dung');
            return;
        }

        if (kind === 'Faq') {
            markText(q('.public-section-head .public-eyebrow'), id, 'eyebrow', 'Nhãn FAQ');
            markText(q('.public-section-head h2'), id, 'title', 'Tiêu đề FAQ', { multiline: true });
            root.querySelectorAll('.public-faq-list details').forEach((details, index) => {
                markText(wrapFaqQuestion(details.querySelector('summary')), id, `items.${index}.question`, `Câu hỏi ${index + 1}`, { multiline: true });
                markText(details.querySelector(':scope > p'), id, `items.${index}.answer`, `Trả lời ${index + 1}`, { multiline: true });
            });
            return;
        }

        if (kind === 'Location') {
            markText(q('.public-location-copy .public-eyebrow'), id, 'eyebrow', 'Nhãn vị trí');
            markText(q('.public-location-copy h2'), id, 'title', 'Tiêu đề vị trí', { multiline: true });
            markText(q('.public-location-copy > p'), id, 'body', 'Mô tả vị trí', { multiline: true });
            markText(q('.public-location-copy address'), id, 'address', 'Địa chỉ', { multiline: true });
            root.querySelectorAll('.public-location-nearby > span').forEach((item, index) => markText(item, id, `nearby.${index}`, `Địa điểm gần ${index + 1}`));
            return;
        }

        if (kind === 'PolicyGrid') {
            markText(q('.public-section-head .public-eyebrow'), id, 'eyebrow', 'Nhãn quy định');
            markText(q('.public-section-head h2'), id, 'title', 'Tiêu đề quy định', { multiline: true });
            root.querySelectorAll('.public-policy-grid > article').forEach((article, index) => {
                markText(article.querySelector('h3'), id, `items.${index}.title`, `Quy định ${index + 1}`);
                markText(article.querySelector('p'), id, `items.${index}.body`, `Nội dung quy định ${index + 1}`, { multiline: true });
            });
            return;
        }

        if (kind === 'Cta') {
            markText(q('.public-cms-cta-card h2'), id, 'title', 'Tiêu đề CTA', { multiline: true });
            markText(q('.public-cms-cta-card p'), id, 'body', 'Mô tả CTA', { multiline: true });
            markText(q('.public-cms-cta-card .public-btn'), id, 'buttonText', 'Nút CTA', { urlKey: 'buttonUrl' });
            return;
        }

        if (kind === 'RichText' && !content.builderKind) {
            markText(q('.public-cms-rich-inner'), id, 'html', 'Nội dung tự do', { html: true, multiline: true });
        }
    }

    function clearDecorations() {
        cancelActive(true);
        document.querySelectorAll('[data-pie-key].pve-inline-field').forEach(node => {
            node.classList.remove('pve-inline-field', 'pve-inline-active', 'pve-inline-saving');
            node.removeAttribute('contenteditable');
            node.removeAttribute('aria-label');
            delete node.dataset.pieSectionId;
            delete node.dataset.pieKey;
            delete node.dataset.pieLabel;
            delete node.dataset.pieMode;
            delete node.dataset.pieMultiline;
            delete node.dataset.pieUrlKey;
        });
        document.querySelectorAll('[data-pie-image-button]').forEach(node => node.remove());
        document.querySelectorAll('.pve-inline-image-host').forEach(node => node.classList.remove('pve-inline-image-host'));
        document.querySelectorAll('[data-pie-image]').forEach(node => {
            delete node.dataset.pieImage;
            delete node.dataset.pieSectionId;
        });
        document.querySelectorAll('[data-pie-generated-question]').forEach(span => span.replaceWith(document.createTextNode(span.textContent || '')));
        state.sections.clear();
        state.dirty = false;
    }

    async function activate() {
        if (state.loading) return;
        const run = ++state.activeRun;
        state.loading = true;
        try {
            if (!state.context) state.context = await DeLongApi.get(contextUrl);
            if (run !== state.activeRun || !document.body.classList.contains('pve-editing')) return;
            if (!state.context?.canEdit) return;
            state.siteApi = state.context.siteApi || (state.context.scope === 'global' ? '/api/admin/site/global' : `/api/admin/properties/${state.context.propertyId}/site`);
            state.api = state.context.sectionApi || state.siteApi;
            const data = await DeLongApi.get(`${state.api}/`);
            if (run !== state.activeRun || !document.body.classList.contains('pve-editing')) return;
            const sections = data?.sections || data?.site?.sections || [];
            state.sections = new Map(sections.map(section => [String(section.id), section]));
            state.dirty = false;
            decorateAll();
            openPendingAdvanced();
        } catch (error) {
            toast(error.message || 'Không thể bật chỉnh sửa nội dung nhanh.', 'error');
        } finally {
            state.loading = false;
        }
    }

    function decorateAll() {
        if (!document.body.classList.contains('pve-editing')) return;
        document.querySelectorAll('.pve-editable-section[data-pve-section-id]').forEach(root => {
            const section = state.sections.get(String(root.dataset.pveSectionId));
            if (section) decorateSection(root, section);
        });
    }

    function ensureBar() {
        if (state.bar?.isConnected) return state.bar;
        const bar = document.createElement('div');
        bar.className = 'pve-inline-bar';
        bar.hidden = true;
        bar.innerHTML = `<div class="pve-inline-bar-copy"><strong data-pie-bar-title>Chỉnh nhanh</strong><small>Enter/Ctrl+Enter để lưu · Esc để hủy</small></div><div class="pve-inline-bar-actions"><button type="button" data-pie-link hidden>⌁ Liên kết</button><button type="button" data-pie-advanced>⚙ Nâng cao</button><button type="button" data-pie-cancel>Hủy</button><button type="button" class="primary" data-pie-save>Lưu</button></div>`;
        document.body.appendChild(bar);
        bar.addEventListener('pointerdown', event => event.stopPropagation());
        bar.querySelector('[data-pie-save]').addEventListener('click', () => saveActive());
        bar.querySelector('[data-pie-cancel]').addEventListener('click', () => cancelActive());
        bar.querySelector('[data-pie-link]').addEventListener('click', () => toggleLinkPanel());
        bar.querySelector('[data-pie-advanced]').addEventListener('click', () => openAdvanced(state.active?.sectionId));
        state.bar = bar;
        return bar;
    }

    function ensureLinkPanel() {
        if (state.linkPanel?.isConnected) return state.linkPanel;
        const panel = document.createElement('div');
        panel.className = 'pve-inline-link-panel';
        panel.hidden = true;
        panel.innerHTML = `<div class="pve-inline-link-head"><strong>Liên kết của nút</strong><button type="button" data-pie-link-close aria-label="Đóng">×</button></div><input type="text" data-pie-link-input placeholder="/booking hoặc https://..." /><div class="pve-inline-link-suggestions"><span>Chọn nhanh</span><button type="button" data-url="/">Trang chủ</button><button type="button" data-url="/rooms">Phòng</button><button type="button" data-url="/booking">Đặt phòng</button><button type="button" data-url="/booking/lookup">Tra cứu</button><button type="button" data-url="/blog">Blog</button><button type="button" data-url="/#gallery">Gallery</button></div><div class="pve-inline-link-foot"><button type="button" data-pie-link-test>↗ Mở thử</button><button type="button" class="primary" data-pie-link-done>Xong</button></div>`;
        document.body.appendChild(panel);
        panel.addEventListener('pointerdown', event => event.stopPropagation());
        panel.querySelector('[data-pie-link-close]').addEventListener('click', () => hideLinkPanel());
        panel.querySelector('[data-pie-link-done]').addEventListener('click', () => {
            if (state.active) state.active.pendingUrl = panel.querySelector('[data-pie-link-input]').value.trim();
            hideLinkPanel();
        });
        panel.querySelectorAll('[data-url]').forEach(button => button.addEventListener('click', () => {
            panel.querySelector('[data-pie-link-input]').value = button.dataset.url || '';
            if (state.active) state.active.pendingUrl = button.dataset.url || '';
        }));
        panel.querySelector('[data-pie-link-input]').addEventListener('input', event => {
            if (state.active) state.active.pendingUrl = event.currentTarget.value;
        });
        panel.querySelector('[data-pie-link-test]').addEventListener('click', () => {
            const url = resolvePreviewUrl(panel.querySelector('[data-pie-link-input]').value);
            if (url) window.open(url, '_blank', 'noopener,noreferrer');
            else toast('Link hiện tại không thể mở thử.', 'error');
        });
        state.linkPanel = panel;
        return panel;
    }

    function placeFloating(node, anchor) {
        if (!node || !anchor) return;
        const rect = anchor.getBoundingClientRect();
        const width = Math.min(node.offsetWidth || 520, window.innerWidth - 20);
        const left = Math.max(10, Math.min(rect.left, window.innerWidth - width - 10));
        let top = rect.bottom + 8;
        const height = node.offsetHeight || 54;
        if (top + height > window.innerHeight - 10) top = Math.max(10, rect.top - height - 8);
        node.style.left = `${Math.round(left)}px`;
        node.style.top = `${Math.round(top)}px`;
    }

    function showBar() {
        const active = state.active;
        if (!active) return;
        const bar = ensureBar();
        bar.querySelector('[data-pie-bar-title]').textContent = `Đang sửa · ${active.label}`;
        bar.querySelector('[data-pie-link]').hidden = !active.urlKey;
        bar.hidden = false;
        requestAnimationFrame(() => placeFloating(bar, active.element));
    }

    function startEditing(element) {
        if (!document.body.classList.contains('pve-editing')) return;
        const sectionId = element.dataset.pieSectionId;
        const key = element.dataset.pieKey;
        const section = state.sections.get(String(sectionId));
        if (!section || !key) return;
        if (state.active?.element === element) return;
        cancelActive(true);
        const content = sectionContent(section);
        const mode = element.dataset.pieMode || 'text';
        const originalDisplay = mode === 'html' ? element.innerHTML : element.textContent;
        state.active = {
            element,
            sectionId: String(sectionId),
            key,
            mode,
            multiline: element.dataset.pieMultiline === '1',
            label: element.dataset.pieLabel || 'Nội dung',
            urlKey: element.dataset.pieUrlKey || '',
            pendingUrl: element.dataset.pieUrlKey ? String(getAt(content, element.dataset.pieUrlKey) || '') : '',
            originalDisplay
        };
        element.classList.add('pve-inline-active');
        element.setAttribute('contenteditable', mode === 'html' ? 'true' : 'plaintext-only');
        element.setAttribute('aria-label', `Đang sửa ${state.active.label}`);
        element.focus({ preventScroll: true });
        const selection = window.getSelection();
        if (selection && element.childNodes.length) {
            try {
                const range = document.createRange();
                range.selectNodeContents(element);
                range.collapse(false);
                selection.removeAllRanges();
                selection.addRange(range);
            } catch { }
        }
        showBar();
    }

    function finishActive() {
        if (!state.active) return;
        const element = state.active.element;
        element.classList.remove('pve-inline-active', 'pve-inline-saving');
        element.removeAttribute('contenteditable');
        element.removeAttribute('aria-label');
        state.active = null;
        if (state.bar) state.bar.hidden = true;
        hideLinkPanel();
    }

    function cancelActive(silent) {
        if (!state.active) {
            if (state.bar) state.bar.hidden = true;
            hideLinkPanel();
            return;
        }
        const { element, mode, originalDisplay } = state.active;
        if (mode === 'html') element.innerHTML = originalDisplay;
        else element.textContent = originalDisplay;
        finishActive();
        if (!silent) toast('Đã hủy thay đổi nhanh.');
    }

    function toggleLinkPanel() {
        if (!state.active?.urlKey) return;
        const panel = ensureLinkPanel();
        if (!panel.hidden) return hideLinkPanel();
        panel.querySelector('[data-pie-link-input]').value = state.active.pendingUrl || '';
        panel.hidden = false;
        requestAnimationFrame(() => {
            placeFloating(panel, state.bar || state.active.element);
            panel.querySelector('[data-pie-link-input]').focus();
            panel.querySelector('[data-pie-link-input]').select();
        });
    }

    function hideLinkPanel() {
        if (state.linkPanel) state.linkPanel.hidden = true;
    }

    async function saveSection(sectionId, changes) {
        const section = state.sections.get(String(sectionId));
        if (!section) throw new Error('Không tìm thấy khối đang sửa.');
        const content = clone(sectionContent(section));
        changes.forEach(change => setAt(content, change.path, change.value));
        const payload = {
            type: section.type,
            name: section.name || '',
            variant: section.variant || 'wide',
            isVisible: section.isVisible !== false,
            contentJson: JSON.stringify(content)
        };
        const saved = await DeLongApi.put(`${state.api}/sections/${section.id}`, payload);
        state.sections.set(String(section.id), saved || Object.assign({}, section, { contentJson: payload.contentJson }));
        state.dirty = true;
        return state.sections.get(String(section.id));
    }

    async function saveActive() {
        const active = state.active;
        if (!active) return;
        const value = active.mode === 'html' ? active.element.innerHTML.trim() : normalizedText(active.element);
        const changes = [{ path: active.key, value }];
        if (active.urlKey) changes.push({ path: active.urlKey, value: String(active.pendingUrl || '').trim() });
        active.element.classList.add('pve-inline-saving');
        const saveButton = state.bar?.querySelector('[data-pie-save]');
        if (saveButton) { saveButton.disabled = true; saveButton.textContent = 'Đang lưu…'; }
        try {
            const saved = await saveSection(active.sectionId, changes);
            const content = sectionContent(saved);
            const stored = getAt(content, active.key);
            if (active.mode === 'html') active.element.innerHTML = stored == null ? value : String(stored);
            else active.element.textContent = stored == null ? value : String(stored);
            if (active.urlKey && active.element instanceof HTMLAnchorElement) active.element.href = resolvePreviewUrl(getAt(content, active.urlKey) || active.pendingUrl) || '#';
            finishActive();
            toast(`Đã lưu ${active.label.toLowerCase()}.`);
        } catch (error) {
            active.element.classList.remove('pve-inline-saving');
            toast(error.message || 'Không thể lưu nội dung nhanh.', 'error');
        } finally {
            if (saveButton) { saveButton.disabled = false; saveButton.textContent = 'Lưu'; }
        }
    }

    function ensureImageInput() {
        if (state.imageInput?.isConnected) return state.imageInput;
        const input = document.createElement('input');
        input.type = 'file';
        input.accept = 'image/png,image/jpeg,image/webp';
        input.hidden = true;
        document.body.appendChild(input);
        input.addEventListener('change', uploadImage);
        state.imageInput = input;
        return input;
    }

    async function beginImageChange(button) {
        const section = state.sections.get(String(button.dataset.pieSectionId));
        if (!section) return;
        cancelActive(true);
        state.imageTarget = {
            button,
            sectionId: String(section.id),
            key: button.dataset.pieKey,
            label: button.dataset.pieLabel || 'Ảnh',
            image: button.parentElement?.querySelector(`[data-pie-image="${CSS.escape(button.dataset.pieKey)}"]`) || button.parentElement?.querySelector('img')
        };
        const input = ensureImageInput();
        input.value = '';
        input.click();
    }

    async function uploadImage(event) {
        const file = event.target.files?.[0];
        const target = state.imageTarget;
        event.target.value = '';
        if (!file || !target) return;
        target.button.disabled = true;
        target.button.classList.add('is-busy');
        target.button.textContent = 'Đang tải…';
        try {
            const form = new FormData();
            form.append('file', file);
            const asset = await DeLongApi.postForm(`${state.siteApi}/assets/section`, form);
            if (!asset?.url) throw new Error('Upload không trả về URL ảnh.');
            const saved = await saveSection(target.sectionId, [{ path: target.key, value: asset.url }]);
            const finalUrl = getAt(sectionContent(saved), target.key) || asset.url;
            if (target.image) target.image.src = finalUrl;
            toast(`Đã đổi ${target.label.toLowerCase()}.`);
        } catch (error) {
            toast(error.message || 'Không thể đổi ảnh.', 'error');
        } finally {
            target.button.disabled = false;
            target.button.classList.remove('is-busy');
            target.button.innerHTML = '<span>✎</span> Đổi ảnh';
            state.imageTarget = null;
        }
    }

    function storePendingAdvanced(sectionId) {
        if (!sectionId) return;
        try {
            sessionStorage.setItem(pendingAdvancedKey, String(sectionId));
            const suffix = editStateSuffix();
            sessionStorage.setItem(`delong:pve:editing:${suffix}`, '1');
            sessionStorage.setItem(`delong:pve:scroll:${suffix}`, String(Math.max(0, window.scrollY || 0)));
        } catch { }
    }

    function openAdvanced(sectionId) {
        if (!sectionId) return;
        if (state.active) cancelActive(true);
        const control = document.querySelector(`.pve-editable-section[data-pve-section-id="${CSS.escape(String(sectionId))}"] .pve-section-controls [data-action="edit"]`);
        if (!state.dirty && control) {
            control.click();
            return;
        }
        storePendingAdvanced(sectionId);
        window.location.reload();
    }

    function openPendingAdvanced() {
        let sectionId = '';
        try { sectionId = sessionStorage.getItem(pendingAdvancedKey) || ''; } catch { }
        if (!sectionId) return;
        const attempt = () => {
            const control = document.querySelector(`.pve-editable-section[data-pve-section-id="${CSS.escape(sectionId)}"] .pve-section-controls [data-action="edit"]`);
            if (!control) return false;
            try { sessionStorage.removeItem(pendingAdvancedKey); } catch { }
            control.click();
            return true;
        };
        if (!attempt()) setTimeout(attempt, 120);
    }

    function syncMode() {
        if (document.body.classList.contains('pve-editing')) {
            if (!state.sections.size && !state.loading) activate();
            else decorateAll();
        } else if (state.sections.size || state.active) {
            ++state.activeRun;
            clearDecorations();
        }
    }

    function queueSync() {
        if (state.queued) return;
        state.queued = true;
        requestAnimationFrame(() => {
            state.queued = false;
            syncMode();
        });
    }

    document.addEventListener('click', event => {
        if (!document.body.classList.contains('pve-editing')) return;
        const imageButton = event.target.closest('[data-pie-image-button]');
        if (imageButton) {
            event.preventDefault();
            event.stopPropagation();
            beginImageChange(imageButton);
            return;
        }
        const field = event.target.closest('[data-pie-key].pve-inline-field');
        if (field) {
            event.preventDefault();
            event.stopPropagation();
            startEditing(field);
        }
    }, true);

    document.addEventListener('click', event => {
        const advanced = event.target.closest('.pve-section-controls [data-action="edit"]');
        if (!advanced || !state.dirty || !document.body.classList.contains('pve-editing')) return;
        const sectionId = advanced.closest('[data-pve-section-id]')?.dataset.pveSectionId;
        if (!sectionId) return;
        event.preventDefault();
        event.stopImmediatePropagation();
        storePendingAdvanced(sectionId);
        window.location.reload();
    }, true);

    document.addEventListener('keydown', event => {
        if (!state.active) return;
        if (event.key === 'Escape') {
            event.preventDefault();
            cancelActive();
            return;
        }
        if (event.key === 'Enter') {
            if (!state.active.multiline || event.ctrlKey || event.metaKey) {
                event.preventDefault();
                saveActive();
            }
        }
    });

    window.addEventListener('scroll', () => {
        if (state.active && state.bar && !state.bar.hidden) placeFloating(state.bar, state.active.element);
        if (state.linkPanel && !state.linkPanel.hidden) placeFloating(state.linkPanel, state.bar || state.active?.element);
    }, { passive: true });
    window.addEventListener('resize', () => {
        if (state.active && state.bar && !state.bar.hidden) placeFloating(state.bar, state.active.element);
        if (state.linkPanel && !state.linkPanel.hidden) placeFloating(state.linkPanel, state.bar || state.active?.element);
    });

    const observer = new MutationObserver(queueSync);
    observer.observe(document.body, { childList: true, subtree: true, attributes: true, attributeFilter: ['class', 'data-pve-section-id'] });
    queueSync();
})();
