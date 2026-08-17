(function () {
    if (!document.body.classList.contains('public-body') || !window.DeLongApi) return;

    const normalizedPath = (window.location.pathname.replace(/\/+$/, '') || '/');
    const scopedMatch = normalizedPath.match(/^\/h\/([^/]+)(?:\/|$)/i);
    const siteSlug = scopedMatch ? decodeURIComponent(scopedMatch[1]) : '';
    const scopedHomePath = scopedMatch ? `/h/${scopedMatch[1]}` : '';
    const isHome = normalizedPath === '/' || (scopedHomePath && normalizedPath === scopedHomePath);
    const propertyBlogMatch = normalizedPath.match(/^\/h\/[^/]+\/blog(?:\/([^/]+))?$/i);
    const isGlobalBlog = normalizedPath === '/blog';
    const contextUrl = `/api/admin/site/visual-context${siteSlug ? `?siteSlug=${encodeURIComponent(siteSlug)}` : ''}`;

    const BASE_TYPES = {
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

    const BUILDER_TYPES = [
        ['BuilderImage', 'Ảnh'],
        ['BuilderDivider', 'Dòng phân cách'],
        ['BuilderSpacer', 'Khoảng cách'],
        ['BuilderColumns2', '2 cột'],
        ['BuilderColumns3', '3 cột'],
        ['BuilderHtml', 'HTML tùy chỉnh']
    ];

    const TYPES = {
        global: [...BASE_TYPES.global, ...BUILDER_TYPES],
        property: [...BASE_TYPES.property, ...BUILDER_TYPES]
    };

    const VARIANTS = {
        Hero: ['split', 'centered', 'image-first', 'image-full', 'booking-overlay', 'editorial'],
        BranchGrid: ['grid-3', 'grid-2', 'editorial'],
        RoomGrid: ['grid-3', 'grid-2', 'featured-first', 'editorial-cards', 'horizontal-scroll'],
        AvailabilitySearch: ['booking-bar', 'card', 'minimal'],
        FeatureGrid: ['split', 'stacked', 'icon-grid', 'dark-band', 'editorial'],
        Faq: ['accordion', 'two-column'],
        Location: ['split', 'card'],
        PolicyGrid: ['grid-3', 'list'],
        RichText: ['narrow', 'wide', 'editorial'],
        Cta: ['card', 'full-width', 'dark', 'offer'],
        BuilderImage: ['wide', 'narrow'],
        BuilderDivider: ['wide'],
        BuilderSpacer: ['wide'],
        BuilderColumns2: ['wide'],
        BuilderColumns3: ['wide'],
        BuilderHtml: ['wide', 'narrow']
    };

    const ELEMENT_TYPES = [
        ['.public-cms-hero', 'Hero'], ['.public-branch-section', 'BranchGrid'], ['.public-cms-room-grid', 'RoomGrid'],
        ['.public-cms-availability', 'AvailabilitySearch'], ['.public-cms-feature', 'FeatureGrid'],
        ['.public-story-faq', 'Faq'], ['.public-story-location', 'Location'], ['.public-story-policies', 'PolicyGrid'],
        ['.public-cms-rich', 'RichText'], ['.public-cms-cta', 'Cta']
    ];

    const BUILDER_KIND_TO_TYPE = {
        image: 'BuilderImage', divider: 'BuilderDivider', spacer: 'BuilderSpacer',
        columns2: 'BuilderColumns2', columns3: 'BuilderColumns3', html: 'BuilderHtml'
    };

    const TYPE_TO_BUILDER_KIND = Object.fromEntries(Object.entries(BUILDER_KIND_TO_TYPE).map(([key, value]) => [value, key]));

    function h(value) {
        return String(value ?? '').replace(/[&<>'"]/g, char => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[char]));
    }

    function clone(value) {
        return JSON.parse(JSON.stringify(value ?? {}));
    }

    function json(value) {
        try { return JSON.parse(value || '{}'); } catch { return {}; }
    }

    function getStore(key) {
        try { return sessionStorage.getItem(key); } catch { return null; }
    }

    function setStore(key, value) {
        try { sessionStorage.setItem(key, value); } catch { }
    }

    function removeStore(key) {
        try { sessionStorage.removeItem(key); } catch { }
    }

    function visualType(section) {
        if (section?.type !== 'RichText') return section?.type || 'RichText';
        const content = json(section.contentJson);
        return BUILDER_KIND_TO_TYPE[content.builderKind] || 'RichText';
    }

    function serverType(type) {
        return TYPE_TO_BUILDER_KIND[type] ? 'RichText' : type;
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
        if (type === 'BuilderImage') return { builderKind: 'image', imageUrl: '', altText: '', caption: '', linkUrl: '', html: '' };
        if (type === 'BuilderDivider') return { builderKind: 'divider', label: '', lineStyle: 'solid', html: '' };
        if (type === 'BuilderSpacer') return { builderKind: 'spacer', size: 'md', html: '' };
        if (type === 'BuilderColumns2') return { builderKind: 'columns2', column1Html: '<h3>Cột 1</h3><p>Nội dung cột 1.</p>', column2Html: '<h3>Cột 2</h3><p>Nội dung cột 2.</p>', html: '' };
        if (type === 'BuilderColumns3') return { builderKind: 'columns3', column1Html: '<h3>Cột 1</h3><p>Nội dung.</p>', column2Html: '<h3>Cột 2</h3><p>Nội dung.</p>', column3Html: '<h3>Cột 3</h3><p>Nội dung.</p>', html: '' };
        if (type === 'BuilderHtml') return { builderKind: 'html', html: '<div class="dl-builder-custom">Nội dung HTML</div>' };
        return { html: '<p>Nội dung mới</p>' };
    }

    function field(label, name, value, cfg) {
        const o = cfg || {};
        const help = o.help ? `<small>${h(o.help)}</small>` : '';
        if (o.type === 'textarea') return `<label class="pve-field"><span>${h(label)}</span><textarea name="${h(name)}" rows="${o.rows || 3}">${h(value)}</textarea>${help}</label>`;
        if (o.type === 'number') return `<label class="pve-field"><span>${h(label)}</span><input type="number" name="${h(name)}" min="${o.min ?? 0}" max="${o.max ?? 24}" value="${h(value)}" />${help}</label>`;
        return `<label class="pve-field"><span>${h(label)}</span><input type="text" name="${h(name)}" value="${h(value)}" />${help}</label>`;
    }

    function selectField(label, name, value, options) {
        return `<label class="pve-field"><span>${h(label)}</span><select name="${h(name)}">${options.map(item => {
            const v = Array.isArray(item) ? item[0] : item;
            const labelText = Array.isArray(item) ? item[1] : item;
            return `<option value="${h(v)}"${String(v) === String(value) ? ' selected' : ''}>${h(labelText)}</option>`;
        }).join('')}</select></label>`;
    }

    function checkField(label, name, checked, value) {
        return `<label class="pve-choice"><input type="checkbox" name="${h(name)}"${value !== undefined ? ` value="${h(value)}"` : ''}${checked ? ' checked' : ''}><span>${h(label)}</span></label>`;
    }

    function imageField(label, value, name) {
        const fieldName = name || 'content.imageUrl';
        return `<div class="pve-field"><span>${h(label)}</span><div class="pve-image-row"><input name="${h(fieldName)}" type="text" value="${h(value)}" /><label class="pve-upload">Tải ảnh<input type="file" accept="image/png,image/jpeg,image/webp" data-section-upload data-target-field="${h(fieldName)}" hidden /></label></div><small>Có thể dán URL hoặc tải ảnh mới.</small></div>`;
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

    function propertyChoices(content, context) {
        const selected = new Set((content.propertyIds || []).map(String));
        const properties = context.properties || [];
        if (!properties.length) return '<div class="pve-notice">Chưa có cơ sở hoạt động.</div>';
        return `<div class="pve-field"><span>Cơ sở hiển thị</span><small>Không chọn mục nào = tự lấy tất cả cơ sở đang hoạt động.</small><div class="pve-choice-list">${properties.map(property => checkField(property.siteName || property.name || 'Cơ sở', 'content.propertyIds', selected.has(String(property.id)), property.id)).join('')}</div></div>`;
    }

    function roomSourceFields(content, context) {
        const mode = content.mode || 'all';
        if (mode === 'byProperty') {
            const quotas = content.propertyQuotas && typeof content.propertyQuotas === 'object' ? content.propertyQuotas : {};
            return `<div class="pve-field"><span>Số phòng theo cơ sở</span><div class="pve-quota-list">${(context.properties || []).map(property => `<label><span>${h(property.siteName || property.name || 'Cơ sở')}</span><input type="number" min="0" max="24" data-property-quota="${h(property.id)}" value="${h(quotas[property.id] || quotas[String(property.id)] || 0)}"></label>`).join('')}</div><small>Đặt 0 để bỏ qua cơ sở đó.</small></div>`;
        }
        if (mode === 'manual') {
            const selected = new Set((content.roomIds || []).map(String));
            const rooms = context.rooms || [];
            return `<div class="pve-field"><span>Chọn phòng thủ công</span><div class="pve-choice-list pve-room-choice-list">${rooms.map(room => checkField(`${room.name} · ${room.propertyName || ''}`, 'content.roomIds', selected.has(String(room.id)), room.id)).join('') || '<small>Chưa có phòng đã xuất bản.</small>'}</div></div>`;
        }
        return '<div class="pve-notice">Tự động lấy phòng đang xuất bản từ toàn hệ thống theo thứ tự hiện tại.</div>';
    }

    function builderFields(type, content) {
        if (type === 'BuilderImage') return imageField('Ảnh', content.imageUrl || '') + field('Alt text', 'content.altText', content.altText || '') + field('Chú thích', 'content.caption', content.caption || '') + field('Link khi bấm ảnh', 'content.linkUrl', content.linkUrl || '', { help: 'Để trống nếu ảnh không cần liên kết.' });
        if (type === 'BuilderDivider') return field('Nhãn giữa dòng (tùy chọn)', 'content.label', content.label || '') + selectField('Kiểu dòng', 'content.lineStyle', content.lineStyle || 'solid', [['solid', 'Liền'], ['dashed', 'Nét đứt'], ['soft', 'Mảnh / nhẹ']]);
        if (type === 'BuilderSpacer') return selectField('Khoảng cách', 'content.size', content.size || 'md', [['sm', 'Nhỏ'], ['md', 'Vừa'], ['lg', 'Lớn'], ['xl', 'Rất lớn']]);
        if (type === 'BuilderColumns2') return '<div class="pve-notice">Hai cột sẽ tự xếp thành một cột trên mobile.</div>' + field('HTML cột 1', 'content.column1Html', content.column1Html || '', { type: 'textarea', rows: 8 }) + field('HTML cột 2', 'content.column2Html', content.column2Html || '', { type: 'textarea', rows: 8 });
        if (type === 'BuilderColumns3') return '<div class="pve-notice">Ba cột responsive, phù hợp cho icon/text/card nội dung.</div>' + field('HTML cột 1', 'content.column1Html', content.column1Html || '', { type: 'textarea', rows: 7 }) + field('HTML cột 2', 'content.column2Html', content.column2Html || '', { type: 'textarea', rows: 7 }) + field('HTML cột 3', 'content.column3Html', content.column3Html || '', { type: 'textarea', rows: 7 });
        if (type === 'BuilderHtml') return field('HTML tùy chỉnh', 'content.html', content.html || '', { type: 'textarea', rows: 16, help: 'HTML vẫn được server sanitize; script và nội dung nguy hiểm không được lưu.' });
        return '';
    }

    function contentFields(type, content, scope, context) {
        if (TYPE_TO_BUILDER_KIND[type]) return builderFields(type, content);
        if (type === 'Hero') return field('Eyebrow', 'content.eyebrow', content.eyebrow || '') +
            field('Tiêu đề', 'content.title', content.title || '', { type: 'textarea', rows: 2 }) +
            field('Mô tả', 'content.body', content.body || '', { type: 'textarea', rows: 3 }) +
            `<div class="pve-grid-2">${field('CTA chính', 'content.primaryText', content.primaryText || '')}${field('URL CTA chính', 'content.primaryUrl', content.primaryUrl || '')}${field('CTA phụ', 'content.secondaryText', content.secondaryText || '')}${field('URL CTA phụ', 'content.secondaryUrl', content.secondaryUrl || '')}</div>` + imageField('Ảnh Hero', content.imageUrl || '');
        if (type === 'BranchGrid') return field('Eyebrow', 'content.eyebrow', content.eyebrow || '') + field('Tiêu đề', 'content.title', content.title || '') + propertyChoices(content, context);
        if (type === 'RoomGrid') return field('Eyebrow', 'content.eyebrow', content.eyebrow || '') + field('Tiêu đề', 'content.title', content.title || '') +
            (scope === 'global' ? selectField('Cách lấy phòng', 'content.mode', content.mode || 'all', [['all', 'Tự lấy tất cả'], ['byProperty', 'Chia theo cơ sở'], ['manual', 'Chọn thủ công']]) : '') +
            field('Số phòng tối đa', 'content.limit', content.limit || 6, { type: 'number', min: 1, max: 24 }) +
            (scope === 'global' ? `<div data-room-source>${roomSourceFields(content, context)}</div>` : '<div class="pve-notice">Trang cơ sở tự lấy phòng đã xuất bản của chính cơ sở này.</div>');
        if (type === 'AvailabilitySearch') return field('Tiêu đề', 'content.title', content.title || '');
        if (type === 'FeatureGrid') return field('Eyebrow', 'content.eyebrow', content.eyebrow || '') + field('Tiêu đề', 'content.title', content.title || '', { type: 'textarea', rows: 2 }) + field('Mô tả', 'content.body', content.body || '', { type: 'textarea', rows: 3 }) + field('Các điểm nổi bật — mỗi dòng một mục', 'content.itemsText', Array.isArray(content.items) ? content.items.join('\n') : '', { type: 'textarea', rows: 5 }) + imageField('Ảnh kể chuyện', content.imageUrl || '');
        if (type === 'Faq' || type === 'PolicyGrid') return `<div class="pve-grid-2">${field('Eyebrow', 'content.eyebrow', content.eyebrow || '')}${field('Tiêu đề', 'content.title', content.title || '')}</div><div class="pve-repeat" data-repeat-type="${type}">${repeatRows(type, content.items)}<button class="pve-add-repeat" type="button" data-repeat-add>+ Thêm ${type === 'Faq' ? 'câu hỏi' : 'mục'}</button></div>`;
        if (type === 'Location') return `<div class="pve-grid-2">${field('Eyebrow', 'content.eyebrow', content.eyebrow || '')}${field('Tiêu đề', 'content.title', content.title || '')}</div>` + field('Mô tả', 'content.body', content.body || '', { type: 'textarea', rows: 3 }) + field('Địa chỉ', 'content.address', content.address || '') + field('Google Maps URL', 'content.mapUrl', content.mapUrl || '') + field('Maps embed URL', 'content.embedUrl', content.embedUrl || '') + field('Địa điểm gần — mỗi dòng một mục', 'content.nearbyText', Array.isArray(content.nearby) ? content.nearby.join('\n') : '', { type: 'textarea', rows: 4 });
        if (type === 'RichText') return field('Nội dung HTML', 'content.html', content.html || '', { type: 'textarea', rows: 12, help: 'Server vẫn sanitize theo quy tắc CMS hiện tại.' });
        if (type === 'Cta') return field('Tiêu đề', 'content.title', content.title || '') + field('Mô tả', 'content.body', content.body || '', { type: 'textarea', rows: 3 }) + `<div class="pve-grid-2">${field('Nút', 'content.buttonText', content.buttonText || '')}${field('URL', 'content.buttonUrl', content.buttonUrl || '')}</div>`;
        return '';
    }

    function buildBuilderHtml(type, content) {
        if (type === 'BuilderImage') {
            const image = content.imageUrl ? `<img src="${h(content.imageUrl)}" alt="${h(content.altText || '')}">` : '<div class="dl-builder-image-empty">Chưa có ảnh</div>';
            const linked = content.linkUrl ? `<a href="${h(content.linkUrl)}">${image}</a>` : image;
            const caption = content.caption ? `<figcaption>${h(content.caption)}</figcaption>` : '';
            return `<figure class="dl-builder-image">${linked}${caption}</figure>`;
        }
        if (type === 'BuilderDivider') return `<div class="dl-builder-divider dl-builder-divider-${h(content.lineStyle || 'solid')}">${content.label ? `<span>${h(content.label)}</span>` : ''}</div>`;
        if (type === 'BuilderSpacer') return `<div class="dl-builder-spacer dl-builder-spacer-${h(content.size || 'md')}"></div>`;
        if (type === 'BuilderColumns2') return `<div class="dl-builder-columns dl-builder-columns-2"><div>${content.column1Html || ''}</div><div>${content.column2Html || ''}</div></div>`;
        if (type === 'BuilderColumns3') return `<div class="dl-builder-columns dl-builder-columns-3"><div>${content.column1Html || ''}</div><div>${content.column2Html || ''}</div><div>${content.column3Html || ''}</div></div>`;
        return content.html || '';
    }

    class Editor {
        constructor(context) {
            this.context = context;
            this.scope = context.scope;
            this.propertyId = context.propertyId || '';
            this.api = this.scope === 'global' ? '/api/admin/site/global' : `/api/admin/properties/${this.propertyId}/site`;
            this.editorialApi = this.scope === 'global' ? '/api/admin/site/global/editorial' : `/api/admin/properties/${this.propertyId}/editorial`;
            this.adminUrl = this.scope === 'global' ? '/Admin/Site/Global' : `/Admin/Site?propertyId=${encodeURIComponent(this.propertyId)}`;
            this.sections = [];
            this.editing = false;
            this.drawer = null;
            this.drawerModel = null;
            this.insertAfterId = null;
            this.dragged = null;
            this.galleryDeleted = [];
            const keySuffix = this.scope === 'global' ? 'global' : this.propertyId;
            this.editStateKey = `delong:pve:editing:${keySuffix}`;
            this.scrollKey = `delong:pve:scroll:${keySuffix}`;
        }

        mount() {
            if (!document.querySelector('link[data-public-visual-editor]')) {
                const css = document.createElement('link');
                css.rel = 'stylesheet'; css.href = '/css/public-visual-editor.css?v=20260817-4'; css.dataset.publicVisualEditor = 'true';
                document.head.appendChild(css);
            }
            this.toolbar();
            const savedScroll = Number(getStore(this.scrollKey) || 0);
            removeStore(this.scrollKey);
            if (savedScroll > 0) requestAnimationFrame(() => window.scrollTo({ top: savedScroll, behavior: 'auto' }));
            if (isHome && getStore(this.editStateKey) === '1') setTimeout(() => this.toggleEdit({ silent: true }), 30);
        }

        toolbar() {
            document.body.classList.add('pve-authorized');
            const label = this.scope === 'global' ? 'Trang chủ chung' : (this.context.propertyName || 'Trang cơ sở');
            const bar = document.createElement('aside');
            bar.className = 'pve-toolbar'; bar.setAttribute('aria-label', 'Công cụ chỉnh sửa website');
            const editorialButtons = this.scope === 'global'
                ? `${isHome ? '<button type="button" data-gallery>Gallery</button><button type="button" data-blog>Blog</button>' : (isGlobalBlog ? '<button type="button" data-blog>Thiết lập Blog</button>' : '')}`
                : `${isHome ? '<button type="button" data-gallery>Gallery</button><button type="button" data-blog>Blog</button>' : (propertyBlogMatch ? `<button type="button" data-blog>${propertyBlogMatch[1] ? 'Sửa bài viết' : 'Quản lý Blog'}</button>` : '')}`;
            bar.innerHTML = `<div class="pve-toolbar-brand"><span>DL</span><strong>Chỉnh website</strong><small>${h(label)}</small></div><div class="pve-toolbar-actions">${isHome ? '<button type="button" class="pve-primary" data-edit-toggle>✦ Chỉnh sửa trang</button><button type="button" data-add hidden>＋ Thêm khối</button>' : ''}${editorialButtons}<button type="button" data-brand>Thương hiệu</button><a href="${this.adminUrl}">Cấu hình đầy đủ</a><a href="/Admin">Quản trị</a><button type="button" data-hide aria-label="Ẩn thanh chỉnh sửa">×</button></div>`;
            document.body.prepend(bar);
            bar.querySelector('[data-edit-toggle]')?.addEventListener('click', () => this.toggleEdit());
            bar.querySelector('[data-add]')?.addEventListener('click', () => this.openCreate(null));
            bar.querySelector('[data-brand]')?.addEventListener('click', () => this.openBranding());
            bar.querySelector('[data-gallery]')?.addEventListener('click', () => this.scope === 'global' ? this.openGlobalEditorial('gallery') : this.openPropertyGallery());
            bar.querySelector('[data-blog]')?.addEventListener('click', () => {
                if (this.scope === 'global') this.openGlobalEditorial('blog');
                else this.openPropertyBlog(propertyBlogMatch?.[1] ? decodeURIComponent(propertyBlogMatch[1]) : null);
            });
            bar.querySelector('[data-hide]')?.addEventListener('click', () => { if (this.editing) this.exitEdit(); document.body.classList.toggle('pve-toolbar-hidden'); });
        }

        async toggleEdit(options) {
            if (this.editing) return this.exitEdit();
            const button = document.querySelector('[data-edit-toggle]');
            if (!button) return;
            button.disabled = true; button.textContent = 'Đang tải…';
            try {
                const data = await DeLongApi.get(`${this.api}/`);
                this.sections = (data?.sections || data?.site?.sections || []).slice().sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0));
                this.editing = true; setStore(this.editStateKey, '1'); document.body.classList.add('pve-editing');
                button.disabled = false; button.textContent = '✓ Kết thúc chỉnh sửa';
                const add = document.querySelector('[data-add]'); if (add) add.hidden = false;
                this.decorate();
                if (!options?.silent) this.toast('Đã bật chỉnh sửa trực tiếp.');
            } catch (error) { button.disabled = false; button.textContent = '✦ Chỉnh sửa trang'; removeStore(this.editStateKey); this.toast(error.message || 'Không thể mở trình chỉnh sửa.', 'error'); }
        }

        exitEdit() {
            this.editing = false; removeStore(this.editStateKey); document.body.classList.remove('pve-editing'); this.closeDrawer();
            document.querySelectorAll('[data-pve-controls],[data-pve-insert],[data-pve-editorial-controls]').forEach(node => node.remove());
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
            document.querySelectorAll('[data-pve-controls],[data-pve-insert],[data-pve-editorial-controls]').forEach(node => node.remove());
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
                controls.innerHTML = `<button class="pve-drag" type="button" draggable="true" title="Kéo để sắp xếp">⋮⋮</button><button type="button" data-action="up" title="Đưa khối lên">↑</button><button type="button" data-action="down" title="Đưa khối xuống">↓</button><span>${h(this.typeLabel(visualType(section)))}</span><button type="button" data-action="edit">Sửa</button><button type="button" data-action="duplicate">Nhân bản</button><button type="button" data-action="hide">Ẩn</button><button type="button" class="danger" data-action="delete">Xóa</button>`;
                element.appendChild(controls);
                const insert = document.createElement('button'); insert.type = 'button'; insert.className = 'pve-insert'; insert.dataset.pveInsert = '1'; insert.textContent = '+ Thêm khối tại đây'; element.appendChild(insert);
                controls.addEventListener('click', event => {
                    const action = event.target.closest('[data-action]')?.dataset.action; if (!action) return;
                    event.preventDefault(); event.stopPropagation();
                    if (action === 'edit') this.openEdit(section);
                    if (action === 'duplicate') this.duplicate(section);
                    if (action === 'hide') this.hide(section);
                    if (action === 'delete') this.remove(section);
                    if (action === 'up') this.moveSection(section, -1);
                    if (action === 'down') this.moveSection(section, 1);
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
            this.decorateEditorial();
        }

        decorateEditorial() {
            const gallery = document.querySelector('#main-content > .public-editorial-gallery');
            const blog = document.querySelector('#main-content > .public-editorial-blog');
            if (gallery) this.editorialControl(gallery, 'Gallery', () => this.scope === 'global' ? this.openGlobalEditorial('gallery') : this.openPropertyGallery());
            if (blog) this.editorialControl(blog, 'Blog', () => this.scope === 'global' ? this.openGlobalEditorial('blog') : this.openPropertyBlog(null));
        }

        editorialControl(element, label, handler) {
            element.classList.add('pve-editorial-section');
            const controls = document.createElement('div'); controls.className = 'pve-editorial-controls'; controls.dataset.pveEditorialControls = '1';
            controls.innerHTML = `<span>${h(label)}</span><button type="button">Sửa ${h(label)}</button>`;
            controls.querySelector('button').addEventListener('click', event => { event.preventDefault(); event.stopPropagation(); handler(); });
            element.appendChild(controls);
        }

        dragEnd() { document.querySelectorAll('.pve-drop-target').forEach(node => node.classList.remove('pve-drop-target')); this.dragged?.classList.remove('pve-dragging'); this.dragged = null; }

        async saveOrder() {
            const rendered = this.renderedElements().map(x => x.element.dataset.pveSectionId).filter(Boolean);
            const renderedSet = new Set(rendered.map(String)); let cursor = 0;
            const ids = this.sections.map(section => renderedSet.has(String(section.id)) ? rendered[cursor++] : section.id);
            try {
                await DeLongApi.put(`${this.api}/sections/reorder`, { ids });
                const map = new Map(this.sections.map(section => [String(section.id), section])); this.sections = ids.map(id => map.get(String(id))).filter(Boolean);
                this.decorate(); this.toast('Đã cập nhật thứ tự khối.');
            } catch (error) { this.toast(error.message || 'Không thể đổi thứ tự.', 'error'); this.reloadPreservingEdit(); }
        }

        async moveSection(section, delta) {
            const elements = [...document.querySelectorAll('.pve-editable-section[data-pve-section-id]')];
            const index = elements.findIndex(node => String(node.dataset.pveSectionId) === String(section.id));
            const target = elements[index + delta];
            if (index < 0 || !target) return this.toast(delta < 0 ? 'Khối đã ở trên cùng.' : 'Khối đã ở dưới cùng.');
            const current = elements[index];
            if (delta < 0) target.before(current); else target.after(current);
            await this.saveOrder();
            current.scrollIntoView({ block: 'center', behavior: 'smooth' });
        }

        typeLabel(type) { return (TYPES[this.scope] || []).find(item => item[0] === type)?.[1] || type; }

        openCreate(afterId) {
            const type = TYPES[this.scope][0][0]; this.insertAfterId = afterId;
            this.openDrawer({ mode: 'create', id: null, type, name: '', variant: VARIANTS[type][0], isVisible: true, baseContent: defaults(type, this.scope) });
        }

        openEdit(section) {
            this.insertAfterId = null;
            const type = visualType(section);
            const baseContent = Object.assign(defaults(type, this.scope), json(section.contentJson));
            this.openDrawer({ mode: 'edit', id: section.id, type, name: section.name || '', variant: section.variant || VARIANTS[type]?.[0] || 'wide', isVisible: section.isVisible !== false, baseContent });
        }

        openDrawer(model) {
            this.closeDrawer(); this.drawerModel = model;
            const drawer = document.createElement('section'); drawer.className = 'pve-drawer'; drawer.setAttribute('role', 'dialog'); drawer.setAttribute('aria-modal', 'true');
            drawer.innerHTML = `<header><div><small>${model.mode === 'create' ? 'THÊM KHỐI' : 'CHỈNH KHỐI'}</small><h2>${model.mode === 'create' ? 'Thêm nội dung mới' : h(model.name || this.typeLabel(model.type))}</h2></div><button type="button" data-close aria-label="Đóng">×</button></header><form data-editor-form><div class="pve-drawer-body"><div class="pve-grid-2">${selectField('Loại khối', 'type', model.type, TYPES[this.scope])}${field('Tên quản trị', 'name', model.name)}${selectField('Layout', 'variant', model.variant, VARIANTS[model.type] || ['wide'])}<label class="pve-check"><input type="checkbox" name="isVisible"${model.isVisible ? ' checked' : ''}><span>Hiển thị khối</span></label></div><div data-content>${contentFields(model.type, model.baseContent, this.scope, this.context)}</div></div><footer>${model.mode === 'edit' ? '<button type="button" class="pve-danger-outline" data-delete>Xóa khối</button>' : '<span></span>'}<div><button type="button" data-close>Hủy</button><button type="submit" class="pve-primary">Lưu thay đổi</button></div></footer></form>`;
            document.body.appendChild(drawer); this.drawer = drawer;
            const form = drawer.querySelector('[data-editor-form]');
            form.elements.type.addEventListener('change', () => {
                const type = form.elements.type.value; this.drawerModel.type = type; this.drawerModel.baseContent = defaults(type, this.scope);
                form.elements.variant.innerHTML = (VARIANTS[type] || ['wide']).map(value => `<option value="${h(value)}">${h(value)}</option>`).join('');
                drawer.querySelector('[data-content]').innerHTML = contentFields(type, this.drawerModel.baseContent, this.scope, this.context); this.bindDynamic();
            });
            form.addEventListener('submit', event => { event.preventDefault(); this.saveDrawer(); });
            drawer.querySelectorAll('[data-close]').forEach(button => button.addEventListener('click', () => this.closeDrawer()));
            drawer.querySelector('[data-delete]')?.addEventListener('click', () => { const section = this.sections.find(x => String(x.id) === String(model.id)); if (section) this.remove(section); });
            this.bindDynamic(); form.elements.name.focus();
        }

        bindDynamic() {
            if (!this.drawer) return;
            this.drawer.querySelectorAll('[data-section-upload]').forEach(input => { if (!input.dataset.bound) { input.dataset.bound = '1'; input.addEventListener('change', event => this.uploadSectionImage(event)); } });
            const mode = this.drawer.querySelector('[name="content.mode"]');
            if (mode && !mode.dataset.bound) {
                mode.dataset.bound = '1';
                mode.addEventListener('change', () => {
                    this.drawerModel.baseContent.mode = mode.value;
                    const target = this.drawer.querySelector('[data-room-source]');
                    if (target) target.innerHTML = roomSourceFields(Object.assign({}, this.drawerModel.baseContent, { mode: mode.value }), this.context);
                });
            }
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
                if (input.type === 'checkbox') return;
                const key = input.name.slice(8);
                if (key === 'itemsText') content.items = input.value.split('\n').map(x => x.trim()).filter(Boolean);
                else if (key === 'nearbyText') content.nearby = input.value.split('\n').map(x => x.trim()).filter(Boolean);
                else if (key === 'limit') content.limit = Math.max(1, Math.min(24, Number(input.value) || 6));
                else content[key] = input.value;
            });
            if (type === 'BranchGrid') content.propertyIds = [...this.drawer.querySelectorAll('[name="content.propertyIds"]:checked')].map(input => input.value);
            if (type === 'RoomGrid' && this.scope === 'global') {
                content.mode = this.drawer.querySelector('[name="content.mode"]')?.value || content.mode || 'all';
                if (content.mode === 'manual') content.roomIds = [...this.drawer.querySelectorAll('[name="content.roomIds"]:checked')].map(input => input.value);
                if (content.mode === 'byProperty') {
                    content.propertyQuotas = {};
                    this.drawer.querySelectorAll('[data-property-quota]').forEach(input => { content.propertyQuotas[input.dataset.propertyQuota] = Math.max(0, Math.min(24, Number(input.value) || 0)); });
                }
            }
            if (type === 'Faq' || type === 'PolicyGrid') content.items = [...this.drawer.querySelectorAll('[data-repeat-row]')].map(row => { const item = {}; row.querySelectorAll('[name]').forEach(input => item[input.name.split('.').pop()] = input.value); return item; });
            if (TYPE_TO_BUILDER_KIND[type]) {
                content.builderKind = TYPE_TO_BUILDER_KIND[type];
                content.html = buildBuilderHtml(type, content);
            }
            return { type: serverType(type), name: form.elements.name.value.trim(), variant: form.elements.variant.value, isVisible: form.elements.isVisible.checked, contentJson: JSON.stringify(content) };
        }

        async saveDrawer() {
            const submit = this.drawer.querySelector('button[type="submit"]'); submit.disabled = true; submit.textContent = 'Đang lưu…';
            try {
                const payload = this.collect(); const model = this.drawerModel;
                const saved = model.mode === 'create' ? await DeLongApi.post(`${this.api}/sections`, payload) : await DeLongApi.put(`${this.api}/sections/${model.id}`, payload);
                if (model.mode === 'create' && this.insertAfterId) await this.placeAfter(saved.id, this.insertAfterId);
                this.toast('Đã lưu. Đang cập nhật giao diện…'); this.reloadPreservingEdit();
            } catch (error) { submit.disabled = false; submit.textContent = 'Lưu thay đổi'; this.toast(error.message || 'Không thể lưu khối.', 'error'); }
        }

        async uploadSectionImage(event) {
            const file = event.target.files?.[0]; event.target.value = ''; if (!file) return;
            const label = event.target.closest('.pve-upload'); const text = label.childNodes[0]; const old = text?.textContent || 'Tải ảnh'; if (text) text.textContent = 'Đang tải…';
            try { const form = new FormData(); form.append('file', file); const asset = await DeLongApi.postForm(`${this.api}/assets/section`, form); const targetName = event.target.dataset.targetField || 'content.imageUrl'; const target = this.drawer.querySelector(`[name="${targetName}"]`); if (target) target.value = asset.url || ''; this.toast('Đã tải ảnh. Bấm Lưu thay đổi để áp dụng.'); }
            catch (error) { this.toast(error.message || 'Không thể tải ảnh.', 'error'); } finally { if (text) text.textContent = old; }
        }

        async placeAfter(newId, afterId) {
            const ids = this.sections.map(x => x.id).filter(id => String(id) !== String(newId)); const index = ids.findIndex(id => String(id) === String(afterId)); ids.splice(Math.max(0, index + 1), 0, newId); await DeLongApi.put(`${this.api}/sections/reorder`, { ids });
        }

        async duplicate(section) {
            if (!confirm(`Nhân bản khối “${section.name || this.typeLabel(visualType(section))}”?`)) return;
            try { const saved = await DeLongApi.post(`${this.api}/sections`, { type: section.type, name: `${section.name || this.typeLabel(visualType(section))} (bản sao)`, variant: section.variant, isVisible: section.isVisible !== false, contentJson: section.contentJson || '{}' }); await this.placeAfter(saved.id, section.id); this.reloadPreservingEdit(); }
            catch (error) { this.toast(error.message || 'Không thể nhân bản khối.', 'error'); }
        }

        async hide(section) {
            if (!confirm(`Ẩn khối “${section.name || this.typeLabel(visualType(section))}” khỏi website?`)) return;
            try { await DeLongApi.put(`${this.api}/sections/${section.id}`, { type: section.type, name: section.name, variant: section.variant, isVisible: false, contentJson: section.contentJson || '{}' }); this.reloadPreservingEdit(); }
            catch (error) { this.toast(error.message || 'Không thể ẩn khối.', 'error'); }
        }

        async remove(section) {
            if (!confirm(`Xóa khối “${section.name || this.typeLabel(visualType(section))}”? Thao tác này không thể hoàn tác.`)) return;
            try { await DeLongApi.delete(`${this.api}/sections/${section.id}`); this.reloadPreservingEdit(); }
            catch (error) { this.toast(error.message || 'Không thể xóa khối.', 'error'); }
        }

        async openGlobalEditorial(kind) {
            try {
                const data = await DeLongApi.get('/api/admin/site/global/editorial/');
                const settings = data.settings || {};
                this.closeDrawer();
                const isGallery = kind === 'gallery';
                const prefix = isGallery ? 'gallery' : 'blog';
                const title = isGallery ? 'Gallery trang chủ chung' : 'Blog trang chủ chung';
                const mode = settings[`${prefix}Mode`] || 'all';
                const selectedProperties = new Set((settings[`${prefix}PropertyIds`] || []).map(String));
                const selectedItems = new Set((settings[isGallery ? 'galleryItemIds' : 'blogPostIds'] || []).map(String));
                const items = isGallery ? (data.gallery || []) : (data.posts || []);
                const manualHtml = items.map(item => checkField(isGallery ? `${item.caption || item.altText || 'Ảnh'} · ${item.propertyName}` : `${item.title} · ${item.propertyName}`, 'editorial.itemIds', selectedItems.has(String(item.id)), item.id)).join('') || '<small>Chưa có nội dung đã xuất bản.</small>';
                const propertyHtml = (this.context.properties || []).map(property => checkField(property.siteName || property.name, 'editorial.propertyIds', selectedProperties.has(String(property.id)), property.id)).join('');
                const drawer = document.createElement('section'); drawer.className = 'pve-drawer'; drawer.setAttribute('role', 'dialog'); drawer.setAttribute('aria-modal', 'true');
                drawer.innerHTML = `<header><div><small>NỘI DUNG TOÀN HỆ THỐNG</small><h2>${h(title)}</h2></div><button type="button" data-close>×</button></header><form data-global-editorial><div class="pve-drawer-body"><label class="pve-check pve-check-top"><input type="checkbox" name="enabled"${settings[`${prefix}Enabled`] !== false ? ' checked' : ''}><span>Hiển thị ${isGallery ? 'Gallery' : 'Blog'}</span></label>${field('Tiêu đề', 'title', settings[`${prefix}Title`] || '')}<div class="pve-grid-2">${selectField('Nguồn nội dung', 'mode', mode, [['all', 'Tất cả'], ['properties', 'Theo cơ sở'], ['manual', 'Chọn thủ công']])}${field('Số lượng hiển thị', 'limit', settings[`${prefix}Limit`] || (isGallery ? 8 : 3), { type: 'number', min: 1, max: isGallery ? 24 : 12 })}${isGallery ? selectField('Layout Gallery', 'layout', settings.galleryLayout || 'mosaic', [['mosaic', 'Mosaic'], ['grid', 'Grid'], ['slider', 'Slider']]) : ''}</div><div data-editorial-source><div class="pve-field" data-source-properties${mode === 'properties' ? '' : ' hidden'}><span>Chọn cơ sở</span><div class="pve-choice-list">${propertyHtml}</div></div><div class="pve-field" data-source-manual${mode === 'manual' ? '' : ' hidden'}><span>Chọn ${isGallery ? 'ảnh' : 'bài viết'}</span><div class="pve-choice-list pve-editorial-choice-list">${manualHtml}</div></div></div>${!isGallery ? '<div class="pve-notice">Bài viết thật thuộc từng cơ sở. Bạn có thể chọn bài nào xuất hiện ở trang chung tại đây; sửa nội dung bài trực tiếp ở trang cơ sở.</div>' : ''}</div><footer><span></span><div><button type="button" data-close>Hủy</button><button type="submit" class="pve-primary">Lưu ${isGallery ? 'Gallery' : 'Blog'}</button></div></footer></form>`;
                document.body.appendChild(drawer); this.drawer = drawer;
                drawer.querySelectorAll('[data-close]').forEach(button => button.addEventListener('click', () => this.closeDrawer()));
                const form = drawer.querySelector('[data-global-editorial]');
                form.elements.mode.addEventListener('change', () => {
                    drawer.querySelector('[data-source-properties]').hidden = form.elements.mode.value !== 'properties';
                    drawer.querySelector('[data-source-manual]').hidden = form.elements.mode.value !== 'manual';
                });
                form.addEventListener('submit', async event => {
                    event.preventDefault(); const submit = form.querySelector('[type="submit"]'); submit.disabled = true; submit.textContent = 'Đang lưu…';
                    const payload = {
                        galleryEnabled: settings.galleryEnabled !== false,
                        galleryMode: settings.galleryMode || 'all',
                        galleryPropertyIds: settings.galleryPropertyIds || [],
                        galleryItemIds: settings.galleryItemIds || [],
                        galleryLimit: settings.galleryLimit || 8,
                        galleryTitle: settings.galleryTitle || '',
                        galleryLayout: settings.galleryLayout || 'mosaic',
                        blogEnabled: settings.blogEnabled !== false,
                        blogMode: settings.blogMode || 'all',
                        blogPropertyIds: settings.blogPropertyIds || [],
                        blogPostIds: settings.blogPostIds || [],
                        blogLimit: settings.blogLimit || 3,
                        blogTitle: settings.blogTitle || ''
                    };
                    payload[`${prefix}Enabled`] = form.elements.enabled.checked;
                    payload[`${prefix}Mode`] = form.elements.mode.value;
                    payload[`${prefix}PropertyIds`] = [...form.querySelectorAll('[name="editorial.propertyIds"]:checked')].map(input => input.value);
                    payload[isGallery ? 'galleryItemIds' : 'blogPostIds'] = [...form.querySelectorAll('[name="editorial.itemIds"]:checked')].map(input => input.value);
                    payload[`${prefix}Limit`] = Math.max(1, Number(form.elements.limit.value) || (isGallery ? 8 : 3));
                    payload[`${prefix}Title`] = form.elements.title.value.trim();
                    if (isGallery) payload.galleryLayout = form.elements.layout.value;
                    try { await DeLongApi.put('/api/admin/site/global/editorial/', payload); this.reloadPreservingEdit(); }
                    catch (error) { submit.disabled = false; submit.textContent = `Lưu ${isGallery ? 'Gallery' : 'Blog'}`; this.toast(error.message || 'Không thể lưu nội dung.', 'error'); }
                });
            } catch (error) { this.toast(error.message || 'Không thể tải cấu hình Gallery/Blog.', 'error'); }
        }

        async openPropertyGallery() {
            try {
                const data = await DeLongApi.get(`${this.editorialApi}/`);
                this.galleryDeleted = [];
                this.closeDrawer();
                const drawer = document.createElement('section'); drawer.className = 'pve-drawer pve-drawer-wide'; drawer.setAttribute('role', 'dialog'); drawer.setAttribute('aria-modal', 'true');
                drawer.innerHTML = `<header><div><small>GALLERY CƠ SỞ</small><h2>Quản lý ảnh trực tiếp</h2></div><button type="button" data-close>×</button></header><form data-gallery-form><div class="pve-drawer-body">${selectField('Layout', 'layout', data.galleryLayout || 'mosaic', [['mosaic', 'Mosaic'], ['grid', 'Grid'], ['slider', 'Slider']])}<div class="pve-gallery-editor-list" data-gallery-list>${(data.gallery || []).map(item => this.galleryRow(item)).join('')}</div><button type="button" class="pve-add-repeat" data-gallery-add>+ Thêm ảnh Gallery</button></div><footer><span></span><div><button type="button" data-close>Hủy</button><button type="submit" class="pve-primary">Lưu Gallery</button></div></footer></form>`;
                document.body.appendChild(drawer); this.drawer = drawer;
                drawer.querySelectorAll('[data-close]').forEach(button => button.addEventListener('click', () => this.closeDrawer()));
                drawer.querySelector('[data-gallery-add]').addEventListener('click', () => { drawer.querySelector('[data-gallery-list]').insertAdjacentHTML('beforeend', this.galleryRow(null)); this.bindGalleryRows(); });
                drawer.querySelector('[data-gallery-form]').addEventListener('submit', event => { event.preventDefault(); this.savePropertyGallery(event.currentTarget); });
                this.bindGalleryRows();
            } catch (error) { this.toast(error.message || 'Không thể tải Gallery.', 'error'); }
        }

        galleryRow(item) {
            const id = item?.id || '';
            const imageUrl = item?.imageUrl || '';
            return `<article class="pve-gallery-editor-row" data-gallery-row data-id="${h(id)}"><div class="pve-gallery-thumb">${imageUrl ? `<img src="${h(imageUrl)}" alt="">` : '<span>Ảnh mới</span>'}</div><div class="pve-gallery-fields">${imageField('Ảnh', imageUrl, 'gallery.imageUrl')}${field('Alt text', 'gallery.altText', item?.altText || '')}${field('Chú thích', 'gallery.caption', item?.caption || '')}<label class="pve-check pve-check-top"><input type="checkbox" name="gallery.published"${item?.isPublished !== false ? ' checked' : ''}><span>Đang hiển thị</span></label></div><div class="pve-row-actions"><button type="button" data-gallery-up title="Lên">↑</button><button type="button" data-gallery-down title="Xuống">↓</button><button type="button" class="danger" data-gallery-remove title="Xóa">×</button></div></article>`;
        }

        bindGalleryRows() {
            if (!this.drawer) return;
            this.drawer.querySelectorAll('[data-gallery-row]').forEach(row => {
                if (row.dataset.bound) return; row.dataset.bound = '1';
                row.querySelector('[data-gallery-up]').addEventListener('click', () => row.previousElementSibling?.before(row));
                row.querySelector('[data-gallery-down]').addEventListener('click', () => row.nextElementSibling?.after(row));
                row.querySelector('[data-gallery-remove]').addEventListener('click', () => { if (row.dataset.id) this.galleryDeleted.push(row.dataset.id); row.remove(); });
                row.querySelector('[data-section-upload]').addEventListener('change', event => this.uploadGalleryRow(event, row));
            });
        }

        async uploadGalleryRow(event, row) {
            const file = event.target.files?.[0]; event.target.value = ''; if (!file) return;
            const label = event.target.closest('.pve-upload'); const text = label.childNodes[0]; const old = text?.textContent || 'Tải ảnh'; if (text) text.textContent = 'Đang tải…';
            try { const form = new FormData(); form.append('file', file); const asset = await DeLongApi.postForm(`${this.api}/assets/section`, form); row.querySelector('[name="gallery.imageUrl"]').value = asset.url || ''; const thumb = row.querySelector('.pve-gallery-thumb'); thumb.innerHTML = `<img src="${h(asset.url || '')}" alt="">`; }
            catch (error) { this.toast(error.message || 'Không thể tải ảnh.', 'error'); } finally { if (text) text.textContent = old; }
        }

        async savePropertyGallery(form) {
            const submit = form.querySelector('[type="submit"]'); submit.disabled = true; submit.textContent = 'Đang lưu…';
            try {
                for (const id of this.galleryDeleted) await DeLongApi.delete(`${this.editorialApi}/gallery/${id}`);
                const ids = [];
                for (const row of [...form.querySelectorAll('[data-gallery-row]')]) {
                    const payload = { imageUrl: row.querySelector('[name="gallery.imageUrl"]').value.trim(), altText: row.querySelector('[name="gallery.altText"]').value.trim(), caption: row.querySelector('[name="gallery.caption"]').value.trim(), isPublished: row.querySelector('[name="gallery.published"]').checked };
                    if (!payload.imageUrl) continue;
                    const saved = row.dataset.id ? await DeLongApi.put(`${this.editorialApi}/gallery/${row.dataset.id}`, payload) : await DeLongApi.post(`${this.editorialApi}/gallery`, payload);
                    ids.push(saved.id);
                }
                await DeLongApi.put(`${this.editorialApi}/gallery/layout`, { layout: form.elements.layout.value });
                if (ids.length) await DeLongApi.put(`${this.editorialApi}/gallery/reorder`, { ids });
                this.reloadPreservingEdit();
            } catch (error) { submit.disabled = false; submit.textContent = 'Lưu Gallery'; this.toast(error.message || 'Không thể lưu Gallery.', 'error'); }
        }

        async openPropertyBlog(focusSlug) {
            try {
                const data = await DeLongApi.get(`${this.editorialApi}/`);
                const posts = data.posts || [];
                if (focusSlug) {
                    const post = posts.find(item => String(item.slug).toLowerCase() === String(focusSlug).toLowerCase());
                    if (post) return this.openBlogPost(post);
                }
                this.closeDrawer();
                const drawer = document.createElement('section'); drawer.className = 'pve-drawer'; drawer.setAttribute('role', 'dialog'); drawer.setAttribute('aria-modal', 'true');
                drawer.innerHTML = `<header><div><small>BLOG CƠ SỞ</small><h2>Quản lý bài viết</h2></div><button type="button" data-close>×</button></header><div class="pve-drawer-body"><button type="button" class="pve-add-repeat" data-blog-add>+ Viết bài mới</button><div class="pve-blog-list">${posts.map(post => `<article class="pve-blog-row"><div>${post.coverImageUrl ? `<img src="${h(post.coverImageUrl)}" alt="">` : '<span>BLOG</span>'}</div><section><small>${post.isPublished ? 'Đang hiển thị' : 'Bản nháp'}</small><strong>${h(post.title)}</strong><span>/${h(post.slug)}</span></section><button type="button" data-blog-edit="${h(post.id)}">Sửa</button><button type="button" class="danger" data-blog-delete="${h(post.id)}">Xóa</button></article>`).join('') || '<div class="pve-notice">Chưa có bài viết.</div>'}</div></div><footer><span></span><div><button type="button" data-close>Đóng</button></div></footer>`;
                document.body.appendChild(drawer); this.drawer = drawer;
                drawer.querySelectorAll('[data-close]').forEach(button => button.addEventListener('click', () => this.closeDrawer()));
                drawer.querySelector('[data-blog-add]').addEventListener('click', () => this.openBlogPost(null));
                drawer.querySelectorAll('[data-blog-edit]').forEach(button => button.addEventListener('click', () => this.openBlogPost(posts.find(post => String(post.id) === String(button.dataset.blogEdit)))));
                drawer.querySelectorAll('[data-blog-delete]').forEach(button => button.addEventListener('click', async () => {
                    const post = posts.find(item => String(item.id) === String(button.dataset.blogDelete));
                    if (!post || !confirm(`Xóa bài “${post.title}”?`)) return;
                    try { await DeLongApi.delete(`${this.editorialApi}/posts/${post.id}`); this.reloadPreservingEdit(); } catch (error) { this.toast(error.message || 'Không thể xóa bài.', 'error'); }
                }));
            } catch (error) { this.toast(error.message || 'Không thể tải Blog.', 'error'); }
        }

        openBlogPost(post) {
            this.closeDrawer();
            const drawer = document.createElement('section'); drawer.className = 'pve-drawer pve-drawer-wide'; drawer.setAttribute('role', 'dialog'); drawer.setAttribute('aria-modal', 'true');
            drawer.innerHTML = `<header><div><small>${post ? 'SỬA BÀI VIẾT' : 'BÀI VIẾT MỚI'}</small><h2>${h(post?.title || 'Viết bài ngay trên website')}</h2></div><button type="button" data-close>×</button></header><form data-blog-form><div class="pve-drawer-body">${field('Tiêu đề', 'title', post?.title || '')}<div class="pve-grid-2">${field('Slug', 'slug', post?.slug || '', { help: 'Để trống khi tạo mới để hệ thống tự sinh.' })}<label class="pve-check"><input type="checkbox" name="published"${post?.isPublished !== false ? ' checked' : ''}><span>Xuất bản</span></label></div>${field('Tóm tắt', 'excerpt', post?.excerpt || '', { type: 'textarea', rows: 3 })}${imageField('Ảnh cover', post?.coverImageUrl || '', 'coverImageUrl')}${field('Nội dung HTML', 'bodyHtml', post?.bodyHtml || '', { type: 'textarea', rows: 18, help: 'HTML được sanitize trước khi lưu.' })}</div><footer><span></span><div><button type="button" data-close>Hủy</button><button type="submit" class="pve-primary">Lưu bài viết</button></div></footer></form>`;
            document.body.appendChild(drawer); this.drawer = drawer;
            drawer.querySelectorAll('[data-close]').forEach(button => button.addEventListener('click', () => this.closeDrawer()));
            drawer.querySelector('[data-section-upload]').addEventListener('change', event => this.uploadSectionImage(event));
            drawer.querySelector('[data-blog-form]').addEventListener('submit', async event => {
                event.preventDefault(); const form = event.currentTarget; const submit = form.querySelector('[type="submit"]'); submit.disabled = true; submit.textContent = 'Đang lưu…';
                const payload = { slug: form.elements.slug.value.trim(), title: form.elements.title.value.trim(), excerpt: form.elements.excerpt.value.trim(), bodyHtml: form.elements.bodyHtml.value, coverImageUrl: form.elements.coverImageUrl.value.trim(), isPublished: form.elements.published.checked };
                try { if (post) await DeLongApi.put(`${this.editorialApi}/posts/${post.id}`, payload); else await DeLongApi.post(`${this.editorialApi}/posts`, payload); this.reloadPreservingEdit(); }
                catch (error) { submit.disabled = false; submit.textContent = 'Lưu bài viết'; this.toast(error.message || 'Không thể lưu bài viết.', 'error'); }
            });
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
                        this.toast('Đã lưu thương hiệu.'); this.reloadPreservingEdit();
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

        reloadPreservingEdit() {
            if (this.editing) setStore(this.editStateKey, '1');
            setStore(this.scrollKey, String(Math.max(0, window.scrollY || 0)));
            window.location.reload();
        }

        closeDrawer() { this.drawer?.remove(); this.drawer = null; this.drawerModel = null; this.insertAfterId = null; }

        toast(message, type) {
            let node = document.querySelector('.pve-toast'); if (!node) { node = document.createElement('div'); node.className = 'pve-toast'; document.body.appendChild(node); }
            node.className = `pve-toast ${type === 'error' ? 'error' : 'success'}`; node.textContent = message; node.hidden = false; clearTimeout(this.toastTimer); this.toastTimer = setTimeout(() => node.hidden = true, 3200);
        }
    }

    DeLongApi.get(contextUrl).then(context => { if (context?.canEdit) new Editor(context).mount(); }).catch(() => {});
})();
