(function () {
    const center = document.querySelector('[data-notification-center]');
    if (!window.DeLongApi) return;

    const pageDataNode = document.getElementById('calendar-page-data') || document.getElementById('bookings-page-data') || document.getElementById('housekeeping-page-data');
    const pageData = (() => {
        try { return JSON.parse(pageDataNode?.textContent || '{}'); }
        catch { return {}; }
    })();
    const propertyId = center?.dataset.propertyId || pageData.propertyId;
    if (!propertyId) return;

    const calendarRoot = document.getElementById('calendar-page');
    let calendarRefreshInFlight = false;
    let calendarRefreshQueued = false;
    let calendarPollTimer = null;
    const calendarRetryTimers = new Set();

    function addDays(key, amount) {
        const [year, month, day] = String(key || '').split('-').map(Number);
        if (!year || !month || !day) return '';
        const date = new Date(Date.UTC(year, month - 1, day));
        date.setUTCDate(date.getUTCDate() + amount);
        return `${date.getUTCFullYear()}-${String(date.getUTCMonth() + 1).padStart(2, '0')}-${String(date.getUTCDate()).padStart(2, '0')}`;
    }

    function calendarVm() {
        return calendarRoot?.__vue_app__?._instance?.proxy || null;
    }

    async function refreshCalendarBookings(reason, mountAttempt = 0) {
        if (!calendarRoot) return;
        const vm = calendarVm();
        if (!vm) {
            if (mountAttempt >= 8) return;
            const timer = setTimeout(() => {
                calendarRetryTimers.delete(timer);
                refreshCalendarBookings(reason, mountAttempt + 1);
            }, 125 * (mountAttempt + 1));
            calendarRetryTimers.add(timer);
            return;
        }
        if (calendarRefreshInFlight) {
            calendarRefreshQueued = true;
            return;
        }

        const startDate = vm.startDate || pageData.startDate;
        const endDate = addDays(startDate, 7);
        if (!startDate || !endDate) return;
        const utcOffset = pageData.utcOffset || '+07:00';
        const query = new URLSearchParams({
            from: `${startDate}T00:00:00${utcOffset}`,
            to: `${endDate}T00:00:00${utcOffset}`
        });

        calendarRefreshInFlight = true;
        try {
            const rows = await DeLongApi.get(`/api/admin/properties/${propertyId}/bookings?${query}`);
            const bookings = Array.isArray(rows) ? rows : [];
            if (Array.isArray(vm.bookings)) vm.bookings.splice(0, vm.bookings.length, ...bookings);
            else vm.bookings = bookings;

            if (vm.selectedBooking?.id) {
                const latest = bookings.find(item => item.id === vm.selectedBooking.id);
                if (latest) vm.selectedBooking = latest;
            }
            document.documentElement.dataset.calendarRealtime = reason || 'reconciled';
        } catch {
            // SSE reconnect, focus and fallback polling will reconcile again.
        } finally {
            calendarRefreshInFlight = false;
            if (calendarRefreshQueued) {
                calendarRefreshQueued = false;
                setTimeout(() => refreshCalendarBookings('queued'), 0);
            }
        }
    }

    function scheduleCalendarReconcile(reason) {
        if (!calendarRoot) return;
        refreshCalendarBookings(reason);
        const timer = setTimeout(() => {
            calendarRetryTimers.delete(timer);
            refreshCalendarBookings(`${reason}-settled`);
        }, 650);
        calendarRetryTimers.add(timer);
    }

    function bookingLiveAssetUrl() {
        const base = '/js/core/admin-booking-live-v2.js?v=20260820-1';
        const host = String(window.location.hostname || '').toLowerCase();
        if (!['localhost', '127.0.0.1', '::1'].includes(host)) return base;
        return `${base}&dev=${Date.now().toString(36)}`;
    }

    function ensureBookingLiveScript() {
        if (!document.getElementById('bookings-page')) return;
        if (document.querySelector('script[data-admin-booking-live-v2]')) return;
        const script = document.createElement('script');
        script.src = bookingLiveAssetUrl();
        script.async = false;
        script.dataset.adminBookingLiveV2 = 'true';
        script.addEventListener('load', () => {
            document.documentElement.dataset.bookingLiveAsset = 'loaded';
        });
        script.addEventListener('error', () => {
            document.documentElement.dataset.bookingLiveAsset = 'error';
            console.error('Không tải được admin-booking-live-v2.js');
        });
        document.head.appendChild(script);
    }

    function scheduleBookingLiveScript() {
        // Calendar/Bookings mount Vue from their own @section Scripts, which is rendered after this file.
        // Load the booking bridge only after window.load so it always binds to the mounted page app.
        if (document.readyState === 'complete') setTimeout(ensureBookingLiveScript, 0);
        else window.addEventListener('load', ensureBookingLiveScript, { once: true });
    }

    function startOperationsRealtime() {
        if (!('EventSource' in window) || window.DeLongApi.__operationsRealtimeBound) return;
        window.DeLongApi.__operationsRealtimeBound = true;
        const source = new EventSource(`/api/admin/properties/${propertyId}/operations/stream`);
        source.addEventListener('operations', event => {
            let detail = { propertyId, raw: event.data || '' };
            try {
                const parsed = JSON.parse(event.data || '{}');
                detail = { ...parsed, propertyId: parsed.propertyId || propertyId };
            } catch { }
            document.dispatchEvent(new CustomEvent('delong:operations-change', { detail }));
            if (String(detail.type || '').startsWith('booking.')) {
                document.dispatchEvent(new CustomEvent('delong:booking-notification', {
                    detail: { propertyId, operations: detail }
                }));
                scheduleCalendarReconcile('operations');
            }
        });
        source.addEventListener('open', () => {
            document.documentElement.dataset.operationsRealtime = 'connected';
            const detail = { propertyId, type: 'stream.reconnected' };
            document.dispatchEvent(new CustomEvent('delong:operations-change', { detail }));
            document.dispatchEvent(new CustomEvent('delong:booking-notification', { detail }));
            scheduleCalendarReconcile('reconnected');
        });
        source.addEventListener('error', () => {
            document.documentElement.dataset.operationsRealtime = 'reconnecting';
        });
        window.addEventListener('beforeunload', () => source.close(), { once: true });
    }

    scheduleBookingLiveScript();
    startOperationsRealtime();

    if (calendarRoot) {
        window.addEventListener('focus', () => refreshCalendarBookings('focus'));
        document.addEventListener('visibilitychange', () => {
            if (!document.hidden) refreshCalendarBookings('visible');
        });
        calendarPollTimer = setInterval(() => {
            if (!document.hidden) refreshCalendarBookings('fallback-poll');
        }, 15000);
        window.addEventListener('beforeunload', () => {
            if (calendarPollTimer) clearInterval(calendarPollTimer);
            calendarRetryTimers.forEach(clearTimeout);
        }, { once: true });
    }

    if (!center) return;

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
        toast.innerHTML = '<strong>Có yêu cầu đặt phòng mới</strong><span>Lịch phòng và danh sách đặt phòng đang được cập nhật.</span>';
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
        source.addEventListener('notification', async event => {
            await refresh();
            document.dispatchEvent(new CustomEvent('delong:booking-notification', {
                detail: { propertyId, raw: event.data || '' }
            }));
            showRealtimeToast();
        });
        source.addEventListener('open', refresh);
        window.addEventListener('beforeunload', () => source.close(), { once: true });
    }
})();
