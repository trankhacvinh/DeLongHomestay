(function () {
    if (!document.body?.classList.contains('public-body') || !window.DeLongApi) return;

    const pagePath = window.location.pathname.replace(/\/+$/, '') || '/';
    const scoped = pagePath.match(/^\/h\/([^/]+)(?:\/|$)/i);
    const siteSlug = scoped ? decodeURIComponent(scoped[1]) : '';
    const contextUrl = `/api/admin/site/visual-context${siteSlug ? `?siteSlug=${encodeURIComponent(siteSlug)}` : ''}`;
    const isRoomIndex = /^\/(?:h\/[^/]+\/)?rooms$/i.test(pagePath);
    const isBlogIndex = /^\/(?:h\/[^/]+\/)?blog$/i.test(pagePath);
    const blogDetail = pagePath.match(/^\/h\/([^/]+)\/blog\/([^/]+)$/i);

    function h(value) {
        return String(value ?? '').replace(/[&<>\'\"]/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[ch]));
    }

    function remember() {
        try { sessionStorage.setItem('delong:pve:contextual:return', `${location.pathname}${location.search}${location.hash}`); } catch { }
    }

    function toast(message, isError) {
        let node = document.querySelector('.pve-context-toast');
        if (!node) {
            node = document.createElement('div');
            node.className = 'pve-context-toast';
            document.body.appendChild(node);
        }
        node.className = `pve-context-toast ${isError ? 'error' : 'success'}`;
        node.textContent = message;
        node.hidden = false;
        clearTimeout(node._timer);
        node._timer = setTimeout(() => { node.hidden = true; }, 3400);
    }

    function openDrawer(title, eyebrow, body, footer, wide) {
        document.querySelector('.pve-context-drawer')?.remove();
        const drawer = document.createElement('section');
        drawer.className = `pve-context-drawer pve-content-card-drawer${wide ? ' pve-context-drawer-room' : ''}`;
        drawer.setAttribute('role', 'dialog');
        drawer.setAttribute('aria-modal', 'true');
        drawer.innerHTML = `<header><div><small>${h(eyebrow)}</small><h2>${h(title)}</h2></div><button type="button" data-content-close aria-label="Đóng">×</button></header><div class="pve-context-drawer-body">${body}</div>${footer || ''}`;
        document.body.appendChild(drawer);
        document.body.classList.add('pve-context-drawer-open');
        const close = () => { drawer.remove(); document.body.classList.remove('pve-context-drawer-open'); };
        drawer.querySelector('[data-content-close]').addEventListener('click', close);
        drawer._close = close;
        return drawer;
    }

    function addTarget(host, label, handler, className) {
        if (!host || host.querySelector(':scope > [data-content-card-target]')) return;
        host.classList.add('pve-context-host');
        const button = document.createElement('button');
        button.type = 'button';
        button.className = `pve-context-target pve-content-card-target ${className || ''}`.trim();
        button.dataset.contentCardTarget = '1';
        button.textContent = `✎ ${label}`;
        button.addEventListener('click', event => {
            event.preventDefault();
            event.stopPropagation();
            handler();
        });
        host.prepend(button);
    }

    class ContentCardsEditor {
        constructor(context) {
            this.context = context;
            this.editorialCache = new Map();
        }

        mount() {
            if (!this.context?.canEdit) return;
            if (isRoomIndex) this.mountRoomCards();
            if (isBlogIndex) this.mountBlogIndex();
            if (blogDetail) this.mountBlogDetail();
        }

        propertyForSlug(slug) {
            return (this.context.properties || []).find(x => String(x.siteSlug || '').toLowerCase() === String(slug || '').toLowerCase()) || null;
        }

        candidateForRoomCard(card) {
            const code = card.querySelector('.public-room-code-badge')?.textContent?.trim() || '';
            const href = card.querySelector('a[href*="/rooms/"]')?.getAttribute('href') || '';
            const propertySlug = href.match(/\/h\/([^/]+)\/rooms\//i)?.[1] || siteSlug;
            return (this.context.rooms || []).find(room =>
                String(room.code || '').toLowerCase() === code.toLowerCase() &&
                (!propertySlug || String(room.propertySiteSlug || '').toLowerCase() === decodeURIComponent(propertySlug).toLowerCase())) || null;
        }

        mountRoomCards() {
            document.querySelectorAll('.public-room-card-v2').forEach(card => {
                const candidate = this.candidateForRoomCard(card);
                if (!candidate) return;
                addTarget(card, 'Sửa card phòng', () => this.openRoomCard(candidate, card));
            });
            this.mountToolbarButton('cards', 'Card phòng', () => {
                const first = document.querySelector('.public-room-card-v2 [data-content-card-target]');
                if (first) first.click(); else toast('Trang chưa có phòng để chỉnh.', true);
            });
        }

        roomPayload(room, changes) {
            return Object.assign({
                code: room.code,
                name: room.name,
                capacity: room.capacity,
                slug: room.slug,
                shortDescription: room.shortDescription || '',
                descriptionHtml: room.descriptionHtml || '',
                isPublished: !!room.isPublished,
                amenities: [...(room.amenities || [])],
                tags: [...(room.tags || [])],
                highlights: [...(room.highlights || [])]
            }, changes || {});
        }

        async openRoomCard(candidate, card) {
            const api = `/api/admin/properties/${candidate.propertyId}/rooms/${candidate.id}/content`;
            try {
                let room = await DeLongApi.get(`${api}/`);
                const href = card.querySelector('a[href*="/rooms/"]')?.getAttribute('href') || '#';
                const body = `<div class="pve-card-editor">
                    <nav class="pve-room-tabs"><button type="button" class="active" data-card-tab="content">Nội dung card</button><button type="button" data-card-tab="cover">Ảnh bìa</button></nav>
                    <section data-card-panel="content">
                        <div class="pve-context-form">
                            <div class="pve-context-grid-2"><label><span>Tên phòng</span><input name="name" value="${h(room.name)}"></label><label><span>Mã phòng</span><input name="code" value="${h(room.code)}"></label></div>
                            <label><span>Mô tả ngắn trên card</span><textarea name="shortDescription" rows="4">${h(room.shortDescription || '')}</textarea></label>
                            <div class="pve-context-grid-2"><label><span>Sức chứa</span><input type="number" min="1" max="50" name="capacity" value="${h(room.capacity)}"></label><label><span>Slug</span><input name="slug" value="${h(room.slug)}"></label></div>
                            <label><span>Tags <small>(mỗi dòng một mục)</small></span><textarea name="tags" rows="5">${h((room.tags || []).join('\n'))}</textarea></label>
                            <label class="pve-context-check"><input type="checkbox" name="isPublished" ${room.isPublished ? 'checked' : ''}><span>Hiển thị phòng trên website</span></label>
                            <div class="pve-card-actions"><a href="${h(href)}" target="_blank" rel="noopener">Mở trang chi tiết ↗</a><button type="button" class="primary" data-card-save>Lưu card phòng</button></div>
                        </div>
                    </section>
                    <section data-card-panel="cover" hidden><div data-cover-root></div></section>
                </div>`;
                const drawer = openDrawer(`Card · ${room.name}`, candidate.propertyName || 'PHÒNG', body, '<footer><span>Card dùng dữ liệu phòng hiện có, không tạo bản nội dung riêng.</span><button type="button" data-card-cancel>Đóng</button></footer>', true);
                drawer.querySelector('[data-card-cancel]').addEventListener('click', () => drawer._close());
                drawer.querySelectorAll('[data-card-tab]').forEach(button => button.addEventListener('click', () => {
                    drawer.querySelectorAll('[data-card-tab]').forEach(x => x.classList.toggle('active', x === button));
                    drawer.querySelectorAll('[data-card-panel]').forEach(panel => { panel.hidden = panel.dataset.cardPanel !== button.dataset.cardTab; });
                    if (button.dataset.cardTab === 'cover') this.renderRoomCovers(drawer.querySelector('[data-cover-root]'), room, api, async next => { room = next; });
                }));
                drawer.querySelector('[data-card-save]').addEventListener('click', async () => {
                    const button = drawer.querySelector('[data-card-save]');
                    button.disabled = true; button.textContent = 'Đang lưu…';
                    const tags = drawer.querySelector('[name="tags"]').value.split(/\r?\n|,/).map(x => x.trim()).filter(Boolean);
                    try {
                        await DeLongApi.put(`${api}/`, this.roomPayload(room, {
                            name: drawer.querySelector('[name="name"]').value.trim(),
                            code: drawer.querySelector('[name="code"]').value.trim(),
                            shortDescription: drawer.querySelector('[name="shortDescription"]').value.trim(),
                            capacity: Number(drawer.querySelector('[name="capacity"]').value || 1),
                            slug: drawer.querySelector('[name="slug"]').value.trim(),
                            tags,
                            isPublished: drawer.querySelector('[name="isPublished"]').checked
                        }));
                        remember(); location.reload();
                    } catch (error) {
                        button.disabled = false; button.textContent = 'Lưu card phòng';
                        toast(error.message || 'Không thể lưu card phòng.', true);
                    }
                });
            } catch (error) { toast(error.message || 'Không thể tải nội dung phòng.', true); }
        }

        renderRoomCovers(root, room, api, setRoom) {
            const images = [...(room.images || [])].sort((a, b) => a.sortOrder - b.sortOrder);
            root.innerHTML = `<div class="pve-cover-toolbar"><label class="pve-context-upload"><span>＋ Tải ảnh bìa mới</span><input type="file" accept="image/png,image/jpeg,image/webp" data-cover-upload></label><small>Chọn một ảnh có sẵn hoặc tải ảnh mới.</small></div>
                <div class="pve-cover-grid">${images.map(image => `<button type="button" class="pve-cover-choice ${image.isCover ? 'is-cover' : ''}" data-cover-id="${h(image.id)}"><span class="pve-cover-thumb"><img src="${h(image.thumbnailUrl || image.cardUrl || image.largeUrl)}" alt="${h(image.altText || '')}">${image.isCover ? '<b>★ Đang dùng</b>' : '<b>Đặt làm bìa</b>'}</span><small>${h(image.altText || 'Chưa có alt text')}</small></button>`).join('') || '<div class="pve-room-image-empty">Phòng chưa có ảnh.</div>'}</div>`;
            root.querySelector('[data-cover-upload]').addEventListener('change', async event => {
                const file = event.target.files?.[0]; event.target.value = '';
                if (!file) return;
                try {
                    const form = new FormData(); form.append('file', file);
                    const created = await DeLongApi.postForm(`${api}/images`, form);
                    await DeLongApi.put(`${api}/images/${created.id}`, { altText: room.name, isCover: true, focalX: created.focalX, focalY: created.focalY });
                    room = await DeLongApi.get(`${api}/`); await setRoom(room); this.renderRoomCovers(root, room, api, setRoom); toast('Đã tải và đặt ảnh bìa mới.');
                } catch (error) { toast(error.message || 'Không thể tải ảnh bìa.', true); }
            });
            root.querySelectorAll('[data-cover-id]').forEach(button => button.addEventListener('click', async () => {
                const image = images.find(x => String(x.id) === button.dataset.coverId);
                if (!image || image.isCover) return;
                try {
                    await DeLongApi.put(`${api}/images/${image.id}`, { altText: image.altText || '', isCover: true, focalX: image.focalX, focalY: image.focalY });
                    room = await DeLongApi.get(`${api}/`); await setRoom(room); this.renderRoomCovers(root, room, api, setRoom); toast('Đã đổi ảnh bìa.');
                } catch (error) { toast(error.message || 'Không thể đổi ảnh bìa.', true); }
            }));
        }

        editorialApi(propertyId) { return `/api/admin/properties/${propertyId}/editorial`; }

        async loadEditorial(propertyId, refresh) {
            const key = String(propertyId);
            if (!refresh && this.editorialCache.has(key)) return this.editorialCache.get(key);
            const data = await DeLongApi.get(`${this.editorialApi(propertyId)}/`);
            this.editorialCache.set(key, data);
            return data;
        }

        mountBlogIndex() {
            document.querySelectorAll('.public-blog-card').forEach(card => {
                const href = card.querySelector('a[href*="/blog/"]')?.getAttribute('href') || '';
                const match = href.match(/\/h\/([^/]+)\/blog\/([^/?#]+)/i);
                if (!match) return;
                const property = this.propertyForSlug(decodeURIComponent(match[1]));
                if (!property) return;
                addTarget(card, 'Sửa bài', () => this.openBlogEditor(property.id, decodeURIComponent(match[2])));
            });
            const head = document.querySelector('.public-blog-page-head');
            if (head) addTarget(head, 'Bài viết mới', () => this.chooseBlogProperty());
            this.mountToolbarButton('blog-new', '＋ Bài viết', () => this.chooseBlogProperty());
        }

        mountBlogDetail() {
            const property = this.propertyForSlug(decodeURIComponent(blogDetail[1]));
            if (!property) return;
            const slug = decodeURIComponent(blogDetail[2]);
            addTarget(document.querySelector('.public-blog-article-header'), 'Sửa bài viết', () => this.openBlogEditor(property.id, slug));
            addTarget(document.querySelector('.public-blog-article-cover'), 'Đổi ảnh bìa', () => this.openBlogEditor(property.id, slug));
            addTarget(document.querySelector('.public-blog-body'), 'Sửa nội dung', () => this.openBlogEditor(property.id, slug));
            this.mountToolbarButton('blog-edit', 'Sửa bài', () => this.openBlogEditor(property.id, slug));
        }

        chooseBlogProperty() {
            const properties = this.context.properties || [];
            if (!properties.length) return toast('Không có cơ sở phù hợp để tạo bài.', true);
            if (properties.length === 1) return this.openBlogEditor(properties[0].id, null);
            const body = `<div class="pve-context-form"><p class="pve-context-note">Chọn cơ sở sở hữu bài viết mới.</p><div class="pve-blog-property-list">${properties.map(property => `<button type="button" data-blog-property="${h(property.id)}"><strong>${h(property.name || property.siteName)}</strong><small>/h/${h(property.siteSlug)}/blog</small></button>`).join('')}</div></div>`;
            const drawer = openDrawer('Tạo bài viết', 'CHỌN CƠ SỞ', body, '<footer><button type="button" data-blog-property-cancel>Hủy</button></footer>');
            drawer.querySelector('[data-blog-property-cancel]').addEventListener('click', () => drawer._close());
            drawer.querySelectorAll('[data-blog-property]').forEach(button => button.addEventListener('click', () => {
                const propertyId = button.dataset.blogProperty; drawer._close(); this.openBlogEditor(propertyId, null);
            }));
        }

        async openBlogEditor(propertyId, slug) {
            try {
                const editorial = await this.loadEditorial(propertyId, true);
                const post = slug ? (editorial.posts || []).find(x => String(x.slug || '').toLowerCase() === String(slug).toLowerCase()) : null;
                if (slug && !post) return toast('Không tìm thấy bài viết cần sửa.', true);
                const body = `<div class="pve-blog-context-editor">
                    <div class="pve-context-form">
                        <div class="pve-context-grid-2"><label><span>Tiêu đề</span><input name="title" value="${h(post?.title || '')}"></label><label><span>Slug</span><input name="slug" value="${h(post?.slug || '')}" placeholder="Tự tạo từ tiêu đề"></label></div>
                        <label><span>Tóm tắt</span><textarea name="excerpt" rows="4">${h(post?.excerpt || '')}</textarea></label>
                        <div class="pve-context-grid-2"><label><span>Ảnh bìa URL</span><input name="coverImageUrl" value="${h(post?.coverImageUrl || '')}"></label><label class="pve-context-upload"><span>Tải ảnh bìa</span><input type="file" accept="image/png,image/jpeg,image/webp" data-blog-cover-upload></label></div>
                        <label><span>Nội dung</span><textarea name="bodyHtml" rows="18">${h(post?.bodyHtml || '')}</textarea></label>
                        <label class="pve-context-check"><input type="checkbox" name="isPublished" ${post?.isPublished ? 'checked' : ''}><span>Xuất bản bài viết</span></label>
                    </div>
                </div>`;
                const footer = `<footer>${post ? '<button type="button" class="danger" data-blog-delete>Xóa bài</button>' : '<span>Bài mới sẽ thuộc cơ sở đã chọn.</span>'}<button type="button" data-blog-cancel>Hủy</button><button type="button" class="primary" data-blog-save>${post ? 'Lưu bài viết' : 'Tạo bài viết'}</button></footer>`;
                const drawer = openDrawer(post ? `Sửa · ${post.title}` : 'Bài viết mới', 'BLOG', body, footer, true);
                drawer.querySelector('[data-blog-cancel]').addEventListener('click', () => drawer._close());
                const textarea = drawer.querySelector('[name="bodyHtml"]');
                window.DeLongRichEditor?.enhance(textarea, { helpText: 'Soạn trực quan bằng Quill; HTML vẫn có sẵn cho developer.' });
                drawer.querySelector('[data-blog-cover-upload]').addEventListener('change', event => this.uploadBlogCover(propertyId, event, drawer));
                drawer.querySelector('[data-blog-save]').addEventListener('click', () => this.saveBlog(propertyId, post, drawer));
                drawer.querySelector('[data-blog-delete]')?.addEventListener('click', () => this.deleteBlog(propertyId, post, drawer));
            } catch (error) { toast(error.message || 'Không thể tải bài viết.', true); }
        }

        async uploadBlogCover(propertyId, event, drawer) {
            const file = event.target.files?.[0]; event.target.value = '';
            if (!file) return;
            try {
                const form = new FormData(); form.append('file', file);
                const asset = await DeLongApi.postForm(`/api/admin/properties/${propertyId}/site/assets/section`, form);
                drawer.querySelector('[name="coverImageUrl"]').value = asset.url || '';
                toast('Đã tải ảnh bìa. Bấm Lưu bài viết để áp dụng.');
            } catch (error) { toast(error.message || 'Không thể tải ảnh bìa.', true); }
        }

        blogPayload(drawer) {
            return {
                title: drawer.querySelector('[name="title"]').value.trim(),
                slug: drawer.querySelector('[name="slug"]').value.trim(),
                excerpt: drawer.querySelector('[name="excerpt"]').value.trim(),
                bodyHtml: drawer.querySelector('[name="bodyHtml"]').value,
                coverImageUrl: drawer.querySelector('[name="coverImageUrl"]').value.trim(),
                isPublished: drawer.querySelector('[name="isPublished"]').checked
            };
        }

        async saveBlog(propertyId, post, drawer) {
            const button = drawer.querySelector('[data-blog-save]'); button.disabled = true; button.textContent = 'Đang lưu…';
            try {
                const api = this.editorialApi(propertyId);
                if (post) await DeLongApi.put(`${api}/posts/${post.id}`, this.blogPayload(drawer));
                else await DeLongApi.post(`${api}/posts`, this.blogPayload(drawer));
                remember(); location.reload();
            } catch (error) {
                button.disabled = false; button.textContent = post ? 'Lưu bài viết' : 'Tạo bài viết';
                toast(error.message || 'Không thể lưu bài viết.', true);
            }
        }

        async deleteBlog(propertyId, post, drawer) {
            if (!post || !confirm(`Xóa bài “${post.title}”?`)) return;
            const button = drawer.querySelector('[data-blog-delete]'); button.disabled = true;
            try { await DeLongApi.delete(`${this.editorialApi(propertyId)}/posts/${post.id}`); remember(); location.href = `/h/${post.propertySiteSlug}/blog`; }
            catch (error) { button.disabled = false; toast(error.message || 'Không thể xóa bài viết.', true); }
        }

        mountToolbarButton(key, label, handler) {
            const tryMount = () => {
                const toolbar = document.querySelector('.pve-toolbar, .pve-context-toolbar');
                if (!toolbar) return false;
                const actions = toolbar.querySelector('.pve-toolbar-actions, .pve-context-actions') || toolbar;
                if (actions.querySelector(`[data-content-toolbar="${key}"]`)) return true;
                const button = document.createElement('button');
                button.type = 'button'; button.dataset.contentToolbar = key; button.textContent = label;
                button.addEventListener('click', handler);
                const admin = actions.querySelector('a[href*="/admin"]');
                if (admin) actions.insertBefore(button, admin); else actions.appendChild(button);
                return true;
            };
            if (tryMount()) return;
            const observer = new MutationObserver(() => { if (tryMount()) observer.disconnect(); });
            observer.observe(document.body, { childList: true, subtree: true });
            setTimeout(() => observer.disconnect(), 5000);
        }
    }

    DeLongApi.get(contextUrl).then(context => new ContentCardsEditor(context).mount()).catch(() => {
        // Guest / unauthorized role: no contextual controls.
    });
})();
