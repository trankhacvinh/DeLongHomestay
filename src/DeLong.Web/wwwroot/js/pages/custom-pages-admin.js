(function () {
    const root = document.querySelector('[data-custom-pages-admin]');
    if (!root || !window.DeLongApi) return;
    const data = JSON.parse(root.querySelector('[data-custom-pages-data]')?.textContent || '{}');
    const list = root.querySelector('[data-page-list]');
    const summary = root.querySelector('[data-page-summary]');
    const scope = root.querySelector('[data-page-scope]');
    const modal = root.querySelector('[data-page-modal]');
    const form = root.querySelector('[data-page-form]');
    const state = { pages: [], editing: null, loading: false };
    const h = value => String(value ?? '').replace(/[&<>"']/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[ch]));
    const fmtDate = value => value ? new Intl.DateTimeFormat('vi-VN', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)) : '—';

    function toast(message, error) {
        let node = document.querySelector('.custom-pages-toast');
        if (!node) { node = document.createElement('div'); node.className = 'custom-pages-toast'; document.body.appendChild(node); }
        node.className = `custom-pages-toast${error ? ' error' : ''}`; node.textContent = message; node.hidden = false;
        clearTimeout(node._timer); node._timer = setTimeout(() => { node.hidden = true; }, 3200);
    }

    function renderScope() {
        const propertyButtons = (data.properties || []).map(property => `<a class="${data.scope === 'property' && String(data.propertyId) === String(property.id) ? 'active' : ''}" href="/Admin/Site/Pages?propertyId=${encodeURIComponent(property.id)}"><strong>${h(property.name)}</strong><small>${h(property.code)}</small></a>`).join('');
        scope.innerHTML = `<div><span class="page-eyebrow">Phạm vi trang</span><strong>${h(data.propertyName || 'Website')}</strong></div><nav>${data.isAdmin ? `<a class="${data.scope === 'global' ? 'active' : ''}" href="/Admin/Site/Pages?scope=global"><strong>Trang chung</strong><small>delonghomestay</small></a>` : ''}${propertyButtons}</nav>`;
    }

    function render() {
        const published = state.pages.filter(page => page.isPublished).length;
        const drafts = state.pages.length - published;
        summary.innerHTML = `<article class="panel"><span>Tổng trang</span><strong>${state.pages.length}</strong></article><article class="panel"><span>Đã xuất bản</span><strong>${published}</strong></article><article class="panel"><span>Bản nháp</span><strong>${drafts}</strong></article>`;
        if (!data.listApi) {
            list.innerHTML = '<div class="panel custom-pages-empty"><strong>Chưa chọn cơ sở.</strong><p>Chọn một cơ sở ở thanh trên hoặc ở mục phạm vi để quản lý trang nội dung.</p></div>';
            return;
        }
        if (!state.pages.length) {
            list.innerHTML = '<div class="panel custom-pages-empty"><strong>Chưa có trang nội dung.</strong><p>Tạo trang Giới thiệu, Liên hệ hoặc landing page đầu tiên rồi mở Visual Editor để thiết kế.</p><button class="btn btn-primary" type="button" data-empty-create>＋ Tạo trang đầu tiên</button></div>';
            list.querySelector('[data-empty-create]')?.addEventListener('click', () => openCreate());
            return;
        }
        list.innerHTML = state.pages.map(page => `<article class="panel custom-page-card" data-page-id="${h(page.id)}">
            <div class="custom-page-status ${page.isPublished ? 'published' : 'draft'}">${page.isPublished ? 'Đã xuất bản' : 'Bản nháp'}</div>
            <div class="custom-page-card-copy"><div><h2>${h(page.title)}</h2><code>${h(page.url)}</code></div><p>${h(page.seoDescription || 'Chưa có meta description.')}</p><div class="custom-page-card-meta"><span>${page.sectionCount} khối</span><span>${page.hideFromNavigation ? 'Ẩn điều hướng' : 'Có thể chọn trong liên kết'}</span><span>Cập nhật ${h(fmtDate(page.updatedAtUtc))}</span></div></div>
            <div class="custom-page-card-actions"><a class="btn btn-primary btn-sm" href="${h(page.url)}?edit=1" target="_blank" rel="noopener">Thiết kế ↗</a><a class="btn btn-light btn-sm" href="${h(page.url)}" target="_blank" rel="noopener">Xem</a><button class="btn btn-light btn-sm" type="button" data-page-edit>Sửa SEO</button><button class="btn btn-light btn-sm" type="button" data-page-duplicate>Nhân bản</button><button class="btn btn-danger btn-sm" type="button" data-page-delete>Xóa</button></div>
        </article>`).join('');
    }

    async function load() {
        renderScope();
        if (!data.listApi) { state.pages = []; render(); return; }
        state.loading = true;
        try { const result = await DeLongApi.get(`${data.listApi}/`); state.pages = result?.pages || []; render(); }
        catch (error) { list.innerHTML = `<div class="panel custom-pages-empty"><strong>Không thể tải trang.</strong><p>${h(error.message || '')}</p></div>`; toast(error.message || 'Không thể tải danh sách trang.', true); }
        finally { state.loading = false; }
    }

    function openModal(page) {
        state.editing = page || null;
        form.reset();
        form.elements.title.value = page?.title || '';
        form.elements.slug.value = page?.slug || '';
        form.elements.published.checked = !!page?.isPublished;
        form.elements.hideNavigation.checked = !!page?.hideFromNavigation;
        form.elements.seoTitle.value = page?.seoTitle || '';
        form.elements.seoDescription.value = page?.seoDescription || '';
        form.elements.ogImageUrl.value = page?.ogImageUrl || '';
        root.querySelector('[data-template-field]').hidden = !!page;
        root.querySelector('[data-page-modal-eyebrow]').textContent = page ? 'CẤU HÌNH TRANG' : 'TRANG MỚI';
        root.querySelector('[data-page-modal-title]').textContent = page ? page.title : 'Tạo trang nội dung';
        root.querySelector('[data-page-save]').textContent = page ? 'Lưu cấu hình' : 'Tạo trang';
        modal.hidden = false; document.body.classList.add('custom-page-modal-open');
        setTimeout(() => form.elements.title.focus(), 20);
    }
    function openCreate() { if (!data.listApi) return toast('Hãy chọn cơ sở trước.', true); openModal(null); }
    function closeModal() { modal.hidden = true; state.editing = null; document.body.classList.remove('custom-page-modal-open'); }

    async function save(event) {
        event.preventDefault();
        const editing = state.editing;
        const button = root.querySelector('[data-page-save]'); button.disabled = true; button.textContent = 'Đang lưu…';
        const payload = {
            title: form.elements.title.value.trim(), slug: form.elements.slug.value.trim(),
            isPublished: form.elements.published.checked, hideFromNavigation: form.elements.hideNavigation.checked,
            seoTitle: form.elements.seoTitle.value.trim(), seoDescription: form.elements.seoDescription.value.trim(),
            ogImageUrl: form.elements.ogImageUrl.value.trim(), template: editing ? '' : form.elements.template.value
        };
        try {
            const saved = editing
                ? await DeLongApi.put(`${data.listApi}/${editing.id}`, payload)
                : await DeLongApi.post(`${data.listApi}/`, payload);
            closeModal(); toast(editing ? 'Đã lưu cấu hình trang.' : 'Đã tạo trang.'); await load();
            if (!editing && saved?.url && confirm('Trang đã được tạo. Mở Visual Editor để thiết kế ngay?')) window.open(`${saved.url}?edit=1`, '_blank', 'noopener,noreferrer');
        } catch (error) { toast(error.message || 'Không thể lưu trang.', true); }
        finally { button.disabled = false; button.textContent = editing ? 'Lưu cấu hình' : 'Tạo trang'; }
    }

    async function duplicate(page) {
        if (!confirm(`Nhân bản trang “${page.title}”? Bản sao sẽ được tạo ở trạng thái nháp.`)) return;
        try { await DeLongApi.post(`${data.listApi}/${page.id}/duplicate`, {}); toast('Đã nhân bản trang.'); await load(); }
        catch (error) { toast(error.message || 'Không thể nhân bản trang.', true); }
    }
    async function remove(page) {
        if (!confirm(`Xóa trang “${page.title}”? Toàn bộ khối nội dung của trang sẽ bị xóa và thao tác này không thể hoàn tác.`)) return;
        try { await DeLongApi.delete(`${data.listApi}/${page.id}`); toast('Đã xóa trang.'); await load(); }
        catch (error) { toast(error.message || 'Không thể xóa trang.', true); }
    }

    async function uploadOg(event) {
        const file = event.target.files?.[0]; event.target.value = '';
        if (!file || !data.siteApi) return;
        const label = event.target.closest('.file-btn');
        const original = label?.childNodes?.[0]?.textContent || 'Tải ảnh';
        if (label?.childNodes?.[0]) label.childNodes[0].textContent = 'Đang tải…';
        try { const body = new FormData(); body.append('file', file); const asset = await DeLongApi.postForm(`${data.siteApi}/assets/section`, body); form.elements.ogImageUrl.value = asset?.url || ''; toast('Đã tải ảnh. Lưu trang để áp dụng.'); }
        catch (error) { toast(error.message || 'Không thể tải ảnh.', true); }
        finally { if (label?.childNodes?.[0]) label.childNodes[0].textContent = original; }
    }

    root.querySelector('[data-page-create]').addEventListener('click', openCreate);
    root.querySelectorAll('[data-page-modal-close]').forEach(button => button.addEventListener('click', closeModal));
    modal.addEventListener('mousedown', event => { if (event.target === modal) closeModal(); });
    form.addEventListener('submit', save);
    root.querySelector('[data-page-og-upload]').addEventListener('change', uploadOg);
    list.addEventListener('click', event => {
        const card = event.target.closest('[data-page-id]'); if (!card) return;
        const page = state.pages.find(item => String(item.id) === String(card.dataset.pageId)); if (!page) return;
        if (event.target.closest('[data-page-edit]')) openModal(page);
        else if (event.target.closest('[data-page-duplicate]')) duplicate(page);
        else if (event.target.closest('[data-page-delete]')) remove(page);
    });
    document.addEventListener('keydown', event => { if (event.key === 'Escape' && !modal.hidden) closeModal(); });
    load();
})();
