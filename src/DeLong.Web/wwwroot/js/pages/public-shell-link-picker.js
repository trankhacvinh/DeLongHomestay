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

    let contextPromise = null;
    let destinationPromise = null;

    function h(value) {
        return String(value ?? '').replace(/[&<>'"]/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[ch]));
    }

    function homeUrl(slug) {
        return slug ? `/h/${encodeURIComponent(slug)}` : '/';
    }

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

    function defaultTokenFor(target, input) {
        if (target?.matches?.('[data-nav-id]')) {
            const id = String(target.dataset.navId || '').toLowerCase();
            if (['home', 'rooms', 'branches', 'booking', 'lookup'].includes(id)) return `@${id}`;
        }
        if (input?.name === 'headerCtaUrl' || input?.name === 'footerBookingUrl') return '@booking';
        return '';
    }

    function sourceValue(target) {
        if (target?.matches?.('[data-nav-id]')) return target.dataset.navSourceUrl || '';
        return target?.dataset?.tokenSource || '';
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
        } catch {
            return '';
        }
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
            try {
                const content = await DeLongApi.get(`/api/admin/properties/${propertyId}/rooms/${room.id}/content/`);
                slug = content?.slug || '';
            } catch { return null; }
        }
        if (!slug || !propertySlug) return null;
        const url = `/h/${encodeURIComponent(propertySlug)}/rooms/${encodeURIComponent(slug)}`;
        return {
            kind: 'room',
            label: room.name || room.code || 'Phòng',
            detail: `${room.propertyName || ''}${room.code ? ` · ${room.code}` : ''}`.replace(/^ · | · $/g, ''),
            url,
            source: url
        };
    }

    async function loadDestinations() {
        if (destinationPromise) return destinationPromise;
        destinationPromise = (async () => {
            const context = await getContext();
            const destinations = [];

            systemLinks.forEach(link => destinations.push({
                kind: 'system',
                label: link.label,
                detail: resolveToken(link.token),
                url: resolveToken(link.token),
                source: link.token,
                icon: link.icon
            }));

            const currentHome = homeUrl(siteSlug);
            const blogUrl = currentHome === '/' ? '/blog' : `${currentHome}/blog`;
            const galleryUrl = `${currentHome === '/' ? '' : currentHome}#gallery` || '/#gallery';
            destinations.push({ kind: 'system', label: 'Blog', detail: blogUrl, url: blogUrl, source: blogUrl, icon: '✎' });
            destinations.push({ kind: 'anchor', label: 'Gallery trên trang chủ', detail: galleryUrl, url: galleryUrl, source: galleryUrl, icon: '▧' });

            if (!context) return destinations;

            const rooms = Array.isArray(context.rooms) ? context.rooms.slice(0, 100) : [];
            const roomResults = await Promise.allSettled(rooms.map(resolveRoomDestination));
            roomResults.forEach(result => {
                if (result.status === 'fulfilled' && result.value) destinations.push(result.value);
            });

            try {
                let editorial;
                if (context.scope === 'property' && context.propertyId) {
                    editorial = await DeLongApi.get(`/api/admin/properties/${context.propertyId}/editorial/`);
                } else if (context.scope === 'global') {
                    editorial = await DeLongApi.get('/api/admin/site/global/editorial/');
                }
                const posts = Array.isArray(editorial?.posts) ? editorial.posts : [];
                posts.filter(post => post.isPublished !== false).slice(0, 100).forEach(post => {
                    const propertySlug = post.propertySiteSlug || siteSlug;
                    if (!propertySlug || !post.slug) return;
                    const url = `/h/${encodeURIComponent(propertySlug)}/blog/${encodeURIComponent(post.slug)}`;
                    destinations.push({
                        kind: 'post',
                        label: post.title || 'Bài viết',
                        detail: post.propertyName || '',
                        url,
                        source: url,
                        icon: '¶'
                    });
                });
            } catch { }

            return destinations;
        })();
        return destinationPromise;
    }

    function destinationLabel(kind) {
        return ({ system: 'Trang hệ thống', room: 'Phòng', post: 'Bài viết', anchor: 'Trong trang' })[kind] || 'Khác';
    }

    function currentStatus(target, input, destinations) {
        const source = sourceValue(target);
        const value = input.value.trim();
        const found = (destinations || []).find(item => item.source === source && item.url === value)
            || (destinations || []).find(item => item.url === value);
        if (found) return `${destinationLabel(found.kind)} · ${found.label}`;
        if (source.startsWith('@') && value === resolveToken(source)) {
            const sys = systemLinks.find(x => x.token === source.toLowerCase());
            return `Trang hệ thống · ${sys?.label || 'Liên kết mặc định'}`;
        }
        return 'Link tùy chỉnh';
    }

    function optionHtml(item, index) {
        return `<button type="button" class="pve-link-destination" data-link-destination="${index}">
            <span class="pve-link-destination-icon">${h(item.icon || (item.kind === 'room' ? '▣' : item.kind === 'post' ? '¶' : '↗'))}</span>
            <span><strong>${h(item.label)}</strong><small>${h(item.detail || item.url)}</small></span>
        </button>`;
    }

    function renderDestinations(picker, destinations, query) {
        const container = picker.querySelector('[data-link-destinations]');
        if (!container) return;
        const needle = String(query || '').trim().toLowerCase();
        const indexed = destinations.map((item, index) => ({ item, index })).filter(({ item }) => {
            if (!needle) return true;
            return `${item.label} ${item.detail} ${item.url} ${destinationLabel(item.kind)}`.toLowerCase().includes(needle);
        });
        if (!indexed.length) {
            container.innerHTML = '<div class="pve-link-empty">Không tìm thấy liên kết phù hợp.</div>';
            return;
        }
        const groups = ['system', 'room', 'post', 'anchor'];
        container.innerHTML = groups.map(kind => {
            const groupItems = indexed.filter(x => x.item.kind === kind);
            if (!groupItems.length) return '';
            return `<section class="pve-link-group"><h4>${h(destinationLabel(kind))}<span>${groupItems.length}</span></h4><div>${groupItems.map(x => optionHtml(x.item, x.index)).join('')}</div></section>`;
        }).join('');
    }

    function buildPicker(target, input) {
        if (!target || !input || target.dataset.smartLinkPicker === '1') return;
        target.dataset.smartLinkPicker = '1';

        const picker = document.createElement('div');
        picker.className = 'pve-system-link-picker pve-smart-link-picker';
        const defaultToken = defaultTokenFor(target, input);
        picker.innerHTML = `
            <div class="pve-smart-link-summary">
                <div><strong>Liên kết</strong><span data-link-status>Đang kiểm tra…</span></div>
                <button type="button" data-link-open>⌕ Chọn link có sẵn</button>
            </div>
            <div class="pve-smart-link-panel" data-link-panel hidden>
                <div class="pve-smart-link-search"><input type="search" data-link-search placeholder="Tìm Trang chủ, phòng, bài viết…"><button type="button" data-link-panel-close aria-label="Đóng">×</button></div>
                <div class="pve-link-loading" data-link-loading>Đang tải các link có sẵn…</div>
                <div class="pve-smart-link-destinations" data-link-destinations></div>
                <div class="pve-smart-link-custom"><strong>Link tùy chỉnh</strong><small>Bạn vẫn có thể nhập trực tiếp vào ô Link phía trên nếu cần URL ngoài hệ thống.</small></div>
            </div>
            <div class="pve-system-link-actions">
                ${defaultToken ? `<button type="button" data-link-reset="${defaultToken}">↺ Khôi phục mặc định</button>` : ''}
                <button type="button" data-link-test>↗ Mở thử</button>
            </div>`;

        input.closest('label')?.insertAdjacentElement('afterend', picker);
        if (!picker.isConnected) target.appendChild(picker);

        let destinations = [];
        const panel = picker.querySelector('[data-link-panel]');
        const search = picker.querySelector('[data-link-search]');

        const updateStatus = () => {
            const status = picker.querySelector('[data-link-status]');
            if (status) status.textContent = currentStatus(target, input, destinations);
            const test = picker.querySelector('[data-link-test]');
            if (test) test.disabled = !safePreviewUrl(input.value);
        };

        const openPanel = async () => {
            panel.hidden = false;
            picker.classList.add('is-open');
            const loading = picker.querySelector('[data-link-loading]');
            if (loading) loading.hidden = false;
            destinations = await loadDestinations();
            if (!picker.isConnected) return;
            if (loading) loading.hidden = true;
            renderDestinations(picker, destinations, search?.value || '');
            updateStatus();
            setTimeout(() => search?.focus(), 0);
        };

        picker.querySelector('[data-link-open]').addEventListener('click', openPanel);
        picker.querySelector('[data-link-panel-close]').addEventListener('click', () => {
            panel.hidden = true;
            picker.classList.remove('is-open');
        });
        search?.addEventListener('input', () => renderDestinations(picker, destinations, search.value));

        picker.addEventListener('click', event => {
            const option = event.target.closest('[data-link-destination]');
            if (option) {
                const item = destinations[Number(option.dataset.linkDestination)];
                if (!item) return;
                setSourceValue(target, item.source || item.url);
                input.value = item.url;
                input.dispatchEvent(new Event('input', { bubbles: true }));
                panel.hidden = true;
                picker.classList.remove('is-open');
                updateStatus();
                return;
            }

            const reset = event.target.closest('[data-link-reset]');
            if (reset) {
                const token = reset.dataset.linkReset;
                setSourceValue(target, token);
                input.value = resolveToken(token);
                input.dispatchEvent(new Event('input', { bubbles: true }));
                updateStatus();
                return;
            }

            if (event.target.closest('[data-link-test]')) {
                const url = safePreviewUrl(input.value);
                if (url) window.open(url, '_blank', 'noopener,noreferrer');
            }
        });

        input.addEventListener('input', updateStatus);
        updateStatus();
    }

    function enhance(drawer) {
        if (!drawer?.querySelector?.('.pve-shell-editor')) return;
        drawer.querySelectorAll('[data-nav-id]').forEach(row => buildPicker(row, row.querySelector('[data-nav-url]')));
        drawer.querySelectorAll('[data-token-source]').forEach(target => {
            const input = target.querySelector('input[name="headerCtaUrl"], input[name="footerBookingUrl"]');
            buildPicker(target, input);
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