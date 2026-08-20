(function () {
    const root = document.getElementById('calendar-page');
    if (!root || root.dataset.calendarV2Page !== 'true') return;

    const pageHead = root.querySelector('.calendar-page-head');
    if (!pageHead) return;

    // Vue compiles the in-DOM template when admin-calendar.js mounts. <script type="application/json">
    // nodes inside that root are side-effect tags and may no longer exist afterwards. Calendar V2 is
    // intentionally loaded after the main calendar app, so bootstrap from both the original JSON (when
    // still present) and the already-mounted Vue state instead of silently returning when the JSON node
    // has disappeared.
    const initial = (() => {
        try { return JSON.parse(document.getElementById('calendar-page-data')?.textContent || '{}'); }
        catch { return {}; }
    })();
    const bootVm = root.__vue_app__?._instance?.proxy || null;
    const propertyId = initial.propertyId || bootVm?.propertyId || '';
    const today = initial.today || bootVm?.today || '';
    const startDate = initial.startDate || bootVm?.startDate || today;
    const timeZone = initial.timeZoneId || root.dataset.timeZoneId || 'Asia/Ho_Chi_Minh';

    // These are legacy V1 placeholders kept only so the shared Vue booking editor can mount. The base
    // calendar CSS can override the HTML hidden attribute, so force them out of the standalone V2 UI.
    root.querySelectorAll('.calendar-toolbar-card[hidden], .calendar-wrap[hidden]').forEach(element => {
        element.style.setProperty('display', 'none', 'important');
    });

    const state = {
        roomIndex: 0,
        from: startDate,
        days: 10,
        loading: false,
        queued: false,
        queuedReason: '',
        data: null,
        requestSerial: 0,
        pollTimer: null
    };

    const panel = document.createElement('section');
    panel.className = 'calendar-v2-panel';
    panel.dataset.calendarV2Panel = 'true';
    panel.innerHTML = [
        '<div class="calendar-v2-roombar">',
        '  <button class="calendar-v2-nav" type="button" data-v2-room-prev aria-label="Phòng trước">‹</button>',
        '  <div class="calendar-v2-roomtitle"><small>PHÒNG</small><strong data-v2-room-name>—</strong><span data-v2-room-meta></span></div>',
        '  <button class="calendar-v2-nav" type="button" data-v2-room-next aria-label="Phòng sau">›</button>',
        '</div>',
        '<div class="calendar-v2-datebar">',
        '  <div><strong data-v2-range>—</strong><span>Cuộn dọc để xem ngày · bấm phần trống để tạo booking · bấm phần đã đặt để mở chi tiết</span></div>',
        '  <div class="calendar-v2-date-actions"><button type="button" data-v2-date-prev>‹ 7 ngày</button><button type="button" data-v2-today>Hôm nay</button><button type="button" data-v2-date-next>7 ngày ›</button></div>',
        '</div>',
        '<div class="calendar-v2-legend"><span><i class="available"></i>Trống</span><span><i class="partial"></i>Còn trống một phần</span><span><i class="held"></i>Giữ phòng</span><span><i class="booked"></i>Đã đặt</span></div>',
        '<div class="calendar-v2-status show" data-v2-status>Đang tải lịch phòng…</div>',
        '<div class="calendar-v2-scroll" data-v2-scroll></div>'
    ].join('');

    pageHead.after(panel);
    document.documentElement.dataset.calendarV2 = 'booted';

    const roomName = panel.querySelector('[data-v2-room-name]');
    const roomMeta = panel.querySelector('[data-v2-room-meta]');
    const rangeLabel = panel.querySelector('[data-v2-range]');
    const statusBox = panel.querySelector('[data-v2-status]');
    const scroll = panel.querySelector('[data-v2-scroll]');

    function showError(message, marker) {
        statusBox.textContent = message;
        statusBox.className = 'calendar-v2-status show error';
        document.documentElement.dataset.calendarV2 = marker || 'error';
    }

    if (!window.DeLongApi) {
        showError('Calendar V2 chưa khởi tạo được API client. Hãy tải lại trang sau khi ứng dụng khởi động xong.', 'missing-api');
        return;
    }
    if (!propertyId) {
        showError('Calendar V2 không đọc được cơ sở hiện tại. Vui lòng chọn lại cơ sở rồi mở lại lịch.', 'missing-property');
        return;
    }
    if (!state.from) {
        showError('Calendar V2 không đọc được ngày bắt đầu của lịch.', 'missing-date');
        return;
    }

    function vm() {
        return root.__vue_app__?._instance?.proxy || bootVm || null;
    }

    function rooms() {
        const app = vm();
        const source = Array.isArray(app?.rooms) ? app.rooms : (initial.rooms || []);
        return source.filter(item => item.isActive).sort((a, b) => Number(a.sortOrder || 0) - Number(b.sortOrder || 0) || String(a.name).localeCompare(String(b.name)));
    }

    function currentRoom() {
        const rows = rooms();
        if (!rows.length) return null;
        state.roomIndex = Math.max(0, Math.min(state.roomIndex, rows.length - 1));
        return rows[state.roomIndex];
    }

    function parseDateKey(key) {
        const [year, month, day] = String(key || '').split('-').map(Number);
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

    function localParts(utcValue) {
        const parts = new Intl.DateTimeFormat('en-CA', {
            timeZone,
            year: 'numeric', month: '2-digit', day: '2-digit',
            hour: '2-digit', minute: '2-digit', hourCycle: 'h23'
        }).formatToParts(new Date(utcValue));
        const get = type => parts.find(part => part.type === type)?.value || '';
        return { year: get('year'), month: get('month'), day: get('day'), hour: get('hour'), minute: get('minute') };
    }

    function localInput(utcValue) {
        const part = localParts(utcValue);
        return `${part.year}-${part.month}-${part.day}T${part.hour}:${part.minute}`;
    }

    function timeText(utcValue) {
        const part = localParts(utcValue);
        return `${part.hour}:${part.minute}`;
    }

    function dateText(key) {
        const value = parseDateKey(key);
        const weekday = new Intl.DateTimeFormat('vi-VN', { weekday: 'short', timeZone: 'UTC' }).format(value);
        const date = new Intl.DateTimeFormat('vi-VN', { day: '2-digit', month: '2-digit', timeZone: 'UTC' }).format(value);
        return `${weekday} · ${date}`;
    }

    function segmentStyle(startUtc, endUtc, slotStartUtc, slotEndUtc) {
        const start = new Date(startUtc).getTime();
        const end = new Date(endUtc).getTime();
        const slotStart = new Date(slotStartUtc).getTime();
        const slotEnd = new Date(slotEndUtc).getTime();
        const total = Math.max(1, slotEnd - slotStart);
        const left = Math.max(0, Math.min(100, ((start - slotStart) / total) * 100));
        const right = Math.max(left, Math.min(100, ((end - slotStart) / total) * 100));
        return `left:${left.toFixed(3)}%;width:${Math.max(0.8, right - left).toFixed(3)}%`;
    }

    function bookingClass(status) {
        return Number(status) === 1 ? 'held' : 'booked';
    }

    function visibleSlots(slots) {
        return (slots || []).filter(slot => Number(slot.rateType) !== 2);
    }

    async function openBooking(bookingId) {
        const app = vm();
        if (!app || typeof app.openBooking !== 'function') return;
        let booking = Array.isArray(app.bookings) ? app.bookings.find(item => item.id === bookingId) : null;
        if (!booking) {
            try {
                booking = await DeLongApi.get(`/api/admin/properties/${propertyId}/bookings/${bookingId}`);
                if (booking && Array.isArray(app.bookings) && !app.bookings.some(item => item.id === booking.id)) app.bookings.push(booking);
            } catch { return; }
        }
        if (booking) app.openBooking(booking);
    }

    function openFreeRange(slot, freeRange, day) {
        const app = vm();
        const room = currentRoom();
        if (!app || !room || typeof app.openCreate !== 'function' || !app.canManage) return;
        app.openCreate(room, { key: day.date });
        app.$nextTick(() => {
            const fullSlot = slot.state === 'available' && Array.isArray(slot.free) && slot.free.length === 1;
            if (fullSlot) {
                app.form.rateId = slot.rateId;
                if (typeof app.applyRate === 'function') app.applyRate();
                return;
            }
            app.form.rateId = '';
            app.form.checkInLocal = localInput(freeRange.startUtc);
            app.form.checkOutLocal = localInput(freeRange.endUtc);
            app.form.roomAmount = Number(slot.price || 0);
            if (typeof app.notify === 'function') app.notify('Đã điền giờ linh hoạt. Giá phòng đang là mức gợi ý và có thể sửa.', 'success');
        });
    }

    function renderSlot(slot, day) {
        const cell = document.createElement('div');
        cell.className = `calendar-v2-slot state-${slot.state}`;

        const track = document.createElement('div');
        track.className = 'calendar-v2-track';
        track.setAttribute('aria-label', `${slot.rateName}: ${slot.state}`);

        (slot.free || []).forEach(range => {
            const segment = document.createElement('button');
            segment.type = 'button';
            segment.className = 'calendar-v2-segment free';
            segment.setAttribute('style', segmentStyle(range.startUtc, range.endUtc, slot.startUtc, slot.endUtc));
            segment.title = `Trống ${timeText(range.startUtc)}–${timeText(range.endUtc)} · bấm để tạo booking`;
            segment.addEventListener('click', event => {
                event.stopPropagation();
                openFreeRange(slot, range, day);
            });
            track.appendChild(segment);
        });

        (slot.occupied || []).forEach(range => {
            const segment = document.createElement('button');
            segment.type = 'button';
            segment.className = `calendar-v2-segment occupied ${bookingClass(range.status)}`;
            segment.setAttribute('style', segmentStyle(range.startUtc, range.endUtc, slot.startUtc, slot.endUtc));
            segment.title = `${Number(range.status) === 1 ? 'Giữ phòng' : 'Đã đặt'} ${timeText(range.startUtc)}–${timeText(range.endUtc)} · bấm để xem booking`;
            segment.addEventListener('click', event => {
                event.stopPropagation();
                openBooking(range.bookingId);
            });
            track.appendChild(segment);
        });

        const caption = document.createElement('small');
        if (slot.state === 'available') caption.textContent = 'Trống';
        else if (slot.state === 'occupied') caption.textContent = 'Đã kín';
        else {
            const freeText = (slot.free || []).map(range => `${timeText(range.startUtc)}–${timeText(range.endUtc)}`).join(', ');
            caption.textContent = freeText ? `Còn ${freeText}` : 'Còn trống một phần';
        }
        cell.append(track, caption);
        return cell;
    }

    function render(data) {
        state.data = data;
        const room = currentRoom();
        roomName.textContent = data?.roomName || room?.name || '—';
        roomMeta.textContent = `${data?.roomCode || room?.code || ''}${room ? ` · tối đa ${room.capacity} khách` : ''}`;
        rangeLabel.textContent = `${dateText(state.from)} → ${dateText(addDays(state.from, state.days - 1))}`;
        statusBox.textContent = '';
        statusBox.className = 'calendar-v2-status';
        scroll.replaceChildren();

        const calendar = Array.isArray(data?.calendar) ? data.calendar : [];
        const headerSlots = visibleSlots(calendar[0]?.slots);
        if (!calendar.length || !headerSlots.length) {
            statusBox.textContent = 'Phòng này chưa có khung giờ / qua đêm đang hoạt động để hiển thị.';
            statusBox.classList.add('show');
            return;
        }

        const table = document.createElement('table');
        table.className = 'calendar-v2-table';
        const thead = document.createElement('thead');
        const headRow = document.createElement('tr');
        const dateHead = document.createElement('th');
        dateHead.textContent = 'Ngày';
        headRow.appendChild(dateHead);
        headerSlots.forEach(slot => {
            const th = document.createElement('th');
            const strong = document.createElement('strong');
            strong.textContent = `${timeText(slot.startUtc)}–${timeText(slot.endUtc)}`;
            const small = document.createElement('small');
            small.textContent = slot.rateName;
            th.append(strong, small);
            headRow.appendChild(th);
        });
        thead.appendChild(headRow);
        table.appendChild(thead);

        const tbody = document.createElement('tbody');
        calendar.forEach(day => {
            const tr = document.createElement('tr');
            const th = document.createElement('th');
            th.textContent = dateText(day.date);
            if (day.date === today) th.classList.add('today');
            tr.appendChild(th);
            const byRate = new Map(visibleSlots(day.slots).map(slot => [slot.rateId, slot]));
            headerSlots.forEach(header => {
                const td = document.createElement('td');
                const slot = byRate.get(header.rateId);
                if (slot) td.appendChild(renderSlot(slot, day));
                else td.innerHTML = '<span class="calendar-v2-missing">—</span>';
                tr.appendChild(td);
            });
            tbody.appendChild(tr);
        });
        table.appendChild(tbody);
        scroll.appendChild(table);
    }

    async function refresh(reason) {
        if (state.loading) {
            state.queued = true;
            state.queuedReason = reason || 'queued';
            return;
        }
        const room = currentRoom();
        if (!room) {
            statusBox.textContent = 'Chưa có phòng đang hoạt động.';
            statusBox.className = 'calendar-v2-status show';
            document.documentElement.dataset.calendarV2 = 'no-rooms';
            return;
        }
        const serial = ++state.requestSerial;
        state.loading = true;
        panel.classList.add('loading');
        statusBox.textContent = 'Đang tải lịch phòng…';
        statusBox.className = 'calendar-v2-status show';
        try {
            const query = new URLSearchParams({ from: state.from, days: String(state.days) });
            const data = await DeLongApi.get(`/api/admin/properties/${propertyId}/operations/availability/rooms/${room.id}?${query}`);
            if (serial === state.requestSerial) render(data);
            document.documentElement.dataset.calendarV2 = reason || 'loaded';
        } catch (error) {
            showError(error?.message || 'Không thể tải lịch theo khung giờ.', 'request-error');
        } finally {
            state.loading = false;
            panel.classList.remove('loading');
            if (state.queued) {
                const queuedReason = state.queuedReason || 'queued';
                state.queued = false;
                state.queuedReason = '';
                setTimeout(() => refresh(queuedReason), 0);
            }
        }
    }

    function moveRoom(amount) {
        const rows = rooms();
        if (!rows.length) return;
        state.roomIndex = (state.roomIndex + amount + rows.length) % rows.length;
        refresh('room');
    }

    panel.querySelector('[data-v2-room-prev]').addEventListener('click', () => moveRoom(-1));
    panel.querySelector('[data-v2-room-next]').addEventListener('click', () => moveRoom(1));
    panel.querySelector('[data-v2-date-prev]').addEventListener('click', () => { state.from = addDays(state.from, -7); refresh('date'); });
    panel.querySelector('[data-v2-date-next]').addEventListener('click', () => { state.from = addDays(state.from, 7); refresh('date'); });
    panel.querySelector('[data-v2-today]').addEventListener('click', () => { state.from = today || state.from; refresh('today'); });

    document.addEventListener('delong:operations-change', event => {
        if (event.detail?.propertyId && event.detail.propertyId !== propertyId) return;
        const changedRoomId = event.detail?.roomId;
        if (changedRoomId && changedRoomId !== currentRoom()?.id) return;
        refresh('realtime');
        setTimeout(() => refresh('realtime-settled'), 500);
    });
    window.addEventListener('focus', () => refresh('focus'));
    document.addEventListener('visibilitychange', () => { if (!document.hidden) refresh('visible'); });
    state.pollTimer = setInterval(() => { if (!document.hidden) refresh('poll'); }, 15000);
    window.addEventListener('beforeunload', () => { if (state.pollTimer) clearInterval(state.pollTimer); }, { once: true });

    document.documentElement.dataset.calendarV2 = 'initializing';
    refresh('initial');
})();