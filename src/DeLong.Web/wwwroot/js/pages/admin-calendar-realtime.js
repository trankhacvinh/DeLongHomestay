(function () {
    const root = document.getElementById('calendar-page');
    if (!root || !window.DeLongApi || root.dataset.bookingRealtimeBound === 'true') return;
    root.dataset.bookingRealtimeBound = 'true';

    const initial = (() => {
        try { return JSON.parse(document.getElementById('calendar-page-data')?.textContent || '{}'); }
        catch { return {}; }
    })();
    const propertyId = initial.propertyId;
    const startDate = initial.startDate;
    const utcOffset = initial.utcOffset || '+07:00';
    if (!propertyId || !startDate) return;

    let eventSource = null;
    let pollTimer = null;
    let refreshTimer = null;
    let refreshing = false;
    let queued = false;

    function parseDateKey(key) {
        const [year, month, day] = String(key).split('-').map(Number);
        return new Date(Date.UTC(year, month - 1, day));
    }

    function dateKey(date) {
        return `${date.getUTCFullYear()}-${String(date.getUTCMonth() + 1).padStart(2, '0')}-${String(date.getUTCDate()).padStart(2, '0')}`;
    }

    function addDays(key, amount) {
        const date = parseDateKey(key);
        date.setUTCDate(date.getUTCDate() + amount);
        return dateKey(date);
    }

    function calendarVm() {
        return root.__vue_app__?._instance?.proxy || null;
    }

    function bookingsUrl() {
        const from = `${startDate}T00:00:00${utcOffset}`;
        const to = `${addDays(startDate, 7)}T00:00:00${utcOffset}`;
        const query = new URLSearchParams({ from, to });
        return `/api/admin/properties/${propertyId}/bookings?${query}`;
    }

    async function refreshBookings() {
        const vm = calendarVm();
        if (!vm) return false;
        if (refreshing) {
            queued = true;
            return true;
        }

        refreshing = true;
        try {
            const rows = await DeLongApi.get(bookingsUrl());
            if (!Array.isArray(rows)) return true;
            const active = rows.filter(booking => [0, 1, 2, 3].includes(Number(booking.status)));
            vm.bookings = active;

            if (vm.selectedBooking?.id) {
                const latest = rows.find(booking => booking.id === vm.selectedBooking.id);
                if (latest) vm.selectedBooking = latest;
            }
            return true;
        } catch {
            return true;
        } finally {
            refreshing = false;
            if (queued) {
                queued = false;
                setTimeout(refreshBookings, 80);
            }
        }
    }

    function scheduleRefresh(delay) {
        if (refreshTimer) clearTimeout(refreshTimer);
        refreshTimer = setTimeout(() => {
            refreshTimer = null;
            refreshBookings();
        }, Math.max(0, Number(delay || 0)));
    }

    function bindRealtime() {
        if ('EventSource' in window) {
            eventSource = new EventSource(`/api/admin/properties/${propertyId}/notifications/stream`);
            eventSource.addEventListener('notification', () => {
                scheduleRefresh(0);
                setTimeout(refreshBookings, 900);
                setTimeout(refreshBookings, 2600);
            });
        }

        // SSE gives immediate updates for website booking requests. The short fallback poll also
        // catches bookings created/edited by another admin session and reconnect gaps.
        pollTimer = setInterval(() => {
            if (!document.hidden) refreshBookings();
        }, 8000);

        window.addEventListener('focus', refreshBookings);
        document.addEventListener('visibilitychange', () => {
            if (!document.hidden) refreshBookings();
        });
        refreshBookings();
    }

    function waitForVue(attempt) {
        if (calendarVm()) {
            bindRealtime();
            return;
        }
        if (attempt >= 60) return;
        setTimeout(() => waitForVue(attempt + 1), 100);
    }

    window.addEventListener('beforeunload', () => {
        if (eventSource) eventSource.close();
        if (pollTimer) clearInterval(pollTimer);
        if (refreshTimer) clearTimeout(refreshTimer);
    }, { once: true });

    waitForVue(0);
})();
