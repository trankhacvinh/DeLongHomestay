(function () {
    if (!document.body?.classList.contains('public-body') || !window.DeLongApi) return;

    const pagePath = window.location.pathname.replace(/\/+$/, '') || '/';
    const scoped = pagePath.match(/^\/h\/([^/]+)(?:\/|$)/i);
    const siteSlug = scoped ? decodeURIComponent(scoped[1]) : '';
    const roomMatch = pagePath.match(/\/rooms\/([^/]+)$/i);
    const roomSlug = roomMatch ? decodeURIComponent(roomMatch[1]) : '';
    const contextUrl = `/api/admin/site/visual-context${siteSlug ? `?siteSlug=${encodeURIComponent(siteSlug)}` : ''}`;

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

    function openDrawer(title, eyebrow, body, footer) {
        document.querySelector('.pve-context-drawer')?.remove();
        const drawer = document.createElement('section');
        drawer.className = 'pve-context-drawer pve-shell-rates-drawer';
        drawer.setAttribute('role', 'dialog');
        drawer.setAttribute('aria-modal', 'true');
        drawer.innerHTML = `<header><div><small>${h(eyebrow)}</small><h2>${h(title)}</h2></div><button type="button" data-enh-close aria-label="Đóng">×</button></header><div class="pve-context-drawer-body">${body}</div>${footer || ''}`;
        document.body.appendChild(drawer);
        document.body.classList.add('pve-context-drawer-open');
        const close = () => { drawer.remove(); document.body.classList.remove('pve-context-drawer-open'); };
        drawer.querySelector('[data-enh-close]').addEventListener('click', close);
        drawer._close = close;
        return drawer;
    }

    function addTarget(host, label, handler) {
        if (!host || host.querySelector(':scope > [data-shell-rates-target]')) return;
        host.classList.add('pve-context-host');
        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'pve-context-target pve-shell-rates-target';
        button.dataset.shellRatesTarget = '1';
        button.textContent = `✎ ${label}`;
        button.addEventListener('click', event => {
            event.preventDefault();
            event.stopPropagation();
            handler();
        });
        host.prepend(button);
    }

    class ShellRatesEditor {
        constructor(context) {
            this.context = context;
            this.propertyId = context.propertyId || null;
            this.roomRef = null;
            this.roomContent = null;
            this.roomAdmin = null;
            this.roomContentApi = '';
            this.roomAdminApi = '';
            this.rateApi = '';
        }

        async mount() {
            if (!this.context?.canEdit) return;
            if (roomSlug) await this.resolveRoom();
            this.mountTargets();
            this.mountToolbarAction();
        }

        mountTargets() {
            addTarget(document.querySelector('.public-site-links'), 'Menu', () => this.openShellEditor('menu'));
            addTarget(document.querySelector('.public-footer-bottom'), 'Footer', () => this.openShellEditor('footer'));
            if (this.roomRef) {
                addTarget(document.querySelector('.public-room-rate-block'), 'Giá & thời gian', () => this.openRateEditor());
                addTarget(document.querySelector('.public-room-booking-card'), 'Giá đặt phòng', () => this.openRateEditor());
            }
        }

        mountToolbarAction() {
            const tryMount = () => {
                const toolbar = document.querySelector('.pve-toolbar, .pve-context-toolbar');
                if (!toolbar || toolbar.querySelector('[data-shell-rates-toolbar]')) return !!toolbar;
                const actions = toolbar.querySelector('.pve-toolbar-actions, .pve-context-actions') || toolbar;
                const button = document.createElement('button');
                button.type = 'button';
                button.dataset.shellRatesToolbar = '1';
                button.textContent = 'Menu / Footer';
                button.addEventListener('click', () => this.openShellEditor('menu'));
                const admin = actions.querySelector('a[href*="/admin"]');
                if (admin) actions.insertBefore(button, admin); else actions.appendChild(button);
                if (this.roomRef && !actions.querySelector('[data-shell-rates-rate-toolbar]')) {
                    const rate = document.createElement('button');
                    rate.type = 'button';
                    rate.dataset.shellRatesRateToolbar = '1';
                    rate.textContent = 'Giá phòng';
                    rate.addEventListener('click', () => this.openRateEditor());
                    if (admin) actions.insertBefore(rate, admin); else actions.appendChild(rate);
                }
                return true;
            };

            if (tryMount()) return;
            const observer = new MutationObserver(() => {
                if (tryMount()) observer.disconnect();
            });
            observer.observe(document.body, { childList: true, subtree: true });
            setTimeout(() => observer.disconnect(), 5000);
        }

        shellApi() {
            return this.context.scope === 'property' && this.propertyId
                ? `/api/admin/properties/${this.propertyId}/site/shell`
                : '/api/admin/site/global/shell';
        }

        async openShellEditor(initialTab) {
            try {
                const shell = await DeLongApi.get(this.shellApi());
                const order = Array.isArray(shell.navigationOrder) ? [...shell.navigationOrder] : ['home', 'rooms', 'branches', 'booking', 'lookup'];
                const labels = {
                    home: shell.homeLabel,
                    rooms: shell.roomsLabel,
                    branches: shell.branchesLabel,
                    booking: shell.bookingLabel,
                    lookup: shell.lookupLabel
                };
                const names = { home: 'Trang chủ', rooms: 'Phòng', branches: 'Cơ sở', booking: 'Đặt phòng', lookup: 'Tra cứu' };
                const navRows = order.map((key, index) => `<div class="pve-shell-nav-row" data-nav-key="${h(key)}">
                    <span class="pve-shell-nav-handle">${index + 1}</span>
                    <label><span>${h(names[key] || key)}</span><input data-nav-label value="${h(labels[key] || '')}"></label>
                    <div class="pve-shell-nav-actions"><button type="button" data-nav-up ${index === 0 ? 'disabled' : ''}>↑</button><button type="button" data-nav-down ${index === order.length - 1 ? 'disabled' : ''}>↓</button></div>
                </div>`).join('');

                const body = `<div class="pve-shell-editor" data-shell-tab="${h(initialTab || 'menu')}">
                    <nav class="pve-room-tabs"><button type="button" data-shell-tab-button="menu" class="${initialTab !== 'footer' ? 'active' : ''}">Menu & CTA</button><button type="button" data-shell-tab-button="footer" class="${initialTab === 'footer' ? 'active' : ''}">Footer</button></nav>
                    <section data-shell-panel="menu" ${initialTab === 'footer' ? 'hidden' : ''}>
                        <div class="pve-context-form">
                            <p class="pve-context-note">Bạn có thể đổi tên và thứ tự menu. Đường dẫn Đặt phòng / Tra cứu vẫn do hệ thống quản lý để không phá luồng đặt phòng.</p>
                            <label><span>Chữ nút CTA trên Header</span><input name="headerCtaText" value="${h(shell.headerCtaText)}"></label>
                            <div class="pve-shell-nav-list">${navRows}</div>
                            <div class="pve-shell-toggle-grid">
                                <label class="pve-context-check"><input type="checkbox" name="showHome" ${shell.showHome !== false ? 'checked' : ''}><span>Hiện Trang chủ</span></label>
                                <label class="pve-context-check"><input type="checkbox" name="showRooms" ${shell.showRooms !== false ? 'checked' : ''}><span>Hiện Phòng</span></label>
                                <label class="pve-context-check"><input type="checkbox" name="showBranches" ${shell.showBranches !== false ? 'checked' : ''}><span>Hiện Cơ sở khi có nhiều cơ sở</span></label>
                            </div>
                        </div>
                    </section>
                    <section data-shell-panel="footer" ${initialTab !== 'footer' ? 'hidden' : ''}>
                        <div class="pve-context-form">
                            <label><span>Mô tả dưới thương hiệu</span><textarea name="footerIntro" rows="4">${h(shell.footerIntro)}</textarea></label>
                            <div class="pve-context-grid-2"><label><span>Chữ link đặt phòng</span><input name="footerBookingText" value="${h(shell.footerBookingText)}"></label><label><span>Tiêu đề Khám phá</span><input name="footerExploreTitle" value="${h(shell.footerExploreTitle)}"></label></div>
                            <div class="pve-context-grid-2"><label><span>Tiêu đề Cơ sở</span><input name="footerBranchesTitle" value="${h(shell.footerBranchesTitle)}"></label><label><span>Tiêu đề Liên hệ</span><input name="footerContactTitle" value="${h(shell.footerContactTitle)}"></label></div>
                            <label><span>Dòng cuối Footer</span><input name="footerBottomText" value="${h(shell.footerBottomText)}"></label>
                            <label class="pve-context-check"><input type="checkbox" name="showFooterContact" ${shell.showFooterContact !== false ? 'checked' : ''}><span>Hiện cột Liên hệ khi cơ sở có thông tin</span></label>
                        </div>
                    </section>
                </div>`;
                const footer = '<footer><span>Lưu xong trang sẽ tải lại để bạn thấy đúng giao diện khách.</span><button type="button" data-shell-cancel>Hủy</button><button type="button" class="primary" data-shell-save>Lưu Menu / Footer</button></footer>';
                const drawer = openDrawer('Menu, CTA & Footer', this.context.propertyName || 'WEBSITE', body, footer);
                drawer.querySelector('[data-shell-cancel]').addEventListener('click', () => drawer._close());
                drawer.querySelectorAll('[data-shell-tab-button]').forEach(button => button.addEventListener('click', () => {
                    const tab = button.dataset.shellTabButton;
                    drawer.querySelectorAll('[data-shell-tab-button]').forEach(x => x.classList.toggle('active', x === button));
                    drawer.querySelectorAll('[data-shell-panel]').forEach(panel => { panel.hidden = panel.dataset.shellPanel !== tab; });
                }));
                this.bindNavigationOrder(drawer);
                drawer.querySelector('[data-shell-save]').addEventListener('click', () => this.saveShell(drawer, shell));
            } catch (error) {
                toast(error.message || 'Không thể tải cấu hình Menu / Footer.', true);
            }
        }

        bindNavigationOrder(drawer) {
            const list = drawer.querySelector('.pve-shell-nav-list');
            const refresh = () => {
                const rows = [...list.querySelectorAll('[data-nav-key]')];
                rows.forEach((row, index) => {
                    row.querySelector('.pve-shell-nav-handle').textContent = index + 1;
                    row.querySelector('[data-nav-up]').disabled = index === 0;
                    row.querySelector('[data-nav-down]').disabled = index === rows.length - 1;
                });
            };
            list.addEventListener('click', event => {
                const row = event.target.closest('[data-nav-key]');
                if (!row) return;
                if (event.target.closest('[data-nav-up]') && row.previousElementSibling) list.insertBefore(row, row.previousElementSibling);
                if (event.target.closest('[data-nav-down]') && row.nextElementSibling) list.insertBefore(row.nextElementSibling, row);
                refresh();
            });
        }

        shellPayload(drawer, original) {
            const rows = [...drawer.querySelectorAll('[data-nav-key]')];
            const labels = Object.fromEntries(rows.map(row => [row.dataset.navKey, row.querySelector('[data-nav-label]').value.trim()]));
            const value = name => drawer.querySelector(`[name="${name}"]`)?.value.trim() || '';
            const checked = name => !!drawer.querySelector(`[name="${name}"]`)?.checked;
            return {
                homeLabel: labels.home || original.homeLabel,
                roomsLabel: labels.rooms || original.roomsLabel,
                branchesLabel: labels.branches || original.branchesLabel,
                bookingLabel: labels.booking || original.bookingLabel,
                lookupLabel: labels.lookup || original.lookupLabel,
                headerCtaText: value('headerCtaText') || original.headerCtaText,
                navigationOrder: rows.map(row => row.dataset.navKey),
                showHome: checked('showHome'),
                showRooms: checked('showRooms'),
                showBranches: checked('showBranches'),
                footerIntro: value('footerIntro') || original.footerIntro,
                footerBookingText: value('footerBookingText') || original.footerBookingText,
                footerExploreTitle: value('footerExploreTitle') || original.footerExploreTitle,
                footerBranchesTitle: value('footerBranchesTitle') || original.footerBranchesTitle,
                footerContactTitle: value('footerContactTitle') || original.footerContactTitle,
                footerBottomText: value('footerBottomText') || original.footerBottomText,
                showFooterContact: checked('showFooterContact')
            };
        }

        async saveShell(drawer, original) {
            const button = drawer.querySelector('[data-shell-save]');
            button.disabled = true;
            button.textContent = 'Đang lưu…';
            try {
                await DeLongApi.put(this.shellApi(), this.shellPayload(drawer, original));
                remember();
                location.reload();
            } catch (error) {
                button.disabled = false;
                button.textContent = 'Lưu Menu / Footer';
                toast(error.message || 'Không thể lưu Menu / Footer.', true);
            }
        }

        async resolveRoom() {
            for (const candidate of Array.isArray(this.context.rooms) ? this.context.rooms : []) {
                try {
                    const contentApi = `/api/admin/properties/${candidate.propertyId}/rooms/${candidate.id}/content`;
                    const content = await DeLongApi.get(`${contentApi}/`);
                    if (String(content?.slug || '').toLowerCase() !== roomSlug.toLowerCase()) continue;
                    this.propertyId = candidate.propertyId;
                    this.roomRef = candidate;
                    this.roomContent = content;
                    this.roomContentApi = contentApi;
                    this.roomAdminApi = `/api/admin/properties/${candidate.propertyId}/rooms/${candidate.id}`;
                    this.rateApi = `/api/admin/properties/${candidate.propertyId}/rooms/${candidate.id}/rates`;
                    this.roomAdmin = await DeLongApi.get(this.roomAdminApi);
                    return;
                } catch (error) {
                    if (error?.status === 403) continue;
                }
            }
        }

        async refreshRoomAdmin() {
            if (!this.roomAdminApi) return null;
            this.roomAdmin = await DeLongApi.get(this.roomAdminApi);
            return this.roomAdmin;
        }

        rateTypeOptions(selected) {
            return [[0, 'Khung giờ'], [1, 'Qua đêm'], [2, 'Theo đêm']]
                .map(([value, text]) => `<option value="${value}" ${Number(selected) === value ? 'selected' : ''}>${text}</option>`).join('');
        }

        rateCard(rate, index) {
            return `<article class="pve-rate-card ${rate.isActive ? '' : 'is-inactive'}" data-rate-id="${h(rate.id)}">
                <div class="pve-rate-card-head"><div><small>${rate.isActive ? 'ĐANG HOẠT ĐỘNG' : 'ĐÃ NGỪNG'}</small><strong>${h(rate.name)}</strong></div><button type="button" class="danger" data-rate-archive ${rate.isActive ? '' : 'disabled'}>Ngừng</button></div>
                <div class="pve-rate-grid">
                    <label><span>Tên giá</span><input data-rate-name value="${h(rate.name)}"></label>
                    <label><span>Loại</span><select data-rate-type>${this.rateTypeOptions(rate.type)}</select></label>
                    <label><span>Bắt đầu / nhận</span><input type="time" data-rate-start value="${h(rate.startTime)}"></label>
                    <label><span>Kết thúc / trả</span><input type="time" data-rate-end value="${h(rate.endTime)}"></label>
                    <label><span>Giá (đ)</span><input type="number" min="0" step="1000" data-rate-price value="${h(rate.price)}"></label>
                    <label><span>Thứ tự</span><input type="number" data-rate-order value="${h(rate.sortOrder ?? index)}"></label>
                </div>
                <label class="pve-context-check pve-rate-active"><input type="checkbox" data-rate-active ${rate.isActive ? 'checked' : ''}><span>Đang hoạt động</span></label>
            </article>`;
        }

        async openRateEditor() {
            if (!this.roomRef || !this.rateApi) return toast('Không xác định được phòng cần chỉnh giá.', true);
            try {
                await this.refreshRoomAdmin();
                this.renderRateDrawer();
            } catch (error) {
                toast(error.message || 'Không thể tải giá phòng.', true);
            }
        }

        renderRateDrawer() {
            const rates = [...(this.roomAdmin?.rates || [])].sort((a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0));
            const nextOrder = rates.length ? Math.max(...rates.map(x => Number(x.sortOrder || 0))) + 1 : 0;
            const body = `<div class="pve-rate-editor">
                <p class="pve-context-note">Giá public, availability và booking đều dùng chính các khung giá này. Mỗi phòng chỉ được có một giá <b>Theo đêm</b> đang hoạt động.</p>
                <div class="pve-rate-list">${rates.map((rate, index) => this.rateCard(rate, index)).join('') || '<div class="pve-room-image-empty">Phòng chưa có khung giá.</div>'}</div>
                <section class="pve-rate-new">
                    <div class="pve-rate-card-head"><div><small>THÊM MỚI</small><strong>Khung giá mới</strong></div></div>
                    <div class="pve-rate-grid">
                        <label><span>Tên giá</span><input data-new-name placeholder="Ví dụ: 2 giờ"></label>
                        <label><span>Loại</span><select data-new-type>${this.rateTypeOptions(0)}</select></label>
                        <label><span>Bắt đầu / nhận</span><input type="time" data-new-start value="08:00"></label>
                        <label><span>Kết thúc / trả</span><input type="time" data-new-end value="10:00"></label>
                        <label><span>Giá (đ)</span><input type="number" min="0" step="1000" data-new-price value="0"></label>
                        <label><span>Thứ tự</span><input type="number" data-new-order value="${nextOrder}"></label>
                    </div>
                    <small>Để trống tên nếu bạn không muốn tạo khung giá mới trong lần lưu này.</small>
                </section>
            </div>`;
            const footer = '<footer><span>Lưu xong trang sẽ tải lại để giá public cập nhật ngay.</span><button type="button" data-rate-cancel>Hủy</button><button type="button" class="primary" data-rate-save-all>Lưu tất cả</button></footer>';
            const drawer = openDrawer(`Giá · ${this.roomContent?.name || this.roomAdmin?.name || 'Phòng'}`, 'GIÁ & THỜI GIAN', body, footer);
            drawer.classList.add('pve-context-drawer-room');
            drawer.querySelector('[data-rate-cancel]').addEventListener('click', () => drawer._close());
            drawer.querySelector('[data-rate-save-all]').addEventListener('click', () => this.saveAllRates(drawer));
            drawer.querySelectorAll('[data-rate-id]').forEach(card => {
                card.querySelector('[data-rate-archive]')?.addEventListener('click', () => this.archiveRate(card.dataset.rateId));
            });
        }

        readExistingRate(card) {
            return {
                name: card.querySelector('[data-rate-name]').value.trim(),
                type: Number(card.querySelector('[data-rate-type]').value),
                startTime: card.querySelector('[data-rate-start]').value,
                endTime: card.querySelector('[data-rate-end]').value,
                price: Number(card.querySelector('[data-rate-price]').value || 0),
                sortOrder: Number(card.querySelector('[data-rate-order]').value || 0),
                isActive: card.querySelector('[data-rate-active]').checked
            };
        }

        readNewRate(drawer) {
            return {
                name: drawer.querySelector('[data-new-name]').value.trim(),
                type: Number(drawer.querySelector('[data-new-type]').value),
                startTime: drawer.querySelector('[data-new-start]').value,
                endTime: drawer.querySelector('[data-new-end]').value,
                price: Number(drawer.querySelector('[data-new-price]').value || 0),
                sortOrder: Number(drawer.querySelector('[data-new-order]').value || 0)
            };
        }

        async saveAllRates(drawer) {
            const button = drawer.querySelector('[data-rate-save-all]');
            button.disabled = true;
            button.textContent = 'Đang lưu…';
            try {
                for (const card of drawer.querySelectorAll('[data-rate-id]')) {
                    await DeLongApi.put(`${this.rateApi}/${card.dataset.rateId}`, this.readExistingRate(card));
                }
                const fresh = this.readNewRate(drawer);
                if (fresh.name) await DeLongApi.post(`${this.rateApi}/`, fresh);
                remember();
                location.reload();
            } catch (error) {
                button.disabled = false;
                button.textContent = 'Lưu tất cả';
                toast(error.message || 'Không thể lưu giá phòng.', true);
            }
        }

        async archiveRate(rateId) {
            if (!confirm('Ngừng khung giá này? Booking lịch sử vẫn được giữ nguyên.')) return;
            try {
                await DeLongApi.delete(`${this.rateApi}/${rateId}`);
                await this.refreshRoomAdmin();
                this.renderRateDrawer();
                toast('Đã ngừng khung giá.');
            } catch (error) {
                toast(error.message || 'Không thể ngừng khung giá.', true);
            }
        }
    }

    DeLongApi.get(contextUrl).then(context => new ShellRatesEditor(context).mount()).catch(() => {
        // Guest or roles without editor permission see the normal public website only.
    });
})();
