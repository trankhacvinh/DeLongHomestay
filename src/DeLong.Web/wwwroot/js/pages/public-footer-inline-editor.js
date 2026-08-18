(function (global) {
    if (!document.body?.classList.contains('public-body') || !global.DeLongApi) return;

    const pagePath = location.pathname.replace(/\/+$/, '') || '/';
    const scoped = pagePath.match(/^\/h\/([^/]+)(?:\/|$)/i);
    const siteSlug = scoped ? decodeURIComponent(scoped[1]) : '';
    const contextUrl = `/api/admin/site/visual-context${siteSlug ? `?siteSlug=${encodeURIComponent(siteSlug)}` : ''}`;
    const state = { context: null, designer: null, loading: false, active: null, bar: null, linkPanel: null, queued: false };

    const clone = value => JSON.parse(JSON.stringify(value ?? {}));

    function toast(message, error) {
        let node = document.querySelector('.psd-editor-toast');
        if (!node) {
            node = document.createElement('div');
            node.className = 'psd-editor-toast';
            document.body.appendChild(node);
        }
        node.className = `psd-editor-toast${error ? ' error' : ''}`;
        node.textContent = message;
        node.hidden = false;
        clearTimeout(node._timer);
        node._timer = setTimeout(() => { node.hidden = true; }, 3200);
    }

    function designerApi() {
        return state.context?.scope === 'global'
            ? '/api/admin/site/global/designer'
            : `/api/admin/properties/${state.context?.propertyId}/site/designer`;
    }

    function resolveUrl(value) {
        const url = String(value || '').trim();
        if (!url.startsWith('@')) return url || '#';
        const prefix = siteSlug ? `/h/${encodeURIComponent(siteSlug)}` : '';
        switch (url.toLowerCase()) {
            case '@home': return prefix || '/';
            case '@rooms': return prefix ? `${prefix}/rooms` : '/rooms';
            case '@branches': return '/#co-so';
            case '@booking': return prefix ? `${prefix}/booking` : '/booking';
            case '@lookup': return prefix ? `${prefix}/booking/lookup` : '/booking/lookup';
            default: return '#';
        }
    }

    function elementType(host) {
        const item = [...(host?.classList || [])].find(name => name.startsWith('psd-footer-element-') && name !== 'psd-footer-element');
        return item ? item.slice('psd-footer-element-'.length) : '';
    }

    function findElement(settings, elementId) {
        for (const row of settings?.footerRows || []) {
            for (const column of row?.columns || []) {
                const element = (column?.elements || []).find(item => String(item?.id || '') === String(elementId || ''));
                if (element) return element;
            }
        }
        return null;
    }

    async function saveDesigner() {
        const payload = {
            header: clone(state.designer?.header || {}),
            footerBuilderEnabled: !!state.designer?.footerBuilderEnabled,
            footerRows: clone(state.designer?.footerRows || [])
        };
        const saved = await DeLongApi.put(`${designerApi()}/`, payload);
        state.designer = saved || payload;
        return state.designer;
    }

    function normalizedText(node) {
        return String(node?.innerText ?? node?.textContent ?? '').replace(/\u00a0/g, ' ').trim();
    }

    function placeFloating(node, anchor) {
        if (!node || !anchor) return;
        const rect = anchor.getBoundingClientRect();
        const width = Math.min(node.offsetWidth || 500, innerWidth - 20);
        const left = Math.max(10, Math.min(rect.left, innerWidth - width - 10));
        let top = rect.bottom + 8;
        const height = node.offsetHeight || 54;
        if (top + height > innerHeight - 10) top = Math.max(10, rect.top - height - 8);
        node.style.left = `${Math.round(left)}px`;
        node.style.top = `${Math.round(top)}px`;
    }

    function ensureBar() {
        if (state.bar?.isConnected) return state.bar;
        const bar = document.createElement('div');
        bar.className = 'psd-footer-inline-bar';
        bar.hidden = true;
        bar.innerHTML = `<div><strong data-pfi-title>Chỉnh Footer</strong><small>Enter/Ctrl+Enter để lưu · Esc để hủy</small></div><div class="psd-footer-inline-bar-actions"><button type="button" data-pfi-link hidden>⌁ Liên kết</button><button type="button" data-pfi-advanced>⚙ Nâng cao</button><button type="button" data-pfi-cancel>Hủy</button><button type="button" class="primary" data-pfi-save>Lưu</button></div>`;
        document.body.appendChild(bar);
        bar.addEventListener('pointerdown', event => event.stopPropagation());
        bar.querySelector('[data-pfi-save]').addEventListener('click', saveActive);
        bar.querySelector('[data-pfi-cancel]').addEventListener('click', () => cancelActive());
        bar.querySelector('[data-pfi-link]').addEventListener('click', toggleLinkPanel);
        bar.querySelector('[data-pfi-advanced]').addEventListener('click', openAdvanced);
        state.bar = bar;
        return bar;
    }

    function ensureLinkPanel() {
        if (state.linkPanel?.isConnected) return state.linkPanel;
        const panel = document.createElement('div');
        panel.className = 'psd-footer-inline-link-panel';
        panel.hidden = true;
        panel.innerHTML = `<div class="psd-footer-inline-link-head"><strong>Liên kết</strong><button type="button" data-pfi-link-close aria-label="Đóng">×</button></div><input type="text" data-pfi-link-input placeholder="/booking hoặc https://..."><div class="psd-footer-inline-link-suggestions"><span>Chọn nhanh</span><button type="button" data-url="@home">Trang chủ</button><button type="button" data-url="@rooms">Phòng</button><button type="button" data-url="@booking">Đặt phòng</button><button type="button" data-url="@lookup">Tra cứu</button><button type="button" data-url="/blog">Blog</button><button type="button" data-url="/#gallery">Gallery</button></div><div class="psd-footer-inline-link-foot"><button type="button" data-pfi-link-test>↗ Mở thử</button><button type="button" class="primary" data-pfi-link-done>Xong</button></div>`;
        document.body.appendChild(panel);
        panel.addEventListener('pointerdown', event => event.stopPropagation());
        panel.querySelector('[data-pfi-link-close]').addEventListener('click', hideLinkPanel);
        panel.querySelector('[data-pfi-link-done]').addEventListener('click', () => {
            if (state.active) state.active.pendingUrl = panel.querySelector('[data-pfi-link-input]').value.trim();
            hideLinkPanel();
        });
        panel.querySelector('[data-pfi-link-input]').addEventListener('input', event => {
            if (state.active) state.active.pendingUrl = event.currentTarget.value;
        });
        panel.querySelectorAll('[data-url]').forEach(button => button.addEventListener('click', () => {
            panel.querySelector('[data-pfi-link-input]').value = button.dataset.url || '';
            if (state.active) state.active.pendingUrl = button.dataset.url || '';
        }));
        panel.querySelector('[data-pfi-link-test]').addEventListener('click', () => {
            const url = resolveUrl(panel.querySelector('[data-pfi-link-input]').value);
            if (url && url !== '#') window.open(url, '_blank', 'noopener,noreferrer');
            else toast('Link hiện tại không thể mở thử.', true);
        });
        state.linkPanel = panel;
        return panel;
    }

    function showBar() {
        if (!state.active) return;
        const bar = ensureBar();
        bar.querySelector('[data-pfi-title]').textContent = `Đang sửa · ${state.active.label}`;
        bar.querySelector('[data-pfi-link]').hidden = !state.active.hasUrl;
        bar.hidden = false;
        requestAnimationFrame(() => placeFloating(bar, state.active.target));
    }

    function hideLinkPanel() {
        if (state.linkPanel) state.linkPanel.hidden = true;
    }

    function toggleLinkPanel() {
        if (!state.active?.hasUrl) return;
        const panel = ensureLinkPanel();
        if (!panel.hidden) return hideLinkPanel();
        panel.querySelector('[data-pfi-link-input]').value = state.active.pendingUrl || '';
        panel.hidden = false;
        requestAnimationFrame(() => {
            placeFloating(panel, state.bar || state.active.target);
            panel.querySelector('[data-pfi-link-input]').focus();
            panel.querySelector('[data-pfi-link-input]').select();
        });
    }

    function startEditing(target) {
        if (!document.body.classList.contains('pve-editing')) return;
        const host = target.closest('.psd-footer-element[data-footer-element-id]');
        const model = findElement(state.designer, host?.dataset.footerElementId);
        if (!host || !model) return;
        if (state.active?.target === target) return;
        cancelActive(true);

        const type = elementType(host);
        const linkIndex = target.dataset.pfiLinkIndex == null ? -1 : Number(target.dataset.pfiLinkIndex);
        const isCustomLink = type === 'links' && linkIndex >= 0 && model.links?.[linkIndex];
        const label = isCustomLink ? `Link ${linkIndex + 1}` : type === 'heading' ? 'Tiêu đề Footer' : type === 'button' ? 'Nút Footer' : 'Văn bản Footer';
        const pendingUrl = isCustomLink ? String(model.links[linkIndex].url || '') : type === 'button' ? String(model.url || '') : '';
        state.active = {
            target, host, elementId: host.dataset.footerElementId, type, linkIndex,
            label, multiline: type === 'text', hasUrl: type === 'button' || isCustomLink,
            pendingUrl, originalDisplay: target.textContent || ''
        };
        target.classList.add('psd-footer-inline-active');
        target.setAttribute('contenteditable', 'plaintext-only');
        target.setAttribute('aria-label', `Đang sửa ${label}`);
        target.focus({ preventScroll: true });
        try {
            const selection = getSelection();
            const range = document.createRange();
            range.selectNodeContents(target); range.collapse(false);
            selection.removeAllRanges(); selection.addRange(range);
        } catch { }
        showBar();
    }

    function finishActive() {
        if (!state.active) return;
        state.active.target.classList.remove('psd-footer-inline-active', 'psd-footer-inline-saving');
        state.active.target.removeAttribute('contenteditable');
        state.active.target.removeAttribute('aria-label');
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
        state.active.target.textContent = state.active.originalDisplay;
        finishActive();
        if (!silent) toast('Đã hủy thay đổi Footer.');
    }

    async function saveActive() {
        const active = state.active;
        if (!active) return;
        const model = findElement(state.designer, active.elementId);
        if (!model) return toast('Không tìm thấy phần tử Footer đang sửa.', true);
        const value = normalizedText(active.target);
        if (active.type === 'links' && active.linkIndex >= 0 && model.links?.[active.linkIndex]) {
            model.links[active.linkIndex].label = value;
            model.links[active.linkIndex].url = String(active.pendingUrl || '').trim();
        } else {
            model.text = value;
            if (active.type === 'button') model.url = String(active.pendingUrl || '').trim();
        }

        active.target.classList.add('psd-footer-inline-saving');
        const save = state.bar?.querySelector('[data-pfi-save]');
        if (save) { save.disabled = true; save.textContent = 'Đang lưu…'; }
        try {
            const saved = await saveDesigner();
            const stored = findElement(saved, active.elementId) || model;
            if (active.type === 'links' && active.linkIndex >= 0 && stored.links?.[active.linkIndex]) {
                active.target.textContent = stored.links[active.linkIndex].label || value;
                active.target.href = resolveUrl(stored.links[active.linkIndex].url || active.pendingUrl);
            } else {
                active.target.textContent = stored.text || value;
                if (active.type === 'button') active.target.href = resolveUrl(stored.url || active.pendingUrl);
            }
            finishActive();
            toast(`Đã lưu ${active.label.toLowerCase()}.`);
        } catch (error) {
            active.target.classList.remove('psd-footer-inline-saving');
            toast(error.message || 'Không thể lưu Footer.', true);
        } finally {
            if (save) { save.disabled = false; save.textContent = 'Lưu'; }
        }
    }

    async function changeImage(host) {
        const model = findElement(state.designer, host?.dataset.footerElementId);
        if (!host || !model) return;
        cancelActive(true);
        if (!global.DeLongMediaLibrary?.pick) return toast('Media Library chưa sẵn sàng.', true);
        global.DeLongMediaLibrary.pick({
            currentUrl: model.imageUrl || '',
            onSelect: async item => {
                const previous = { imageUrl: model.imageUrl, altText: model.altText };
                model.imageUrl = item.url || model.imageUrl || '';
                if (!model.altText) model.altText = item.altText || item.title || '';
                const button = host.querySelector('[data-pfi-image]');
                if (button) { button.disabled = true; button.textContent = 'Đang lưu…'; }
                try {
                    const saved = await saveDesigner();
                    const stored = findElement(saved, host.dataset.footerElementId) || model;
                    const image = host.querySelector('img');
                    if (image) { image.src = stored.imageUrl || model.imageUrl; image.alt = stored.altText || model.altText || ''; }
                    toast('Đã đổi ảnh Footer.');
                } catch (error) {
                    model.imageUrl = previous.imageUrl; model.altText = previous.altText;
                    toast(error.message || 'Không thể đổi ảnh Footer.', true);
                } finally {
                    if (button) { button.disabled = false; button.textContent = '✎ Đổi ảnh'; }
                }
            }
        });
    }

    function openAdvanced() {
        cancelActive(true);
        if (global.DeLongShellDesigner?.open) global.DeLongShellDesigner.open('footer');
        else toast('Footer Builder chưa sẵn sàng.', true);
    }

    function addAdvancedTool(host) {
        if (host.querySelector(':scope > [data-pfi-advanced]')) return;
        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'psd-footer-inline-advanced';
        button.dataset.pfiAdvanced = '1';
        button.title = 'Mở thiết kế Footer nâng cao';
        button.setAttribute('aria-label', 'Mở thiết kế Footer nâng cao');
        button.textContent = '⚙';
        host.appendChild(button);
    }

    function decorateElement(host) {
        if (!host || host.dataset.pfiReady === '1') return;
        const model = findElement(state.designer, host.dataset.footerElementId);
        if (!model) return;
        const type = elementType(host);
        host.dataset.pfiReady = '1';
        host.classList.add('psd-footer-inline-editable');
        addAdvancedTool(host);

        let target = null;
        if (type === 'heading') target = host.querySelector('h2,h3,h4');
        else if (type === 'text') target = host.querySelector('p');
        else if (type === 'button') target = host.querySelector('.psd-footer-button');
        if (target) {
            target.classList.add('psd-footer-inline-content');
            target.title = type === 'button' ? 'Click để sửa nút Footer' : 'Click để sửa trực tiếp';
        }

        if (type === 'links') {
            host.querySelectorAll('.psd-footer-link-list > a').forEach((anchor, index) => {
                anchor.classList.add('psd-footer-inline-content');
                anchor.dataset.pfiLinkIndex = String(index);
                anchor.title = 'Click để sửa tên và liên kết';
            });
        }

        if (type === 'image') {
            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'psd-footer-inline-image-button';
            button.dataset.pfiImage = '1';
            button.textContent = '✎ Đổi ảnh';
            host.appendChild(button);
            const image = host.querySelector('img');
            if (image) image.title = 'Click để đổi ảnh từ Media Library';
        }
    }

    function decorateAll() {
        if (!document.body.classList.contains('pve-editing') || !state.designer?.footerBuilderEnabled) return;
        document.querySelectorAll('[data-psd-footer-builder] .psd-footer-element[data-footer-element-id]').forEach(decorateElement);
        document.querySelector('.public-hospitality-footer')?.classList.add('psd-footer-inline-mode');
    }

    function clearDecorations() {
        cancelActive(true);
        document.querySelector('.public-hospitality-footer')?.classList.remove('psd-footer-inline-mode');
        document.querySelectorAll('[data-pfi-ready="1"]').forEach(host => {
            delete host.dataset.pfiReady;
            host.classList.remove('psd-footer-inline-editable');
            host.querySelectorAll(':scope > [data-pfi-advanced],:scope > [data-pfi-image]').forEach(button => button.remove());
            host.querySelectorAll('.psd-footer-inline-content').forEach(node => {
                node.classList.remove('psd-footer-inline-content', 'psd-footer-inline-active', 'psd-footer-inline-saving');
                node.removeAttribute('contenteditable');
                node.removeAttribute('aria-label');
                node.removeAttribute('title');
                delete node.dataset.pfiLinkIndex;
            });
        });
    }

    async function activate() {
        if (state.loading || !document.body.classList.contains('pve-editing')) return;
        state.loading = true;
        try {
            if (!state.context) state.context = await DeLongApi.get(contextUrl);
            if (!state.context?.canEdit || !document.body.classList.contains('pve-editing')) return;
            state.designer = await DeLongApi.get(`${designerApi()}/`);
            if (!document.body.classList.contains('pve-editing')) return;
            decorateAll();
        } catch (error) {
            toast(error.message || 'Không thể bật chỉnh sửa nhanh Footer.', true);
        } finally {
            state.loading = false;
        }
    }

    function syncMode() {
        if (document.body.classList.contains('pve-editing')) {
            if (!state.designer && !state.loading) activate();
            else decorateAll();
        } else if (state.designer || state.active) {
            clearDecorations();
            state.designer = null;
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
        const advanced = event.target.closest('[data-pfi-advanced]');
        if (advanced) {
            event.preventDefault(); event.stopPropagation();
            openAdvanced();
            return;
        }
        const imageButton = event.target.closest('[data-pfi-image]');
        if (imageButton) {
            event.preventDefault(); event.stopPropagation();
            changeImage(imageButton.closest('.psd-footer-element'));
            return;
        }
        const image = event.target.closest('.psd-footer-element-image img');
        if (image) {
            event.preventDefault(); event.stopPropagation();
            changeImage(image.closest('.psd-footer-element'));
            return;
        }
        const content = event.target.closest('.psd-footer-inline-content');
        if (content) {
            event.preventDefault(); event.stopPropagation();
            startEditing(content);
        }
    }, true);

    document.addEventListener('keydown', event => {
        if (!state.active) return;
        if (event.key === 'Escape') {
            event.preventDefault(); cancelActive();
            return;
        }
        if (event.key === 'Enter' && (!state.active.multiline || event.ctrlKey || event.metaKey)) {
            event.preventDefault(); saveActive();
        }
    });

    addEventListener('scroll', () => {
        if (state.active && state.bar && !state.bar.hidden) placeFloating(state.bar, state.active.target);
        if (state.linkPanel && !state.linkPanel.hidden) placeFloating(state.linkPanel, state.bar || state.active?.target);
    }, { passive: true });
    addEventListener('resize', () => {
        if (state.active && state.bar && !state.bar.hidden) placeFloating(state.bar, state.active.target);
        if (state.linkPanel && !state.linkPanel.hidden) placeFloating(state.linkPanel, state.bar || state.active?.target);
    });

    new MutationObserver(queueSync).observe(document.body, { childList: true, subtree: true, attributes: true, attributeFilter: ['class', 'data-footer-element-id'] });
    queueSync();
})(window);
