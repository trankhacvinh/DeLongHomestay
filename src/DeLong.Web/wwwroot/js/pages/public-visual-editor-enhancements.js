(function () {
    if (!document.body?.classList.contains('public-body') || !window.DeLongApi) return;

    let queued = false;
    let editorialPromise = null;
    let globalContextPromise = null;

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

    function loadGlobalContext() {
        if (!globalContextPromise) globalContextPromise = DeLongApi.get('/api/admin/site/visual-context').catch(() => null);
        return globalContextPromise;
    }

    function escapeHtml(value) {
        return String(value ?? '').replace(/[&<>'\"]/g, char => ({ '&':'&amp;','<':'&lt;','>':'&gt;',"'":'&#39;','\"':'&quot;' }[char]));
    }

    function filenameAlt(name) {
        return String(name || '')
            .replace(/\.[^.]+$/, '')
            .replace(/[-_]+/g, ' ')
            .replace(/\s+/g, ' ')
            .trim();
    }

    function mediaChoiceContent(item, isGallery) {
        const imageUrl = isGallery ? item.imageUrl : item.coverImageUrl;
        const title = isGallery ? (item.caption || item.altText || 'Ảnh Gallery') : (item.title || 'Bài viết');
        const property = item.propertyName || '';
        return `
            <span class="pve-media-choice-thumb">${imageUrl ? `<img src="${escapeHtml(imageUrl)}" alt="">` : '<span>Không có ảnh</span>'}</span>
            <span class="pve-media-choice-copy"><strong>${escapeHtml(title)}</strong><small>${escapeHtml(property)}</small></span>`;
    }

    function enhanceMediaChoice(label, item, isGallery) {
        const input = label.querySelector('input[name="editorial.itemIds"]');
        if (!input || !item) return;
        label.classList.add('pve-media-choice');
        label.querySelectorAll(':scope > span').forEach(span => span.remove());
        label.insertAdjacentHTML('beforeend', mediaChoiceContent(item, isGallery));
    }

    function appendGalleryChoice(list, item, checked) {
        if (!list || !item?.id) return;
        if (list.querySelector(`input[name="editorial.itemIds"][value="${CSS.escape(String(item.id))}"]`)) return;
        const label = document.createElement('label');
        label.className = 'pve-choice pve-media-choice';
        label.innerHTML = `<input type="checkbox" name="editorial.itemIds" value="${escapeHtml(item.id)}"${checked ? ' checked' : ''}>${mediaChoiceContent(item, true)}`;
        list.appendChild(label);
    }

    async function installGlobalGalleryUpload(drawer, form, list) {
        if (drawer.dataset.globalGalleryUpload === '1') return;
        drawer.dataset.globalGalleryUpload = '1';

        const source = drawer.querySelector('[data-editorial-source]');
        if (!source) return;
        const context = await loadGlobalContext();
        if (!drawer.isConnected) return;
        const properties = Array.isArray(context?.properties) ? context.properties : [];

        const block = document.createElement('section');
        block.className = 'pve-global-gallery-upload';
        block.innerHTML = `
            <div class="pve-global-gallery-upload-head">
                <div><strong>Thêm ảnh mới</strong><small>Ảnh sẽ được lưu vào Gallery của cơ sở đã chọn rồi có thể dùng ngay ở trang chung.</small></div>
                <span data-global-gallery-upload-status></span>
            </div>
            <div class="pve-global-gallery-upload-controls">
                <label><span>Lưu vào cơ sở</span><select data-global-gallery-property ${properties.length ? '' : 'disabled'}>
                    ${properties.map(property => `<option value="${escapeHtml(property.id)}">${escapeHtml(property.siteName || property.name || 'Cơ sở')}</option>`).join('')}
                </select></label>
                <label class="pve-global-gallery-upload-button ${properties.length ? '' : 'is-disabled'}">＋ Tải ảnh
                    <input type="file" accept="image/png,image/jpeg,image/webp" multiple data-global-gallery-files ${properties.length ? '' : 'disabled'} hidden>
                </label>
            </div>
            ${properties.length ? '' : '<small class="pve-global-gallery-upload-empty">Chưa có cơ sở hoạt động để lưu ảnh.</small>'}`;
        source.insertAdjacentElement('beforebegin', block);

        const fileInput = block.querySelector('[data-global-gallery-files]');
        const propertySelect = block.querySelector('[data-global-gallery-property]');
        const status = block.querySelector('[data-global-gallery-upload-status]');
        if (!fileInput || !propertySelect) return;

        fileInput.addEventListener('change', async event => {
            const files = [...(event.target.files || [])].filter(file => /^image\/(png|jpeg|webp)$/i.test(file.type));
            event.target.value = '';
            if (!files.length) return;
            const propertyId = propertySelect.value;
            if (!propertyId) return;

            fileInput.disabled = true;
            propertySelect.disabled = true;
            block.classList.add('is-uploading');
            let success = 0;
            try {
                for (let index = 0; index < files.length; index++) {
                    const file = files[index];
                    if (status) status.textContent = `Đang tải ${index + 1}/${files.length}`;
                    try {
                        const upload = new FormData();
                        upload.append('file', file);
                        const asset = await DeLongApi.postForm(`/api/admin/properties/${propertyId}/site/assets/section`, upload);
                        if (!asset?.url) throw new Error('Upload không trả về URL ảnh.');
                        const item = await DeLongApi.post(`/api/admin/properties/${propertyId}/editorial/gallery`, {
                            imageUrl: asset.url,
                            altText: filenameAlt(file.name) || 'Không gian homestay',
                            caption: '',
                            isPublished: true
                        });
                        appendGalleryChoice(list, item, true);
                        success++;
                    } catch (error) {
                        console.warn('Global gallery upload failed', error);
                    }
                }
                editorialPromise = null;
                if (status) status.textContent = success === files.length ? `Đã thêm ${success} ảnh` : `Đã thêm ${success}/${files.length} ảnh`;
                if (success && form.elements.mode?.value !== 'manual') {
                    const note = document.createElement('small');
                    note.className = 'pve-global-gallery-upload-note';
                    note.textContent = 'Ảnh mới đã được tạo. Nếu muốn chọn chính xác từng ảnh, chuyển Nguồn nội dung sang “Chọn thủ công”.';
                    block.querySelector('.pve-global-gallery-upload-note')?.remove();
                    block.appendChild(note);
                }
            } finally {
                fileInput.disabled = false;
                propertySelect.disabled = false;
                block.classList.remove('is-uploading');
                setTimeout(() => { if (status) status.textContent = ''; }, 3500);
            }
        });
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
            enhanceMediaChoice(label, item, isGallery);
        });

        if (isGallery) await installGlobalGalleryUpload(drawer, form, list);
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
