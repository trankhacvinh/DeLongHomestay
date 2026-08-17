(function () {
    if (!document.body?.classList.contains('public-body') || !window.DeLongApi) return;

    const pagePath = window.location.pathname.replace(/\/+$/, '') || '/';
    const scoped = pagePath.match(/^\/h\/([^/]+)(?:\/|$)/i);
    const siteSlug = scoped ? decodeURIComponent(scoped[1]) : '';
    const contextUrl = `/api/admin/site/visual-context${siteSlug ? `?siteSlug=${encodeURIComponent(siteSlug)}` : ''}`;
    let contextPromise = null;

    function h(value) {
        return String(value ?? '').replace(/[&<>'"]/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[ch]));
    }

    function ensurePublicAnchors() {
        document.querySelectorAll('.public-editorial-gallery').forEach((gallery, index) => {
            if (!gallery.id) gallery.id = index === 0 ? 'gallery' : `gallery-${index + 1}`;
        });
    }

    function getContext() {
        if (!contextPromise) contextPromise = DeLongApi.get(contextUrl).catch(() => null);
        return contextPromise;
    }

    function toast(message, isError) {
        let node = document.querySelector('.pve-gallery-studio-toast');
        if (!node) {
            node = document.createElement('div');
            node.className = 'pve-gallery-studio-toast';
            document.body.appendChild(node);
        }
        node.className = `pve-gallery-studio-toast ${isError ? 'error' : 'success'}`;
        node.textContent = message;
        node.hidden = false;
        clearTimeout(node._timer);
        node._timer = setTimeout(() => { node.hidden = true; }, 3200);
    }

    function rows(drawer) { return [...drawer.querySelectorAll('[data-gallery-row]')]; }
    function rowData(row) {
        return {
            imageUrl: row.querySelector('[name="gallery.imageUrl"]')?.value.trim() || '',
            altText: row.querySelector('[name="gallery.altText"]')?.value.trim() || '',
            caption: row.querySelector('[name="gallery.caption"]')?.value.trim() || '',
            published: !!row.querySelector('[name="gallery.published"]')?.checked
        };
    }

    function updateCount(drawer) {
        const all = rows(drawer);
        const published = all.filter(row => rowData(row).published).length;
        const count = drawer.querySelector('[data-gallery-studio-count]');
        if (count) count.textContent = `${all.length} ảnh · ${published} đang hiển thị`;
    }

    function renderPreview(drawer) {
        const preview = drawer.querySelector('[data-gallery-studio-preview]');
        if (!preview) return;
        const layout = drawer.querySelector('[name="layout"]')?.value || 'mosaic';
        const data = rows(drawer).map(rowData).filter(item => item.imageUrl);
        preview.dataset.layout = layout;
        preview.innerHTML = data.length ? data.map(item => `<figure class="${item.published ? '' : 'is-hidden'}"><img src="${h(item.imageUrl)}" alt="${h(item.altText)}">${item.caption ? `<figcaption>${h(item.caption)}</figcaption>` : ''}${item.published ? '' : '<span>Đang ẩn</span>'}</figure>`).join('') : '<div class="pve-gallery-preview-empty">Chưa có ảnh để xem trước.</div>';
        updateCount(drawer);
    }

    function applyFilter(drawer, value) {
        drawer.dataset.galleryFilter = value;
        rows(drawer).forEach(row => {
            const published = rowData(row).published;
            row.hidden = value === 'published' ? !published : value === 'hidden' ? published : false;
        });
        drawer.querySelectorAll('[data-gallery-filter]').forEach(button => button.classList.toggle('active', button.dataset.galleryFilter === value));
    }

    function bindDrag(drawer, row) {
        if (row.dataset.galleryStudioDrag === '1') return;
        row.dataset.galleryStudioDrag = '1';
        row.draggable = true;
        row.addEventListener('dragstart', event => {
            row.classList.add('is-dragging');
            event.dataTransfer.effectAllowed = 'move';
            try { event.dataTransfer.setData('text/plain', row.dataset.id || 'gallery-row'); } catch { }
        });
        row.addEventListener('dragend', () => {
            row.classList.remove('is-dragging');
            drawer.querySelectorAll('.is-drag-over').forEach(x => x.classList.remove('is-drag-over'));
            renderPreview(drawer);
        });
        row.addEventListener('dragover', event => {
            event.preventDefault();
            if (row.classList.contains('is-dragging')) return;
            row.classList.add('is-drag-over');
            const dragging = drawer.querySelector('[data-gallery-row].is-dragging');
            if (!dragging) return;
            const rect = row.getBoundingClientRect();
            const before = event.clientY < rect.top + rect.height / 2;
            row.parentElement?.insertBefore(dragging, before ? row : row.nextSibling);
        });
        row.addEventListener('dragleave', () => row.classList.remove('is-drag-over'));
    }

    function bindRows(drawer) { rows(drawer).forEach(row => bindDrag(drawer, row)); }

    async function uploadFiles(drawer, files) {
        const context = await getContext();
        if (!context?.propertyId) return toast('Gallery trực tiếp cần mở trong phạm vi một cơ sở.', true);
        const addButton = drawer.querySelector('[data-gallery-add]');
        const list = drawer.querySelector('[data-gallery-list]');
        if (!addButton || !list) return;
        const uploadApi = `/api/admin/properties/${context.propertyId}/site/assets/section`;
        const progress = drawer.querySelector('[data-gallery-upload-progress]');
        const validFiles = [...files].filter(file => /^image\/(png|jpeg|webp)$/i.test(file.type));
        if (!validFiles.length) return toast('Chọn ảnh PNG, JPG hoặc WebP.', true);

        for (let index = 0; index < validFiles.length; index++) {
            const file = validFiles[index];
            if (progress) progress.textContent = `Đang tải ${index + 1}/${validFiles.length}: ${file.name}`;
            addButton.click();
            const row = list.querySelector('[data-gallery-row]:last-child');
            if (!row) continue;
            row.classList.add('is-uploading');
            try {
                const form = new FormData();
                form.append('file', file);
                const asset = await DeLongApi.postForm(uploadApi, form);
                const url = asset?.url || '';
                row.querySelector('[name="gallery.imageUrl"]').value = url;
                const alt = row.querySelector('[name="gallery.altText"]');
                if (alt && !alt.value) alt.value = file.name.replace(/\.[^.]+$/, '').replace(/[-_]+/g, ' ');
                const thumb = row.querySelector('.pve-gallery-thumb');
                if (thumb) thumb.innerHTML = `<img src="${h(url)}" alt="">`;
                row.classList.remove('is-uploading');
                bindDrag(drawer, row);
                renderPreview(drawer);
            } catch (error) {
                row.remove();
                toast(error.message || `Không thể tải ${file.name}.`, true);
            }
        }
        if (progress) progress.textContent = '';
        renderPreview(drawer);
    }

    function installStudio(drawer) {
        if (drawer.dataset.galleryStudio === '1') return;
        const form = drawer.querySelector('[data-gallery-form]');
        const list = drawer.querySelector('[data-gallery-list]');
        const addButton = drawer.querySelector('[data-gallery-add]');
        if (!form || !list || !addButton) return;
        drawer.dataset.galleryStudio = '1';
        drawer.classList.add('pve-gallery-studio-drawer');

        const toolbar = document.createElement('div');
        toolbar.className = 'pve-gallery-studio-toolbar';
        toolbar.innerHTML = `<div><strong>Thư viện ảnh</strong><small data-gallery-studio-count></small></div><div class="pve-gallery-filter-group"><button type="button" class="active" data-gallery-filter="all">Tất cả</button><button type="button" data-gallery-filter="published">Đang hiện</button><button type="button" data-gallery-filter="hidden">Đang ẩn</button></div><label class="pve-gallery-bulk-upload">＋ Tải nhiều ảnh<input type="file" accept="image/png,image/jpeg,image/webp" multiple data-gallery-bulk hidden></label>`;

        const layout = document.createElement('div');
        layout.className = 'pve-gallery-studio-layout';
        const main = document.createElement('div');
        main.className = 'pve-gallery-studio-main';
        const preview = document.createElement('aside');
        preview.className = 'pve-gallery-studio-preview-wrap';
        preview.innerHTML = '<div class="pve-gallery-preview-head"><div><small>XEM TRƯỚC</small><strong>Gallery public</strong></div><span>Kéo ảnh để đổi thứ tự</span></div><div class="pve-gallery-studio-preview" data-gallery-studio-preview></div>';
        list.before(layout);
        layout.append(main, preview);
        main.append(toolbar, list, addButton);
        const progress = document.createElement('small');
        progress.className = 'pve-gallery-upload-progress';
        progress.dataset.galleryUploadProgress = '1';
        main.appendChild(progress);

        toolbar.querySelectorAll('[data-gallery-filter]').forEach(button => button.addEventListener('click', () => applyFilter(drawer, button.dataset.galleryFilter)));
        toolbar.querySelector('[data-gallery-bulk]').addEventListener('change', event => {
            const files = event.target.files;
            event.target.value = '';
            if (files?.length) uploadFiles(drawer, files);
        });
        drawer.addEventListener('input', () => requestAnimationFrame(() => renderPreview(drawer)));
        drawer.addEventListener('change', () => requestAnimationFrame(() => { applyFilter(drawer, drawer.dataset.galleryFilter || 'all'); renderPreview(drawer); }));
        drawer.addEventListener('click', event => {
            if (event.target.closest('[data-gallery-up], [data-gallery-down], [data-gallery-remove], [data-gallery-add]')) requestAnimationFrame(() => { bindRows(drawer); applyFilter(drawer, drawer.dataset.galleryFilter || 'all'); renderPreview(drawer); });
        });

        const listObserver = new MutationObserver(() => requestAnimationFrame(() => { bindRows(drawer); applyFilter(drawer, drawer.dataset.galleryFilter || 'all'); renderPreview(drawer); }));
        listObserver.observe(list, { childList: true });
        bindRows(drawer);
        applyFilter(drawer, 'all');
        renderPreview(drawer);
    }

    function enhance() {
        ensurePublicAnchors();
        document.querySelectorAll('.pve-drawer').forEach(installStudio);
    }

    let queued = false;
    function queue() {
        if (queued) return;
        queued = true;
        requestAnimationFrame(() => { queued = false; enhance(); });
    }

    const observer = new MutationObserver(queue);
    observer.observe(document.body, { childList: true, subtree: true });
    enhance();
})();