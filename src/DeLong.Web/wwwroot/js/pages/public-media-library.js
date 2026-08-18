(function (global) {
    if (!document.body?.classList.contains('public-body') || !global.DeLongApi) return;

    const path = location.pathname.replace(/\/+$/, '') || '/';
    const scoped = path.match(/^\/h\/([^/]+)(?:\/|$)/i);
    const siteSlug = scoped ? decodeURIComponent(scoped[1]) : '';
    const contextUrl = `/api/admin/site/visual-context${siteSlug ? `?siteSlug=${encodeURIComponent(siteSlug)}` : ''}`;
    const state = {
        context: null,
        contextPromise: null,
        api: '',
        siteApi: '',
        modal: null,
        items: [],
        selectedId: '',
        callback: null,
        currentUrl: '',
        loading: false,
        filter: 'all',
        kindFilter: 'all',
        query: '',
        unusedOnly: false,
        queued: false
    };

    const h = value => String(value ?? '').replace(/[&<>"']/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[ch]));
    const baseUrl = value => String(value || '').split('?')[0];
    const isRoom = item => String(item?.kind || '').toLowerCase() === 'room';
    const fmtBytes = bytes => {
        const n = Number(bytes) || 0;
        if (n < 1024) return `${n} B`;
        if (n < 1024 * 1024) return `${(n / 1024).toFixed(n < 10240 ? 1 : 0)} KB`;
        return `${(n / 1024 / 1024).toFixed(1)} MB`;
    };
    const dateText = value => {
        try { return new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short' }).format(new Date(value)); }
        catch { return ''; }
    };

    function toast(message, error) {
        let node = document.querySelector('.pml-toast');
        if (!node) { node = document.createElement('div'); node.className = 'pml-toast'; document.body.appendChild(node); }
        node.className = `pml-toast ${error ? 'error' : 'success'}`;
        node.textContent = message;
        node.hidden = false;
        clearTimeout(node._timer);
        node._timer = setTimeout(() => { node.hidden = true; }, 3200);
    }

    async function context() {
        if (state.context) return state.context;
        if (!state.contextPromise) state.contextPromise = DeLongApi.get(contextUrl).catch(() => null);
        state.context = await state.contextPromise;
        if (!state.context?.canEdit) return null;
        state.api = state.context.scope === 'global'
            ? '/api/admin/site/global/media'
            : `/api/admin/properties/${state.context.propertyId}/media`;
        state.siteApi = state.context.scope === 'global'
            ? '/api/admin/site/global'
            : `/api/admin/properties/${state.context.propertyId}/site`;
        return state.context;
    }

    function ensureModal() {
        if (state.modal?.isConnected) return state.modal;
        const root = document.createElement('div');
        root.className = 'pml-modal';
        root.hidden = true;
        root.innerHTML = `<div class="pml-backdrop" data-pml-close></div><section class="pml-dialog" role="dialog" aria-modal="true" aria-label="Media Library"><header><div><small>MEDIA LIBRARY</small><h2>Ảnh đã tải lên</h2></div><button type="button" data-pml-close aria-label="Đóng">×</button></header><div class="pml-toolbar"><label class="pml-search"><span>⌕</span><input type="search" placeholder="Tìm tên ảnh, phòng, cơ sở…" data-pml-search></label><select data-pml-scope><option value="all">Tất cả phạm vi</option><option value="property">Cơ sở này</option><option value="global">Dùng chung</option></select><select data-pml-kind><option value="all">Tất cả loại</option><option value="section">Ảnh website</option><option value="room">Ảnh phòng</option></select><label class="pml-unused"><input type="checkbox" data-pml-unused> Chưa dùng</label><label class="pml-upload">＋ Tải ảnh mới<input type="file" accept="image/png,image/jpeg,image/webp" multiple data-pml-upload hidden></label></div><div class="pml-stats" data-pml-stats></div><div class="pml-body"><main><div class="pml-loading" data-pml-loading hidden>Đang tải Media Library…</div><div class="pml-empty" data-pml-empty hidden>Không có media phù hợp.</div><div class="pml-grid" data-pml-grid></div></main><aside class="pml-detail" data-pml-detail><div class="pml-detail-empty">Chọn một ảnh để xem thông tin.</div></aside></div><footer><span data-pml-selection></span><div><button type="button" data-pml-close>Đóng</button><button type="button" class="primary" data-pml-use hidden>Dùng ảnh này</button></div></footer></section>`;
        document.body.appendChild(root);
        root.querySelectorAll('[data-pml-close]').forEach(button => button.addEventListener('click', close));
        root.querySelector('[data-pml-search]').addEventListener('input', event => { state.query = event.currentTarget.value.trim().toLowerCase(); renderGrid(); });
        root.querySelector('[data-pml-scope]').addEventListener('change', event => { state.filter = event.currentTarget.value; renderGrid(); });
        root.querySelector('[data-pml-kind]').addEventListener('change', event => { state.kindFilter = event.currentTarget.value; renderGrid(); });
        root.querySelector('[data-pml-unused]').addEventListener('change', event => { state.unusedOnly = event.currentTarget.checked; renderGrid(); });
        root.querySelector('[data-pml-upload]').addEventListener('change', async event => {
            const files = [...(event.currentTarget.files || [])]; event.currentTarget.value = '';
            if (files.length) await uploadFiles(files);
        });
        root.querySelector('[data-pml-use]').addEventListener('click', useSelected);
        root.querySelector('[data-pml-grid]').addEventListener('click', event => {
            const card = event.target.closest('[data-pml-id]'); if (card) select(card.dataset.pmlId);
        });
        root.querySelector('[data-pml-grid]').addEventListener('dblclick', event => {
            const card = event.target.closest('[data-pml-id]');
            if (card && state.callback) { select(card.dataset.pmlId); useSelected(); }
        });
        root.querySelector('[data-pml-detail]').addEventListener('click', async event => {
            if (event.target.closest('[data-pml-save-meta]')) await saveMetadata();
            if (event.target.closest('[data-pml-delete]')) await deleteSelected();
            if (event.target.closest('[data-pml-copy]')) await copyUrl();
        });
        root.addEventListener('keydown', event => { if (event.key === 'Escape') close(); });
        state.modal = root;
        return root;
    }

    async function open(options) {
        const ctx = await context();
        if (!ctx) return;
        const modal = ensureModal();
        state.callback = typeof options?.onSelect === 'function' ? options.onSelect : null;
        state.currentUrl = String(options?.currentUrl || '');
        state.selectedId = '';
        state.query = '';
        state.filter = 'all';
        state.kindFilter = 'all';
        state.unusedOnly = false;
        modal.querySelector('[data-pml-search]').value = '';
        modal.querySelector('[data-pml-unused]').checked = false;
        modal.querySelector('[data-pml-kind]').value = 'all';
        const scope = modal.querySelector('[data-pml-scope]');
        scope.value = 'all';
        scope.hidden = ctx.scope === 'global';
        modal.querySelector('[data-pml-use]').hidden = !state.callback;
        modal.hidden = false;
        document.body.classList.add('pml-open');
        await load();
        const same = state.items.find(item => [item.url, item.largeUrl, item.cardUrl, item.thumbnailUrl].some(url => baseUrl(url) === baseUrl(state.currentUrl)));
        if (same) select(String(same.id));
    }

    function close() {
        if (state.modal) state.modal.hidden = true;
        document.body.classList.remove('pml-open');
        state.callback = null;
    }

    async function load() {
        if (state.loading || !state.api) return;
        state.loading = true;
        const modal = ensureModal();
        modal.querySelector('[data-pml-loading]').hidden = false;
        try {
            const data = await DeLongApi.get(`${state.api}/`);
            state.items = Array.isArray(data?.items) ? data.items : [];
            modal.querySelector('[data-pml-stats]').innerHTML = `<span><strong>${Number(data?.totalCount || state.items.length)}</strong> media</span><span><strong>${fmtBytes(data?.totalBytes)}</strong> đang lưu</span><span><strong>${Number(data?.roomImageCount || 0)}</strong> ảnh phòng · ${fmtBytes(data?.roomImageBytes)}</span><span><strong>${Number(data?.unusedCount || 0)}</strong> website chưa dùng · ${fmtBytes(data?.unusedBytes)}</span>`;
            renderGrid();
            renderDetail();
        } catch (error) {
            toast(error.message || 'Không thể tải Media Library.', true);
        } finally {
            state.loading = false;
            modal.querySelector('[data-pml-loading]').hidden = true;
        }
    }

    function filteredItems() {
        const query = state.query;
        return state.items.filter(item => {
            if (state.filter === 'global' && !item.isGlobal) return false;
            if (state.filter === 'property' && item.isGlobal) return false;
            if (state.kindFilter !== 'all' && String(item.kind || 'section') !== state.kindFilter) return false;
            if (state.unusedOnly && (isRoom(item) || Number(item.usageCount) > 0)) return false;
            if (!query) return true;
            return [item.title, item.altText, item.originalFileName, item.propertyName, item.roomName, item.roomCode]
                .some(value => String(value || '').toLowerCase().includes(query));
        });
    }

    function renderGrid() {
        const modal = ensureModal(), grid = modal.querySelector('[data-pml-grid]'), items = filteredItems();
        modal.querySelector('[data-pml-empty]').hidden = items.length > 0 || state.loading;
        grid.innerHTML = items.map(item => {
            const room = isRoom(item);
            const badge = room
                ? (Number(item.usageCount) > 0 ? `<b>${item.usageCount} tham chiếu thêm</b>` : '<b class="room-source">Ảnh phòng</b>')
                : (Number(item.usageCount) > 0 ? `<b>${item.usageCount} nơi dùng</b>` : '<b class="unused">Chưa dùng</b>');
            return `<button type="button" class="pml-card${room ? ' room-media' : ''}${String(item.id) === state.selectedId ? ' selected' : ''}" data-pml-id="${h(item.id)}"><span class="pml-thumb"><img src="${h(item.thumbnailUrl || item.url)}" alt="${h(item.altText || '')}" loading="lazy"><em>${item.isGlobal ? 'Dùng chung' : h(item.propertyName || 'Cơ sở')}</em>${badge}${room && item.isCover ? '<i>★ Bìa phòng</i>' : ''}</span><span class="pml-card-copy"><strong>${h(item.title || item.originalFileName || 'Ảnh')}</strong><small>${item.width}×${item.height} · ${fmtBytes(item.byteSize)}${room ? ' · original + 3 bản' : ''}</small></span></button>`;
        }).join('');
        const selected = state.items.find(item => String(item.id) === state.selectedId);
        modal.querySelector('[data-pml-selection]').textContent = selected ? `Đã chọn: ${selected.title || selected.originalFileName}` : `${items.length} kết quả`;
    }

    function select(id) {
        state.selectedId = String(id || '');
        renderGrid();
        renderDetail();
    }

    function renderDetail() {
        const detail = ensureModal().querySelector('[data-pml-detail]');
        const item = state.items.find(x => String(x.id) === state.selectedId);
        if (!item) { detail.innerHTML = '<div class="pml-detail-empty">Chọn một ảnh để xem thông tin.</div>'; return; }
        if (isRoom(item)) {
            const usage = Number(item.usageCount) || 0;
            detail.innerHTML = `<img class="pml-detail-preview" src="${h(item.largeUrl || item.url)}" alt="${h(item.altText || '')}"><div class="pml-detail-meta"><span>Ảnh phòng</span><span>${h(item.propertyName || 'Cơ sở')}</span><span>${h(item.roomName || item.roomCode || 'Phòng')}</span><span>${item.width}×${item.height}</span><span>${fmtBytes(item.byteSize)}</span><span>${dateText(item.createdAtUtc)}</span></div><label><span>Alt text</span><textarea rows="2" disabled>${h(item.altText || '')}</textarea></label><div class="pml-usage used">Ảnh này thuộc gallery phòng${item.isCover ? ' và đang là ảnh bìa' : ''}.${usage ? ` Ngoài ra còn ${usage} tham chiếu khác trên website.` : ''} Bạn có thể dùng lại bản Large cho section hiện tại mà không upload thêm file.</div><div class="pml-detail-actions"><button type="button" data-pml-copy>Sao chép Large URL</button>${item.roomId ? `<a href="/Admin/Rooms/${h(item.roomId)}/Content" target="_blank" rel="noopener">Quản lý ảnh phòng ↗</a>` : ''}<small>Xóa/đổi alt ảnh phòng thực hiện trong Admin Media hoặc Nội dung phòng để tránh thao tác nhầm khi đang chọn ảnh.</small></div><small class="pml-file-name">${h(item.originalFileName || '')}</small>`;
            return;
        }
        const editable = !!item.canEdit;
        detail.innerHTML = `<img class="pml-detail-preview" src="${h(item.url)}" alt="${h(item.altText || '')}"><div class="pml-detail-meta"><span>${h(item.propertyName || 'Dùng chung')}</span><span>${item.width}×${item.height}</span><span>${fmtBytes(item.byteSize)}</span><span>${dateText(item.createdAtUtc)}</span></div><label><span>Tiêu đề nội bộ</span><input name="mediaTitle" value="${h(item.title || '')}"${editable ? '' : ' disabled'}></label><label><span>Alt text</span><textarea name="mediaAlt" rows="2"${editable ? '' : ' disabled'}>${h(item.altText || '')}</textarea></label><div class="pml-usage ${Number(item.usageCount) ? 'used' : 'unused'}">${Number(item.usageCount) ? `Đang được dùng ở ${item.usageCount} vị trí.` : 'Chưa được sử dụng — có thể xóa để giải phóng dung lượng.'}</div><div class="pml-detail-actions"><button type="button" data-pml-copy>Sao chép URL</button>${editable ? '<button type="button" data-pml-save-meta>Lưu thông tin</button><button type="button" class="danger" data-pml-delete>Xóa</button>' : '<small>Media dùng chung chỉ Admin mới quản lý được.</small>'}</div><small class="pml-file-name">${h(item.originalFileName || '')}</small>`;
    }

    async function uploadFiles(files) {
        const modal = ensureModal();
        const upload = modal.querySelector('.pml-upload');
        const valid = files.filter(file => /^image\/(png|jpeg|webp)$/i.test(file.type));
        if (!valid.length) return toast('Chỉ hỗ trợ PNG, JPG hoặc WebP.', true);
        upload.classList.add('busy');
        let last = null;
        try {
            for (let i = 0; i < valid.length; i++) {
                upload.childNodes[0].textContent = `Đang tải ${i + 1}/${valid.length}…`;
                const form = new FormData(); form.append('file', valid[i]);
                last = await DeLongApi.postForm(`${state.api}/upload`, form);
            }
            await load();
            if (last?.id) select(String(last.id));
            toast(valid.length > 1 ? `Đã thêm ${valid.length} ảnh vào thư viện.` : 'Đã thêm ảnh vào thư viện.');
        } catch (error) {
            toast(error.message || 'Không thể tải ảnh.', true);
        } finally {
            upload.classList.remove('busy');
            upload.childNodes[0].textContent = '＋ Tải ảnh mới';
        }
    }

    async function saveMetadata() {
        const item = state.items.find(x => String(x.id) === state.selectedId); if (!item || isRoom(item)) return;
        const detail = ensureModal().querySelector('[data-pml-detail]');
        try {
            const saved = await DeLongApi.put(`${state.api}/${item.id}`, {
                title: detail.querySelector('[name="mediaTitle"]')?.value.trim() || '',
                altText: detail.querySelector('[name="mediaAlt"]')?.value.trim() || ''
            });
            Object.assign(item, saved || {}); renderGrid(); renderDetail(); toast('Đã lưu thông tin media.');
        } catch (error) { toast(error.message || 'Không thể lưu thông tin media.', true); }
    }

    async function deleteSelected() {
        const item = state.items.find(x => String(x.id) === state.selectedId); if (!item) return;
        if (isRoom(item)) return toast('Hãy xóa ảnh phòng từ Admin Media Library hoặc Nội dung phòng.', true);
        if (Number(item.usageCount) > 0) return toast(`Ảnh đang được dùng ở ${item.usageCount} vị trí nên chưa thể xóa.`, true);
        if (!confirm(`Xóa “${item.title || item.originalFileName}” khỏi storage? Thao tác này không thể hoàn tác.`)) return;
        try { await DeLongApi.delete(`${state.api}/${item.id}`); state.selectedId = ''; await load(); toast('Đã xóa media không sử dụng.'); }
        catch (error) { toast(error.message || 'Không thể xóa media.', true); }
    }

    async function copyUrl() {
        const item = state.items.find(x => String(x.id) === state.selectedId); if (!item) return;
        try { await navigator.clipboard.writeText(item.url); toast(isRoom(item) ? 'Đã sao chép Large URL.' : 'Đã sao chép URL.'); }
        catch { toast('Không thể sao chép URL tự động.', true); }
    }

    function useSelected() {
        const item = state.items.find(x => String(x.id) === state.selectedId);
        if (!item || !state.callback) return;
        const callback = state.callback;
        close();
        callback(item);
    }

    async function applyInlineSectionImage(button, item) {
        const ctx = await context(); if (!ctx) return;
        const sectionId = button.dataset.pieSectionId, key = button.dataset.pieKey;
        if (!sectionId || !key) return;
        try {
            const data = await DeLongApi.get(`${state.siteApi}/`);
            const sections = data?.sections || data?.site?.sections || [];
            const section = sections.find(x => String(x.id) === String(sectionId));
            if (!section) throw new Error('Không tìm thấy section đang sửa.');
            let content; try { content = JSON.parse(section.contentJson || '{}'); } catch { content = {}; }
            setAt(content, key, item.url);
            await DeLongApi.put(`${state.siteApi}/sections/${section.id}`, {
                type: section.type,
                name: section.name || '',
                variant: section.variant || 'wide',
                isVisible: section.isVisible !== false,
                contentJson: JSON.stringify(content)
            });
            const image = button.parentElement?.querySelector(`[data-pie-image="${CSS.escape(key)}"]`) || button.parentElement?.querySelector('img');
            if (image) image.src = item.url;
            toast(isRoom(item) ? 'Đã dùng lại ảnh phòng từ Media Library.' : 'Đã chọn ảnh từ Media Library.');
        } catch (error) { toast(error.message || 'Không thể áp dụng ảnh.', true); }
    }

    function setAt(object, path, value) {
        const parts = String(path || '').split('.').filter(Boolean); if (!parts.length) return;
        let cursor = object;
        for (let i = 0; i < parts.length - 1; i++) {
            const next = parts[i + 1];
            if (cursor[parts[i]] == null || typeof cursor[parts[i]] !== 'object') cursor[parts[i]] = /^\d+$/.test(next) ? [] : {};
            cursor = cursor[parts[i]];
        }
        cursor[parts.at(-1)] = value;
    }

    function enhanceToolbar() {
        const actions = document.querySelector('.pve-toolbar-actions');
        if (!actions || actions.querySelector('[data-pml-manage]')) return;
        const button = document.createElement('button'); button.type = 'button'; button.dataset.pmlManage = '1'; button.textContent = 'Media';
        button.addEventListener('click', () => open());
        const admin = actions.querySelector('a[href*="/Admin"]'); actions.insertBefore(button, admin || null);
    }

    function addPickerButton(row, input) {
        if (!row || !input || row.querySelector('[data-media-pick]')) return;
        const button = document.createElement('button'); button.type = 'button'; button.className = 'pve-media-pick'; button.dataset.mediaPick = '1'; button.textContent = 'Thư viện';
        button.addEventListener('click', () => open({ currentUrl: input.value, onSelect: item => {
            input.value = item.url; input.dispatchEvent(new Event('input', { bubbles: true })); input.dispatchEvent(new Event('change', { bubbles: true }));
            const galleryRow = input.closest('[data-gallery-row]'); const thumb = galleryRow?.querySelector('.pve-gallery-thumb');
            if (thumb) thumb.innerHTML = `<img src="${h(item.url)}" alt="">`;
        }}));
        row.appendChild(button);
    }

    function enhanceImageRows() {
        document.querySelectorAll('.pve-image-row').forEach(row => addPickerButton(row, row.querySelector('input[type="text"]')));
        document.querySelectorAll('.pve-row-inline-inspector').forEach(panel => {
            const upload = panel.querySelector('[data-ri-upload]'), input = panel.querySelector('[name="imageUrl"]');
            if (!upload || !input || panel.querySelector('[data-pml-row-image]')) return;
            const button = document.createElement('button'); button.type = 'button'; button.dataset.pmlRowImage = '1'; button.textContent = 'Thư viện';
            button.addEventListener('click', () => open({ currentUrl: input.value, onSelect: item => { input.value = item.url; } }));
            upload.after(button);
        });
        document.querySelectorAll('[data-ae-upload]').forEach(upload => {
            if (upload.parentElement?.querySelector('[data-pml-ae]')) return;
            const form = upload.closest('[data-ae-type]'), field = upload.dataset.aeUpload, input = form?.querySelector(`[name="${CSS.escape(field || '')}"]`);
            if (!input) return;
            const button = document.createElement('button'); button.type = 'button'; button.dataset.pmlAe = '1'; button.textContent = 'Thư viện';
            button.addEventListener('click', () => open({ currentUrl: input.value, onSelect: item => { input.value = item.url; input.dispatchEvent(new Event('input', { bubbles: true })); } }));
            upload.after(button);
        });
    }

    function enhanceGalleryStudio() {
        document.querySelectorAll('.pve-gallery-studio-toolbar').forEach(toolbar => {
            if (toolbar.querySelector('[data-pml-gallery]')) return;
            const button = document.createElement('button'); button.type = 'button'; button.className = 'pml-gallery-button'; button.dataset.pmlGallery = '1'; button.textContent = '＋ Chọn từ Media';
            button.addEventListener('click', () => open({ onSelect: item => {
                const drawer = toolbar.closest('.pve-gallery-studio-drawer'), add = drawer?.querySelector('[data-gallery-add]');
                add?.click();
                const row = drawer?.querySelector('[data-gallery-row]:last-child'); if (!row) return;
                const url = row.querySelector('[name="gallery.imageUrl"]'), alt = row.querySelector('[name="gallery.altText"]'), thumb = row.querySelector('.pve-gallery-thumb');
                if (url) url.value = item.url; if (alt && !alt.value) alt.value = item.altText || item.title || ''; if (thumb) thumb.innerHTML = `<img src="${h(item.url)}" alt="">`;
                url?.dispatchEvent(new Event('input', { bubbles: true }));
            }}));
            const bulk = toolbar.querySelector('.pve-gallery-bulk-upload'); toolbar.querySelector(':scope > div:last-child')?.insertBefore(button, bulk || null);
        });
    }

    function enhance() { enhanceToolbar(); enhanceImageRows(); enhanceGalleryStudio(); }
    function queue() { if (state.queued) return; state.queued = true; requestAnimationFrame(() => { state.queued = false; enhance(); }); }

    document.addEventListener('click', event => {
        const button = event.target.closest('[data-pie-image-button]');
        if (!button || !document.body.classList.contains('pve-editing')) return;
        event.preventDefault(); event.stopImmediatePropagation();
        const image = button.parentElement?.querySelector('img');
        open({ currentUrl: image?.src || '', onSelect: item => applyInlineSectionImage(button, item) });
    }, true);

    new MutationObserver(queue).observe(document.body, { childList: true, subtree: true, attributes: true, attributeFilter: ['class'] });
    addEventListener('resize', () => state.modal && !state.modal.hidden && renderGrid());
    global.DeLongMediaLibrary = { open, pick: options => open(options || {}), manage: () => open(), refresh: load };
    context().then(ctx => { if (ctx) enhance(); });
})(window);
