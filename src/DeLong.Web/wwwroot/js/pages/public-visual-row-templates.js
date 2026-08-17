(function () {
    if (!document.body?.classList.contains('public-body')) return;

    const ROW_CLIPBOARD = 'delong:pve:rowClipboard';
    const USER_TEMPLATES = 'delong:pve:rowTemplates:v1';
    const MAX_USER_TEMPLATES = 20;

    function clone(value) { return JSON.parse(JSON.stringify(value ?? {})); }
    function h(value) { return String(value ?? '').replace(/[&<>'"]/g, char => ({ '&':'&amp;','<':'&lt;','>':'&gt;',"'":'&#39;','"':'&quot;' }[char])); }
    function id(prefix) { return `${prefix}-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 8)}`; }
    function storageGet(key) { try { return localStorage.getItem(key); } catch { return null; } }
    function storageSet(key, value) { try { localStorage.setItem(key, value); return true; } catch { return false; } }
    function sessionSet(key, value) { try { sessionStorage.setItem(key, value); return true; } catch { return false; } }
    function sessionGet(key) { try { return sessionStorage.getItem(key); } catch { return null; } }

    function responsiveElement(overrides) {
        const base = {
            desktop: { visible: true, size: 'auto', align: 'inherit' },
            tablet: { visible: true, size: 'auto', align: 'inherit' },
            mobile: { visible: true, size: 'auto', align: 'inherit' }
        };
        return Object.assign(base, overrides || {});
    }

    function element(kind, data) {
        const base = { kind, responsive: responsiveElement() };
        if (kind === 'heading') Object.assign(base, { text: 'Tiêu đề mới', level: 'h3', align: 'left' });
        if (kind === 'text') Object.assign(base, { html: '<p>Nhập nội dung của bạn.</p>' });
        if (kind === 'image') Object.assign(base, { imageUrl: '', altText: '', caption: '', linkUrl: '' });
        if (kind === 'button') Object.assign(base, { text: 'Xem thêm', url: '#', style: 'primary', align: 'left' });
        if (kind === 'divider') Object.assign(base, { style: 'solid', label: '' });
        if (kind === 'spacer') Object.assign(base, { size: 'md' });
        if (kind === 'html') Object.assign(base, { html: '<div>Nội dung HTML</div>' });
        return Object.assign(base, data || {});
    }

    function responsiveRow(opts) {
        const options = opts || {};
        return {
            desktop: { visible: true, width: options.desktopWidth || 'wide', gap: options.gap || 'md', paddingX: options.paddingX || 'md', paddingY: options.paddingY || 'md', align: options.align || 'top' },
            tablet: { visible: true, width: options.tabletWidth || 'wide', gap: options.tabletGap || options.gap || 'md', paddingX: options.tabletPaddingX || 'md', paddingY: options.tabletPaddingY || 'md', align: options.tabletAlign || options.align || 'top' },
            mobile: { visible: true, width: 'full', gap: options.mobileGap || 'md', paddingX: options.mobilePaddingX || 'sm', paddingY: options.mobilePaddingY || 'md', align: options.mobileAlign || 'top', stack: options.mobileStack || 'stack' }
        };
    }

    function row(layout, columns, options) {
        const opts = options || {};
        return {
            builderKind: 'row', rowVersion: 4, layout,
            theme: opts.theme || 'plain', gap: opts.gap || 'md', align: opts.align || 'top', padding: opts.padding || 'md', mobile: opts.mobileStack || 'stack',
            responsive: responsiveRow(opts),
            columns: columns.map(items => ({ elements: items })),
            html: ''
        };
    }

    const SYSTEM_TEMPLATES = [
        {
            id: 'image-left', category: 'Nội dung', name: 'Ảnh trái · nội dung phải', description: 'Bố cục giới thiệu phòng, không gian hoặc câu chuyện thương hiệu.',
            row: row('2-equal', [
                [element('image')],
                [element('heading', { text: 'Không gian để nghỉ chậm lại', level: 'h2', responsive: responsiveElement({ desktop: { visible:true, size:'xl', align:'inherit' }, tablet:{visible:true,size:'lg',align:'inherit'}, mobile:{visible:true,size:'md',align:'inherit'} }) }), element('text', { html: '<p>Viết một đoạn giới thiệu ngắn, rõ ràng và giàu cảm xúc về trải nghiệm khách sẽ nhận được.</p>' }), element('button', { text: 'Khám phá thêm', url: '#', style: 'primary' })]
            ], { theme: 'plain', gap: 'lg', paddingY: 'lg', mobileStack: 'stack' })
        },
        {
            id: 'content-left', category: 'Nội dung', name: 'Nội dung trái · ảnh phải', description: 'Biến thể cân bằng cho About, Story hoặc giới thiệu cơ sở.',
            row: row('2-equal', [
                [element('heading', { text: 'Một kỳ nghỉ riêng tư hơn', level: 'h2', responsive: responsiveElement({ desktop:{visible:true,size:'xl',align:'inherit'}, tablet:{visible:true,size:'lg',align:'inherit'}, mobile:{visible:true,size:'md',align:'inherit'} }) }), element('text', { html: '<p>Đưa thông điệp chính ở bên trái và dùng ảnh bên phải để tạo điểm nhấn thị giác.</p>' }), element('button', { text: 'Xem phòng', url: '/rooms', style: 'outline' })],
                [element('image')]
            ], { theme: 'soft', gap: 'lg', paddingY: 'lg', mobileStack: 'stack' })
        },
        {
            id: 'hero-centered', category: 'Hero', name: 'Hero nội dung trung tâm', description: 'Heading lớn, mô tả và CTA cho một section mở đầu.',
            row: row('single', [[
                element('heading', { text: 'Một nơi để nghỉ chậm lại', level: 'h2', align: 'center', responsive: responsiveElement({ desktop:{visible:true,size:'xxl',align:'center'}, tablet:{visible:true,size:'xl',align:'center'}, mobile:{visible:true,size:'lg',align:'center'} }) }),
                element('text', { html: '<p style="text-align:center">Tạo thông điệp ngắn gọn và dẫn khách tới hành động quan trọng nhất.</p>', responsive: responsiveElement({ desktop:{visible:true,size:'auto',align:'center'}, tablet:{visible:true,size:'auto',align:'center'}, mobile:{visible:true,size:'auto',align:'center'} }) }),
                element('button', { text: 'Đặt phòng', url: '/booking', style: 'primary', align: 'center', responsive: responsiveElement({ desktop:{visible:true,size:'auto',align:'center'}, tablet:{visible:true,size:'auto',align:'center'}, mobile:{visible:true,size:'auto',align:'center'} }) })
            ]], { theme: 'cream', desktopWidth: 'contained', tabletWidth: 'contained', paddingY: 'xl', paddingX: 'lg' })
        },
        {
            id: 'three-features', category: 'Tiện ích', name: '3 điểm nổi bật', description: 'Ba cột cho tiện ích, lợi ích hoặc lý do chọn homestay.',
            row: row('3-equal', [
                [element('heading', { text: 'Riêng tư', level: 'h3' }), element('text', { html: '<p>Không gian tách biệt, phù hợp cho nghỉ ngơi và thư giãn.</p>' })],
                [element('heading', { text: 'Rõ ràng', level: 'h3' }), element('text', { html: '<p>Thông tin phòng, giá và thời gian được trình bày minh bạch.</p>' })],
                [element('heading', { text: 'Thuận tiện', level: 'h3' }), element('text', { html: '<p>Đặt phòng trực tiếp và được nhân viên xác nhận nhanh chóng.</p>' })]
            ], { theme: 'soft', gap: 'md', paddingY: 'lg' })
        },
        {
            id: 'three-images', category: 'Hình ảnh', name: '3 ảnh ngang', description: 'Một hàng ba ảnh cho không gian, tiện nghi hoặc trải nghiệm.',
            row: row('3-equal', [[element('image')], [element('image')], [element('image')]], { theme: 'plain', gap: 'sm', paddingX: 'none', paddingY: 'sm', tabletGap: 'sm', mobileGap: 'sm' })
        },
        {
            id: 'cta-dark', category: 'CTA', name: 'CTA nền tối', description: 'Khối kêu gọi đặt phòng nổi bật gần cuối trang.',
            row: row('single', [[
                element('heading', { text: 'Sẵn sàng chọn phòng phù hợp?', level: 'h2', align: 'center', responsive: responsiveElement({ desktop:{visible:true,size:'xl',align:'center'}, tablet:{visible:true,size:'lg',align:'center'}, mobile:{visible:true,size:'md',align:'center'} }) }),
                element('text', { html: '<p style="text-align:center">Xem phòng còn trống và gửi yêu cầu đặt chỗ trực tiếp.</p>', responsive: responsiveElement({ desktop:{visible:true,size:'auto',align:'center'}, tablet:{visible:true,size:'auto',align:'center'}, mobile:{visible:true,size:'auto',align:'center'} }) }),
                element('button', { text: 'Đặt phòng ngay', url: '/booking', style: 'primary', align: 'center', responsive: responsiveElement({ desktop:{visible:true,size:'auto',align:'center'}, tablet:{visible:true,size:'auto',align:'center'}, mobile:{visible:true,size:'auto',align:'center'} }) })
            ]], { theme: 'dark', desktopWidth: 'contained', tabletWidth: 'contained', paddingY: 'xl', paddingX: 'lg' })
        },
        {
            id: 'story-quote', category: 'Nội dung', name: 'Story + trích dẫn', description: 'Câu chuyện thương hiệu và một đoạn quote tạo cảm xúc.',
            row: row('2-left', [
                [element('heading', { text: 'Câu chuyện của chúng tôi', level: 'h2' }), element('text', { html: '<p>Kể ngắn gọn vì sao không gian này được tạo ra và điều bạn muốn khách cảm nhận.</p>' })],
                [element('text', { html: '<blockquote>“Một nơi ở tốt không chỉ đẹp trong ảnh, mà còn khiến khách cảm thấy dễ chịu ngay khi bước vào.”</blockquote><p>Thêm một đoạn giải thích hoặc lời nhắn của chủ nhà.</p>' })]
            ], { theme: 'cream', gap: 'lg', paddingY: 'lg' })
        },
        {
            id: 'info-two-column', category: 'Tiện ích', name: 'Thông tin 2 cột', description: 'Phù hợp cho chính sách, giờ nhận phòng hoặc hướng dẫn.',
            row: row('2-equal', [
                [element('heading', { text: 'Nhận phòng', level: 'h3' }), element('text', { html: '<p>Ghi giờ nhận phòng, hướng dẫn check-in và những lưu ý quan trọng.</p>' })],
                [element('heading', { text: 'Trả phòng', level: 'h3' }), element('text', { html: '<p>Ghi giờ trả phòng, quy trình bàn giao và thông tin hỗ trợ.</p>' })]
            ], { theme: 'plain', gap: 'lg', paddingY: 'md' })
        },
        {
            id: 'promo-banner', category: 'CTA', name: 'Banner ưu đãi', description: 'Banner gọn cho chương trình theo mùa hoặc ưu đãi trực tiếp.',
            row: row('2-right', [
                [element('heading', { text: 'Ưu đãi đặt trực tiếp', level: 'h2' }), element('text', { html: '<p>Thêm nội dung ưu đãi hoặc quyền lợi khi khách đặt trực tiếp trên website.</p>' })],
                [element('button', { text: 'Xem phòng trống', url: '/booking', style: 'primary', align: 'center', responsive: responsiveElement({ desktop:{visible:true,size:'auto',align:'center'}, tablet:{visible:true,size:'auto',align:'center'}, mobile:{visible:true,size:'auto',align:'center'} }) })]
            ], { theme: 'soft', gap: 'md', align: 'center', paddingY: 'lg' })
        }
    ];

    function readUserTemplates() {
        const raw = storageGet(USER_TEMPLATES);
        if (!raw) return [];
        try {
            const parsed = JSON.parse(raw);
            return Array.isArray(parsed) ? parsed.filter(item => item && item.id && item.name && item.row).slice(0, MAX_USER_TEMPLATES) : [];
        } catch { return []; }
    }

    function writeUserTemplates(items) {
        return storageSet(USER_TEMPLATES, JSON.stringify(items.slice(0, MAX_USER_TEMPLATES)));
    }

    function cleanTemplateRow(input) {
        const value = clone(input || {});
        if (value && typeof value === 'object') value.html = '';
        return value;
    }

    function waitFor(selector, timeout) {
        const existing = document.querySelector(selector);
        if (existing) return Promise.resolve(existing);
        return new Promise((resolve, reject) => {
            const observer = new MutationObserver(() => {
                const node = document.querySelector(selector);
                if (!node) return;
                observer.disconnect(); clearTimeout(timer); resolve(node);
            });
            observer.observe(document.body, { childList: true, subtree: true });
            const timer = setTimeout(() => { observer.disconnect(); reject(new Error('Không mở được Row Builder.')); }, timeout || 4000);
        });
    }

    function miniPreview(rowData) {
        const layout = rowData?.layout || 'single';
        const columns = Array.isArray(rowData?.columns) ? rowData.columns : [];
        return `<div class="pve-template-mini pve-template-mini-${h(layout)}">${columns.map(column => `<div>${(column.elements || []).slice(0, 4).map(item => `<span class="kind-${h(item.kind || 'text')}">${item.kind === 'image' ? 'IMG' : item.kind === 'button' ? 'BTN' : item.kind === 'heading' ? 'H' : 'TXT'}</span>`).join('')}</div>`).join('')}</div>`;
    }

    class RowTemplateLibrary {
        constructor() {
            this.modal = null;
            this.category = 'Tất cả';
            this.query = '';
            this.observer = new MutationObserver(() => this.enhance());
        }

        mount() {
            this.observer.observe(document.body, { childList: true, subtree: true });
            this.enhance();
        }

        enhance() {
            this.enhanceToolbar();
            this.enhanceBuilder();
        }

        enhanceToolbar() {
            const actions = document.querySelector('.pve-toolbar-actions');
            if (!actions || actions.querySelector('[data-row-template-library]')) return;
            const button = document.createElement('button');
            button.type = 'button'; button.dataset.rowTemplateLibrary = '1'; button.textContent = '▦ Mẫu Row';
            const rowButton = actions.querySelector('[data-row-builder-add]');
            if (rowButton) rowButton.after(button); else actions.prepend(button);
            button.addEventListener('click', () => this.open());
        }

        enhanceBuilder() {
            const drawer = document.querySelector('.pve-row-builder-drawer');
            const tools = drawer?.querySelector('.pve-row-header-tools');
            if (!tools || tools.querySelector('[data-row-template-open]')) return;

            const open = document.createElement('button');
            open.type = 'button'; open.dataset.rowTemplateOpen = '1'; open.textContent = 'Thư viện mẫu';
            const save = document.createElement('button');
            save.type = 'button'; save.dataset.rowTemplateSave = '1'; save.textContent = 'Lưu mẫu';
            const copy = tools.querySelector('[data-row-copy]');
            if (copy) { tools.insertBefore(open, copy); tools.insertBefore(save, copy); }
            else { tools.prepend(save); tools.prepend(open); }
            open.addEventListener('click', () => this.open());
            save.addEventListener('click', () => this.saveCurrent());
        }

        allTemplates() {
            return [...SYSTEM_TEMPLATES.map(item => Object.assign({ source: 'system' }, item)), ...readUserTemplates().map(item => Object.assign({ source: 'user', category: 'Của tôi' }, item))];
        }

        open() {
            this.close();
            const modal = document.createElement('section');
            modal.className = 'pve-template-library';
            modal.setAttribute('role', 'dialog'); modal.setAttribute('aria-modal', 'true');
            modal.innerHTML = `<div class="pve-template-dialog">
                <header><div><small>ROW TEMPLATE LIBRARY</small><h2>Chọn bố cục có sẵn</h2><p>Dùng mẫu để dựng nhanh rồi chỉnh lại bằng Row Builder.</p></div><button type="button" data-template-close aria-label="Đóng">×</button></header>
                <div class="pve-template-tools"><input type="search" data-template-search placeholder="Tìm mẫu: hero, ảnh, CTA…"><div data-template-filters></div></div>
                <div class="pve-template-body" data-template-body></div>
                <footer><span><strong>Mẫu hệ thống</strong> luôn có sẵn. <strong>Mẫu của tôi</strong> được lưu trên trình duyệt này.</span><button type="button" data-template-blank>＋ Row trống</button></footer>
            </div>`;
            document.body.appendChild(modal); this.modal = modal;
            modal.querySelector('[data-template-close]').addEventListener('click', () => this.close());
            modal.addEventListener('click', event => { if (event.target === modal) this.close(); });
            modal.querySelector('[data-template-search]').addEventListener('input', event => { this.query = event.target.value.trim().toLowerCase(); this.render(); });
            modal.querySelector('[data-template-blank]').addEventListener('click', () => { this.close(); document.querySelector('[data-row-builder-add]')?.click(); });
            this.render();
        }

        render() {
            if (!this.modal) return;
            const categories = ['Tất cả', ...new Set(SYSTEM_TEMPLATES.map(item => item.category)), 'Của tôi'];
            const filters = this.modal.querySelector('[data-template-filters]');
            filters.innerHTML = categories.map(category => `<button type="button" data-template-filter="${h(category)}" class="${category === this.category ? 'active' : ''}">${h(category)}</button>`).join('');
            filters.querySelectorAll('[data-template-filter]').forEach(button => button.addEventListener('click', () => { this.category = button.dataset.templateFilter; this.render(); }));

            const items = this.allTemplates().filter(item => {
                const categoryOk = this.category === 'Tất cả' || (this.category === 'Của tôi' ? item.source === 'user' : item.category === this.category);
                const queryOk = !this.query || `${item.name} ${item.description || ''} ${item.category || ''}`.toLowerCase().includes(this.query);
                return categoryOk && queryOk;
            });
            const body = this.modal.querySelector('[data-template-body]');
            if (!items.length) {
                body.innerHTML = '<div class="pve-template-empty"><strong>Không có mẫu phù hợp.</strong><span>Thử từ khóa khác hoặc lưu Row hiện tại thành mẫu của bạn.</span></div>';
                return;
            }
            body.innerHTML = `<div class="pve-template-grid">${items.map(item => `<article class="pve-template-card" data-template-id="${h(item.id)}" data-template-source="${h(item.source)}">
                <div class="pve-template-preview">${miniPreview(item.row)}<span>${h(item.category || 'Mẫu')}</span></div>
                <div class="pve-template-copy"><strong>${h(item.name)}</strong><p>${h(item.description || 'Mẫu Row đã lưu.')}</p></div>
                <div class="pve-template-actions"><button type="button" class="primary" data-template-use>Dùng mẫu</button>${item.source === 'user' ? '<button type="button" data-template-delete>Xóa</button>' : ''}</div>
            </article>`).join('')}</div>`;
            body.querySelectorAll('.pve-template-card').forEach(card => {
                const item = items.find(entry => entry.id === card.dataset.templateId && entry.source === card.dataset.templateSource);
                card.querySelector('[data-template-use]')?.addEventListener('click', () => this.apply(item));
                card.querySelector('[data-template-delete]')?.addEventListener('click', () => this.deleteUser(item));
            });
        }

        async apply(template) {
            if (!template?.row) return;
            if (!sessionSet(ROW_CLIPBOARD, JSON.stringify(cleanTemplateRow(template.row)))) return this.toast('Không thể chuẩn bị mẫu Row.', true);
            this.close();
            let drawer = document.querySelector('.pve-row-builder-drawer');
            if (!drawer) {
                const add = document.querySelector('[data-row-builder-add]');
                if (!add) return this.toast('Hãy bật chế độ Chỉnh sửa trang trước.', true);
                add.click();
                try { drawer = await waitFor('.pve-row-builder-drawer', 4500); }
                catch (error) { return this.toast(error.message, true); }
            }
            const nameInput = drawer.querySelector('[name="rowName"]');
            if (nameInput && !nameInput.value.trim().startsWith('Row -')) nameInput.value = `Row - ${template.name}`.slice(0, 120);
            const paste = drawer.querySelector('[data-row-paste]');
            if (!paste) return this.toast('Row Builder chưa sẵn sàng để nhận mẫu.', true);
            paste.disabled = false;
            paste.click();
        }

        saveCurrent() {
            const drawer = document.querySelector('.pve-row-builder-drawer');
            const copyButton = drawer?.querySelector('[data-row-copy]');
            if (!drawer || !copyButton) return this.toast('Mở một Row rồi bấm “Lưu mẫu”.', true);
            copyButton.click();
            const raw = sessionGet(ROW_CLIPBOARD);
            if (!raw) return this.toast('Không đọc được Row hiện tại.', true);
            let data;
            try { data = cleanTemplateRow(JSON.parse(raw)); } catch { return this.toast('Dữ liệu Row hiện tại không hợp lệ.', true); }
            const defaultName = (drawer.querySelector('[name="rowName"]')?.value || 'Mẫu Row').replace(/^Row\s*-?\s*/i, '').trim() || 'Mẫu Row';
            const name = prompt('Tên mẫu để dùng lại:', defaultName);
            if (!name?.trim()) return;
            const items = readUserTemplates();
            items.unshift({ id: id('custom'), name: name.trim().slice(0, 80), description: 'Mẫu Row do bạn lưu.', createdAt: new Date().toISOString(), row: data });
            if (!writeUserTemplates(items)) return this.toast('Không thể lưu mẫu trên trình duyệt này.', true);
            this.toast('Đã lưu Row vào “Mẫu của tôi”.');
        }

        deleteUser(template) {
            if (!template || template.source !== 'user') return;
            if (!confirm(`Xóa mẫu “${template.name}”?`)) return;
            const items = readUserTemplates().filter(item => item.id !== template.id);
            writeUserTemplates(items); this.render();
        }

        close() { this.modal?.remove(); this.modal = null; }

        toast(message, error) {
            let node = document.querySelector('.pve-toast');
            if (!node) { node = document.createElement('div'); node.className = 'pve-toast'; document.body.appendChild(node); }
            node.className = `pve-toast ${error ? 'error' : 'success'}`; node.textContent = message; node.hidden = false;
            clearTimeout(node._templateTimer); node._templateTimer = setTimeout(() => { node.hidden = true; }, 3200);
        }
    }

    new RowTemplateLibrary().mount();
})();
