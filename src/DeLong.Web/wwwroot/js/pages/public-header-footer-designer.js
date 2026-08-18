(function (global) {
    if (!document.body?.classList.contains('public-body') || !global.DeLongApi) return;

    const pagePath = location.pathname.replace(/\/+$/, '') || '/';
    const scoped = pagePath.match(/^\/h\/([^/]+)(?:\/|$)/i);
    const siteSlug = scoped ? decodeURIComponent(scoped[1]) : '';
    const contextUrl = `/api/admin/site/visual-context${siteSlug ? `?siteSlug=${encodeURIComponent(siteSlug)}` : ''}`;
    const h = value => String(value ?? '').replace(/[&<>"']/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[ch]));
    const clone = value => JSON.parse(JSON.stringify(value ?? {}));
    const id = prefix => `${prefix}-${global.crypto?.randomUUID?.().replace(/-/g, '') || `${Date.now()}${Math.random().toString(16).slice(2)}`}`.slice(0, 64);

    function toast(message, error) {
        let node = document.querySelector('.psd-editor-toast');
        if (!node) { node = document.createElement('div'); node.className = 'psd-editor-toast'; document.body.appendChild(node); }
        node.className = `psd-editor-toast${error ? ' error' : ''}`;
        node.textContent = message; node.hidden = false;
        clearTimeout(node._timer); node._timer = setTimeout(() => { node.hidden = true; }, 3400);
    }

    class HeaderFooterDesigner {
        constructor(context) {
            this.context = context;
            this.propertyId = context.propertyId || null;
            this.drawer = null;
            this.designer = null;
            this.shell = null;
            this.siteSettings = null;
            this.branding = null;
            this.footerDraft = [];
        }

        designerApi() {
            return this.context.scope === 'global'
                ? '/api/admin/site/global/designer'
                : `/api/admin/properties/${this.propertyId}/site/designer`;
        }

        shellApi() {
            return this.context.scope === 'global'
                ? '/api/admin/site/global/shell'
                : `/api/admin/properties/${this.propertyId}/site/shell`;
        }

        siteApi() { return `/api/admin/properties/${this.propertyId}/site`; }

        async load() {
            const tasks = [DeLongApi.get(`${this.designerApi()}/`), DeLongApi.get(`${this.shellApi()}/`)];
            if (this.context.scope === 'global') tasks.push(DeLongApi.get('/api/admin/site/global/branding'));
            else tasks.push(DeLongApi.get(`${this.siteApi()}/`));
            const [designer, shell, extra] = await Promise.all(tasks);
            this.designer = designer || {};
            this.shell = shell || {};
            if (this.context.scope === 'global') this.branding = extra || {};
            else this.siteSettings = clone(extra?.settings || {});
        }

        close() {
            this.drawer?.remove(); this.drawer = null;
            document.body.classList.remove('psd-designer-open');
        }

        openDrawer(title, eyebrow, body, footer) {
            this.close();
            const drawer = document.createElement('section');
            drawer.className = 'pve-context-drawer psd-designer-drawer';
            drawer.setAttribute('role', 'dialog'); drawer.setAttribute('aria-modal', 'true');
            drawer.innerHTML = `<header><div><small>${h(eyebrow)}</small><h2>${h(title)}</h2></div><button type="button" data-psd-close aria-label="Đóng">×</button></header><div class="pve-context-drawer-body">${body}</div>${footer || ''}`;
            document.body.appendChild(drawer); document.body.classList.add('psd-designer-open', 'pve-context-drawer-open');
            this.drawer = drawer;
            drawer.querySelector('[data-psd-close]').addEventListener('click', () => { this.close(); document.body.classList.remove('pve-context-drawer-open'); });
            return drawer;
        }

        colorField(label, name, value) {
            return `<label class="psd-color-field"><span>${h(label)}</span><div><input type="color" data-color-picker="${h(name)}" value="${h(/^#[0-9a-f]{6}$/i.test(value || '') ? value : '#ffffff')}"><input name="${h(name)}" value="${h(value || '')}" placeholder="#fbfaf6 hoặc transparent"></div></label>`;
        }

        bindColorFields(drawer) {
            drawer.querySelectorAll('[data-color-picker]').forEach(picker => picker.addEventListener('input', () => {
                const input = drawer.querySelector(`[name="${picker.dataset.colorPicker}"]`);
                if (input) input.value = picker.value;
            }));
        }

        async openHeader() {
            try {
                await this.load();
                const d = this.designer?.header || {};
                const globalScope = this.context.scope === 'global';
                const brand = globalScope ? this.branding : this.siteSettings;
                const siteName = globalScope ? (brand.overrideSiteName || '') : (brand.siteName || '');
                const tagline = globalScope ? (brand.overrideTagline || '') : (brand.tagline || '');
                const logo = globalScope ? (brand.overrideLogoUrl || '') : (brand.logoUrl || '');
                const body = `<div class="psd-designer-form">
                    <section class="psd-editor-section"><div class="psd-section-head"><span>01</span><div><strong>Thương hiệu Header</strong><small>${globalScope ? 'Để trống để tiếp tục dùng fallback thương hiệu hiện tại.' : 'Tên, tagline và logo đang dùng trên toàn bộ trang của cơ sở.'}</small></div></div>
                        <label><span>Tên website</span><input name="psdSiteName" value="${h(siteName)}" ${globalScope ? `placeholder="${h(brand.siteName || 'De Long Homestay')}"` : ''}></label>
                        <label><span>Tagline</span><input name="psdTagline" value="${h(tagline)}" ${globalScope ? `placeholder="${h(brand.tagline || '')}"` : ''}></label>
                        <label><span>Logo</span><div class="psd-media-field"><input name="psdLogoUrl" value="${h(logo)}" placeholder="URL ảnh logo"><button type="button" data-psd-pick-logo>Chọn từ Media</button></div></label>
                    </section>
                    <section class="psd-editor-section"><div class="psd-section-head"><span>02</span><div><strong>Hành vi Header</strong><small>Bật sticky để Header bám phía trên khi cuộn. Trạng thái sticky có bộ style riêng.</small></div></div>
                        <label class="pve-context-check psd-sticky-toggle"><input type="checkbox" name="psdSticky" ${d.sticky !== false ? 'checked' : ''}><span><strong>Sticky Header</strong><small>Tắt để Header cuộn đi cùng nội dung trang.</small></span></label>
                    </section>
                    <section class="psd-editor-section"><div class="psd-section-head"><span>03</span><div><strong>Style Header bình thường</strong><small>Trạng thái khi trang ở đầu hoặc khi Sticky bị tắt.</small></div></div>
                        <div class="psd-grid-2">${this.colorField('Màu nền', 'psdBackground', d.background || '#fbfaf6')}${this.colorField('Màu chữ', 'psdTextColor', d.textColor || '#172422')}${this.colorField('Màu viền', 'psdBorderColor', d.borderColor || '#d5e0dc')}</div>
                        <div class="psd-check-row"><label class="pve-context-check"><input type="checkbox" name="psdShadow" ${d.shadow ? 'checked' : ''}><span>Đổ bóng</span></label><label class="pve-context-check"><input type="checkbox" name="psdBlur" ${d.blur !== false ? 'checked' : ''}><span>Blur nền</span></label></div>
                    </section>
                    <section class="psd-editor-section psd-sticky-style"><div class="psd-section-head"><span>04</span><div><strong>Style sau khi cuộn</strong><small>Có thể dùng nền/chữ khác hoàn toàn với Header ban đầu.</small></div></div>
                        <div class="psd-grid-2">${this.colorField('Màu nền sticky', 'psdStickyBackground', d.stickyBackground || '#fbfaf6')}${this.colorField('Màu chữ sticky', 'psdStickyTextColor', d.stickyTextColor || '#172422')}${this.colorField('Màu viền sticky', 'psdStickyBorderColor', d.stickyBorderColor || '#d5e0dc')}</div>
                        <div class="psd-check-row"><label class="pve-context-check"><input type="checkbox" name="psdStickyShadow" ${d.stickyShadow ? 'checked' : ''}><span>Đổ bóng sticky</span></label><label class="pve-context-check"><input type="checkbox" name="psdStickyBlur" ${d.stickyBlur !== false ? 'checked' : ''}><span>Blur sticky</span></label></div>
                    </section>
                </div>`;
                const footer = '<footer><span>Style Header áp dụng cho mọi trang trong scope hiện tại.</span><button type="button" data-psd-cancel>Hủy</button><button type="button" class="primary" data-psd-save-header>Lưu Header</button></footer>';
                const drawer = this.openDrawer('Thiết kế Header', this.context.propertyName || 'WEBSITE', body, footer);
                this.bindColorFields(drawer);
                drawer.querySelector('[data-psd-cancel]').addEventListener('click', () => { this.close(); document.body.classList.remove('pve-context-drawer-open'); });
                drawer.querySelector('[data-psd-pick-logo]').addEventListener('click', () => this.pickImage(drawer.querySelector('[name="psdLogoUrl"]')));
                drawer.querySelector('[data-psd-save-header]').addEventListener('click', () => this.saveHeader(drawer));
            } catch (error) { toast(error.message || 'Không thể mở thiết kế Header.', true); }
        }

        headerPayload(drawer) {
            const value = name => drawer.querySelector(`[name="${name}"]`)?.value.trim() || '';
            const checked = name => !!drawer.querySelector(`[name="${name}"]`)?.checked;
            return {
                sticky: checked('psdSticky'), background: value('psdBackground'), textColor: value('psdTextColor'), borderColor: value('psdBorderColor'),
                shadow: checked('psdShadow'), blur: checked('psdBlur'), stickyBackground: value('psdStickyBackground'),
                stickyTextColor: value('psdStickyTextColor'), stickyBorderColor: value('psdStickyBorderColor'),
                stickyShadow: checked('psdStickyShadow'), stickyBlur: checked('psdStickyBlur')
            };
        }

        designerPayload(header, rows, enabled) {
            return { header: header || this.designer?.header || {}, footerBuilderEnabled: enabled ?? !!this.designer?.footerBuilderEnabled, footerRows: rows || this.designer?.footerRows || [] };
        }

        async saveHeader(drawer) {
            const button = drawer.querySelector('[data-psd-save-header]'); button.disabled = true; button.textContent = 'Đang lưu…';
            const val = name => drawer.querySelector(`[name="${name}"]`)?.value.trim() || '';
            try {
                if (this.context.scope === 'global') {
                    await DeLongApi.put('/api/admin/site/global/branding', {
                        siteName: val('psdSiteName'), tagline: val('psdTagline'), logoUrl: val('psdLogoUrl'),
                        faviconUrl: this.branding?.overrideFaviconUrl || '', ogImageUrl: this.branding?.overrideOgImageUrl || '',
                        metaTitle: this.branding?.overrideMetaTitle || '', metaDescription: this.branding?.overrideMetaDescription || ''
                    });
                } else {
                    const next = clone(this.siteSettings || {});
                    next.siteName = val('psdSiteName'); next.tagline = val('psdTagline'); next.logoUrl = val('psdLogoUrl');
                    await DeLongApi.put(`${this.siteApi()}/settings`, next);
                }
                await DeLongApi.put(`${this.designerApi()}/`, this.designerPayload(this.headerPayload(drawer)));
                this.remember(); location.reload();
            } catch (error) {
                button.disabled = false; button.textContent = 'Lưu Header'; toast(error.message || 'Không thể lưu Header.', true);
            }
        }

        starterRows() {
            const s = this.shell || {};
            return [{
                id: id('row'), background: '#102827', textColor: '#d7e3df', padding: 'lg', gap: 'lg', columns: [
                    { id: id('col'), span: 5, elements: [
                        this.newElement('brand'), Object.assign(this.newElement('text'), { text: s.footerIntro || 'Một nơi để nghỉ chậm lại, chọn phòng rõ ràng và gửi yêu cầu đặt chỗ trực tiếp.' }),
                        Object.assign(this.newElement('button'), { text: s.footerBookingText || 'Đặt phòng →', url: s.footerBookingUrl || '@booking', variant: 'outline' })
                    ] },
                    { id: id('col'), span: 2, elements: [Object.assign(this.newElement('heading'), { text: s.footerExploreTitle || 'Khám phá', variant: 'h4' }), this.newElement('menu')] },
                    { id: id('col'), span: 2, elements: [Object.assign(this.newElement('heading'), { text: s.footerBranchesTitle || 'Cơ sở', variant: 'h4' }), this.newElement('branches')] },
                    { id: id('col'), span: 3, elements: [Object.assign(this.newElement('heading'), { text: s.footerContactTitle || 'Liên hệ', variant: 'h4' }), this.newElement('contact')] }
                ]
            }];
        }

        newElement(type) {
            return { id: id('el'), type: type || 'text', text: '', url: '', imageUrl: '', altText: '', align: 'left', variant: type === 'spacer' ? 'md' : 'default', links: [] };
        }

        async openFooter() {
            try {
                await this.load();
                this.footerDraft = clone(this.designer?.footerRows || []);
                const enabled = !!this.designer?.footerBuilderEnabled;
                const contact = this.context.scope === 'property' ? `<section class="psd-editor-section"><div class="psd-section-head"><span>01</span><div><strong>Thông tin liên hệ</strong><small>Element Liên hệ trong Footer sẽ lấy dữ liệu động từ các trường này.</small></div></div><label><span>Địa chỉ</span><input name="psdAddress" value="${h(this.siteSettings?.address || '')}"></label><div class="psd-grid-2"><label><span>Điện thoại</span><input name="psdPhone" value="${h(this.siteSettings?.phone || '')}"></label><label><span>Email</span><input name="psdEmail" value="${h(this.siteSettings?.email || '')}"></label></div><div class="psd-grid-2"><label><span>Facebook</span><input name="psdFacebook" value="${h(this.siteSettings?.facebookUrl || '')}"></label><label><span>Zalo</span><input name="psdZalo" value="${h(this.siteSettings?.zaloUrl || '')}"></label></div></section>` : '';
                const body = `<div class="psd-designer-form">${contact}<section class="psd-editor-section"><div class="psd-section-head"><span>${this.context.scope === 'property' ? '02' : '01'}</span><div><strong>Footer Builder</strong><small>Tạo Row / Column / Element giống cách dựng section. Menu, Cơ sở và Liên hệ vẫn là dữ liệu động.</small></div></div><label class="pve-context-check psd-footer-enabled"><input type="checkbox" name="psdFooterEnabled" ${enabled ? 'checked' : ''}><span><strong>Dùng Footer Builder</strong><small>Tắt để quay lại Footer mặc định hiện tại mà không mất cấu hình builder.</small></span></label><div class="psd-builder-toolbar"><button type="button" data-psd-starter>⌁ Bắt đầu từ Footer hiện tại</button><button type="button" data-psd-add-row>＋ Thêm hàng</button></div><div data-psd-footer-builder-editor></div></section></div>`;
                const footer = '<footer><span>Menu / CTA vẫn quản lý ở công cụ Menu hiện tại.</span><button type="button" data-psd-cancel>Hủy</button><button type="button" class="primary" data-psd-save-footer>Lưu Footer</button></footer>';
                const drawer = this.openDrawer('Thiết kế Footer', this.context.propertyName || 'WEBSITE', body, footer);
                drawer.querySelector('[data-psd-cancel]').addEventListener('click', () => { this.close(); document.body.classList.remove('pve-context-drawer-open'); });
                drawer.querySelector('[data-psd-starter]').addEventListener('click', () => {
                    if (this.footerDraft.length && !confirm('Thay cấu trúc Footer hiện tại bằng mẫu từ Footer cũ?')) return;
                    this.footerDraft = this.starterRows(); this.renderFooterBuilder();
                });
                drawer.querySelector('[data-psd-add-row]').addEventListener('click', () => { if (this.footerDraft.length >= 6) return toast('Footer tối đa 6 hàng.', true); this.footerDraft.push({ id: id('row'), background: 'transparent', textColor: '#d7e3df', padding: 'md', gap: 'md', columns: [{ id: id('col'), span: 12, elements: [] }] }); this.renderFooterBuilder(); });
                drawer.querySelector('[data-psd-save-footer]').addEventListener('click', () => this.saveFooter(drawer));
                this.bindFooterEvents(); this.renderFooterBuilder();
            } catch (error) { toast(error.message || 'Không thể mở thiết kế Footer.', true); }
        }

        elementOptions(selected) {
            const options = [['brand','Thương hiệu / Logo'],['heading','Tiêu đề'],['text','Văn bản'],['image','Ảnh'],['button','Nút'],['links','Danh sách link'],['menu','Menu chính'],['branches','Danh sách cơ sở'],['contact','Liên hệ'],['divider','Phân cách'],['spacer','Khoảng cách']];
            return options.map(([value,label]) => `<option value="${value}" ${selected === value ? 'selected' : ''}>${label}</option>`).join('');
        }

        findRow(rowId) { return this.footerDraft.find(row => row.id === rowId); }
        findColumn(rowId, colId) { return this.findRow(rowId)?.columns?.find(col => col.id === colId); }
        findElement(rowId, colId, elId) { return this.findColumn(rowId, colId)?.elements?.find(el => el.id === elId); }

        elementEditor(element) {
            const base = `<div class="psd-element-common"><label><span>Loại</span><select data-el-field="type">${this.elementOptions(element.type)}</select></label><label><span>Căn</span><select data-el-field="align"><option value="left" ${element.align === 'left' ? 'selected' : ''}>Trái</option><option value="center" ${element.align === 'center' ? 'selected' : ''}>Giữa</option><option value="right" ${element.align === 'right' ? 'selected' : ''}>Phải</option></select></label></div>`;
            if (element.type === 'brand' || element.type === 'menu' || element.type === 'branches' || element.type === 'contact') return base + `<p class="psd-mini-note">${element.type === 'brand' ? 'Lấy logo/tên thương hiệu hiện tại.' : element.type === 'menu' ? 'Lấy danh sách Menu chính và tự cập nhật khi Menu thay đổi.' : element.type === 'branches' ? 'Lấy danh sách cơ sở đang hoạt động.' : 'Lấy địa chỉ, điện thoại và email hiện tại.'}</p>`;
            if (element.type === 'heading') return base + `<label><span>Nội dung</span><input data-el-field="text" value="${h(element.text)}"></label><label><span>Cỡ</span><select data-el-field="variant"><option value="h2" ${element.variant === 'h2' ? 'selected' : ''}>H2</option><option value="h3" ${element.variant === 'h3' ? 'selected' : ''}>H3</option><option value="h4" ${element.variant === 'h4' ? 'selected' : ''}>H4</option></select></label>`;
            if (element.type === 'text') return base + `<label><span>Văn bản</span><textarea rows="4" data-el-field="text">${h(element.text)}</textarea></label>`;
            if (element.type === 'image') return base + `<label><span>Ảnh</span><div class="psd-media-field"><input data-el-field="imageUrl" value="${h(element.imageUrl)}" placeholder="URL ảnh"><button type="button" data-psd-pick-footer-image>Media</button></div></label><label><span>Alt text</span><input data-el-field="altText" value="${h(element.altText)}"></label><label><span>Link khi bấm</span><input data-el-field="url" value="${h(element.url)}"></label>`;
            if (element.type === 'button') return base + `<div class="psd-grid-2"><label><span>Chữ nút</span><input data-el-field="text" value="${h(element.text)}"></label><label><span>Link</span><input data-el-field="url" value="${h(element.url)}"></label></div><label><span>Kiểu</span><select data-el-field="variant"><option value="default" ${element.variant === 'default' ? 'selected' : ''}>Mặc định</option><option value="pill" ${element.variant === 'pill' ? 'selected' : ''}>Pill</option><option value="outline" ${element.variant === 'outline' ? 'selected' : ''}>Outline</option></select></label>`;
            if (element.type === 'links') return base + `<div class="psd-link-list">${(element.links || []).map((link,index) => `<div class="psd-link-row" data-link-index="${index}"><input data-link-field="label" value="${h(link.label)}" placeholder="Tên"><input data-link-field="url" value="${h(link.url)}" placeholder="Link"><label title="Mở tab mới"><input type="checkbox" data-link-field="openInNewTab" ${link.openInNewTab ? 'checked' : ''}> ↗</label><button type="button" data-psd-remove-link>×</button></div>`).join('')}</div><button type="button" class="psd-small-action" data-psd-add-link>＋ Thêm link</button>`;
            if (element.type === 'divider') return base;
            if (element.type === 'spacer') return base + `<label><span>Chiều cao</span><select data-el-field="variant"><option value="sm" ${element.variant === 'sm' ? 'selected' : ''}>Nhỏ</option><option value="md" ${element.variant === 'md' ? 'selected' : ''}>Vừa</option><option value="lg" ${element.variant === 'lg' ? 'selected' : ''}>Lớn</option></select></label>`;
            return base;
        }

        renderFooterBuilder() {
            if (!this.drawer) return;
            const root = this.drawer.querySelector('[data-psd-footer-builder-editor]');
            if (!root) return;
            if (!this.footerDraft.length) { root.innerHTML = '<div class="psd-builder-empty"><strong>Footer Builder chưa có hàng.</strong><span>Bấm “Bắt đầu từ Footer hiện tại” để có mẫu 4 cột, hoặc tự thêm một hàng trống.</span></div>'; return; }
            root.innerHTML = this.footerDraft.map((row, rowIndex) => `<article class="psd-footer-row-editor" data-row-id="${h(row.id)}"><header><div><span>HÀNG ${rowIndex + 1}</span><strong>${(row.columns || []).length} cột</strong></div><div><button type="button" data-row-up ${rowIndex === 0 ? 'disabled' : ''}>↑</button><button type="button" data-row-down ${rowIndex === this.footerDraft.length - 1 ? 'disabled' : ''}>↓</button><button type="button" class="danger" data-row-delete>×</button></div></header><div class="psd-row-style"><label><span>Nền</span><input data-row-field="background" value="${h(row.background || 'transparent')}"></label><label><span>Chữ</span><input data-row-field="textColor" value="${h(row.textColor || '#d7e3df')}"></label><label><span>Padding</span><select data-row-field="padding"><option value="sm" ${row.padding === 'sm' ? 'selected' : ''}>Nhỏ</option><option value="md" ${row.padding === 'md' ? 'selected' : ''}>Vừa</option><option value="lg" ${row.padding === 'lg' ? 'selected' : ''}>Lớn</option><option value="xl" ${row.padding === 'xl' ? 'selected' : ''}>Rất lớn</option></select></label><label><span>Gap</span><select data-row-field="gap"><option value="sm" ${row.gap === 'sm' ? 'selected' : ''}>Nhỏ</option><option value="md" ${row.gap === 'md' ? 'selected' : ''}>Vừa</option><option value="lg" ${row.gap === 'lg' ? 'selected' : ''}>Lớn</option></select></label><button type="button" data-col-add ${(row.columns || []).length >= 4 ? 'disabled' : ''}>＋ Cột</button></div><div class="psd-columns-editor">${(row.columns || []).map((col,colIndex) => `<section class="psd-column-editor" data-col-id="${h(col.id)}"><div class="psd-column-head"><strong>Cột ${colIndex + 1}</strong><label>Rộng <select data-col-field="span">${[2,3,4,5,6,7,8,9,10,12].map(n => `<option value="${n}" ${Number(col.span) === n ? 'selected' : ''}>${n}/12</option>`).join('')}</select></label><div><button type="button" data-col-left ${colIndex === 0 ? 'disabled' : ''}>←</button><button type="button" data-col-right ${colIndex === (row.columns || []).length - 1 ? 'disabled' : ''}>→</button><button type="button" class="danger" data-col-delete ${(row.columns || []).length <= 1 ? 'disabled' : ''}>×</button></div></div><div class="psd-elements-editor">${(col.elements || []).map((el,elIndex) => `<article class="psd-element-editor" data-el-id="${h(el.id)}"><div class="psd-element-head"><strong>${h((el.type || 'text').toUpperCase())}</strong><div><button type="button" data-el-up ${elIndex === 0 ? 'disabled' : ''}>↑</button><button type="button" data-el-down ${elIndex === (col.elements || []).length - 1 ? 'disabled' : ''}>↓</button><button type="button" class="danger" data-el-delete>×</button></div></div>${this.elementEditor(el)}</article>`).join('')}</div><div class="psd-element-add"><select data-add-element-type>${this.elementOptions('text')}</select><button type="button" data-el-add ${(col.elements || []).length >= 12 ? 'disabled' : ''}>＋ Phần tử</button></div></section>`).join('')}</div></article>`).join('');
        }

        bindFooterEvents() {
            const root = this.drawer.querySelector('[data-psd-footer-builder-editor]');
            root.addEventListener('click', event => {
                const rowNode = event.target.closest('[data-row-id]'), colNode = event.target.closest('[data-col-id]'), elNode = event.target.closest('[data-el-id]');
                const row = rowNode ? this.findRow(rowNode.dataset.rowId) : null;
                const col = row && colNode ? this.findColumn(row.id, colNode.dataset.colId) : null;
                const el = col && elNode ? this.findElement(row.id, col.id, elNode.dataset.elId) : null;
                if (event.target.closest('[data-row-up]') && row) { const i = this.footerDraft.indexOf(row); [this.footerDraft[i-1],this.footerDraft[i]]=[row,this.footerDraft[i-1]]; this.renderFooterBuilder(); }
                else if (event.target.closest('[data-row-down]') && row) { const i = this.footerDraft.indexOf(row); [this.footerDraft[i+1],this.footerDraft[i]]=[row,this.footerDraft[i+1]]; this.renderFooterBuilder(); }
                else if (event.target.closest('[data-row-delete]') && row) { this.footerDraft = this.footerDraft.filter(x => x !== row); this.renderFooterBuilder(); }
                else if (event.target.closest('[data-col-add]') && row && row.columns.length < 4) { row.columns.push({ id:id('col'), span:12, elements:[] }); this.renderFooterBuilder(); }
                else if (event.target.closest('[data-col-left]') && col) { const i=row.columns.indexOf(col); [row.columns[i-1],row.columns[i]]=[col,row.columns[i-1]]; this.renderFooterBuilder(); }
                else if (event.target.closest('[data-col-right]') && col) { const i=row.columns.indexOf(col); [row.columns[i+1],row.columns[i]]=[col,row.columns[i+1]]; this.renderFooterBuilder(); }
                else if (event.target.closest('[data-col-delete]') && col && row.columns.length > 1) { row.columns=row.columns.filter(x=>x!==col); this.renderFooterBuilder(); }
                else if (event.target.closest('[data-el-add]') && col && col.elements.length < 12) { const type=colNode.querySelector('[data-add-element-type]').value; col.elements.push(this.newElement(type)); this.renderFooterBuilder(); }
                else if (event.target.closest('[data-el-up]') && el) { const i=col.elements.indexOf(el); [col.elements[i-1],col.elements[i]]=[el,col.elements[i-1]]; this.renderFooterBuilder(); }
                else if (event.target.closest('[data-el-down]') && el) { const i=col.elements.indexOf(el); [col.elements[i+1],col.elements[i]]=[el,col.elements[i+1]]; this.renderFooterBuilder(); }
                else if (event.target.closest('[data-el-delete]') && el) { col.elements=col.elements.filter(x=>x!==el); this.renderFooterBuilder(); }
                else if (event.target.closest('[data-psd-add-link]') && el) { el.links ||= []; if(el.links.length<12) el.links.push({label:'Liên kết',url:'/',openInNewTab:false}); this.renderFooterBuilder(); }
                else if (event.target.closest('[data-psd-remove-link]') && el) { const link=event.target.closest('[data-link-index]'); el.links.splice(Number(link.dataset.linkIndex),1); this.renderFooterBuilder(); }
                else if (event.target.closest('[data-psd-pick-footer-image]') && el) { this.pickImage(null, item => { el.imageUrl=item.url; if(!el.altText) el.altText=item.altText||item.title||''; this.renderFooterBuilder(); }); }
            });
            const update = event => {
                const rowNode=event.target.closest('[data-row-id]'), colNode=event.target.closest('[data-col-id]'), elNode=event.target.closest('[data-el-id]');
                const row=rowNode?this.findRow(rowNode.dataset.rowId):null, col=row&&colNode?this.findColumn(row.id,colNode.dataset.colId):null, el=col&&elNode?this.findElement(row.id,col.id,elNode.dataset.elId):null;
                if (row && event.target.dataset.rowField) row[event.target.dataset.rowField]=event.target.value;
                if (col && event.target.dataset.colField) col[event.target.dataset.colField]=Number(event.target.value);
                if (el && event.target.dataset.elField) { const field=event.target.dataset.elField; el[field]=event.target.type==='checkbox'?event.target.checked:event.target.value; if(field==='type') this.renderFooterBuilder(); }
                if (el && event.target.dataset.linkField) { const linkNode=event.target.closest('[data-link-index]'), link=el.links?.[Number(linkNode?.dataset.linkIndex)]; if(link) link[event.target.dataset.linkField]=event.target.type==='checkbox'?event.target.checked:event.target.value; }
            };
            root.addEventListener('input', update); root.addEventListener('change', update);
        }

        async saveFooter(drawer) {
            const button = drawer.querySelector('[data-psd-save-footer]'); button.disabled=true; button.textContent='Đang lưu…';
            try {
                if (this.context.scope === 'property') {
                    const value=name=>drawer.querySelector(`[name="${name}"]`)?.value.trim()||'';
                    const next=clone(this.siteSettings||{});
                    next.address=value('psdAddress'); next.phone=value('psdPhone'); next.email=value('psdEmail'); next.facebookUrl=value('psdFacebook'); next.zaloUrl=value('psdZalo');
                    await DeLongApi.put(`${this.siteApi()}/settings`, next);
                }
                const enabled=!!drawer.querySelector('[name="psdFooterEnabled"]')?.checked;
                await DeLongApi.put(`${this.designerApi()}/`, this.designerPayload(this.designer?.header||{},this.footerDraft,enabled));
                this.remember(); location.reload();
            } catch(error) { button.disabled=false; button.textContent='Lưu Footer'; toast(error.message||'Không thể lưu Footer.',true); }
        }

        pickImage(input, callback) {
            if (!global.DeLongMediaLibrary?.pick) return toast('Media Library chưa sẵn sàng.', true);
            global.DeLongMediaLibrary.pick({ currentUrl: input?.value || '', onSelect: item => { if (input) input.value=item.url; callback?.(item); } });
        }

        remember() { try { sessionStorage.setItem('delong:pve:contextual:return', `${location.pathname}${location.search}${location.hash}`); } catch {} }
    }

    DeLongApi.get(contextUrl).then(context => {
        if (!context?.canEdit) return;
        const editor = new HeaderFooterDesigner(context);
        global.DeLongShellDesigner = { open: kind => kind === 'footer' ? editor.openFooter() : editor.openHeader() };
        document.addEventListener('click', event => {
            const headerAction = event.target.closest('[data-contextual-header],[data-context-header]');
            const footerAction = event.target.closest('[data-contextual-footer],[data-context-footer]');
            const contextual = event.target.closest('.pve-context-target');
            const host = contextual?.closest('.public-site-header,.public-hospitality-footer');
            const kind = headerAction || host?.classList.contains('public-site-header') ? 'header' : footerAction || host?.classList.contains('public-hospitality-footer') ? 'footer' : '';
            if (!kind) return;
            event.preventDefault(); event.stopImmediatePropagation();
            if (kind === 'header') editor.openHeader(); else editor.openFooter();
        }, true);
    }).catch(() => {
        // Roles without Visual Editor permission keep the normal public shell.
    });
})(window);
