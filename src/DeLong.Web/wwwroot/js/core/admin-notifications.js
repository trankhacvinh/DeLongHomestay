(function () {
    const center = document.querySelector('[data-notification-center]');
    if (!center || !window.DeLongApi) return;

    const propertyId = center.dataset.propertyId;
    if (!propertyId) return;

    const toggle = center.querySelector('[data-notification-toggle]');
    const popover = center.querySelector('[data-notification-popover]');
    const badge = center.querySelector('[data-notification-badge]');
    const list = center.querySelector('[data-notification-list]');
    const empty = center.querySelector('[data-notification-empty]');
    const readAll = center.querySelector('[data-notification-read-all]');
    const baseUrl = `/api/admin/properties/${propertyId}/notifications`;
    let feed = { items: [], unreadCount: 0 };

    function updateBadge(count) {
        const value = Number(count || 0);
        badge.textContent = value > 99 ? '99+' : String(value);
        badge.hidden = value <= 0;
        toggle?.setAttribute('aria-label', value > 0 ? `Thông báo, ${value} chưa đọc` : 'Thông báo');
    }

    function timeText(value) {
        const date = new Date(value);
        const diffSeconds = Math.max(0, Math.floor((Date.now() - date.getTime()) / 1000));
        if (diffSeconds < 60) return 'Vừa xong';
        if (diffSeconds < 3600) return `${Math.floor(diffSeconds / 60)} phút trước`;
        if (diffSeconds < 86400) return `${Math.floor(diffSeconds / 3600)} giờ trước`;
        return new Intl.DateTimeFormat('vi-VN', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' }).format(date);
    }

    function render() {
        list.replaceChildren();
        const items = Array.isArray(feed.items) ? feed.items : [];
        empty.hidden = items.length > 0;
        readAll.hidden = Number(feed.unreadCount || 0) <= 0;
        updateBadge(feed.unreadCount);

        items.forEach(item => {
            const link = document.createElement('a');
            link.className = `notification-item${item.isRead ? '' : ' unread'}`;
            link.href = item.actionUrl || '#';
            link.dataset.notificationId = item.id;

            const dot = document.createElement('span');
            dot.className = 'notification-item-dot';
            dot.setAttribute('aria-hidden', 'true');

            const body = document.createElement('span');
            body.className = 'notification-item-body';
            const title = document.createElement('strong');
            title.textContent = item.title || 'Thông báo';
            const message = document.createElement('span');
            message.textContent = item.message || '';
            const time = document.createElement('small');
            time.textContent = timeText(item.createdAtUtc);
            body.append(title, message, time);
            link.append(dot, body);

            link.addEventListener('click', async event => {
                if (!item.actionUrl) event.preventDefault();
                if (item.isRead) return;
                event.preventDefault();
                try {
                    await DeLongApi.post(`${baseUrl}/${item.id}/read`, {});
                } catch {
                    // Navigation should still work even if marking read failed.
                }
                if (item.actionUrl) window.location.assign(item.actionUrl);
                else await refresh();
            });
            list.appendChild(link);
        });
    }

    async function refresh() {
        try {
            feed = await DeLongApi.get(`${baseUrl}?take=20`);
            render();
        } catch {
            // Keep the bell unobtrusive if the feed cannot be loaded temporarily.
        }
    }

    function showRealtimeToast() {
        const root = document.getElementById('global-toast-root');
        if (!root) return;
        const toast = document.createElement('div');
        toast.className = 'notification-live-toast';
        toast.innerHTML = '<strong>Có yêu cầu đặt phòng mới</strong><span>Mở chuông thông báo để xem chi tiết.</span>';
        root.appendChild(toast);
        requestAnimationFrame(() => toast.classList.add('show'));
        setTimeout(() => {
            toast.classList.remove('show');
            setTimeout(() => toast.remove(), 250);
        }, 5000);
    }

    toggle?.addEventListener('click', () => {
        const willOpen = popover.hidden;
        popover.hidden = !willOpen;
        toggle.setAttribute('aria-expanded', willOpen ? 'true' : 'false');
        if (willOpen) refresh();
    });

    readAll?.addEventListener('click', async () => {
        try {
            await DeLongApi.post(`${baseUrl}/read-all`, {});
            feed.items = (feed.items || []).map(item => ({ ...item, isRead: true }));
            feed.unreadCount = 0;
            render();
        } catch {
            // A later refresh will reconcile the state.
        }
    });

    document.addEventListener('click', event => {
        if (!center.contains(event.target)) {
            popover.hidden = true;
            toggle?.setAttribute('aria-expanded', 'false');
        }
    });
    document.addEventListener('keydown', event => {
        if (event.key === 'Escape') {
            popover.hidden = true;
            toggle?.setAttribute('aria-expanded', 'false');
        }
    });

    refresh();
    if ('EventSource' in window) {
        const source = new EventSource(`${baseUrl}/stream`);
        source.addEventListener('notification', async () => {
            await refresh();
            showRealtimeToast();
        });
        source.addEventListener('open', refresh);
        window.addEventListener('beforeunload', () => source.close(), { once: true });
    }
})();
