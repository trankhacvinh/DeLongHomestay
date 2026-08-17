(function () {
    if (!document.body?.classList.contains('public-body') || !window.DeLongApi) return;

    let queued = false;
    let editorialPromise = null;

    function queue() {
        if (queued) return;
        queued = true;
        requestAnimationFrame(() => {
            queued = false;
            enhanceDrawers();
        });
    }

    function rich(textarea, helpText) {
        if (!textarea || !window.DeLongRichEditor) return;
        window.DeLongRichEditor.enhance(textarea, { helpText });
    }

    function loadEditorial() {
        if (!editorialPromise) editorialPromise = DeLongApi.get('/api/admin/site/global/editorial/').catch(() => null);
        return editorialPromise;
    }

    function escapeHtml(value) {
        return String(value ?? '').replace(/[&<>'"]/g, char => ({ '&':'&amp;','<':'&lt;','>':'&gt;',"'":'&#39;','"':'&quot;' }[char]));
    }

    async function enhanceGlobalEditorial(drawer) {
        const form = drawer.querySelector('[data-global-editorial]');
        if (!form || drawer.dataset.mediaEnhanced === '1') return;
        drawer.dataset.mediaEnhanced = '1';
        drawer.classList.add('pve-editorial-picker-drawer');

        const heading = drawer.querySelector('h2')?.textContent || '';
        const isGallery = /Gallery/i.test(heading);
        const data = await loadEditorial();
        if (!data || !drawer.isConnected) return;
        const items = isGallery ? (data.gallery || []) : (data.posts || []);
        const byId = new Map(items.map(item => [String(item.id), item]));
        const list = drawer.querySelector('[data-source-manual] .pve-editorial-choice-list');
        if (!list) return;
        list.classList.add('pve-media-grid');

        list.querySelectorAll('label.pve-choice').forEach(label => {
            const input = label.querySelector('input[name="editorial.itemIds"]');
            const item = byId.get(String(input?.value || ''));
            if (!input || !item) return;
            label.classList.add('pve-media-choice');
            const existingText = label.querySelector('span')?.textContent || '';
            label.querySelector('span')?.remove();
            const imageUrl = isGallery ? item.imageUrl : item.coverImageUrl;
            const title = isGallery ? (item.caption || item.altText || 'Ảnh Gallery') : (item.title || 'Bài viết');
            const property = item.propertyName || '';
            label.insertAdjacentHTML('beforeend', `
                <span class="pve-media-choice-thumb">${imageUrl ? `<img src="${escapeHtml(imageUrl)}" alt="">` : '<span>Không có ảnh</span>'}</span>
                <span class="pve-media-choice-copy"><strong>${escapeHtml(title)}</strong><small>${escapeHtml(property || existingText)}</small></span>`);
        });
    }

    function enhanceDrawers() {
        document.querySelectorAll('.pve-drawer').forEach(drawer => {
            if (drawer.querySelector('[data-blog-form]')) {
                drawer.classList.add('pve-blog-editor-drawer');
                rich(drawer.querySelector('textarea[name="bodyHtml"]'), 'Soạn bài ở chế độ Trực quan; chuyển sang HTML khi cần tinh chỉnh mã.');
            }

            drawer.querySelectorAll('textarea[name="content.html"], textarea[name="content.column1Html"], textarea[name="content.column2Html"], textarea[name="content.column3Html"], textarea[data-rich-row]').forEach(textarea => {
                rich(textarea, 'Trực quan phù hợp cho người dùng thường; HTML vẫn có sẵn cho developer.');
            });

            if (drawer.querySelector('[data-global-editorial]')) enhanceGlobalEditorial(drawer);
        });
    }

    const observer = new MutationObserver(queue);
    observer.observe(document.body, { childList: true, subtree: true });
    enhanceDrawers();
})();
