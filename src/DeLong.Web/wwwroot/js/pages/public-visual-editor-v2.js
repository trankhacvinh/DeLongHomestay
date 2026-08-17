(function () {
    if (!document.body.classList.contains('public-body') || !window.DeLongApi) return;

    const normalizedPath = (window.location.pathname.replace(/\/+$/, '') || '/');
    const scopedMatch = normalizedPath.match(/^\/h\/([^/]+)(?:\/|$)/i);
    const siteSlug = scopedMatch ? decodeURIComponent(scopedMatch[1]) : '';
    const scopedHomePath = scopedMatch ? `/h/${scopedMatch[1]}` : '';
    const isHome = normalizedPath === '/' || (scopedHomePath && normalizedPath === scopedHomePath);
    const contextUrl = `/api/admin/site/visual-context${siteSlug ? `?siteSlug=${encodeURIComponent(siteSlug)}` : ''}`;

    const TYPES = {
        global: [
            ['Hero', 'Hero / mở đầu'], ['BranchGrid', 'Danh sách cơ sở'], ['RoomGrid', 'Danh sách phòng'],
            ['AvailabilitySearch', 'Kiểm tra phòng nhanh'], ['FeatureGrid', 'Nội dung + điểm nổi bật'],
            ['RichText', 'Nội dung tự do'], ['Cta', 'Kêu gọi hành động']
        ],
        property: [
            ['Hero', 'Hero / mở đầu'], ['AvailabilitySearch', 'Kiểm tra phòng nhanh'], ['RoomGrid', 'Danh sách phòng'],
            ['FeatureGrid', 'Nội dung + điểm nổi bật'], ['Faq', 'Câu hỏi thường gặp'], ['Location', 'Vị trí & chỉ đường'],
            ['PolicyGrid', 'Quy định lưu trú'], ['RichText', 'Nội dung tự do'], ['Cta', 'Kêu gọi hành động']
        ]
    };

    const VARIANTS = {
        Hero: ['split', 'centered', 'image-first', 'image-full', 'booking-overlay', 'editorial'],
        BranchGrid: ['grid-3', 'grid-2', 'editorial'],
        RoomGrid: ['grid-3', 'grid-2', 'featured-first', 'editorial-cards', 'horizontal-scroll'],
        AvailabilitySearch: ['booking-bar', 'card', 'minimal'],
        FeatureGrid: ['split', 'stacked', 'icon-grid', 'dark-band', 'editorial'],
        Faq: ['accordion', 'two-column'], Location: ['split', 'card'], PolicyGrid: ['grid-3', 'list'],
        RichText: ['narrow', 'wide', 'editorial'], Cta: ['card', 'full-width', 'dark', 'offer']
    };

    const ELEMENT_TYPES = [
        ['.public-cms-hero', 'Hero'], ['.public-branch-section', 'BranchGrid'], ['.public-cms-room-grid', 'RoomGrid'],
        ['.public-cms-availability', 'AvailabilitySearch'], ['.public-cms-feature', 'FeatureGrid'],
        ['.public-story-faq', 'Faq'], ['.public-story-location', 'Location'], ['.public-story-policies', 'PolicyGrid'],
        ['.public-cms-rich', 'RichText'], ['.public-cms-cta', 'Cta']
    ];

    function h(value) {
        return String(value ?? '').replace(/[&<>'"]/g, char => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[char]));
    }

    function clone(value) {
        return JSON.parse(JSON.stringify(value ?? {}));
    }

    function json(value) {
        try { return JSON.parse(value || '{}'); } catch { return {}; }
    }

    function defaults(type, scope) {
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

    function field(label, name, value, cfg) {
        const o = cfg || {};
        const help = o.help ? `<small>${h(o.help)}</small>` : '';
        if (o.type === 'textarea') return `<label class="pve-field"><span>${h(label)}</span><textarea name="${h(name)}" rows="${o.rows || 3}">${h(value)}</textarea>${help}</label>`;
        if (o.type === 'number') return `<label class="pve-field"><span>${h(label)}</span><input type="number" name="${h(name)}" min="${o.min ?? 1}" max="${o.max ?? 24}" value="${h(value)}" />${help}</label>`;
        return `<label class="pve-field"><span>${h(label)}</span><input type="text" name="${h(name)}" value="${h(value)}" />${help}</label>`;
    }

    function selectField(label, name, value, options) {
        return `<label class="pve-field"><span>${h(label)}</span><select name="${h(name)}">${options.map(item => {
            const v = Array.isArray(item) ? item[0] : item;
            const labelText = Array.isArray(item) ? item[1] : item;
            return `<option value="${h(v)}"${String(v) === String(value) ? ' selected' : ''}>${h(labelText)}</option>`;
        }).join('')}</select></label>`;
    }

    function imageField(label, value) {
        return `<div class="pve-field"><span>${h(label)}</span><div class="pve-image-row"><input name="content.imageUrl" type="text" value="${h(value)}" /><label class="pve-upload">Tải ảnh<input type="file" accept="image/png,image/jpeg,image/webp" data-section-upload hidden /></label></div><small>Có thể dán URL hoặc tải ảnh mới.</small></div>`;
    }

    function brandImageField(label, name, value, kind) {
        return `<div class="pve-field"><span>${h(label)}</span><div class="pve-image-row"><input name="${h(name)}" type="text" value="${h(value)}" /><label class="pve-upload">Tải ảnh<input type="file" accept="image/png,image/jpeg,image/webp" data-brand-upload="${kind}" data-target-field="${name}" hidden /></label></div></div>`;
    }

    function repeatRows(type, items) {
        const fallback = type === 'Faq' ? [{ question: '', answer: '' }] : [{ title: '', body: '' }];
        const source = Array.isArray(items) && items.length ? items : fallback;
        return source.map((item, index) => type === 'Faq'
            ? `<div class="pve-repeat-row" data-repeat-row><div class="pve-repeat-head"><strong>Câu hỏi ${index + 1}</strong><button type="button" data-repeat-remove>×</button></div>${field('Câu hỏi', `repeat.${index}.question`, item.question || '')}${field('Trả lời', `repeat.${index}.answer`, item.answer || '', { type: 'textarea', rows: 3 })}</div>`
            : `<div class="pve-repeat-row" data-repeat-row><div class="pve-repeat-head"><strong>Mục ${index + 1}</strong><button type="button" data-repeat-remove>×</button></div>${field('Tiêu đề', `repeat.${index}.title`, item.title || '')}${field('Nội dung', `repeat.${index}.body`, item.body || '', { type: 'textarea', rows: 3 })}</div>`
        ).join('');
    }

    function contentFields(type, content, scope) {
        if (type === 'Hero') return field('Eyebrow', 'content.eyebrow', content.eyebrow || '') +
            field('Tiêu đề', 'content.title', content.title || '', { type: 'textarea', rows: 2 }) +
            field('Mô tả', 'content.body', content.body || '', { type: 'textarea', rows: 3 }) +
            `<div class="pve-grid-2">${field('CTA chính', 'content.primaryText', content.primaryText || '')}${field('URL CTA chính', 'content.primaryUrl', content.primaryUrl || '')}${field('CTA phụ', 'content.secondaryText', content.secondaryText || '')}${field('URL CTA phụ', 'content.secondaryUrl', content.secondaryUrl || '')}</div>` + imageField('Ảnh Hero', content.imageUrl || '');
        if (type === 'BranchGrid') return field('Eyebrow', 'content.eyebrow', content.eyebrow || '') + field('Tiêu đề', 'content.title', content.title || '') + '<div class="pve-notice">Visual editor giữ nguyên danh sách cơ sở đang chọn. Muốn thay nguồn cơ sở, dùng “Mở cấu hình đầy đủ”.</div>';
        if (type === 'RoomGrid') return field('Eyebrow', 'content.eyebrow', content.eyebrow || '') + field('Tiêu đề', 'content.title', content.title || '') +
            (scope === 'global' ? selectField('Cách lấy phòng', 'content.mode', content.mode || 'all', [['all', 'Tự lấy tất cả'], ['byProperty', 'Chia theo cơ sở'], ['manual', 'Chọn thủ công']]) : '') +
            field('Số phòng tối đa', 'content.limit', content.limit || 6, { type: 'number', min: 1, max: 24 }) +
            (scope === 'global' ? '<div class="pve-notice">Quota và danh sách phòng thủ công hiện tại được giữ nguyên. Dùng cấu hình đầy đủ nếu cần thay các lựa chọn chi tiết.</div>' : '');
        if (type === 'AvailabilitySearch') return field('Tiêu đề', 'content.title', content.title || '');
        if (type === 'FeatureGrid') return field('Eyebrow', 'content.eyebrow', content.eyebrow || '') + field('Tiêu đề', 'content.title', content.title || '', { type: 'textarea', rows: 2 }) + field('Mô tả', 'content.body', content.body || '', { type: 'textarea', rows: 3 }) + field('Các điểm nổi bật — mỗi dòng một mục', 'content.itemsText', Array.isArray(content.items) ? content.items.join('\n') : '', { type: 'textarea', rows: 5 }) + imageField('Ảnh kể chuyện', content.imageUrl || '');
        if (type === 'Faq' || type === 'PolicyGrid') return `<div class="pve-grid-2">${field('Eyebrow', 'content.eyebrow', content.eyebrow || '')}${field('Tiêu đề', 'content.title', content.title || '')}</div><div class="pve-repeat" data-repeat-type="${type}">${repeatRows(type, content.items)}<button class="pve-add-repeat" type="button" data-repeat-add>+ Thêm ${type === 'Faq' ? 'câu hỏi' : 'mục'}</button></div>`;
        if (type === 'Location') return `<div class="pve-grid-2">${field('Eyebrow', 'content.eyebrow', content.eyebrow || '')}${field('Tiêu đề', 'content.title', content.title || '')}</div>` + field('Mô tả', 'content.body', content.body || '', { type: 'textarea', rows: 3 }) + field('Địa chỉ', 'content.address', content.address || '') + field('Google Maps URL', 'content.mapUrl', content.mapUrl || '') + field('Maps embed URL', 'content.embedUrl', content.embedUrl || '') + field('Địa điểm gần — mỗi dòng một mục', 'content.nearbyText', Array.isArray(content.nearby) ? content.nearby.join('\n') : '', { type: 'textarea', rows: 4 });
        if (type === 'RichText') return field('Nội dung HTML', 'content.html', content.html || '', { type: 'textarea', rows: 12, help: 'Server vẫn sanitize theo quy tắc CMS hiện tại.' });
        if (type === 'Cta') return field('Tiêu đề', 'content.title', content.title || '') + field('Mô tả', 'content.body', content.body || '', { type: 'textarea', rows: 3 }) + `<div class="pve-grid-2">${field('Nút', 'content.buttonText', content.buttonText || '')}${field('URL', 'content.buttonUrl', content.buttonUrl || '')}</div>`;
        return '';
    }

    class Editor {
        constructor(context) {
            this.context = context;
            this.scope = context.scope;
            this.propertyId = context.propertyId || '';
            this.api = this.scope === 'global' ? '/api/admin/site/global' : `/api/admin/properties/${this.propertyId}/site`;
            this.adminUrl = this.scope === 'global' ? '/Admin/Site/Global' : `/Admin/Site?propertyId=${encodeURIComponent(this.propertyId)}`;
            this.sections = [];
            this.editing = false;
            this.drawer = null;
            this.drawerModel = null;
            this.insertAfterId = null;
            this.dragged = null;
        }

        mount() {
            const css = document.createElement('link');
            css.rel = 'stylesheet'; css.href = '/css/public-visual-editor.css?v=20260817-2'; css.dataset.publicVisualEditor = 'true';
            document.head.appendChild(css);
            this.toolbar();
        }

        toolbar() {
            document.body.classList.add('pve-authorized');
            const label = this.scope === 'global' ? 'Trang chủ chung' : (this.context.propertyName || 'Trang cơ sở');
            const bar = document.createElement('aside');
            bar.className = 'pve-toolbar'; bar.setAttribute('aria-label', 'Công cụ chỉnh sửa website');
            bar.innerHTML = `<div class="pve-toolbar-brand"><span>DL</span><strong>Chỉnh website</strong><small>${h(label)}</small></div><div class="pve-toolbar-actions">${isHome ? '<button type="button" class="pve-primary" data-edit-toggle>✦ Chỉnh sửa trang</button><button type="button" data-add hidden>＋ Thêm khối</button>' : ''}<button type="button" data-brand>Thương hiệu</button><a href="${this.adminUrl}">Mở cấu hình đầy đủ</a><a href="/Admin">Quản trị</a><button type="button" data-hide aria-label="Ẩn thanh chỉnh sửa">×</button></div>`;
            document.body.prepend(bar);
            bar.querySelector('[data-edit-toggle]')?.addEventListener('click', () => this.toggleEdit());
            bar.querySelector('[data-add]')?.addEventListener('click', () => this.openCreate(null));
            bar.querySelector('[data-brand]')?.addEventListener('click', () => this.openBranding());
            bar.querySelector('[data-hide]')?.addEventListener('click', () => { if (this.editing) this.exitEdit(); document.body.classList.toggle('pve-toolbar-hidden'); });
        }

        async toggleEdit() {
            if (this.editing) return this.exitEdit();
            const button = document.querySelector('[data-edit-toggle]');
            button.disabled = true; button.textContent = 'Đang tải…';
            try {
                const data = await DeLongApi.get(`${this.api}/`);
                this.sections = (data?.sections || data?.site?.sections || []).slice().sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0));
                this.editing = true; document.body.classList.add('pve-editing');
                button.disabled = false; button.textContent = '✓ Kết thúc chỉnh sửa';
                const add = document.querySelector('[data-add]'); if (add) add.hidden = false;
                this.decorate(); this.toast('Đã bật chỉnh sửa trực tiếp.');
            } catch (error) { button.disabled = false; button.textContent = '✦ Chỉnh sửa trang'; this.toast(error.message || 'Không thể mở trình chỉnh sửa.', 'error'); }
        }

        exitEdit() {
            this.editing = false; document.body.classList.remove('pve-editing'); this.closeDrawer();
            document.querySelectorAll('[data-pve-controls],[data-pve-insert]').forEach(node => node.remove());
            document.querySelectorAll('.pve-editable-section').forEach(node => {
                node.classList.remove('pve-editable-section', 'pve-dragging', 'pve-drop-target');
                delete node.dataset.pveSectionId; delete node.dataset.pveSectionType;
            });
            const button = document.querySelector('[data-edit-toggle]'); if (button) button.textContent = '✦ Chỉnh sửa trang';
            const add = document.querySelector('[data-add]'); if (add) add.hidden = true;
        }

        renderedElements() {
            return [...document.querySelectorAll('#main-content > section')].map(element => {
                const pair = ELEMENT_TYPES.find(([selector]) => element.matches(selector));
                return pair ? { element, type: pair[1] } : null;
            }).filter(Boolean);
        }

        decorate() {
            document.querySelectorAll('[data-pve-controls],[data-pve-insert]').forEach(node => node.remove());
            const queues = new Map();
            this.sections.filter(section => section.isVisible !== false).forEach(section => {
                if (!queues.has(section.type)) queues.set(section.type, []);
                queues.get(section.type).push(section);
            });

            this.renderedElements().forEach(({ element, type }) => {
                const section = queues.get(type)?.shift();
                if (!section) return;
                element.classList.add('pve-editable-section'); element.dataset.pveSectionId = section.id; element.dataset.pveSectionType = section.type;
                const controls = document.createElement('div'); controls.className = 'pve-section-controls'; controls.dataset.pveControls = '1';
                controls.innerHTML = `<button class="pve-drag" type="button" draggable="true" title="Kéo để sắp xếp">⋮⋮</button><span>${h(this.typeLabel(section.type))}</span><button type="button" data-action="edit">Sửa</button><button type="button" data-action="duplicate">Nhân bản</button><button type="button" data-action="hide">Ẩn</button><button type="button" class="danger" data-action="delete">Xóa</button>`;
                element.appendChild(controls);
                const insert = document.createElement('button'); insert.type = 'button'; insert.className = 'pve-insert'; insert.dataset.pveInsert = '1'; insert.textContent = '+ Thêm khối tại đây'; element.appendChild(insert);
                controls.addEventListener('click', event => {
                    const action = event.target.closest('[data-action]')?.dataset.action; if (!action) return;
                    event.preventDefault(); event.stopPropagation();
                    if (action === 'edit') this.openEdit(section); if (action === 'duplicate') this.duplicate(section); if (action === 'hide') this.hide(section); if (action === 'delete') this.remove(section);
                });
                insert.addEventListener('click', event => { event.preventDefault(); event.stopPropagation(); this.openCreate(section.id); });
                const handle = controls.querySelector('.pve-drag');
                handle.addEventListener('dragstart', event => { this.dragged = element; element.classList.add('pve-dragging'); event.dataTransfer.effectAllowed = 'move'; event.dataTransfer.setData('text/plain', section.id); });
                handle.addEventListener('dragend', () => this.dragEnd());
                element.addEventListener('dragover', event => { if (!this.dragged || this.dragged === element) return; event.preventDefault(); element.classList.add('pve-drop-target'); });
                element.addEventListener('dragleave', () => element.classList.remove('pve-drop-target'));
                element.addEventListener('drop', async event => {
                    if (!this.dragged || this.dragged === element) return; event.preventDefault(); element.classList.remove('pve-drop-target');
                    const rect = element.getBoundingClientRect(); if (event.clientY < rect.top + rect.height / 2) element.before(this.dragged); else element.after(this.dragged);
                    await this.saveOrder();
                });
            });
        }

        dragEnd() { document.querySelectorAll('.pve-drop-target').forEach(node => node.classList.remove('pve-drop-target')); this.dragged?.classList.remove('pve-dragging'); this.dragged = null; }

        async saveOrder() {
            const rendered = this.renderedElements().map(x => x.element.dataset.pveSectionId).filter(Boolean);
            const renderedSet = new Set(rendered.map(String)); let cursor = 0;
            const ids = this.sections.map(section => renderedSet.has(String(section.id)) ? rendered[cursor++] : section.id);
            try {
                await DeLongApi.put(`${this.api}/sections/reorder`, { ids });
                const map = new Map(this.sections.map(section => [String(section.id), section])); this.sections = ids.map(id => map.get(String(id))).filter(Boolean);
                this.toast('Đã cập nhật thứ tự khối.');
            } catch (error) { this.toast(error.message || 'Không thể đổi thứ tự.', 'error'); window.location.reload(); }
        }

        typeLabel(type) { return (TYPES[this.scope] || []).find(item => item[0] === type)?.[1] || type; }

        openCreate(afterId) {
            const type = TYPES[this.scope][0][0]; this.insertAfterId = afterId;
            this.openDrawer({ mode: 'create', id: null, type, name: '', variant: VARIANTS[type][0], isVisible: true, baseContent: defaults(type, this.scope) });
        }

        openEdit(section) {
            this.insertAfterId = null;
            const baseContent = Object.assign(defaults(section.type, this.scope), json(section.contentJson));
            this.openDrawer({ mode: 'edit', id: section.id, type: section.type, name: section.name || '', variant: section.variant || VARIANTS[section.type]?.[0] || 'card', isVisible: section.isVisible !== false, baseContent });
        }

        openDrawer(model) {
            this.closeDrawer(); this.drawerModel = model;
            const drawer = document.createElement('section'); drawer.className = 'pve-drawer'; drawer.setAttribute('role', 'dialog'); drawer.setAttribute('aria-modal', 'true');
            drawer.innerHTML = `<header><div><small>${model.mode === 'create' ? 'THÊM KHỐI' : 'CHỈNH KHỐI'}</small><h2>${model.mode === 'create' ? 'Thêm nội dung mới' : h(model.name || this.typeLabel(model.type))}</h2></div><button type="button" data-close aria-label="Đóng">×</button></header><form data-editor-form><div class="pve-drawer-body"><div class="pve-grid-2">${selectField('Loại khối', 'type', model.type, TYPES[this.scope])}${field('Tên quản trị', 'name', model.name)}${selectField('Layout', 'variant', model.variant, VARIANTS[model.type] || ['card'])}<label class="pve-check"><input type="checkbox" name="isVisible"${model.isVisible ? ' checked' : ''}><span>Hiển thị khối</span></label></div><div data-content>${contentFields(model.type, model.baseContent, this.scope)}</div></div><footer>${model.mode === 'edit' ? '<button type="button" class="pve-danger-outline" data-delete>Xóa khối</button>' : '<span></span>'}<div><button type="button" data-close>Hủy</button><button type="submit" class="pve-primary">Lưu thay đổi</button></div></footer></form>`;
            document.body.appendChild(drawer); this.drawer = drawer;
            const form = drawer.querySelector('[data-editor-form]');
            form.elements.type.addEventListener('change', () => {
                const type = form.elements.type.value; this.drawerModel.type = type; this.drawerModel.baseContent = defaults(type, this.scope);
                form.elements.variant.innerHTML = (VARIANTS[type] || ['card']).map(value => `<option value="${h(value)}">${h(value)}</option>`).join('');
                drawer.querySelector('[data-content]').innerHTML = contentFields(type, this.drawerModel.baseContent, this.scope); this.bindDynamic();
            });
            form.addEventListener('submit', event => { event.preventDefault(); this.saveDrawer(); });
            drawer.querySelectorAll('[data-close]').forEach(button => button.addEventListener('click', () => this.closeDrawer()));
            drawer.querySelector('[data-delete]')?.addEventListener('click', () => { const section = this.sections.find(x => String(x.id) === String(model.id)); if (section) this.remove(section); });
            this.bindDynamic(); form.elements.name.focus();
        }

        bindDynamic() {
            if (!this.drawer) return;
            this.drawer.querySelectorAll('[data-section-upload]').forEach(input => { if (!input.dataset.bound) { input.dataset.bound = '1'; input.addEventListener('change', event => this.uploadSectionImage(event)); } });
            const add = this.drawer.querySelector('[data-repeat-add]');
            if (add && !add.dataset.bound) { add.dataset.bound = '1'; add.addEventListener('click', () => {
                const wrap = add.parentElement; const type = wrap.dataset.repeatType; const index = wrap.querySelectorAll('[data-repeat-row]').length;
                const holder = document.createElement('div'); holder.innerHTML = repeatRows(type, type === 'Faq' ? [{ question: '', answer: '' }] : [{ title: '', body: '' }]);
                const row = holder.firstElementChild; row.querySelectorAll('[name]').forEach(input => input.name = input.name.replace('repeat.0.', `repeat.${index}.`)); add.before(row); this.bindRepeat(row);
            }); }
            this.drawer.querySelectorAll('[data-repeat-row]').forEach(row => this.bindRepeat(row));
        }

        bindRepeat(row) {
            const button = row.querySelector('[data-repeat-remove]'); if (!button || button.dataset.bound) return; button.dataset.bound = '1';
            button.addEventListener('click', () => {
                const wrap = row.parentElement; if (wrap.querySelectorAll('[data-repeat-row]').length <= 1) return; row.remove();
                [...wrap.querySelectorAll('[data-repeat-row]')].forEach((item, index) => { item.querySelector('strong').textContent = `${wrap.dataset.repeatType === 'Faq' ? 'Câu hỏi' : 'Mục'} ${index + 1}`; item.querySelectorAll('[name]').forEach(input => input.name = input.name.replace(/repeat\.\d+\./, `repeat.${index}.`)); });
            });
        }

        collect() {
            const form = this.drawer.querySelector('[data-editor-form]'); const type = form.elements.type.value;
            const content = Object.assign(defaults(type, this.scope), clone(this.drawerModel?.baseContent || {}));
            this.drawer.querySelectorAll('[name^="content."]').forEach(input => {
                const key = input.name.slice(8);
                if (key === 'itemsText') content.items = input.value.split('\n').map(x => x.trim()).filter(Boolean);
                else if (key === 'nearbyText') content.nearby = input.value.split('\n').map(x => x.trim()).filter(Boolean);
                else if (key === 'limit') content.limit = Math.max(1, Math.min(24, Number(input.value) || 6));
                else content[key] = input.value;
            });
            if (type === 'Faq' || type === 'PolicyGrid') content.items = [...this.drawer.querySelectorAll('[data-repeat-row]')].map(row => { const item = {}; row.querySelectorAll('[name]').forEach(input => item[input.name.split('.').pop()] = input.value); return item; });
            return { type, name: form.elements.name.value.trim(), variant: form.elements.variant.value, isVisible: form.elements.isVisible.checked, contentJson: JSON.stringify(content) };
        }

        async saveDrawer() {
            const submit = this.drawer.querySelector('button[type="submit"]'); submit.disabled = true; submit.textContent = 'Đang lưu…';
            try {
                const payload = this.collect(); const model = this.drawerModel;
                const saved = model.mode === 'create' ? await DeLongApi.post(`${this.api}/sections`, payload) : await DeLongApi.put(`${this.api}/sections/${model.id}`, payload);
                if (model.mode === 'create' && this.insertAfterId) await this.placeAfter(saved.id, this.insertAfterId);
                this.toast('Đã lưu. Đang cập nhật giao diện…'); window.location.reload();
            } catch (error) { submit.disabled = false; submit.textContent = 'Lưu thay đổi'; this.toast(error.message || 'Không thể lưu khối.', 'error'); }
        }

        async uploadSectionImage(event) {
            const file = event.target.files?.[0]; event.target.value = ''; if (!file) return;
            const label = event.target.closest('.pve-upload'); const text = label.childNodes[0]; const old = text?.textContent || 'Tải ảnh'; if (text) text.textContent = 'Đang tải…';
            try { const form = new FormData(); form.append('file', file); const asset = await DeLongApi.postForm(`${this.api}/assets/section`, form); const target = this.drawer.querySelector('[name="content.imageUrl"]'); if (target) target.value = asset.url || ''; this.toast('Đã tải ảnh. Bấm Lưu thay đổi để áp dụng.'); }
            catch (error) { this.toast(error.message || 'Không thể tải ảnh.', 'error'); } finally { if (text) text.textContent = old; }
        }

        async placeAfter(newId, afterId) {
            const ids = this.sections.map(x => x.id).filter(id => String(id) !== String(newId)); const index = ids.findIndex(id => String(id) === String(afterId)); ids.splice(Math.max(0, index + 1), 0, newId); await DeLongApi.put(`${this.api}/sections/reorder`, { ids });
        }

        async duplicate(section) {
            if (!confirm(`Nhân bản khối “${section.name || this.typeLabel(section.type)}”?`)) return;
            try { const saved = await DeLongApi.post(`${this.api}/sections`, { type: section.type, name: `${section.name || this.typeLabel(section.type)} (bản sao)`, variant: section.variant, isVisible: section.isVisible !== false, contentJson: section.contentJson || '{}' }); await this.placeAfter(saved.id, section.id); window.location.reload(); }
            catch (error) { this.toast(error.message || 'Không thể nhân bản khối.', 'error'); }
        }

        async hide(section) {
            if (!confirm(`Ẩn khối “${section.name || this.typeLabel(section.type)}” khỏi website?`)) return;
            try { await DeLongApi.put(`${this.api}/sections/${section.id}`, { type: section.type, name: section.name, variant: section.variant, isVisible: false, contentJson: section.contentJson || '{}' }); window.location.reload(); }
            catch (error) { this.toast(error.message || 'Không thể ẩn khối.', 'error'); }
        }

        async remove(section) {
            if (!confirm(`Xóa khối “${section.name || this.typeLabel(section.type)}”? Thao tác này không thể hoàn tác.`)) return;
            try { await DeLongApi.delete(`${this.api}/sections/${section.id}`); window.location.reload(); }
            catch (error) { this.toast(error.message || 'Không thể xóa khối.', 'error'); }
        }

        async openBranding() {
            try {
                const data = await DeLongApi.get(this.scope === 'global' ? '/api/admin/site/global/branding' : `${this.api}/`);
                const settings = this.scope === 'global' ? data : (data?.settings || data?.site?.settings || {});
                this.closeDrawer();
                const get = key => this.scope === 'global' ? (settings?.[`override${key[0].toUpperCase()}${key.slice(1)}`] || '') : (settings?.[key] || '');
                const drawer = document.createElement('section'); drawer.className = 'pve-drawer'; drawer.setAttribute('role', 'dialog'); drawer.setAttribute('aria-modal', 'true');
                drawer.innerHTML = `<header><div><small>THƯƠNG HIỆU</small><h2>${this.scope === 'global' ? 'Nhận diện website chung' : h(this.context.propertyName || 'Website cơ sở')}</h2></div><button type="button" data-close>×</button></header><form data-brand-form><div class="pve-drawer-body">${field('Tên thương hiệu', 'siteName', get('siteName'), { help: this.scope === 'global' ? 'Để trống để kế thừa khi chỉ có một cơ sở.' : '' })}${field('Tagline', 'tagline', get('tagline'))}${brandImageField('Logo', 'logoUrl', get('logoUrl'), 'logo')}${brandImageField('Favicon', 'faviconUrl', get('faviconUrl'), 'favicon')}${brandImageField('Ảnh Open Graph', 'ogImageUrl', get('ogImageUrl'), 'og')}${field('Meta title', 'metaTitle', get('metaTitle'))}${field('Meta description', 'metaDescription', get('metaDescription'), { type: 'textarea', rows: 3 })}</div><footer><span></span><div><button type="button" data-close>Hủy</button><button type="submit" class="pve-primary">Lưu thương hiệu</button></div></footer></form>`;
                document.body.appendChild(drawer); this.drawer = drawer;
                drawer.querySelectorAll('[data-close]').forEach(button => button.addEventListener('click', () => this.closeDrawer()));
                drawer.querySelectorAll('[data-brand-upload]').forEach(input => input.addEventListener('change', event => this.uploadBrandImage(event)));
                drawer.querySelector('[data-brand-form]').addEventListener('submit', async event => {
                    event.preventDefault(); const form = event.currentTarget; const submit = form.querySelector('[type="submit"]'); submit.disabled = true; submit.textContent = 'Đang lưu…';
                    try {
                        const changed = { siteName: form.elements.siteName.value.trim(), tagline: form.elements.tagline.value.trim(), logoUrl: form.elements.logoUrl.value.trim(), faviconUrl: form.elements.faviconUrl.value.trim(), ogImageUrl: form.elements.ogImageUrl.value.trim(), metaTitle: form.elements.metaTitle.value.trim(), metaDescription: form.elements.metaDescription.value.trim() };
                        if (this.scope === 'global') await DeLongApi.put('/api/admin/site/global/branding', changed);
                        else await DeLongApi.put(`${this.api}/settings`, Object.assign({}, settings, changed));
                        this.toast('Đã lưu thương hiệu.'); window.location.reload();
                    } catch (error) { submit.disabled = false; submit.textContent = 'Lưu thương hiệu'; this.toast(error.message || 'Không thể lưu thương hiệu.', 'error'); }
                });
            } catch (error) { this.toast(error.message || 'Không thể tải cấu hình thương hiệu.', 'error'); }
        }

        async uploadBrandImage(event) {
            const file = event.target.files?.[0]; event.target.value = ''; if (!file || !this.drawer) return;
            const label = event.target.closest('.pve-upload'); const text = label.childNodes[0]; const old = text?.textContent || 'Tải ảnh'; if (text) text.textContent = 'Đang tải…';
            try { const form = new FormData(); form.append('file', file); const asset = await DeLongApi.postForm(`${this.api}/assets/${event.target.dataset.brandUpload}`, form); const target = this.drawer.querySelector(`[name="${event.target.dataset.targetField}"]`); if (target) target.value = asset.url || ''; this.toast('Đã tải ảnh. Bấm Lưu thương hiệu để áp dụng.'); }
            catch (error) { this.toast(error.message || 'Không thể tải ảnh.', 'error'); } finally { if (text) text.textContent = old; }
        }

        closeDrawer() { this.drawer?.remove(); this.drawer = null; this.drawerModel = null; this.insertAfterId = null; }

        toast(message, type) {
            let node = document.querySelector('.pve-toast'); if (!node) { node = document.createElement('div'); node.className = 'pve-toast'; document.body.appendChild(node); }
            node.className = `pve-toast ${type === 'error' ? 'error' : 'success'}`; node.textContent = message; node.hidden = false; clearTimeout(this.toastTimer); this.toastTimer = setTimeout(() => node.hidden = true, 3200);
        }
    }

    DeLongApi.get(contextUrl).then(context => { if (context?.canEdit) new Editor(context).mount(); }).catch(() => {});
})();
