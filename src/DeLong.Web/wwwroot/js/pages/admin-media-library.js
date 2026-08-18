(function () {
    const root = document.getElementById('admin-media-library');
    const dataNode = document.getElementById('admin-media-data');
    if (!root || !dataNode || !window.DeLongApi) return;

    let config = {};
    try { config = JSON.parse(dataNode.textContent || '{}'); } catch { config = {}; }

    const state = { items: [], selectedId: '', query: '', propertyFilter: 'all', unusedOnly: false, loading: false, cleaning: false };
    const h = value => String(value ?? '').replace(/[&<>"']/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[ch]));
    const fmtBytes = value => {
        const n = Number(value) || 0;
        if (n < 1024) return `${n} B`;
        if (n < 1024 * 1024) return `${(n / 1024).toFixed(n < 10240 ? 1 : 0)} KB`;
        if (n < 1024 * 1024 * 1024) return `${(n / 1024 / 1024).toFixed(1)} MB`;
        return `${(n / 1024 / 1024 / 1024).toFixed(2)} GB`;
    };
    const dateText = value => {
        try { return new Intl.DateTimeFormat('vi-VN', { dateStyle: 'medium' }).format(new Date(value)); }
        catch { return ''; }
    };

    function toast(message, error) {
        let node = document.querySelector('.media-admin-toast');
        if (!node) { node = document.createElement('div'); node.className = 'media-admin-toast'; document.body.appendChild(node); }
        node.className = `media-admin-toast${error ? ' error' : ''}`;
        node.textContent = message; node.hidden = false;
        clearTimeout(node._timer); node._timer = setTimeout(() => { node.hidden = true; }, 4200);
    }

    function propertyOptions() {
        const list = Array.isArray(config.properties) ? config.properties : [];
        const filter = root.querySelector('[data-media-property-filter]');
        const uploadScope = root.querySelector('[data-media-upload-scope]');
        if (config.isAdmin) {
            filter.innerHTML = `<option value="all">Tất cả cơ sở</option><option value="global">Dùng chung</option>${list.map(x => `<option value="${h(x.id)}">${h(x.name)} · ${h(x.code)}</option>`).join('')}`;
            uploadScope.innerHTML = `<option value="global">Dùng chung toàn hệ thống</option>${list.map(x => `<option value="${h(x.id)}">${h(x.name)} · ${h(x.code)}</option>`).join('')}`;
            if (config.propertyId && list.some(x => String(x.id) === String(config.propertyId))) uploadScope.value = String(config.propertyId);
        } else {
            filter.innerHTML = `<option value="all">Cơ sở này + dùng chung</option><option value="property">Chỉ cơ sở này</option><option value="global">Dùng chung</option>`;
            uploadScope.innerHTML = `<option value="${h(config.propertyId)}">${h(config.propertyName || 'Cơ sở này')}</option>`;
            uploadScope.disabled = true;
        }
    }

    async function load() {
        if (state.loading) return;
        state.loading = true;
        root.querySelector('[data-media-loading]').hidden = false;
        root.querySelector('[data-media-empty]').hidden = true;
        try {
            const data = await DeLongApi.get(`${config.listApi}/`);
            state.items = Array.isArray(data?.items) ? data.items : [];
            root.querySelector('[data-media-total-count]').textContent = Number(data?.totalCount || state.items.length).toLocaleString('vi-VN');
            root.querySelector('[data-media-total-bytes]').textContent = fmtBytes(data?.totalBytes);
            root.querySelector('[data-media-unused-count]').textContent = Number(data?.unusedCount || 0).toLocaleString('vi-VN');
            root.querySelector('[data-media-unused-bytes]').textContent = `${fmtBytes(data?.unusedBytes)} có thể dọn`;
            if (state.selectedId && !state.items.some(x => String(x.id) === state.selectedId)) state.selectedId = '';
            render();
        } catch (error) {
            toast(error.message || 'Không thể tải Media Library.', true);
        } finally {
            state.loading = false;
            root.querySelector('[data-media-loading]').hidden = true;
        }
    }

    function matchesPropertyFilter(item) {
        if (config.isAdmin) {
            if (state.propertyFilter === 'global') return !!item.isGlobal;
            if (state.propertyFilter !== 'all') return String(item.propertyId || '') === state.propertyFilter;
            return true;
        }
        if (state.propertyFilter === 'global') return !!item.isGlobal;
        if (state.propertyFilter === 'property') return !item.isGlobal;
        return true;
    }

    function filtered() {
        return state.items.filter(item => {
            if (state.unusedOnly && Number(item.usageCount) > 0) return false;
            if (!matchesPropertyFilter(item)) return false;
            if (!state.query) return true;
            return [item.title, item.altText, item.originalFileName, item.propertyName].some(value => String(value || '').toLowerCase().includes(state.query));
        });
    }

    function cleanupCandidates() {
        return state.items.filter(item =>
            !!item.canDelete &&
            Number(item.usageCount) === 0 &&
            matchesPropertyFilter(item));
    }

    function syncCleanupButton() {
        const button = root.querySelector('[data-media-show-unused]');
        if (!button) return;
        if (state.cleaning) return;
        const candidates = cleanupCandidates();
        button.disabled = candidates.length === 0;
        button.textContent = candidates.length
            ? `Dọn ${candidates.length.toLocaleString('vi-VN')} media chưa dùng`
            : 'Không có media cần dọn';
        button.title = candidates.length
            ? `Xóa vĩnh viễn ${candidates.length} file chưa được sử dụng trong phạm vi đang chọn`
            : 'Không có file chưa dùng có quyền xóa trong phạm vi đang chọn';
    }

    function render() {
        const items = filtered();
        const grid = root.querySelector('[data-media-grid]');
        root.querySelector('[data-media-result-count]').textContent = `${items.length.toLocaleString('vi-VN')} media`;
        root.querySelector('[data-media-empty]').hidden = items.length > 0 || state.loading;
        grid.innerHTML = items.map(item => `<button type="button" class="media-admin-card${String(item.id) === state.selectedId ? ' selected' : ''}" data-media-id="${h(item.id)}"><span class="media-admin-thumb"><img src="${h(item.url)}" alt="${h(item.altText || '')}" loading="lazy"><em>${item.isGlobal ? 'Dùng chung' : h(item.propertyName || 'Cơ sở')}</em>${Number(item.usageCount) > 0 ? `<b>${item.usageCount} nơi dùng</b>` : '<b class="unused">Chưa dùng</b>'}</span><span class="media-admin-card-copy"><strong>${h(item.title || item.originalFileName || 'Ảnh')}</strong><small>${Number(item.width) || 0}×${Number(item.height) || 0} · ${fmtBytes(item.byteSize)}</small></span></button>`).join('');
        renderDetail();
        syncCleanupButton();
    }

    function selected() { return state.items.find(x => String(x.id) === state.selectedId); }

    function renderDetail() {
        const detail = root.querySelector('[data-media-detail]');
        const item = selected();
        if (!item) {
            detail.innerHTML = '<div class="media-admin-detail-empty"><strong>Chưa chọn media</strong><span>Click một thumbnail bên trái để xem dung lượng, metadata và trạng thái sử dụng.</span></div>';
            return;
        }
        const editable = !!item.canDelete;
        detail.innerHTML = `<img class="media-admin-detail-preview" src="${h(item.url)}" alt="${h(item.altText || '')}"><div class="media-admin-detail-meta"><span>${h(item.propertyName || 'Dùng chung')}</span><span>${item.width}×${item.height}</span><span>${fmtBytes(item.byteSize)}</span><span>${dateText(item.createdAtUtc)}</span></div><label><span>Tiêu đề nội bộ</span><input name="mediaTitle" value="${h(item.title || '')}"${editable ? '' : ' disabled'}></label><label><span>Alt text</span><textarea name="mediaAlt" rows="3"${editable ? '' : ' disabled'}>${h(item.altText || '')}</textarea></label><div class="media-admin-usage ${Number(item.usageCount) ? 'used' : 'unused'}">${Number(item.usageCount) ? `Đang được tham chiếu ở ${item.usageCount} vị trí. Hệ thống sẽ chặn xóa file này.` : 'Chưa được sử dụng. Nếu xóa, file WebP vật lý và metadata MediaAsset sẽ được xóa.'}</div><div class="media-admin-detail-actions"><button type="button" class="btn btn-light btn-sm" data-media-copy>Sao chép URL</button>${editable ? '<button type="button" class="btn btn-light btn-sm" data-media-save>Lưu metadata</button><button type="button" class="btn btn-sm danger" data-media-delete>Xóa file</button>' : '<small>Media dùng chung chỉ Admin mới sửa/xóa được.</small>'}</div><small class="media-admin-file">${h(item.originalFileName || '')}</small>`;
    }

    function mutationApi(item) {
        if (config.isAdmin) return '/api/admin/site/global/media';
        return `/api/admin/properties/${config.propertyId}/media`;
    }

    function uploadApi() {
        const target = root.querySelector('[data-media-upload-scope]')?.value || '';
        if (config.isAdmin && target === 'global') return '/api/admin/site/global/media';
        const propertyId = config.isAdmin ? target : config.propertyId;
        return `/api/admin/properties/${propertyId}/media`;
    }

    async function upload(files) {
        const valid = [...files].filter(file => /^image\/(png|jpeg|webp)$/i.test(file.type));
        if (!valid.length) return toast('Chỉ hỗ trợ PNG, JPG hoặc WebP.', true);
        const button = root.querySelector('.media-upload-button');
        const old = button.childNodes[0]?.textContent || '＋ Chọn ảnh';
        try {
            button.classList.add('disabled');
            for (let i = 0; i < valid.length; i++) {
                if (button.childNodes[0]) button.childNodes[0].textContent = `Đang tải ${i + 1}/${valid.length}…`;
                const form = new FormData(); form.append('file', valid[i]);
                await DeLongApi.postForm(`${uploadApi()}/upload`, form);
            }
            await load();
            toast(valid.length > 1 ? `Đã xử lý ${valid.length} ảnh. File trùng sẽ được dùng lại.` : 'Đã thêm ảnh vào Media Library.');
        } catch (error) { toast(error.message || 'Không thể tải ảnh.', true); }
        finally { button.classList.remove('disabled'); if (button.childNodes[0]) button.childNodes[0].textContent = old; }
    }

    async function saveMetadata() {
        const item = selected(); if (!item) return;
        const detail = root.querySelector('[data-media-detail]');
        try {
            const saved = await DeLongApi.put(`${mutationApi(item)}/${item.id}`, { title: detail.querySelector('[name="mediaTitle"]')?.value.trim() || '', altText: detail.querySelector('[name="mediaAlt"]')?.value.trim() || '' });
            Object.assign(item, saved || {}); render(); toast('Đã lưu metadata media.');
        } catch (error) { toast(error.message || 'Không thể lưu metadata.', true); }
    }

    async function removeSelected() {
        const item = selected(); if (!item) return;
        if (Number(item.usageCount) > 0) return toast(`Media đang được dùng ở ${item.usageCount} vị trí nên chưa thể xóa.`, true);
        if (!confirm(`Xóa “${item.title || item.originalFileName}”?\n\nHệ thống sẽ xóa file vật lý khỏi storage và xóa metadata khỏi database. Thao tác không thể hoàn tác.`)) return;
        try {
            await DeLongApi.delete(`${mutationApi(item)}/${item.id}`);
            state.selectedId = ''; await load(); toast('Đã xóa file media khỏi storage.');
        } catch (error) { toast(error.message || 'Không thể xóa media.', true); }
    }

    async function cleanupUnused() {
        if (state.cleaning) return;
        const candidates = cleanupCandidates();
        if (!candidates.length) return toast('Không có media chưa dùng nào có thể dọn trong phạm vi đang chọn.');

        const bytes = candidates.reduce((sum, item) => sum + (Number(item.byteSize) || 0), 0);
        const scopeText = root.querySelector('[data-media-property-filter]')?.selectedOptions?.[0]?.textContent?.trim() || 'phạm vi hiện tại';
        if (!confirm(`Dọn ${candidates.length.toLocaleString('vi-VN')} media chưa dùng (${fmtBytes(bytes)})?\n\nPhạm vi: ${scopeText}.\nHệ thống sẽ xóa vĩnh viễn file vật lý khỏi storage và metadata tương ứng. Server sẽ kiểm tra usage lại trước từng lần xóa.`)) return;

        const button = root.querySelector('[data-media-show-unused]');
        state.cleaning = true;
        button.disabled = true;
        let removed = 0;
        let skipped = 0;
        let failed = 0;
        try {
            for (let index = 0; index < candidates.length; index++) {
                const item = candidates[index];
                button.textContent = `Đang dọn ${index + 1}/${candidates.length}…`;
                try {
                    await DeLongApi.delete(`${mutationApi(item)}/${item.id}`);
                    removed++;
                } catch (error) {
                    if (error?.status === 409) skipped++;
                    else failed++;
                }
            }
            state.selectedId = '';
            await load();
            if (failed > 0) toast(`Đã xóa ${removed} media; ${skipped} file được giữ lại vì vừa phát sinh usage; ${failed} file lỗi khi xóa.`, true);
            else if (skipped > 0) toast(`Đã xóa ${removed} media. ${skipped} file được giữ lại vì đang được sử dụng.`);
            else toast(`Đã dọn ${removed} media chưa dùng và giải phóng khoảng ${fmtBytes(bytes)}.`);
        } finally {
            state.cleaning = false;
            syncCleanupButton();
        }
    }

    async function copyUrl() {
        const item = selected(); if (!item) return;
        try { await navigator.clipboard.writeText(item.url); toast('Đã sao chép URL.'); }
        catch { toast('Không thể sao chép URL tự động.', true); }
    }

    propertyOptions();
    root.querySelector('[data-media-search]').addEventListener('input', event => { state.query = event.currentTarget.value.trim().toLowerCase(); render(); });
    root.querySelector('[data-media-property-filter]').addEventListener('change', event => { state.propertyFilter = event.currentTarget.value; render(); });
    root.querySelector('[data-media-unused]').addEventListener('change', event => { state.unusedOnly = event.currentTarget.checked; render(); });
    root.querySelector('[data-media-refresh]').addEventListener('click', load);
    root.querySelector('[data-media-show-unused]').addEventListener('click', cleanupUnused);
    root.querySelector('[data-media-upload]').addEventListener('change', event => { const files = event.currentTarget.files; event.currentTarget.value = ''; if (files?.length) upload(files); });
    root.querySelector('[data-media-grid]').addEventListener('click', event => { const card = event.target.closest('[data-media-id]'); if (!card) return; state.selectedId = card.dataset.mediaId || ''; render(); });
    root.querySelector('[data-media-detail]').addEventListener('click', event => {
        if (event.target.closest('[data-media-save]')) saveMetadata();
        if (event.target.closest('[data-media-delete]')) removeSelected();
        if (event.target.closest('[data-media-copy]')) copyUrl();
    });

    const dropzone = root.querySelector('[data-media-dropzone]');
    ['dragenter', 'dragover'].forEach(name => dropzone.addEventListener(name, event => { event.preventDefault(); dropzone.classList.add('is-dragover'); }));
    ['dragleave', 'drop'].forEach(name => dropzone.addEventListener(name, event => { event.preventDefault(); dropzone.classList.remove('is-dragover'); }));
    dropzone.addEventListener('drop', event => { if (event.dataTransfer?.files?.length) upload(event.dataTransfer.files); });

    load();
})();
