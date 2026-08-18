(function () {
    const root = document.querySelector('[data-seo-center]');
    if (!root) return;

    const search = root.querySelector('[data-seo-search]');
    const filters = [...root.querySelectorAll('[data-seo-filter]')];
    const items = [...root.querySelectorAll('[data-seo-item]')];
    const empty = root.querySelector('[data-seo-no-results]');
    const mediaApi = root.dataset.mediaApi || '';
    const mediaAdminUrl = root.dataset.mediaAdminUrl || '/Admin/Site/Media';
    let activeFilter = 'all';

    const mediaState = {
        modal: null,
        items: [],
        query: '',
        kind: 'all',
        selectedId: '',
        targetInput: null,
        loading: false
    };

    const h = value => String(value ?? '').replace(/[&<>"']/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[ch]));
    const isRoom = item => String(item?.kind || '').toLowerCase() === 'room';
    const imageUrl = item => isRoom(item) ? (item.largeUrl || item.url || '') : (item.url || '');

    function matchesFilter(item) {
        if (activeFilter === 'all') return true;
        if (activeFilter === 'issue') return item.dataset.issue === '1';
        return item.dataset.type === activeFilter;
    }

    function sync() {
        const query = (search?.value || '').trim().toLocaleLowerCase('vi');
        let visible = 0;
        items.forEach(item => {
            const haystack = item.dataset.search || '';
            const show = matchesFilter(item) && (!query || haystack.includes(query));
            item.hidden = !show;
            if (show) visible += 1;
        });
        if (empty) empty.hidden = visible > 0 || items.length === 0;
    }

    function syncMediaField(input) {
        const field = input?.closest('.seo-media-field');
        const preview = field?.querySelector('[data-seo-media-preview]');
        const clear = field?.querySelector('[data-seo-media-clear]');
        const value = (input?.value || '').trim();
        if (preview) {
            preview.hidden = !value;
            if (value) preview.src = value;
            else preview.removeAttribute('src');
        }
        if (clear) clear.hidden = !value;
    }

    function ensureMediaModal() {
        if (mediaState.modal?.isConnected) return mediaState.modal;
        const modal = document.createElement('div');
        modal.className = 'seo-media-modal';
        modal.dataset.seoMediaModal = '1';
        modal.hidden = true;
        modal.innerHTML = `
            <div class="seo-media-backdrop" data-seo-media-close></div>
            <section class="seo-media-dialog" role="dialog" aria-modal="true" aria-labelledby="seo-media-title">
                <header>
                    <div><small>MEDIA LIBRARY</small><h2 id="seo-media-title">Chọn ảnh</h2><p>Chọn ảnh đã tải lên. Ảnh phòng dùng bản Large đã tối ưu.</p></div>
                    <button type="button" class="seo-media-close" data-seo-media-close aria-label="Đóng">×</button>
                </header>
                <div class="seo-media-toolbar">
                    <label class="seo-media-search"><span>⌕</span><input type="search" data-seo-media-search placeholder="Tìm ảnh, phòng, cơ sở..." /></label>
                    <div class="seo-media-tabs" role="group" aria-label="Loại ảnh">
                        <button type="button" class="active" data-seo-media-kind="all">Tất cả</button>
                        <button type="button" data-seo-media-kind="section">Website</button>
                        <button type="button" data-seo-media-kind="room">Phòng</button>
                    </div>
                    <a class="btn btn-light btn-sm" href="${h(mediaAdminUrl)}" target="_blank" rel="noopener">Quản lý Media ↗</a>
                </div>
                <div class="seo-media-body">
                    <div class="seo-media-loading" data-seo-media-loading hidden>Đang tải ảnh...</div>
                    <div class="seo-media-empty" data-seo-media-empty hidden>Không có ảnh phù hợp.</div>
                    <div class="seo-media-grid" data-seo-media-grid></div>
                </div>
                <footer><span data-seo-media-count></span><div><button type="button" class="btn btn-light btn-sm" data-seo-media-close>Hủy</button><button type="button" class="btn btn-primary btn-sm" data-seo-media-use disabled>Dùng ảnh này</button></div></footer>
            </section>`;
        document.body.appendChild(modal);
        modal.querySelectorAll('[data-seo-media-close]').forEach(button => button.addEventListener('click', closeMedia));
        modal.querySelector('[data-seo-media-search]')?.addEventListener('input', event => {
            mediaState.query = event.currentTarget.value.trim().toLocaleLowerCase('vi');
            renderMediaGrid();
        });
        modal.querySelectorAll('[data-seo-media-kind]').forEach(button => button.addEventListener('click', () => {
            mediaState.kind = button.dataset.seoMediaKind || 'all';
            modal.querySelectorAll('[data-seo-media-kind]').forEach(item => item.classList.toggle('active', item === button));
            renderMediaGrid();
        }));
        modal.querySelector('[data-seo-media-grid]')?.addEventListener('click', event => {
            const card = event.target.closest('[data-seo-media-id]');
            if (!card) return;
            mediaState.selectedId = card.dataset.seoMediaId || '';
            renderMediaGrid();
        });
        modal.querySelector('[data-seo-media-grid]')?.addEventListener('dblclick', event => {
            if (!event.target.closest('[data-seo-media-id]')) return;
            useMedia();
        });
        modal.querySelector('[data-seo-media-use]')?.addEventListener('click', useMedia);
        modal.addEventListener('keydown', event => { if (event.key === 'Escape') closeMedia(); });
        mediaState.modal = modal;
        return modal;
    }

    function filteredMedia() {
        return mediaState.items.filter(item => {
            const kind = String(item.kind || 'section').toLowerCase();
            if (mediaState.kind !== 'all' && kind !== mediaState.kind) return false;
            if (!mediaState.query) return true;
            return [item.title, item.altText, item.originalFileName, item.propertyName, item.roomName, item.roomCode]
                .some(value => String(value || '').toLocaleLowerCase('vi').includes(mediaState.query));
        });
    }

    function renderMediaGrid() {
        const modal = ensureMediaModal();
        const grid = modal.querySelector('[data-seo-media-grid]');
        const list = filteredMedia();
        const empty = modal.querySelector('[data-seo-media-empty]');
        if (empty) empty.hidden = list.length > 0 || mediaState.loading;
        if (grid) {
            grid.innerHTML = list.map(item => {
                const room = isRoom(item);
                const src = item.thumbnailUrl || item.cardUrl || imageUrl(item);
                const scope = item.isGlobal ? 'Dùng chung' : (item.propertyName || 'Cơ sở');
                const title = item.title || item.roomName || item.originalFileName || 'Ảnh';
                return `<button type="button" class="seo-media-card${String(item.id) === mediaState.selectedId ? ' selected' : ''}" data-seo-media-id="${h(item.id)}"><span class="seo-media-thumb"><img src="${h(src)}" alt="${h(item.altText || '')}" loading="lazy"><b>${room ? 'Ảnh phòng' : 'Website'}</b></span><span><strong>${h(title)}</strong><small>${h(scope)}${room && item.roomName ? ` · ${h(item.roomName)}` : ''}</small></span></button>`;
            }).join('');
        }
        const selected = mediaState.items.find(item => String(item.id) === mediaState.selectedId);
        const use = modal.querySelector('[data-seo-media-use]');
        if (use) use.disabled = !selected;
        const count = modal.querySelector('[data-seo-media-count]');
        if (count) count.textContent = selected ? `Đã chọn: ${selected.title || selected.roomName || selected.originalFileName || 'Ảnh'}` : `${list.length} ảnh`;
    }

    async function openMedia(input) {
        if (!mediaApi || !window.DeLongApi || !input) return;
        const modal = ensureMediaModal();
        mediaState.targetInput = input;
        mediaState.selectedId = '';
        mediaState.query = '';
        mediaState.kind = 'all';
        const searchInput = modal.querySelector('[data-seo-media-search]');
        if (searchInput) searchInput.value = '';
        modal.querySelectorAll('[data-seo-media-kind]').forEach(button => button.classList.toggle('active', button.dataset.seoMediaKind === 'all'));
        modal.hidden = false;
        document.body.classList.add('seo-media-open');
        mediaState.loading = true;
        modal.querySelector('[data-seo-media-loading]').hidden = false;
        try {
            const data = await DeLongApi.get(`${mediaApi}/`);
            mediaState.items = Array.isArray(data?.items) ? data.items : [];
            const current = (input.value || '').split('?')[0];
            const same = mediaState.items.find(item => [item.url, item.largeUrl, item.cardUrl, item.thumbnailUrl]
                .some(url => String(url || '').split('?')[0] === current));
            if (same) mediaState.selectedId = String(same.id);
        } catch (error) {
            mediaState.items = [];
            const empty = modal.querySelector('[data-seo-media-empty]');
            if (empty) { empty.hidden = false; empty.textContent = error.message || 'Không thể tải Media Library.'; }
        } finally {
            mediaState.loading = false;
            modal.querySelector('[data-seo-media-loading]').hidden = true;
            renderMediaGrid();
            setTimeout(() => searchInput?.focus(), 0);
        }
    }

    function closeMedia() {
        if (mediaState.modal) mediaState.modal.hidden = true;
        document.body.classList.remove('seo-media-open');
        mediaState.targetInput = null;
    }

    function useMedia() {
        const item = mediaState.items.find(item => String(item.id) === mediaState.selectedId);
        if (!item || !mediaState.targetInput) return;
        mediaState.targetInput.value = imageUrl(item);
        mediaState.targetInput.dispatchEvent(new Event('change', { bubbles: true }));
        syncMediaField(mediaState.targetInput);
        closeMedia();
    }

    search?.addEventListener('input', sync);
    filters.forEach(button => button.addEventListener('click', () => {
        activeFilter = button.dataset.seoFilter || 'all';
        filters.forEach(item => item.classList.toggle('active', item === button));
        sync();
    }));

    root.querySelectorAll('[data-seo-media-input]').forEach(syncMediaField);
    root.addEventListener('click', event => {
        const pick = event.target.closest('[data-seo-media-pick]');
        if (pick) {
            const input = pick.closest('.seo-media-field')?.querySelector('[data-seo-media-input]');
            openMedia(input);
            return;
        }
        const clear = event.target.closest('[data-seo-media-clear]');
        if (clear) {
            const input = clear.closest('.seo-media-field')?.querySelector('[data-seo-media-input]');
            if (input) { input.value = ''; syncMediaField(input); }
        }
    });

    sync();
})();