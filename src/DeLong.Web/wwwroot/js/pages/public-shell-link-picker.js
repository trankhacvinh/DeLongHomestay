(function () {
    if (!document.body?.classList.contains('public-body') || !window.DeLongApi) return;

    const pagePath = window.location.pathname.replace(/\/+$/, '') || '/';
    const scoped = pagePath.match(/^\/h\/([^/]+)(?:\/|$)/i);
    const siteSlug = scoped ? decodeURIComponent(scoped[1]) : '';
    const contextUrl = `/api/admin/site/visual-context${siteSlug ? `?siteSlug=${encodeURIComponent(siteSlug)}` : ''}`;

    const systemLinks = [
        { token: '@home', label: 'Trang chủ', icon: '⌂' },
        { token: '@rooms', label: 'Danh sách phòng', icon: '▦' },
        { token: '@booking', label: 'Đặt phòng', icon: '◷' },
        { token: '@lookup', label: 'Tra cứu đặt phòng', icon: '⌕' },
        { token: '@branches', label: 'Cơ sở', icon: '⌖' }
    ];

    const categoryLabels = {
        all: 'Tất cả',
        system: 'Trang hệ thống',
        room: 'Phòng',
        post: 'Bài viết',
        anchor: 'Trong trang'
    };

    let contextPromise = null;
    let dynamicPromise = null;
    let cachedDestinations = baseDestinations();
    let dialog = null;
    let active = null;
    let activeCategory = 'all';

    function h(value) {
        return String(value ?? '').replace(/[&<>'"]/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[ch]));
    }

    function homeUrl(slug) { return slug ? `/h/${encodeURIComponent(slug)}` : '/'; }

    function resolveToken(token) {
        const prefix = siteSlug ? `/h/${encodeURIComponent(siteSlug)}` : '';
        switch (String(token || '').toLowerCase()) {
            case '@home': return prefix || '/';
            case '@rooms': return prefix ? `${prefix}/rooms` : '/rooms';
            case '@branches': return '/#co-so';
            case '@booking': return prefix ? `${prefix}/booking` : '/booking';
            case '@lookup': return prefix ? `${prefix}/booking/lookup` : '/booking/lookup';
            default: return String(token || '');
        }
    }

    function baseDestinations() {
        const currentHome = homeUrl(siteSlug);
        const blogUrl = currentHome === '/' ? '/blog' : `${currentHome}/blog`;
        const galleryUrl = currentHome === '/' ? '/#gallery' : `${currentHome}#gallery`;
        return [
            ...systemLinks.map(link => ({
                kind: 'system',
                label: link.label,
                detail: resolveToken(link.token),
                url: resolveToken(link.token),
                source: link.token,
                icon: link.icon
            })),
            { kind: 'system', label: 'Blog', detail: blogUrl, url: blogUrl, source: blogUrl, icon: '✎' },
            { kind: 'anchor', label: 'Gallery trên trang chủ', detail: galleryUrl, url: galleryUrl, source: galleryUrl, icon: '▧' }
        ];
    }

    function defaultTokenFor(target, input) {
        if (target?.matches?.('[data-nav-id]')) {
            const id = String(target.dataset.navId || '').toLowerCase();
            if (['home', 'rooms', 'branches', 'booking', 'lookup'].includes(id)) return `@${id}`;
        }
        if (input?.name === 'headerCtaUrl' || input?.name === 'footerBookingUrl') return '@booking';
        return '';
    }

    function sourceValue(target) {
        return target?.matches?.('[data-nav-id]') ? target.dataset.navSourceUrl || '' : target?.dataset?.tokenSource || '';
    }

    function setSourceValue(target, value) {
        if (target?.matches?.('[data-nav-id]')) target.dataset.navSourceUrl = value;
        else if (target?.dataset) target.dataset.tokenSource = value;
    }

    function safePreviewUrl(value) {
        const raw = String(value || '').trim();
        if (!raw || /^(javascript|data|vbscript):/i.test(raw)) return '';
        try {
            const url = new URL(raw, window.location.origin);
            if (!['http:', 'https:', 'mailto:', 'tel:'].includes(url.protocol)) return '';
            return url.href;
        } catch { return ''; }
    }

    function getContext() {
        if (!contextPromise) contextPromise = DeLongApi.get(contextUrl).catch(() => null);
        return contextPromise;
    }

    async function resolveRoomDestination(room) {
        const propertyId = room?.propertyId;
        const propertySlug = room?.propertySiteSlug || siteSlug;
        if (!propertyId || !room?.id) return null;
        let slug = room.slug || '';
        if (!slug) {
            try { slug = (await DeLongApi.get(`/api/admin/properties/${propertyId}/rooms/${room.id}/content/`))?.slug || ''; }
            catch { return null; }
        }
        if (!slug || !propertySlug) return null;
        const url = `/h/${encodeURIComponent(propertySlug)}/rooms/${encodeURIComponent(slug)}`;
        return {
            kind: 'room',
            label: room.name || room.code || 'Phòng',
            detail: `${room.propertyName || ''}${room.code ? ` · ${room.code}` : ''}`.replace(/^ · | · $/g, ''),
            url,
            source: url,
            icon: '▣'
        };
    }

    function loadDynamicDestinations() {
        if (dynamicPromise) return dynamicPromise;
        dynamicPromise = (async () => {
            const context = await getContext();
            if (!context) return [];
            const extras = [];

            const rooms = Array.isArray(context.rooms) ? context.rooms.slice(0, 100) : [];
            const roomResults = await Promise.allSettled(rooms.map(resolveRoomDestination));
            roomResults.forEach(result => {
                if (result.status === 'fulfilled' && result.value) extras.push(result.value);
            });

            try {
                const editorial = context.scope === 'property' && context.propertyId
                    ? await DeLongApi.get(`/api/admin/properties/${context.propertyId}/editorial/`)
                    : context.scope === 'global' ? await DeLongApi.get('/api/admin/site/global/editorial/') : null;
                (Array.isArray(editorial?.posts) ? editorial.posts : [])
                    .filter(post => post.isPublished !== false)
                    .slice(0, 100)
                    .forEach(post => {
                        const propertySlug = post.propertySiteSlug || siteSlug;
                        if (!propertySlug || !post.slug) return;
                        const url = `/h/${encodeURIComponent(propertySlug)}/blog/${encodeURIComponent(post.slug)}`;
                        extras.push({
                            kind: 'post',
                            label: post.title || 'Bài viết',
                            detail: post.propertyName || '',
                            url,
                            source: url,
                            icon: '¶'
                        });
                    });
            } catch { }
            return extras;
        })();
        return dynamicPromise;
    }

    function destinationLabel(kind) {
        return categoryLabels[kind] || 'Khác';
    }

    function findDestination(target, input) {
        const source = sourceValue(target);
        const value = input?.value?.trim() || '';
        return cachedDestinations.find(item => item.source === source && item.url === value)
            || cachedDestinations.find(item => item.url === value)
            || null;
    }

    function statusInfo(target, input) {
        const source = sourceValue(target);
        const value = input?.value?.trim() || '';
        const found = findDestination(target, input);
        if (found) return { kind: found.kind, text: `${destinationLabel(found.kind)} · ${found.label}` };
        if (source.startsWith('@') && value === resolveToken(source)) {
            const sys = systemLinks.find(x => x.token === source.toLowerCase());
            return { kind: 'system', text: `Trang hệ thống · ${sys?.label || 'Liên kết mặc định'}` };
        }
        return { kind: 'custom', text: 'Link tùy chỉnh' };
    }

    function refreshPicker(picker) {
        const target = picker?._target;
        const input = picker?._input;
        if (!target || !input) return;
        const info = statusInfo(target, input);
        const status = picker.querySelector('[data-link-status]');
        if (status) {
            status.textContent = info.text;
            status.dataset.kind = info.kind;
        }
        const test = picker.querySelector('[data-link-test]');
        if (test) test.disabled = !safePreviewUrl(input.value);
    }

    function refreshAllPickers() {
        document.querySelectorAll('.pve-smart-link-picker').forEach(refreshPicker);
    }

    function optionHtml(item, index) {
        const selected = active && ((sourceValue(active.target) === item.source && active.input.value.trim() === item.url) || active.input.value.trim() === item.url);
        return `<button type="button" class="pve-link-destination${selected ? ' is-selected' : ''}" data-link-destination="${index}">
            <span class="pve-link-destination-icon">${h(item.icon || '↗')}</span>
            <span><strong>${h(item.label)}</strong><small>${h(item.detail || item.url)}</small></span>
            ${selected ? '<span class="pve-link-selected-mark">✓</span>' : ''}
        </button>`;
    }

    function filteredDestinations(query) {
        const needle = String(query || '').trim().toLowerCase();
        return cachedDestinations
            .map((item, index) => ({ item, index }))
            .filter(({ item }) => activeCategory === 'all' || item.kind === activeCategory)
            .filter(({ item }) => !needle || `${item.label} ${item.detail} ${item.url} ${destinationLabel(item.kind)}`.toLowerCase().includes(needle));
    }

    function renderDialog() {
        if (!dialog || dialog.hidden) return;
        const query = dialog.querySelector('[data-link-search]')?.value || '';
        const indexed = filteredDestinations(query);
        const container = dialog.querySelector('[data-link-destinations]');
        if (!container) return;

        dialog.querySelectorAll('[data-link-category]').forEach(button => {
            const category = button.dataset.linkCategory;
            button.classList.toggle('active', category === activeCategory);
            const count = category === 'all' ? cachedDestinations.length : cachedDestinations.filter(item => item.kind === category).length;
            const badge = button.querySelector('span');
            if (badge) badge.textContent = count;
        });

        if (!indexed.length) {
            container.innerHTML = '<div class="pve-link-empty">Không tìm thấy liên kết phù hợp.</div>';
        } else {
            const groups = activeCategory === 'all' ? ['system', 'room', 'post', 'anchor'] : [activeCategory];
            container.innerHTML = groups.map(kind => {
                const groupItems = indexed.filter(x => x.item.kind === kind);
                if (!groupItems.length) return '';
                return `<section class="pve-link-group"><h4>${h(destinationLabel(kind))}<span>${groupItems.length}</span></h4><div>${groupItems.map(x => optionHtml(x.item, x.index)).join('')}</div></section>`;
            }).join('');
        }

        const current = dialog.querySelector('[data-link-current]');
        if (current && active) {
            const info = statusInfo(active.target, active.input);
            current.innerHTML = `<small>Đang chọn</small><strong>${h(info.text)}</strong><span>${h(active.input.value.trim() || 'Chưa có link')}</span>`;
        }
    }

    function ensureDialog() {
        if (dialog) return dialog;
        dialog = document.createElement('div');
        dialog.className = 'pve-link-dialog-backdrop';
        dialog.dataset.linkDialog = '1';
        dialog.hidden = true;
        dialog.innerHTML = `<section class="pve-link-dialog" role="dialog" aria-modal="true" aria-label="Chọn liên kết">
            <header>
                <div><small>LIÊN KẾT</small><h3>Chọn liên kết</h3><p>Chọn trang có sẵn thay vì phải nhớ URL hoặc slug.</p></div>
                <button type="button" data-link-dialog-close aria-label="Đóng">×</button>
            </header>
            <div class="pve-link-dialog-body">
                <div class="pve-link-dialog-search"><span>⌕</span><input type="search" data-link-search placeholder="Tìm trang, tên phòng, mã phòng, bài viết…"></div>
                <nav class="pve-link-categories" aria-label="Loại liên kết">
                    <button type="button" class="active" data-link-category="all">Tất cả <span>0</span></button>
                    <button type="button" data-link-category="system">Hệ thống <span>0</span></button>
                    <button type="button" data-link-category="room">Phòng <span>0</span></button>
                    <button type="button" data-link-category="post">Bài viết <span>0</span></button>
                    <button type="button" data-link-category="anchor">Trong trang <span>0</span></button>
                </nav>
                <div class="pve-link-progress" data-link-loading hidden>Đang tải thêm phòng và bài viết…</div>
                <div class="pve-link-dialog-results" data-link-destinations></div>
            </div>
            <footer>
                <div data-link-current></div>
                <div><button type="button" data-link-custom>Nhập URL thủ công</button><button type="button" data-link-dialog-close>Đóng</button></div>
            </footer>
        </section>`;
        document.body.appendChild(dialog);

        const close = () => closeDialog();
        dialog.querySelectorAll('[data-link-dialog-close]').forEach(button => button.addEventListener('click', close));
        dialog.addEventListener('mousedown', event => { if (event.target === dialog) close(); });
        dialog.querySelector('[data-link-search]').addEventListener('input', renderDialog);
        dialog.querySelectorAll('[data-link-category]').forEach(button => button.addEventListener('click', () => {
            activeCategory = button.dataset.linkCategory || 'all';
            renderDialog();
        }));
        dialog.querySelector('[data-link-custom]').addEventListener('click', () => {
            const input = active?.input;
            closeDialog(false);
            setTimeout(() => { input?.focus(); input?.select?.(); }, 0);
        });
        dialog.querySelector('[data-link-destinations]').addEventListener('click', event => {
            const option = event.target.closest('[data-link-destination]');
            if (!option || !active) return;
            const item = cachedDestinations[Number(option.dataset.linkDestination)];
            if (!item) return;
            setSourceValue(active.target, item.source || item.url);
            active.input.value = item.url;
            active.input.dispatchEvent(new Event('input', { bubbles: true }));
            active.input.dispatchEvent(new Event('change', { bubbles: true }));
            const input = active.input;
            closeDialog(false);
            refreshAllPickers();
            setTimeout(() => input?.focus(), 0);
        });
        document.addEventListener('keydown', event => {
            if (event.key === 'Escape' && dialog && !dialog.hidden) closeDialog();
        });
        return dialog;
    }

    function closeDialog(restoreFocus = true) {
        if (!dialog || dialog.hidden) return;
        const input = active?.input;
        dialog.hidden = true;
        document.body.classList.remove('pve-link-dialog-open');
        activeCategory = 'all';
        dialog.querySelector('[data-link-search]').value = '';
        active = null;
        if (restoreFocus) setTimeout(() => input?.focus(), 0);
    }

    async function openDialog(picker) {
        const target = picker?._target;
        const input = picker?._input;
        if (!target || !input) return;
        ensureDialog();
        active = { picker, target, input };
        activeCategory = 'all';
        dialog.hidden = false;
        document.body.classList.add('pve-link-dialog-open');
        dialog.querySelector('[data-link-search]').value = '';
        renderDialog();
        setTimeout(() => dialog.querySelector('[data-link-search]')?.focus(), 0);

        const loading = dialog.querySelector('[data-link-loading]');
        if (loading) loading.hidden = false;
        const extras = await loadDynamicDestinations();
        cachedDestinations = [...baseDestinations(), ...extras];
        if (loading) loading.hidden = true;
        refreshAllPickers();
        if (!dialog.hidden) renderDialog();
    }

    function buildPicker(target, input) {
        if (!target || !input || target.dataset.smartLinkPicker === '1') return;
        target.dataset.smartLinkPicker = '1';
        const picker = document.createElement('div');
        picker.className = 'pve-system-link-picker pve-smart-link-picker';
        picker._target = target;
        picker._input = input;
        const defaultToken = defaultTokenFor(target, input);
        picker.innerHTML = `<div class="pve-smart-link-compact">
            <span data-link-status>Đang kiểm tra…</span>
            <div>
                <button type="button" class="primary" data-link-open>⌕ Chọn liên kết</button>
                ${defaultToken ? `<button type="button" data-link-reset="${defaultToken}" title="Khôi phục link hệ thống mặc định">↺ Mặc định</button>` : ''}
                <button type="button" data-link-test title="Mở thử link hiện tại">↗ Mở thử</button>
            </div>
        </div>`;
        input.closest('label')?.insertAdjacentElement('afterend', picker);
        if (!picker.isConnected) target.appendChild(picker);

        picker.querySelector('[data-link-open]').addEventListener('click', () => openDialog(picker));
        picker.querySelector('[data-link-reset]')?.addEventListener('click', event => {
            const token = event.currentTarget.dataset.linkReset;
            setSourceValue(target, token);
            input.value = resolveToken(token);
            input.dispatchEvent(new Event('input', { bubbles: true }));
            input.dispatchEvent(new Event('change', { bubbles: true }));
            refreshPicker(picker);
        });
        picker.querySelector('[data-link-test]').addEventListener('click', () => {
            const url = safePreviewUrl(input.value);
            if (url) window.open(url, '_blank', 'noopener,noreferrer');
        });
        input.addEventListener('input', () => refreshPicker(picker));
        refreshPicker(picker);
    }

    function enhance(drawer) {
        if (!drawer?.querySelector?.('.pve-shell-editor')) return;
        drawer.querySelectorAll('[data-nav-id]').forEach(row => buildPicker(row, row.querySelector('[data-nav-url]')));
        drawer.querySelectorAll('[data-token-source]').forEach(target => {
            buildPicker(target, target.querySelector('input[name="headerCtaUrl"], input[name="footerBookingUrl"]'));
        });
    }

    let queued = false;
    function queue() {
        if (queued) return;
        queued = true;
        requestAnimationFrame(() => {
            queued = false;
            document.querySelectorAll('.pve-shell-rates-drawer').forEach(enhance);
        });
    }

    const observer = new MutationObserver(queue);
    observer.observe(document.body, { childList: true, subtree: true });
    queue();
})();
