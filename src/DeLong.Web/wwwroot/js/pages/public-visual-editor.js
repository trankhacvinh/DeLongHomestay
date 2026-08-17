(function () {
    if (!document.body.classList.contains('public-body') || !window.DeLongApi) return;

    const path = window.location.pathname.replace(/\/+$/, '') || '/';
    const scopedMatch = path.match(/^\/h\/([^/]+)(?:\/|$)/i);
    const siteSlug = scopedMatch ? decodeURIComponent(scopedMatch[1]) : '';
    const isHome = path === '/' || (scopedMatch && path === `/h/${scopedMatch[1]}`);
    const contextUrl = `/api/admin/site/visual-context${siteSlug ? `?siteSlug=${encodeURIComponent(siteSlug)}` : ''}`;

    const typeDefinitions = {
        global: [
            ['Hero', 'Hero / mở đầu'],
            ['BranchGrid', 'Danh sách cơ sở'],
            ['RoomGrid', 'Danh sách phòng'],
            ['AvailabilitySearch', 'Kiểm tra phòng nhanh'],
            ['FeatureGrid', 'Nội dung + điểm nổi bật'],
            ['RichText', 'Nội dung tự do'],
            ['Cta', 'Kêu gọi hành động']
        ],
        property: [
            ['Hero', 'Hero / mở đầu'],
            ['AvailabilitySearch', 'Kiểm tra phòng nhanh'],
            ['RoomGrid', 'Danh sách phòng'],
            ['FeatureGrid', 'Nội dung + điểm nổi bật'],
            ['Faq', 'Câu hỏi thường gặp'],
            ['Location', 'Vị trí & chỉ đường'],
            ['PolicyGrid', 'Quy định lưu trú'],
            ['RichText', 'Nội dung tự do'],
            ['Cta', 'Kêu gọi hành động']
        ]
    };

    const variants = {
        Hero: ['split', 'centered', 'image-first', 'image-full', 'booking-overlay', 'editorial'],
        BranchGrid: ['grid-3', 'grid-2', 'editorial'],
        RoomGrid: ['grid-3', 'grid-2', 'featured-first', 'editorial-cards', 'horizontal-scroll'],
        AvailabilitySearch: ['booking-bar', 'card', 'minimal'],
        FeatureGrid: ['split', 'stacked', 'icon-grid', 'dark-band', 'editorial'],
        Faq: ['accordion', 'two-column'],
        Location: ['split', 'card'],
        PolicyGrid: ['grid-3', 'list'],
        RichText: ['narrow', 'wide', 'editorial'],
        Cta: ['card', 'full-width', 'dark', 'offer']
    };

    function defaultContent(type, scope) {
        if (type === 'Hero') return scope === 'global'
            ? { eyebrow: 'DE LONG HOMESTAY', title: '', body: '', primaryText: 'Xem tất cả phòng', primaryUrl: '/rooms', secondaryText: 'Khám phá các cơ sở', secondaryUrl: '/#co-so', imageUrl: '' }
            : { eyebrow: '', title: '', body: '', primaryText: 'Đặt phòng', primaryUrl: '/booking', secondaryText: 'Xem phòng', secondaryUrl: '/rooms', imageUrl: '' };
        if (type === 'BranchGrid') return { eyebrow: 'CƠ SỞ', title: 'Chọn nơi bạn muốn ghé', propertyIds: [] };
        if (type === 'RoomGrid') return scope === 'global'
            ? { eyebrow: 'PHÒNG', title: 'Một vài lựa chọn đang mở', mode: 'all', limit: 6, propertyQuotas: {}, roomIds: [] }
            : { eyebrow: 'KHÔNG GIAN', title: 'Chọn căn phòng hợp với nhịp của bạn', limit: 6 };
        if (type === 'AvailabilitySearch') return { title: scope === 'global' ? 'Chọn cơ sở và ngày bạn muốn ghé' : 'Chọn ngày bạn muốn ghé' };
        if (type === 'FeatureGrid') return { eyebrow: '', title: '', body: '', items: [], imageUrl: '' };
        if (type === 'Faq') return { eyebrow: 'THÔNG TIN', title: 'Câu hỏi thường gặp', items: [{ question: '', answer: '' }] };
        if (type === 'Location') return { eyebrow: 'VỊ TRÍ', title: 'Tìm đường đến cơ sở', body: '', address: '', mapUrl: '', embedUrl: '', nearby: [] };
        if (type === 'PolicyGrid') return { eyebrow: 'LƯU TRÚ', title: 'Quy định lưu trú', items: [{ title: '', body: '' }] };
        if (type === 'Cta') return { title: '', body: '', buttonText: scope === 'global' ? 'Xem phòng' : 'Đặt phòng', buttonUrl: scope === 'global' ? '/rooms' : '/booking' };
        return { html: '<p>Nội dung mới</p>' };
    }

    function parseJson(value) {
        try { return JSON.parse(value || '{}'); } catch { return {}; }
    }

    function escapeHtml(value) {
        return String(value ?? '').replace(/[&<>'"]/g, char => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[char]));
    }

    function field(label, name, value, options) {
        const config = options || {};
        const help = config.help ? `<small>${escapeHtml(config.help)}</small>` : '';
        if (config.type === 'textarea') {
            return `<label class="pve-field"><span>${escapeHtml(label)}</span><textarea name="${escapeHtml(name)}" rows="${config.rows || 3}">${escapeHtml(value)}</textarea>${help}</label>`;
        }
        if (config.type === 'select') {
            const choices = (config.choices || []).map(choice => `<option value="${escapeHtml(choice)}"${String(choice) === String(value) ? ' selected' : ''}>${escapeHtml(choice)}</option>`).join('');
            return `<label class="pve-field"><span>${escapeHtml(label)}</span><select name="${escapeHtml(name)}">${choices}</select>${help}</label>`;
        }
        if (config.type === 'number') {
            return `<label class="pve-field"><span>${escapeHtml(label)}</span><input name="${escapeHtml(name)}" type="number" min="${config.min ?? 1}" max="${config.max ?? 24}" value="${escapeHtml(value)}" />${help}</label>`;
        }
        return `<label class="pve-field"><span>${escapeHtml(label)}</span><input name="${escapeHtml(name)}" type="text" value="${escapeHtml(value)}" />${help}</label>`;
    }

    function itemRows(type, items) {
        const list = Array.isArray(items) && items.length ? items : type === 'Faq' ? [{ question: '', answer: '' }] : [{ title: '', body: '' }];
        return list.map((item, index) => {
            if (type === 'Faq') {
                return `<div class="pve-repeat-row" data-repeat-row><div class="pve-repeat-head"><strong>Câu hỏi ${index + 1}</strong><button type="button" data-repeat-remove>×</button></div>${field('Câu hỏi', `repeat.${index}.question`, item.question || '')}${field('Trả lời', `repeat.${index}.answer`, item.answer || '', { type: 'textarea', rows: 3 })}</div>`;
            }
            return `<div class="pve-repeat-row" data-repeat-row><div class="pve-repeat-head"><strong>Mục ${index + 1}</strong><button type="button" data-repeat-remove>×</button></div>${field('Tiêu đề', `repeat.${index}.title`, item.title || '')}${field('Nội dung', `repeat.${index}.body`, item.body || '', { type: 'textarea', rows: 3 })}</div>`;
        }).join('');
    }

    function contentFields(type, content, scope) {
        if (type === 'Hero') return [
            field('Eyebrow', 'content.eyebrow', content.eyebrow || ''),
            field('Tiêu đề', 'content.title', content.title || '', { type: 'textarea', rows: 2 }),
            field('Mô tả', 'content.body', content.body || '', { type: 'textarea', rows: 3 }),
            `<div class="pve-grid-2">${field('CTA chính', 'content.primaryText', content.primaryText || '')}${field('URL CTA chính', 'content.primaryUrl', content.primaryUrl || '')}${field('CTA phụ', 'content.secondaryText', content.secondaryText || '')}${field('URL CTA phụ', 'content.secondaryUrl', content.secondaryUrl || '')}</div>`,
            imageField('Ảnh Hero', content.imageUrl || '')
        ].join('');
        if (type === 'BranchGrid') return [
            field('Eyebrow', 'content.eyebrow', content.eyebrow || ''),
            field('Tiêu đề', 'content.title', content.title || ''),
            `<div class="pve-notice">Danh sách cơ sở chọn thủ công là thiết lập nâng cao. Visual editor giữ nguyên lựa chọn hiện tại; dùng “Mở cấu hình đầy đủ” nếu cần đổi nguồn cơ sở.</div>`
        ].join('');
        if (type === 'RoomGrid') return [
            field('Eyebrow', 'content.eyebrow', content.eyebrow || ''),
            field('Tiêu đề', 'content.title', content.title || ''),
            scope === 'global' ? field('Cách lấy phòng', 'content.mode', content.mode || 'all', { type: 'select', choices: ['all', 'byProperty', 'manual'], help: 'all = tự lấy; byProperty/manual có thiết lập chi tiết trong cấu hình đầy đủ.' }) : '',
            field('Số phòng tối đa', 'content.limit', content.limit || 6, { type: 'number', min: 1, max: 24 })
        ].join('');
        if (type === 'AvailabilitySearch') return field('Tiêu đề', 'content.title', content.title || '');
        if (type === 'FeatureGrid') return [
            field('Eyebrow', 'content.eyebrow', content.eyebrow || ''),
            field('Tiêu đề', 'content.title', content.title || '', { type: 'textarea', rows: 2 }),
            field('Mô tả', 'content.body', content.body || '', { type: 'textarea', rows: 3 }),
            field('Các điểm nổi bật — mỗi dòng một mục', 'content.itemsText', Array.isArray(content.items) ? content.items.join('\n') : '', { type: 'textarea', rows: 5 }),
            imageField('Ảnh kể chuyện', content.imageUrl || '')
        ].join('');
        if (type === 'Faq' || type === 'PolicyGrid') return `<div class="pve-grid-2">${field('Eyebrow', 'content.eyebrow', content.eyebrow || '')}${field('Tiêu đề', 'content.title', content.title || '')}</div><div class="pve-repeat" data-repeat data-repeat-type="${type}">${itemRows(type, content.items)}<button class="pve-add-repeat" type="button" data-repeat-add>+ Thêm ${type === 'Faq' ? 'câu hỏi' : 'mục'}</button></div>`;
        if (type === 'Location') return [
            `<div class="pve-grid-2">${field('Eyebrow', 'content.eyebrow', content.eyebrow || '')}${field('Tiêu đề', 'content.title', content.title || '')}</div>`,
            field('Mô tả', 'content.body', content.body || '', { type: 'textarea', rows: 3 }),
            field('Địa chỉ', 'content.address', content.address || ''),
            field('Google Maps URL', 'content.mapUrl', content.mapUrl || ''),
            field('Maps embed URL', 'content.embedUrl', content.embedUrl || ''),
            field('Địa điểm gần — mỗi dòng một mục', 'content.nearbyText', Array.isArray(content.nearby) ? content.nearby.join('\n') : '', { type: 'textarea', rows: 4 })
        ].join('');
        if (type === 'RichText') return field('Nội dung HTML', 'content.html', content.html || '', { type: 'textarea', rows: 12, help: 'HTML vẫn được kiểm tra/sanitize ở server theo luồng CMS hiện tại.' });
        if (type === 'Cta') return [
            field('Tiêu đề', 'content.title', content.title || ''),
            field('Mô tả', 'content.body', content.body || '', { type: 'textarea', rows: 3 }),
            `<div class="pve-grid-2">${field('Nút', 'content.buttonText', content.buttonText || '')}${field('URL', 'content.buttonUrl', content.buttonUrl || '')}</div>`
        ].join('');
        return '';
    }

    function imageField(label, value) {
        return `<div class="pve-field"><span>${escapeHtml(label)}</span><div class="pve-image-row"><input name="content.imageUrl" type="text" value="${escapeHtml(value)}" /><label class="pve-upload">Tải ảnh<input type="file" accept="image/png,image/jpeg,image/webp" data-image-upload hidden /></label></div><small>Có thể dán URL hoặc tải ảnh mới.</small></div>`;
    }

    function brandImageField(label, fieldName, value, kind) {
        return `<div class="pve-field"><span>${escapeHtml(label)}</span><div class="pve-image-row"><input name="${fieldName}" type="text" value="${escapeHtml(value)}" /><label class="pve-upload">Tải ảnh<input type="file" accept="image/png,image/jpeg,image/webp" data-brand-upload="${kind}" data-field="${fieldName}" hidden /></label></div></div>`;
    }

    class VisualEditor {
        constructor(context) {
            this.context = context;
            this.scope = context.scope;
            this.propertyId = context.propertyId || '';
            this.sections = [];
            this.editing = false;
            this.dragged = null;
            this.drawer = null;
            this.pendingInsertAfter = null;
            this.baseApi = this.scope === 'global' ? '/api/admin/site/global' : `/api/admin/properties/${this.propertyId}/site`;
            this.adminUrl = this.scope === 'global' ? '/Admin/Site/Global' : `/Admin/Site?propertyId=${encodeURIComponent(this.propertyId)}`;
        }

        mount() {
            this.injectCss();
            this.renderToolbar();
        }

        injectCss() {
            if (document.querySelector('link[data-public-visual-editor]')) return;
            const link = document.createElement('link');
            link.rel = 'stylesheet';
            link.href = '/css/public-visual-editor.css?v=20260817-1';
            link.dataset.publicVisualEditor = 'true';
            document.head.appendChild(link);
        }

        renderToolbar() {
            document.body.classList.add('pve-authorized');
            const bar = document.createElement('aside');
            bar.className = 'pve-toolbar';
            bar.setAttribute('aria-label', 'Công cụ chỉnh sửa website');
            const label = this.scope === 'global' ? 'Trang chủ chung' : (this.context.propertyName || 'Trang cơ sở');
            bar.innerHTML = `<div class="pve-toolbar-brand"><span>DL</span><strong>Chỉnh website</strong><small>${escapeHtml(label)}</small></div><div class="pve-toolbar-actions">${isHome ? '<button type="button" class="pve-primary" data-pve-toggle>✦ Chỉnh sửa trang</button><button type="button" data-pve-add hidden>＋ Thêm khối</button>' : ''}<button type="button" data-pve-brand>Thương hiệu</button><a href="${this.adminUrl}">Mở cấu hình đầy đủ</a><a href="/Admin">Quản trị</a><button type="button" data-pve-hide aria-label="Ẩn thanh chỉnh sửa">×</button></div>`;
            document.body.prepend(bar);
            bar.querySelector('[data-pve-toggle]')?.addEventListener('click', () => this.toggle());
            bar.querySelector('[data-pve-add]')?.addEventListener('click', () => this.openCreate(null));
            bar.querySelector('[data-pve-brand]')?.addEventListener('click', () => this.openBranding());
            bar.querySelector('[data-pve-hide]')?.addEventListener('click', () => {
                if (this.editing) this.exitEditing();
                document.body.classList.toggle('pve-toolbar-hidden');
            });
        }

        async toggle() {
            if (this.editing) {
                this.exitEditing();
                return;
            }
            await this.enterEditing();
        }

        async enterEditing() {
            const toggle = document.querySelector('[data-pve-toggle]');
            if (toggle) { toggle.disabled = true; toggle.textContent = 'Đang tải…'; }
            try {
                const data = await DeLongApi.get(`${this.baseApi}/`);
                this.sections = (data?.sections || data?.site?.sections || []).slice().sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0));
                this.editing = true;
                document.body.classList.add('pve-editing');
                if (toggle) { toggle.disabled = false; toggle.textContent = '✓ Kết thúc chỉnh sửa'; }
                const add = document.querySelector('[data-pve-add]');
                if (add) add.hidden = false;
                this.decorateSections();
                this.toast('Đã bật chế độ chỉnh sửa. Kéo khối để sắp xếp hoặc bấm Sửa.');
            } catch (error) {
                if (toggle) { toggle.disabled = false; toggle.textContent = '✦ Chỉnh sửa trang'; }
                this.toast(error.message || 'Không thể mở chế độ chỉnh sửa.', 'error');
            }
        }

        exitEditing() {
            this.editing = false;
            document.body.classList.remove('pve-editing');
            document.querySelectorAll('[data-pve-controls], [data-pve-insert]').forEach(node => node.remove());
            document.querySelectorAll('.pve-editable-section').forEach(section => {
                section.classList.remove('pve-editable-section', 'pve-dragging', 'pve-drop-target');
                section.removeAttribute('data-pve-section-id');
                section.removeAttribute('data-pve-section-type');
            });
            const toggle = document.querySelector('[data-pve-toggle]');
            if (toggle) toggle.textContent = '✦ Chỉnh sửa trang';
            const add = document.querySelector('[data-pve-add]');
            if (add) add.hidden = true;
            this.closeDrawer();
        }

        cmsElements() {
            const selector = [
                '.public-cms-hero', '.public-branch-section', '.public-cms-room-grid', '.public-cms-availability',
                '.public-cms-feature', '.public-story-faq', '.public-story-location', '.public-story-policies',
                '.public-cms-rich', '.public-cms-cta'
            ].join(',');
            return [...document.querySelectorAll(`#main-content > ${selector.split(',').map(x => x.trim()).join(', #main-content > ')}`)];
        }

        decorateSections() {
            document.querySelectorAll('[data-pve-controls], [data-pve-insert]').forEach(node => node.remove());
            const visible = this.sections.filter(section => section.isVisible !== false);
            const elements = this.cmsElements();
            elements.forEach((element, index) => {
                const section = visible[index];
                if (!section) return;
                element.classList.add('pve-editable-section');
                element.dataset.pveSectionId = section.id;
                element.dataset.pveSectionType = section.type;
                const controls = document.createElement('div');
                controls.className = 'pve-section-controls';
                controls.dataset.pveControls = 'true';
                controls.innerHTML = `<button class="pve-drag" type="button" draggable="true" title="Kéo để sắp xếp">⋮⋮</button><span>${escapeHtml(this.typeLabel(section.type))}</span><button type="button" data-action="edit">Sửa</button><button type="button" data-action="duplicate">Nhân bản</button><button type="button" data-action="hide">Ẩn</button><button type="button" class="danger" data-action="delete">Xóa</button>`;
                element.appendChild(controls);
                const insert = document.createElement('button');
                insert.type = 'button';
                insert.className = 'pve-insert';
                insert.dataset.pveInsert = 'true';
                insert.textContent = '+ Thêm khối tại đây';
                element.appendChild(insert);

                controls.addEventListener('click', event => {
                    const button = event.target.closest('[data-action]');
                    if (!button) return;
                    event.preventDefault(); event.stopPropagation();
                    const action = button.dataset.action;
                    if (action === 'edit') this.openEdit(section);
                    if (action === 'duplicate') this.duplicate(section);
                    if (action === 'hide') this.hide(section);
                    if (action === 'delete') this.remove(section);
                });
                insert.addEventListener('click', event => { event.preventDefault(); event.stopPropagation(); this.openCreate(section.id); });
                const dragHandle = controls.querySelector('.pve-drag');
                dragHandle.addEventListener('dragstart', event => this.onDragStart(event, element));
                dragHandle.addEventListener('dragend', () => this.onDragEnd());
                element.addEventListener('dragover', event => this.onDragOver(event, element));
                element.addEventListener('dragleave', () => element.classList.remove('pve-drop-target'));
                element.addEventListener('drop', event => this.onDrop(event, element));
            });
        }

        typeLabel(type) {
            return (typeDefinitions[this.scope] || []).find(item => item[0] === type)?.[1] || type;
        }

        onDragStart(event, sectionElement) {
            this.dragged = sectionElement;
            sectionElement.classList.add('pve-dragging');
            event.dataTransfer.effectAllowed = 'move';
            event.dataTransfer.setData('text/plain', sectionElement.dataset.pveSectionId || '');
        }

        onDragOver(event, target) {
            if (!this.dragged || target === this.dragged) return;
            event.preventDefault();
            target.classList.add('pve-drop-target');
            event.dataTransfer.dropEffect = 'move';
        }

        async onDrop(event, target) {
            if (!this.dragged || target === this.dragged) return;
            event.preventDefault();
            target.classList.remove('pve-drop-target');
            const box = target.getBoundingClientRect();
            const before = event.clientY < box.top + box.height / 2;
            if (before) target.before(this.dragged); else target.after(this.dragged);
            await this.saveDomOrder();
        }

        onDragEnd() {
            document.querySelectorAll('.pve-drop-target').forEach(node => node.classList.remove('pve-drop-target'));
            this.dragged?.classList.remove('pve-dragging');
            this.dragged = null;
        }

        async saveDomOrder() {
            const visibleIds = this.cmsElements().map(element => element.dataset.pveSectionId).filter(Boolean);
            if (!visibleIds.length) return;
            let visibleIndex = 0;
            const ids = this.sections.map(section => section.isVisible === false ? section.id : visibleIds[visibleIndex++]);
            try {
                await DeLongApi.put(`${this.baseApi}/sections/reorder`, { ids });
                const byId = new Map(this.sections.map(section => [String(section.id), section]));
                this.sections = ids.map(id => byId.get(String(id))).filter(Boolean);
                this.toast('Đã cập nhật thứ tự khối.');
            } catch (error) {
                this.toast(error.message || 'Không thể đổi thứ tự.', 'error');
                window.location.reload();
            }
        }

        async openBranding() {
            try {
                const data = await DeLongApi.get(this.scope === 'global' ? '/api/admin/site/global/branding' : `${this.baseApi}/`);
                const settings = this.scope === 'global' ? (data || {}) : (data?.settings || data?.site?.settings || {});
                this.closeDrawer();
                const drawer = document.createElement('section');
                drawer.className = 'pve-drawer';
                drawer.setAttribute('role', 'dialog');
                drawer.setAttribute('aria-modal', 'true');
                drawer.innerHTML = `<header><div><small>THƯƠNG HIỆU</small><h2>${this.scope === 'global' ? 'Nhận diện website chung' : escapeHtml(this.context.propertyName || 'Website cơ sở')}</h2></div><button type="button" data-close aria-label="Đóng">×</button></header><form data-brand-form><div class="pve-drawer-body">${field('Tên thương hiệu', 'siteName', this.scope === 'global' ? (settings.overrideSiteName || '') : (settings.siteName || ''), { help: this.scope === 'global' ? 'Để trống để kế thừa khi chỉ có một cơ sở.' : '' })}${field('Tagline', 'tagline', this.scope === 'global' ? (settings.overrideTagline || '') : (settings.tagline || ''))}${brandImageField('Logo', 'logoUrl', this.scope === 'global' ? (settings.overrideLogoUrl || '') : (settings.logoUrl || ''), 'logo')}${brandImageField('Favicon', 'faviconUrl', this.scope === 'global' ? (settings.overrideFaviconUrl || '') : (settings.faviconUrl || ''), 'favicon')}${brandImageField('Ảnh Open Graph', 'ogImageUrl', this.scope === 'global' ? (settings.overrideOgImageUrl || '') : (settings.ogImageUrl || ''), 'og')}${field('Meta title', 'metaTitle', this.scope === 'global' ? (settings.overrideMetaTitle || '') : (settings.metaTitle || ''))}${field('Meta description', 'metaDescription', this.scope === 'global' ? (settings.overrideMetaDescription || '') : (settings.metaDescription || ''), { type: 'textarea', rows: 3 })}</div><footer><span></span><div><button type="button" data-close>Hủy</button><button type="submit" class="pve-primary">Lưu thương hiệu</button></div></footer></form>`;
                document.body.appendChild(drawer);
                this.drawer = drawer;
                drawer.querySelectorAll('[data-close]').forEach(button => button.addEventListener('click', () => this.closeDrawer()));
                drawer.querySelectorAll('[data-brand-upload]').forEach(input => input.addEventListener('change', event => this.uploadBrandImage(event)));
                drawer.querySelector('[data-brand-form]').addEventListener('submit', async event => {
                    event.preventDefault();
                    const form = event.currentTarget;
                    const submit = form.querySelector('button[type="submit"]');
                    submit.disabled = true; submit.textContent = 'Đang lưu…';
                    try {
                        if (this.scope === 'global') {
                            await DeLongApi.put('/api/admin/site/global/branding', {
                                siteName: form.elements.siteName.value.trim(), tagline: form.elements.tagline.value.trim(),
                                logoUrl: form.elements.logoUrl.value.trim(), faviconUrl: form.elements.faviconUrl.value.trim(), ogImageUrl: form.elements.ogImageUrl.value.trim(),
                                metaTitle: form.elements.metaTitle.value.trim(), metaDescription: form.elements.metaDescription.value.trim()
                            });
                        } else {
                            const payload = Object.assign({}, settings, {
                                siteName: form.elements.siteName.value.trim(), tagline: form.elements.tagline.value.trim(),
                                logoUrl: form.elements.logoUrl.value.trim(), faviconUrl: form.elements.faviconUrl.value.trim(), ogImageUrl: form.elements.ogImageUrl.value.trim(),
                                metaTitle: form.elements.metaTitle.value.trim(), metaDescription: form.elements.metaDescription.value.trim()
                            });
                            await DeLongApi.put(`${this.baseApi}/settings`, payload);
                        }
                        this.toast('Đã lưu thương hiệu. Đang cập nhật giao diện…');
                        window.location.reload();
                    } catch (error) {
                        submit.disabled = false; submit.textContent = 'Lưu thương hiệu';
                        this.toast(error.message || 'Không thể lưu thương hiệu.', 'error');
                    }
                });
            } catch (error) { this.toast(error.message || 'Không thể tải cấu hình thương hiệu.', 'error'); }
        }

        async uploadBrandImage(event) {
            const file = event.target.files?.[0]; event.target.value = '';
            if (!file || !this.drawer) return;
            const kind = event.target.dataset.brandUpload;
            const fieldName = event.target.dataset.field;
            const label = event.target.closest('.pve-upload');
            const textNode = label.childNodes[0];
            const previous = textNode?.textContent || 'Tải ảnh';
            if (textNode) textNode.textContent = 'Đang tải…';
            try {
                const form = new FormData(); form.append('file', file);
                const asset = await DeLongApi.postForm(`${this.baseApi}/assets/${kind}`, form);
                const target = this.drawer.querySelector(`[name="${fieldName}"]`);
                if (target) target.value = asset.url || '';
                this.toast('Đã tải ảnh. Bấm Lưu thương hiệu để áp dụng.');
            } catch (error) { this.toast(error.message || 'Không thể tải ảnh.', 'error'); }
            finally { if (textNode) textNode.textContent = previous; }
        }

        openCreate(insertAfterId) {
            const firstType = (typeDefinitions[this.scope] || [])[0]?.[0] || 'Hero';
            this.pendingInsertAfter = insertAfterId;
            this.openDrawer({ mode: 'create', id: null, type: firstType, name: '', variant: (variants[firstType] || ['card'])[0], isVisible: true, content: defaultContent(firstType, this.scope) });
        }

        openEdit(section) {
            this.pendingInsertAfter = null;
            this.openDrawer({ mode: 'edit', id: section.id, type: section.type, name: section.name || '', variant: section.variant || (variants[section.type] || ['card'])[0], isVisible: section.isVisible !== false, content: Object.assign(defaultContent(section.type, this.scope), parseJson(section.contentJson)) });
        }

        openDrawer(model) {
            this.closeDrawer();
            const drawer = document.createElement('section');
            drawer.className = 'pve-drawer';
            drawer.setAttribute('role', 'dialog');
            drawer.setAttribute('aria-modal', 'true');
            drawer.dataset.mode = model.mode;
            drawer.dataset.sectionId = model.id || '';
            drawer.innerHTML = `<header><div><small>${model.mode === 'create' ? 'THÊM KHỐI' : 'CHỈNH KHỐI'}</small><h2>${model.mode === 'create' ? 'Thêm nội dung mới' : escapeHtml(model.name || this.typeLabel(model.type))}</h2></div><button type="button" data-close aria-label="Đóng">×</button></header><form data-pve-form><div class="pve-drawer-body"><div class="pve-grid-2">${field('Loại khối', 'type', model.type, { type: 'select', choices: (typeDefinitions[this.scope] || []).map(x => x[0]) })}${field('Tên quản trị', 'name', model.name || '')}${field('Layout', 'variant', model.variant, { type: 'select', choices: variants[model.type] || ['card'] })}<label class="pve-check"><input type="checkbox" name="isVisible"${model.isVisible ? ' checked' : ''} /><span>Hiển thị khối</span></label></div><div data-content-fields>${contentFields(model.type, model.content, this.scope)}</div></div><footer>${model.mode === 'edit' ? '<button type="button" class="pve-danger-outline" data-delete>Xóa khối</button>' : '<span></span>'}<div><button type="button" data-close>Hủy</button><button type="submit" class="pve-primary">Lưu thay đổi</button></div></footer></form>`;
            document.body.appendChild(drawer);
            this.drawer = drawer;

            const form = drawer.querySelector('[data-pve-form]');
            const typeSelect = form.elements.type;
            const variantSelect = form.elements.variant;
            const contentRoot = drawer.querySelector('[data-content-fields]');
            typeSelect.addEventListener('change', () => {
                const type = typeSelect.value;
                variantSelect.innerHTML = (variants[type] || ['card']).map(value => `<option value="${value}">${escapeHtml(value)}</option>`).join('');
                contentRoot.innerHTML = contentFields(type, defaultContent(type, this.scope), this.scope);
                this.bindDrawerDynamic();
            });
            form.addEventListener('submit', event => { event.preventDefault(); this.saveDrawer(model.mode, model.id); });
            drawer.querySelectorAll('[data-close]').forEach(button => button.addEventListener('click', () => this.closeDrawer()));
            drawer.querySelector('[data-delete]')?.addEventListener('click', () => {
                const section = this.sections.find(item => String(item.id) === String(model.id));
                if (section) this.remove(section);
            });
            this.bindDrawerDynamic();
            drawer.querySelector('input[name="name"]')?.focus();
        }

        bindDrawerDynamic() {
            if (!this.drawer) return;
            this.drawer.querySelectorAll('[data-image-upload]').forEach(input => {
                input.addEventListener('change', event => this.uploadImage(event));
            });
            this.drawer.querySelector('[data-repeat-add]')?.addEventListener('click', () => {
                const repeat = this.drawer.querySelector('[data-repeat]');
                const type = repeat?.dataset.repeatType;
                if (!repeat || !type) return;
                const rows = [...repeat.querySelectorAll('[data-repeat-row]')];
                const index = rows.length;
                const placeholder = document.createElement('div');
                placeholder.innerHTML = itemRows(type, type === 'Faq' ? [{ question: '', answer: '' }] : [{ title: '', body: '' }]);
                const row = placeholder.firstElementChild;
                row.querySelectorAll('[name]').forEach(input => input.name = input.name.replace('repeat.0.', `repeat.${index}.`));
                repeat.querySelector('[data-repeat-add]').before(row);
                this.bindRepeatRemove(row);
            });
            this.drawer.querySelectorAll('[data-repeat-row]').forEach(row => this.bindRepeatRemove(row));
        }

        bindRepeatRemove(row) {
            const button = row.querySelector('[data-repeat-remove]');
            if (!button || button.dataset.bound) return;
            button.dataset.bound = 'true';
            button.addEventListener('click', () => {
                const repeat = row.closest('[data-repeat]');
                if (repeat.querySelectorAll('[data-repeat-row]').length <= 1) return;
                row.remove();
                [...repeat.querySelectorAll('[data-repeat-row]')].forEach((current, index) => {
                    current.querySelector('strong').textContent = `${repeat.dataset.repeatType === 'Faq' ? 'Câu hỏi' : 'Mục'} ${index + 1}`;
                    current.querySelectorAll('[name]').forEach(input => input.name = input.name.replace(/repeat\.\d+\./, `repeat.${index}.`));
                });
            });
        }

        collectPayload() {
            const form = this.drawer.querySelector('[data-pve-form]');
            const type = form.elements.type.value;
            const content = defaultContent(type, this.scope);
            this.drawer.querySelectorAll('[name^="content."]').forEach(input => {
                const key = input.name.slice('content.'.length);
                if (key === 'itemsText') content.items = input.value.split('\n').map(value => value.trim()).filter(Boolean);
                else if (key === 'nearbyText') content.nearby = input.value.split('\n').map(value => value.trim()).filter(Boolean);
                else if (key === 'limit') content[key] = Math.max(1, Math.min(24, Number(input.value) || 6));
                else content[key] = input.value;
            });
            if (type === 'Faq' || type === 'PolicyGrid') {
                content.items = [...this.drawer.querySelectorAll('[data-repeat-row]')].map(row => {
                    const inputs = row.querySelectorAll('[name]');
                    const item = {};
                    inputs.forEach(input => { item[input.name.split('.').pop()] = input.value; });
                    return item;
                });
            }
            return {
                type,
                name: form.elements.name.value.trim(),
                variant: form.elements.variant.value,
                isVisible: form.elements.isVisible.checked,
                contentJson: JSON.stringify(content)
            };
        }

        async saveDrawer(mode, id) {
            const submit = this.drawer.querySelector('button[type="submit"]');
            submit.disabled = true; submit.textContent = 'Đang lưu…';
            try {
                const payload = this.collectPayload();
                let saved;
                if (mode === 'create') saved = await DeLongApi.post(`${this.baseApi}/sections`, payload);
                else saved = await DeLongApi.put(`${this.baseApi}/sections/${id}`, payload);
                if (mode === 'create' && this.pendingInsertAfter) await this.placeAfter(saved.id, this.pendingInsertAfter);
                this.toast('Đã lưu. Đang cập nhật giao diện…');
                window.location.reload();
            } catch (error) {
                submit.disabled = false; submit.textContent = 'Lưu thay đổi';
                this.toast(error.message || 'Không thể lưu khối.', 'error');
            }
        }

        async uploadImage(event) {
            const file = event.target.files?.[0]; event.target.value = '';
            if (!file) return;
            const label = event.target.closest('.pve-upload');
            const previous = label.childNodes[0]?.textContent || 'Tải ảnh';
            label.childNodes[0].textContent = 'Đang tải…';
            try {
                const form = new FormData(); form.append('file', file);
                const asset = await DeLongApi.postForm(`${this.baseApi}/assets/section`, form);
                const target = this.drawer.querySelector('input[name="content.imageUrl"]');
                if (target) target.value = asset.url || '';
                this.toast('Đã tải ảnh. Bấm Lưu thay đổi để áp dụng.');
            } catch (error) { this.toast(error.message || 'Không thể tải ảnh.', 'error'); }
            finally { label.childNodes[0].textContent = previous; }
        }

        async placeAfter(newId, afterId) {
            const ids = this.sections.map(item => item.id);
            const existingIndex = ids.findIndex(value => String(value) === String(afterId));
            const filtered = ids.filter(value => String(value) !== String(newId));
            filtered.splice(Math.max(0, existingIndex + 1), 0, newId);
            await DeLongApi.put(`${this.baseApi}/sections/reorder`, { ids: filtered });
        }

        async duplicate(section) {
            if (!window.confirm(`Nhân bản khối “${section.name || this.typeLabel(section.type)}”?`)) return;
            try {
                const saved = await DeLongApi.post(`${this.baseApi}/sections`, {
                    type: section.type,
                    name: `${section.name || this.typeLabel(section.type)} (bản sao)`,
                    variant: section.variant,
                    isVisible: section.isVisible !== false,
                    contentJson: section.contentJson || '{}'
                });
                await this.placeAfter(saved.id, section.id);
                window.location.reload();
            } catch (error) { this.toast(error.message || 'Không thể nhân bản khối.', 'error'); }
        }

        async hide(section) {
            if (!window.confirm(`Ẩn khối “${section.name || this.typeLabel(section.type)}” khỏi website?`)) return;
            try {
                await DeLongApi.put(`${this.baseApi}/sections/${section.id}`, {
                    type: section.type, name: section.name, variant: section.variant,
                    isVisible: false, contentJson: section.contentJson || '{}'
                });
                window.location.reload();
            } catch (error) { this.toast(error.message || 'Không thể ẩn khối.', 'error'); }
        }

        async remove(section) {
            if (!window.confirm(`Xóa khối “${section.name || this.typeLabel(section.type)}”? Thao tác này không thể hoàn tác.`)) return;
            try {
                await DeLongApi.delete(`${this.baseApi}/sections/${section.id}`);
                window.location.reload();
            } catch (error) { this.toast(error.message || 'Không thể xóa khối.', 'error'); }
        }

        closeDrawer() {
            this.drawer?.remove();
            this.drawer = null;
            this.pendingInsertAfter = null;
        }

        toast(message, type) {
            let toast = document.querySelector('.pve-toast');
            if (!toast) {
                toast = document.createElement('div');
                toast.className = 'pve-toast';
                document.body.appendChild(toast);
            }
            toast.className = `pve-toast ${type === 'error' ? 'error' : 'success'}`;
            toast.textContent = message;
            toast.hidden = false;
            clearTimeout(this.toastTimer);
            this.toastTimer = setTimeout(() => { toast.hidden = true; }, 3200);
        }
    }

    DeLongApi.get(contextUrl)
        .then(context => {
            if (!context?.canEdit) return;
            new VisualEditor(context).mount();
        })
        .catch(() => {
            // Anonymous users and accounts without website permissions should see the public site unchanged.
        });
})();
