(function () {
    if (!window.DeLongApi) return;

    const center = document.querySelector('[data-notification-center]');
    const pageDataNode = document.getElementById('calendar-page-data') ||
        document.getElementById('bookings-page-data') ||
        document.getElementById('housekeeping-page-data');
    const pageData = (() => {
        try { return JSON.parse(pageDataNode?.textContent || '{}'); }
        catch { return {}; }
    })();
    const propertyId = center?.dataset.propertyId || pageData.propertyId;
    if (!propertyId) return;

    const calendarRoot = document.getElementById('calendar-page');
    let calendarBookingsInFlight = false;
    let calendarBookingsQueued = false;
    let calendarRoomsInFlight = false;
    let calendarRoomsQueued = false;
    const retryTimers = new Set();
    let calendarPollTimer = null;

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

    function retryAfterMount(callback, reason, attempt) {
        if (attempt >= 8) return;
        const timer = setTimeout(() => {
            retryTimers.delete(timer);
            callback(reason, attempt + 1);
        }, 125 * (attempt + 1));
        retryTimers.add(timer);
    }

    async function refreshCalendarBookings(reason, mountAttempt = 0) {
        if (!calendarRoot) return;
        const vm = calendarVm();
        if (!vm) return retryAfterMount(refreshCalendarBookings, reason, mountAttempt);
        if (calendarBookingsInFlight) {
            calendarBookingsQueued = true;
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

        calendarBookingsInFlight = true;
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
            // SSE reconnect, focus and fallback polling reconcile again.
        } finally {
            calendarBookingsInFlight = false;
            if (calendarBookingsQueued) {
                calendarBookingsQueued = false;
                setTimeout(() => refreshCalendarBookings('queued'), 0);
            }
        }
    }

    async function refreshCalendarRooms(reason, mountAttempt = 0) {
        if (!calendarRoot) return;
        const vm = calendarVm();
        if (!vm) return retryAfterMount(refreshCalendarRooms, reason, mountAttempt);
        if (calendarRoomsInFlight) {
            calendarRoomsQueued = true;
            return;
        }

        calendarRoomsInFlight = true;
        try {
            const rows = await DeLongApi.get(`/api/admin/properties/${propertyId}/rooms`);
            const rooms = Array.isArray(rows) ? rows : [];
            if (Array.isArray(vm.rooms)) vm.rooms.splice(0, vm.rooms.length, ...rooms);
            else vm.rooms = rooms;
        } catch {
            // Keep the last known room state until the next reconcile.
        } finally {
            calendarRoomsInFlight = false;
            if (calendarRoomsQueued) {
                calendarRoomsQueued = false;
                setTimeout(() => refreshCalendarRooms('queued'), 0);
            }
        }
    }

    function scheduleCalendarBookings(reason) {
        if (!calendarRoot) return;
        refreshCalendarBookings(reason);
        const timer = setTimeout(() => {
            retryTimers.delete(timer);
            refreshCalendarBookings(`${reason}-settled`);
        }, 650);
        retryTimers.add(timer);
    }

    function emitOperations(detail) {
        document.dispatchEvent(new CustomEvent('delong:operations-change', { detail }));
        const type = String(detail.type || '');
        if (type.startsWith('booking.')) {
            // Compatibility bridge for the existing Bookings-page live reconciler.
            document.dispatchEvent(new CustomEvent('delong:booking-notification', {
                detail: { propertyId, operations: detail }
            }));
            scheduleCalendarBookings('operations');
            if (type === 'booking.status-changed' || type === 'booking.hold-expired')
                refreshCalendarRooms('booking-status');
        } else if (type === 'housekeeping.changed') {
            refreshCalendarRooms('housekeeping');
        }
    }

    function startStream() {
        if (!('EventSource' in window)) {
            document.documentElement.dataset.operationsRealtime = 'unsupported';
            return;
        }

        const source = new EventSource(`/api/admin/properties/${propertyId}/operations/stream`);
        source.addEventListener('operations', event => {
            let detail = { propertyId, raw: event.data || '' };
            try {
                const parsed = JSON.parse(event.data || '{}');
                detail = { ...parsed, propertyId: parsed.propertyId || propertyId };
            } catch { }
            emitOperations(detail);
        });
        source.addEventListener('open', () => {
            document.documentElement.dataset.operationsRealtime = 'connected';
            const detail = { propertyId, type: 'stream.reconnected' };
            document.dispatchEvent(new CustomEvent('delong:operations-change', { detail }));
            document.dispatchEvent(new CustomEvent('delong:booking-notification', { detail }));
            scheduleCalendarBookings('reconnected');
            refreshCalendarRooms('reconnected');
        });
        source.addEventListener('error', () => {
            document.documentElement.dataset.operationsRealtime = 'reconnecting';
        });
        window.addEventListener('beforeunload', () => source.close(), { once: true });
    }

    startStream();

    if (calendarRoot) {
        window.addEventListener('focus', () => {
            refreshCalendarBookings('focus');
            refreshCalendarRooms('focus');
        });
        document.addEventListener('visibilitychange', () => {
            if (document.hidden) return;
            refreshCalendarBookings('visible');
            refreshCalendarRooms('visible');
        });
        calendarPollTimer = setInterval(() => {
            if (!document.hidden) refreshCalendarBookings('fallback-poll');
        }, 15000);
    }

    window.addEventListener('beforeunload', () => {
        if (calendarPollTimer) clearInterval(calendarPollTimer);
        retryTimers.forEach(clearTimeout);
    }, { once: true });
})();
