(function () {
    if (!document.body?.classList.contains('public-body') || !window.DeLongApi) return;

    const path = window.location.pathname.replace(/\/+$/, '') || '/';
    const scoped = path.match(/^\/h\/([^/]+)(?:\/|$)/i);
    const siteSlug = scoped ? decodeURIComponent(scoped[1]) : '';
    const roomMatch = path.match(/\/rooms\/([^/]+)$/i);
    const roomSlug = roomMatch ? decodeURIComponent(roomMatch[1]) : '';
    const contextUrl = `/api/admin/site/visual-context${siteSlug ? `?siteSlug=${encodeURIComponent(siteSlug)}` : ''}`;

    function h(value) {
        return String(value ?? '').replace(/[&<>\'\"]/g, char => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[char]));
    }
    function clone(value) { return JSON.parse(JSON.stringify(value ?? {})); }
    function csv(value) { return String(value || '').split(/\r?\n|,/).map(x => x.trim()).filter(Boolean); }
    function lines(value) { return Array.isArray(value) ? value.join('\n') : ''; }
    function sessionRemember() {
        try { sessionStorage.setItem('delong:pve:contextual:return', `${location.pathname}${location.search}${location.hash}`); } catch { }
    }

    class ContextualEditor {
        constructor(context) {
            this.context = context;
            this.propertyId = context.propertyId || null;
            this.toolbar = null;
            this.drawer = null;
            this.room = null;
            this.roomRef = null;
            this.roomApi = '';
            this.settings = null;
            this.mode = '';
        }

        async mount() {
            if (!this.context?.canEdit) return;
            if (roomSlug) await this.resolveRoom();
            this.mountToolbar();
            this.mountInlineTargets();
        }

        mountToolbar() {
            const existing = document.querySelector('.pve-toolbar');
            if (existing) {
                this.toolbar = existing;
                const actions = existing.querySelector('.pve-toolbar-actions') || existing;
                this.addAction(actions, 'header', 'Header', () => this.openShell('header'));
                this.addAction(actions, 'footer', 'Footer', () => this.openShell('footer'));
                if (this.room) this.addAction(actions, 'room', 'Chỉnh phòng', () => this.openRoom('content'));
                return;
            }

            const bar = document.createElement('div');
            bar.className = 'pve-context-toolbar';
            bar.innerHTML = `<div class="pve-context-brand"><span>DL</span><strong>Chỉnh website</strong><small>${h(this.room ? this.room.name : this.context.propertyName || 'Website')}</small></div>
                <div class="pve-context-actions">
                    <button type="button" data-context-header>Header</button>
                    <button type="button" data-context-footer>Footer</button>
                    ${this.room ? '<button type="button" class="primary" data-context-room>Chỉnh phòng</button>' : ''}
                    <a href="/admin">Quản trị</a>
                    <button type="button" data-context-close aria-label="Ẩn thanh chỉnh sửa">×</button>
                </div>`;
            document.body.prepend(bar);
            document.body.classList.add('pve-contextual-active');
            this.toolbar = bar;
            bar.querySelector('[data-context-header]').addEventListener('click', () => this.openShell('header'));
            bar.querySelector('[data-context-footer]').addEventListener('click', () => this.openShell('footer'));
            bar.querySelector('[data-context-room]')?.addEventListener('click', () => this.openRoom('content'));
            bar.querySelector('[data-context-close]').addEventListener('click', () => {
                bar.remove(); document.body.classList.remove('pve-contextual-active');
                document.querySelectorAll('.pve-context-target').forEach(x => x.remove());
            });
        }

        addAction(root, key, label, handler) {
            if (!root || root.querySelector(`[data-contextual-${key}]`)) return;
            const btn = document.createElement('button');
            btn.type = 'button';
            btn.setAttribute(`data-contextual-${key}`, '1'); btn.textContent = label;
            const admin = root.querySelector('a[href*="/admin"], [data-admin]');
            if (admin) root.insertBefore(btn, admin); else root.appendChild(btn);
            btn.addEventListener('click', handler);
        }

        mountInlineTargets() {
            this.addTarget(document.querySelector('.public-site-header'), 'Header', () => this.openShell('header'));
            this.addTarget(document.querySelector('.public-hospitality-footer'), 'Footer', () => this.openShell('footer'));
            if (!this.room) return;
            this.addTarget(document.querySelector('.public-room-intro-row'), 'Nội dung phòng', () => this.openRoom('content'));
            this.addTarget(document.querySelector('#room-gallery, .public-room-detail-art'), 'Quản lý ảnh', () => this.openRoom('images'));
            this.addTarget(document.querySelector('.public-room-highlight-block'), 'Điểm nổi bật', () => this.openRoom('highlights'));
            this.addTarget(document.querySelector('.public-room-story'), 'Mô tả', () => this.openRoom('story'));
            const amenity = [...document.querySelectorAll('.public-room-content-block')].find(x => x.querySelector('.public-amenity-grid'));
            this.addTarget(amenity, 'Tiện nghi', () => this.openRoom('amenities'));
        }

        addTarget(host, label, handler) {
            if (!host || host.querySelector(':scope > .pve-context-target')) return;
            host.classList.add('pve-context-host');
            const btn = document.createElement('button');
            btn.type = 'button'; btn.className = 'pve-context-target'; btn.textContent = `✎ ${label}`;
            btn.addEventListener('click', event => { event.preventDefault(); event.stopPropagation(); handler(); });
            host.prepend(btn);
        }

        async resolveRoom() {
            const candidates = Array.isArray(this.context.rooms) ? this.context.rooms : [];
            for (const candidate of candidates) {
                try {
                    const api = `/api/admin/properties/${candidate.propertyId}/rooms/${candidate.id}/content`;
                    const data = await DeLongApi.get(`${api}/`);
                    if (String(data?.slug || '').toLowerCase() !== roomSlug.toLowerCase()) continue;
                    this.room = data; this.roomRef = candidate; this.propertyId = candidate.propertyId; this.roomApi = api;
                    return;
                } catch (error) {
                    if (error?.status === 403) continue;
                }
            }
        }

        closeDrawer() {
            this.drawer?.remove(); this.drawer = null;
            document.body.classList.remove('pve-context-drawer-open');
        }

        openDrawer(title, eyebrow, body, footer) {
            this.closeDrawer();
            const drawer = document.createElement('section');
            drawer.className = 'pve-context-drawer';
            drawer.setAttribute('role', 'dialog'); drawer.setAttribute('aria-modal', 'true');
            drawer.innerHTML = `<header><div><small>${h(eyebrow)}</small><h2>${h(title)}</h2></div><button type="button" data-context-drawer-close aria-label="Đóng">×</button></header>
                <div class="pve-context-drawer-body">${body}</div>${footer || ''}`;
            document.body.appendChild(drawer); document.body.classList.add('pve-context-drawer-open');
            this.drawer = drawer;
            drawer.querySelector('[data-context-drawer-close]').addEventListener('click', () => this.closeDrawer());
            return drawer;
        }

        async openShell(kind) {
            if (this.context.scope === 'property' && this.propertyId) return this.openPropertyShell(kind);
            return this.openGlobalBranding(kind);
        }

        async openPropertyShell(kind) {
            try {
                const api = `/api/admin/properties/${this.propertyId}/site`;
                const data = await DeLongApi.get(`${api}/`);
                this.settings = clone(data.settings || {});
                const s = this.settings;
                const headerMode = kind === 'header';
                const body = headerMode ? `<div class="pve-context-form">
                        <label><span>Tên website</span><input name="siteName" value="${h(s.siteName)}"></label>
                        <label><span>Tagline</span><input name="tagline" value="${h(s.tagline)}"></label>
                        <div class="pve-context-grid-2"><label><span>Logo URL</span><input name="logoUrl" value="${h(s.logoUrl)}"></label><label class="pve-context-upload"><span>Tải logo</span><input type="file" accept="image/png,image/jpeg,image/webp,image/svg+xml" data-shell-upload="logo"></label></div>
                        <p class="pve-context-note">Header public lấy trực tiếp từ cấu hình cơ sở. Lưu ở đây sẽ cập nhật cả các trang thuộc cơ sở.</p>
                    </div>` : `<div class="pve-context-form">
                        <label><span>Địa chỉ</span><input name="address" value="${h(s.address)}"></label>
                        <div class="pve-context-grid-2"><label><span>Điện thoại</span><input name="phone" value="${h(s.phone)}"></label><label><span>Email</span><input name="email" value="${h(s.email)}"></label></div>
                        <label><span>Facebook</span><input name="facebookUrl" value="${h(s.facebookUrl)}"></label>
                        <label><span>Zalo</span><input name="zaloUrl" value="${h(s.zaloUrl)}"></label>
                        <label><span>Google Maps</span><input name="googleMapsUrl" value="${h(s.googleMapsUrl)}"></label>
                        <p class="pve-context-note">Footer tự lấy thương hiệu và thông tin liên hệ của cơ sở. Các liên kết hệ thống Đặt phòng / Tra cứu vẫn được giữ cố định để tránh làm hỏng luồng khách.</p>
                    </div>`;
                const footer = `<footer><button type="button" data-context-cancel>Hủy</button><button type="button" class="primary" data-shell-save>Lưu ${headerMode ? 'Header' : 'Footer'}</button></footer>`;
                const drawer = this.openDrawer(headerMode ? 'Chỉnh Header' : 'Chỉnh Footer', this.context.propertyName || 'CƠ SỞ', body, footer);
                drawer.querySelector('[data-context-cancel]').addEventListener('click', () => this.closeDrawer());
                drawer.querySelector('[data-shell-save]').addEventListener('click', () => this.savePropertyShell(api, headerMode));
                drawer.querySelector('[data-shell-upload]')?.addEventListener('change', event => this.uploadShellAsset(api, event));
            } catch (error) { this.toast(error.message || 'Không thể tải cấu hình website.', true); }
        }

        async uploadShellAsset(api, event) {
            const file = event.target.files?.[0]; event.target.value = '';
            if (!file) return;
            try {
                const form = new FormData(); form.append('file', file);
                const asset = await DeLongApi.postForm(`${api}/assets/logo`, form);
                const input = this.drawer?.querySelector('[name="logoUrl"]');
                if (input) input.value = asset.url || '';
                this.toast('Đã tải logo. Bấm Lưu Header để áp dụng.');
            } catch (error) { this.toast(error.message || 'Không thể tải logo.', true); }
        }

        async savePropertyShell(api, headerMode) {
            const button = this.drawer?.querySelector('[data-shell-save]');
            if (!button || !this.settings) return;
            button.disabled = true; button.textContent = 'Đang lưu…';
            const next = clone(this.settings);
            const value = name => this.drawer.querySelector(`[name="${name}"]`)?.value.trim() || '';
            if (headerMode) {
                next.siteName = value('siteName'); next.tagline = value('tagline'); next.logoUrl = value('logoUrl');
            } else {
                ['address','phone','email','facebookUrl','zaloUrl','googleMapsUrl'].forEach(key => next[key] = value(key));
            }
            try {
                await DeLongApi.put(`${api}/settings`, next); sessionRemember(); location.reload();
            } catch (error) {
                button.disabled = false; button.textContent = `Lưu ${headerMode ? 'Header' : 'Footer'}`;
                this.toast(error.message || 'Không thể lưu cấu hình.', true);
            }
        }

        async openGlobalBranding(kind) {
            try {
                const data = await DeLongApi.get('/api/admin/site/global/branding');
                const body = `<div class="pve-context-form">
                    <p class="pve-context-note">${kind === 'footer' ? 'Footer chung dùng cùng thương hiệu với Header. Các cột điều hướng hệ thống được giữ cố định.' : 'Để trống trường override sẽ tiếp tục kế thừa cấu hình của cơ sở duy nhất khi phù hợp.'}</p>
                    <label><span>Tên website chung</span><input name="siteName" value="${h(data.overrideSiteName)}" placeholder="${h(data.siteName)}"></label>
                    <label><span>Tagline</span><input name="tagline" value="${h(data.overrideTagline)}" placeholder="${h(data.tagline)}"></label>
                    <div class="pve-context-grid-2"><label><span>Logo URL</span><input name="logoUrl" value="${h(data.overrideLogoUrl)}"></label><label class="pve-context-upload"><span>Tải logo</span><input type="file" accept="image/png,image/jpeg,image/webp,image/svg+xml" data-global-logo></label></div>
                    <label><span>Meta title</span><input name="metaTitle" value="${h(data.overrideMetaTitle)}"></label>
                    <label><span>Meta description</span><textarea name="metaDescription" rows="4">${h(data.overrideMetaDescription)}</textarea></label>
                </div>`;
                const footer = `<footer><button type="button" data-context-cancel>Hủy</button><button type="button" class="primary" data-global-save>Lưu thương hiệu</button></footer>`;
                const drawer = this.openDrawer(kind === 'footer' ? 'Thương hiệu Footer chung' : 'Thương hiệu Header chung', 'TOÀN HỆ THỐNG', body, footer);
                drawer.querySelector('[data-context-cancel]').addEventListener('click', () => this.closeDrawer());
                drawer.querySelector('[data-global-save]').addEventListener('click', () => this.saveGlobalBranding(data));
                drawer.querySelector('[data-global-logo]').addEventListener('change', event => this.uploadGlobalLogo(event));
            } catch (error) { this.toast(error.message || 'Không thể tải thương hiệu chung.', true); }
        }

        async uploadGlobalLogo(event) {
            const file = event.target.files?.[0]; event.target.value = '';
            if (!file) return;
            try {
                const form = new FormData(); form.append('file', file);
                const asset = await DeLongApi.postForm('/api/admin/site/global/assets/logo', form);
                const input = this.drawer?.querySelector('[name="logoUrl"]');
                if (input) input.value = asset.url || '';
                this.toast('Đã tải logo. Bấm Lưu thương hiệu để áp dụng.');
            } catch (error) { this.toast(error.message || 'Không thể tải logo.', true); }
        }

        async saveGlobalBranding(original) {
            const button = this.drawer?.querySelector('[data-global-save]'); if (!button) return;
            button.disabled = true; button.textContent = 'Đang lưu…';
            const val = name => this.drawer.querySelector(`[name="${name}"]`)?.value.trim() || '';
            const payload = {
                siteName: val('siteName'), tagline: val('tagline'), logoUrl: val('logoUrl'),
                faviconUrl: original.overrideFaviconUrl || '', ogImageUrl: original.overrideOgImageUrl || '',
                metaTitle: val('metaTitle'), metaDescription: val('metaDescription')
            };
            try { await DeLongApi.put('/api/admin/site/global/branding', payload); sessionRemember(); location.reload(); }
            catch (error) { button.disabled = false; button.textContent = 'Lưu thương hiệu'; this.toast(error.message || 'Không thể lưu thương hiệu.', true); }
        }

        async refreshRoom() {
            if (!this.roomApi) return;
            this.room = await DeLongApi.get(`${this.roomApi}/`);
        }

        async openRoom(tab) {
            if (!this.room || !this.roomApi) return this.toast('Không xác định được phòng cần chỉnh.', true);
            this.mode = tab || 'content';
            const body = `<div class="pve-room-editor">
                <nav class="pve-room-tabs">
                    <button type="button" data-room-tab="content">Nội dung</button>
                    <button type="button" data-room-tab="story">Mô tả</button>
                    <button type="button" data-room-tab="highlights">Điểm nổi bật</button>
                    <button type="button" data-room-tab="amenities">Tiện nghi</button>
                    <button type="button" data-room-tab="images">Ảnh</button>
                </nav>
                <div data-room-tab-panel></div>
            </div>`;
            const footer = `<footer><span>Chỉnh đúng dữ liệu đang hiển thị trên trang phòng.</span><button type="button" data-context-cancel>Đóng</button></footer>`;
            const drawer = this.openDrawer(`Chỉnh ${this.room.name}`, 'NỘI DUNG PHÒNG', body, footer);
            drawer.classList.add('pve-context-drawer-room');
            drawer.querySelector('[data-context-cancel]').addEventListener('click', () => this.closeDrawer());
            drawer.querySelectorAll('[data-room-tab]').forEach(btn => btn.addEventListener('click', () => this.renderRoomTab(btn.dataset.roomTab)));
            this.renderRoomTab(this.mode);
        }

        renderRoomTab(tab) {
            if (!this.drawer || !this.room) return;
            this.mode = tab;
            this.drawer.querySelectorAll('[data-room-tab]').forEach(btn => btn.classList.toggle('active', btn.dataset.roomTab === tab));
            const root = this.drawer.querySelector('[data-room-tab-panel]');
            if (tab === 'images') return this.renderImages(root);
            if (tab === 'story') {
                root.innerHTML = `<div class="pve-context-form"><label><span>Mô tả chi tiết</span><textarea data-room-story rows="15">${h(this.room.descriptionHtml || '')}</textarea></label><div class="pve-room-savebar"><button type="button" class="primary" data-room-save>Lưu mô tả</button></div></div>`;
                const textarea = root.querySelector('[data-room-story]');
                window.DeLongRichEditor?.enhance(textarea, { helpText: 'Soạn trực quan bằng Quill hoặc chuyển sang HTML khi cần.' });
                root.querySelector('[data-room-save]').addEventListener('click', () => this.saveRoom({ descriptionHtml: textarea.value }));
                return;
            }
            if (tab === 'highlights' || tab === 'amenities') {
                const key = tab === 'highlights' ? 'highlights' : 'amenities';
                const label = tab === 'highlights' ? 'Điểm nổi bật' : 'Tiện nghi';
                root.innerHTML = `<div class="pve-context-form"><label><span>${label}</span><textarea data-room-list rows="12" placeholder="Mỗi dòng một mục">${h(lines(this.room[key]))}</textarea></label><p class="pve-context-note">Mỗi dòng là một mục. Bạn có thể dán danh sách nhiều dòng từ Excel/ghi chú.</p><div class="pve-room-savebar"><button type="button" class="primary" data-room-save>Lưu ${label.toLowerCase()}</button></div></div>`;
                root.querySelector('[data-room-save]').addEventListener('click', () => this.saveRoom({ [key]: csv(root.querySelector('[data-room-list]').value) }));
                return;
            }
            root.innerHTML = `<div class="pve-context-form">
                <div class="pve-context-grid-2"><label><span>Tên phòng</span><input name="name" value="${h(this.room.name)}"></label><label><span>Mã phòng</span><input name="code" value="${h(this.room.code)}"></label></div>
                <label><span>Mô tả ngắn</span><textarea name="shortDescription" rows="4">${h(this.room.shortDescription || '')}</textarea></label>
                <div class="pve-context-grid-2"><label><span>Sức chứa</span><input type="number" min="1" max="50" name="capacity" value="${h(this.room.capacity)}"></label><label><span>Slug</span><input name="slug" value="${h(this.room.slug)}"></label></div>
                <label class="pve-context-check"><input type="checkbox" name="isPublished" ${this.room.isPublished ? 'checked' : ''}><span>Đang xuất bản trên website</span></label>
                <p class="pve-context-note">Đổi slug sẽ thay đổi URL trang phòng. Mã phòng và slug vẫn được kiểm tra trùng ở server.</p>
                <div class="pve-room-savebar"><button type="button" class="primary" data-room-save>Lưu nội dung phòng</button></div>
            </div>`;
            root.querySelector('[data-room-save]').addEventListener('click', () => this.saveRoom({
                name: root.querySelector('[name="name"]').value.trim(), code: root.querySelector('[name="code"]').value.trim(),
                shortDescription: root.querySelector('[name="shortDescription"]').value.trim(),
                capacity: Number(root.querySelector('[name="capacity"]').value || 1), slug: root.querySelector('[name="slug"]').value.trim(),
                isPublished: root.querySelector('[name="isPublished"]').checked
            }));
        }

        roomPayload(changes) {
            return Object.assign({
                code: this.room.code, name: this.room.name, capacity: this.room.capacity, slug: this.room.slug,
                shortDescription: this.room.shortDescription || '', descriptionHtml: this.room.descriptionHtml || '',
                isPublished: !!this.room.isPublished, amenities: [...(this.room.amenities || [])], tags: [...(this.room.tags || [])], highlights: [...(this.room.highlights || [])]
            }, changes || {});
        }

        async saveRoom(changes) {
            const button = this.drawer?.querySelector('[data-room-save]');
            if (button) { button.disabled = true; button.textContent = 'Đang lưu…'; }
            try {
                this.room = await DeLongApi.put(`${this.roomApi}/`, this.roomPayload(changes));
                sessionRemember(); location.reload();
            } catch (error) {
                if (button) { button.disabled = false; button.textContent = 'Lưu lại'; }
                this.toast(error.message || 'Không thể lưu nội dung phòng.', true);
            }
        }

        renderImages(root) {
            const images = [...(this.room.images || [])].sort((a, b) => a.sortOrder - b.sortOrder);
            root.innerHTML = `<div class="pve-room-image-toolbar"><label class="pve-context-upload"><span>＋ Tải ảnh</span><input type="file" multiple accept="image/png,image/jpeg,image/webp" data-room-image-upload></label><small>${images.length} ảnh · ảnh bìa được đánh dấu ★</small></div>
                <div class="pve-room-image-grid">${images.map((image, index) => `<article data-room-image="${h(image.id)}">
                    <div class="pve-room-image-thumb"><img src="${h(image.thumbnailUrl || image.cardUrl || image.largeUrl)}" alt="${h(image.altText || '')}">${image.isCover ? '<span>★ Bìa</span>' : ''}</div>
                    <label><span>Alt text</span><input value="${h(image.altText || '')}" data-image-alt></label>
                    <div class="pve-room-image-actions">
                        <button type="button" data-image-up ${index === 0 ? 'disabled' : ''}>↑</button><button type="button" data-image-down ${index === images.length - 1 ? 'disabled' : ''}>↓</button>
                        <button type="button" data-image-cover ${image.isCover ? 'disabled' : ''}>Đặt bìa</button>
                        <button type="button" data-image-save-alt>Lưu alt</button><button type="button" class="danger" data-image-delete>Xóa</button>
                    </div>
                </article>`).join('') || '<div class="pve-room-image-empty">Phòng chưa có ảnh. Tải ảnh đầu tiên để làm ảnh bìa.</div>'}</div>`;
            root.querySelector('[data-room-image-upload]').addEventListener('change', event => this.uploadRoomImages(event));
            root.querySelectorAll('[data-room-image]').forEach((card, index) => {
                const image = images[index];
                card.querySelector('[data-image-cover]')?.addEventListener('click', () => this.updateRoomImage(image, { isCover: true }));
                card.querySelector('[data-image-save-alt]').addEventListener('click', () => this.updateRoomImage(image, { altText: card.querySelector('[data-image-alt]').value.trim(), isCover: image.isCover }));
                card.querySelector('[data-image-delete]').addEventListener('click', () => this.deleteRoomImage(image));
                card.querySelector('[data-image-up]')?.addEventListener('click', () => this.reorderRoomImages(images, index, index - 1));
                card.querySelector('[data-image-down]')?.addEventListener('click', () => this.reorderRoomImages(images, index, index + 1));
            });
        }

        async uploadRoomImages(event) {
            const files = [...(event.target.files || [])]; event.target.value = '';
            if (!files.length) return;
            try {
                for (const file of files) { const form = new FormData(); form.append('file', file); await DeLongApi.postForm(`${this.roomApi}/images`, form); }
                await this.refreshRoom(); this.renderRoomTab('images'); this.toast(`Đã tải ${files.length} ảnh.`);
            } catch (error) { this.toast(error.message || 'Không thể tải ảnh phòng.', true); }
        }

        async updateRoomImage(image, changes) {
            try {
                await DeLongApi.put(`${this.roomApi}/images/${image.id}`, { altText: changes.altText ?? image.altText ?? '', isCover: changes.isCover ?? image.isCover, focalX: image.focalX, focalY: image.focalY });
                await this.refreshRoom(); this.renderRoomTab('images'); this.toast('Đã cập nhật ảnh.');
            } catch (error) { this.toast(error.message || 'Không thể cập nhật ảnh.', true); }
        }

        async deleteRoomImage(image) {
            if (!confirm('Xóa ảnh này khỏi phòng?')) return;
            try { await DeLongApi.delete(`${this.roomApi}/images/${image.id}`); await this.refreshRoom(); this.renderRoomTab('images'); this.toast('Đã xóa ảnh.'); }
            catch (error) { this.toast(error.message || 'Không thể xóa ảnh.', true); }
        }

        async reorderRoomImages(images, from, to) {
            if (to < 0 || to >= images.length) return;
            const next = [...images]; [next[from], next[to]] = [next[to], next[from]];
            try { await DeLongApi.post(`${this.roomApi}/images/reorder`, { imageIds: next.map(x => x.id) }); await this.refreshRoom(); this.renderRoomTab('images'); }
            catch (error) { this.toast(error.message || 'Không thể đổi thứ tự ảnh.', true); }
        }

        toast(message, error) {
            let node = document.querySelector('.pve-context-toast');
            if (!node) { node = document.createElement('div'); node.className = 'pve-context-toast'; document.body.appendChild(node); }
            node.className = `pve-context-toast ${error ? 'error' : 'success'}`; node.textContent = message; node.hidden = false;
            clearTimeout(node._timer); node._timer = setTimeout(() => { node.hidden = true; }, 3200);
        }
    }

    DeLongApi.get(contextUrl).then(context => new ContextualEditor(context).mount()).catch(() => {
        // Guest / role without visual-edit permission: public website remains untouched.
    });
})();