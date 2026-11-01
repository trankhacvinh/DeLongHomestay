// Multi-room calendar overview ("Tổng quan tất cả phòng").
// Every room the signed-in user may see is laid out side by side like the team's Excel sheet:
// day rows on the left, one block of time-slot columns per room. Pan with native scrolling,
// pinch (or Ctrl + wheel) to zoom, tap a free cell to book, tap a booking to open it.
// Which rooms are returned is decided by the server from the user's branch assignments.
(function (global) {
    const STORAGE_KEY = 'delong.calendar.overview';
    const MIN_SCALE = 0.35;
    const MAX_SCALE = 1.6;
    const COMPACT_SCALE = 0.6;
    const WEEKDAYS = ['Chủ Nhật', 'Thứ Hai', 'Thứ Ba', 'Thứ Tư', 'Thứ Năm', 'Thứ Sáu', 'Thứ Bảy'];
    const WEEKDAYS_SHORT = ['CN', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7'];
    const ROOM_COLORS = ['#C97B6B', '#2F5D6B', '#7A8F3C', '#8A5A9E', '#B8873A', '#3D7F8F', '#A35468', '#4F6FA8', '#6B7F5C', '#9A6B3F'];

    function parseKey(key) {
        const [y, m, d] = String(key || '').split('-').map(Number);
        return new Date(Date.UTC(y, m - 1, d));
    }
    function toKey(date) {
        return `${date.getUTCFullYear()}-${String(date.getUTCMonth() + 1).padStart(2, '0')}-${String(date.getUTCDate()).padStart(2, '0')}`;
    }
    function addDays(key, amount) {
        const date = parseKey(key);
        date.setUTCDate(date.getUTCDate() + amount);
        return toKey(date);
    }
    function monthStart(key) { return `${String(key).slice(0, 7)}-01`; }
    function monthLength(key) {
        const date = parseKey(monthStart(key));
        return new Date(Date.UTC(date.getUTCFullYear(), date.getUTCMonth() + 1, 0)).getUTCDate();
    }
    function addMonths(key, amount) {
        const date = parseKey(monthStart(key));
        date.setUTCMonth(date.getUTCMonth() + amount);
        return toKey(date);
    }
    function clamp(value, min, max) { return Math.max(min, Math.min(max, value)); }
    function readPrefs() {
        try { return JSON.parse(localStorage.getItem(STORAGE_KEY) || '{}') || {}; } catch { return {}; }
    }
    function writePrefs(prefs) {
        try { localStorage.setItem(STORAGE_KEY, JSON.stringify(prefs)); } catch { }
    }

    const timeFormatters = new Map();
    function timeText(utcValue, timeZone) {
        let formatter = timeFormatters.get(timeZone);
        if (!formatter) {
            formatter = new Intl.DateTimeFormat('en-GB', { timeZone, hour: '2-digit', minute: '2-digit', hourCycle: 'h23' });
            timeFormatters.set(timeZone, formatter);
        }
        return formatter.format(new Date(utcValue));
    }
    function minutesOfDay(utcValue, timeZone) {
        const [h, m] = timeText(utcValue, timeZone).split(':').map(Number);
        return h * 60 + m;
    }
    function priceText(value) {
        const amount = Number(value || 0);
        if (!amount) return '';
        if (amount >= 1000 && amount % 1000 === 0) return `${amount / 1000}k`;
        return new Intl.NumberFormat('vi-VN').format(amount);
    }
    function visibleSlots(slots) {
        return (slots || []).filter(slot => Number(slot.rateType) !== 2);
    }
    function roomColor(index) { return ROOM_COLORS[index % ROOM_COLORS.length]; }

    function bookingVisual(range, colors) {
        colors = colors || {};
        if (range?.isRoomBlock) return { key: 'locked', color: '#64748B', label: 'Tạm khóa' };
        if (Number(range?.balanceAmount || 0) > 0) return { key: 'unpaid', color: colors.unpaidColor || '#C94B4B', label: 'Chưa thanh toán hết' };
        if (![5, 6].includes(Number(range?.status)) && range?.hasSpecialRequest === true)
            return { key: 'special', color: colors.specialRequestColor || '#7C5CC4', label: 'Có yêu cầu đặc biệt' };
        let visual;
        if (range?.isFlexibleTime === true) visual = { key: 'flexible', color: colors.flexibleTimeColor || '#D6A72C', label: 'Giờ linh động' };
        else if (range?.isMixedSlot === true) visual = { key: 'mixed', color: colors.mixedSlotColor || '#287D9B', label: 'Booking nhiều khung' };
        else if (Number(range?.status) === 0) visual = { key: 'requested', color: colors.requestedColor || '#64748B', label: 'Yêu cầu' };
        else if (Number(range?.status) === 1) visual = { key: 'held', color: colors.heldColor || '#D39B3C', label: 'Giữ phòng' };
        else if (Number(range?.status) === 3) visual = { key: 'checkedIn', color: colors.checkedInColor || '#176B63', label: 'Đã nhận phòng' };
        else visual = { key: 'confirmed', color: colors.confirmedColor || '#397967', label: Number(range?.status) === 4 ? 'Hoàn tất' : 'Đã xác nhận' };
        return range?.displayColor ? { ...visual, color: range.displayColor } : visual;
    }

    function segmentPosition(startUtc, endUtc, slot) {
        const slotStart = new Date(slot.startUtc).getTime();
        const total = Math.max(1, new Date(slot.endUtc).getTime() - slotStart);
        const left = clamp(((new Date(startUtc).getTime() - slotStart) / total) * 100, 0, 100);
        const right = clamp(((new Date(endUtc).getTime() - slotStart) / total) * 100, left, 100);
        return { left, width: Math.max(1.5, right - left) };
    }

    function create(host, options) {
        options = options || {};
        const prefs = readPrefs();
        const today = options.today || toKey(new Date());
        const state = {
            period: prefs.period === 'month' ? 'month' : '14',
            from: '',
            days: 14,
            scope: typeof prefs.scope === 'string' ? prefs.scope : '',
            scale: clamp(Number(prefs.scale) || 0.8, MIN_SCALE, MAX_SCALE),
            data: null,
            model: [],
            active: false,
            loading: false,
            queued: false,
            serial: 0,
            signature: '',
            pollTimer: null,
            changeTimer: null,
            restoredScroll: false
        };
        resetRange();

        host.innerHTML = [
            '<div class="cal-ov-toolbar">',
            '  <div class="cal-ov-range"><button type="button" class="cal-ov-arrow" data-ov-prev aria-label="Khoảng trước">‹</button><strong data-ov-range-label>—</strong><button type="button" class="cal-ov-arrow" data-ov-next aria-label="Khoảng sau">›</button><button type="button" data-ov-today>Hôm nay</button></div>',
            '  <div class="cal-ov-period" role="group" aria-label="Số ngày hiển thị"><button type="button" data-ov-period="14">14 ngày</button><button type="button" data-ov-period="month">Cả tháng</button></div>',
            '  <div class="cal-ov-pickers"><label class="cal-ov-scope" data-ov-scope-wrap hidden><span>Chi nhánh</span><select data-ov-scope aria-label="Chi nhánh"></select></label>',
            '  <label class="cal-ov-jump"><span>Đến phòng</span><select data-ov-jump aria-label="Đến phòng"><option value="">Đến phòng…</option></select></label></div>',
            '  <div class="cal-ov-zoom" role="group" aria-label="Thu phóng"><button type="button" data-ov-zoom-out aria-label="Thu nhỏ">−</button><output data-ov-zoom-label>80%</output><button type="button" data-ov-zoom-in aria-label="Phóng to">+</button><button type="button" data-ov-fit>Toàn cảnh</button></div>',
            '</div>',
            '<div class="cal-ov-status" data-ov-status role="status">Đang tải tổng quan…</div>',
            '<div class="cal-ov-viewport" data-ov-viewport tabindex="0" aria-label="Lịch tổng quan tất cả phòng. Kéo để di chuyển, chụm hai ngón để thu phóng."><div class="cal-ov-stage" data-ov-stage></div></div>',
            '<p class="cal-ov-hint">Kéo để di chuyển · chụm hai ngón (hoặc Ctrl + cuộn chuột) để thu phóng · chạm đúp để xem toàn cảnh · chạm ô trống để đặt phòng</p>'
        ].join('');

        const $ = selector => host.querySelector(selector);
        const viewport = $('[data-ov-viewport]');
        const stage = $('[data-ov-stage]');
        const statusBox = $('[data-ov-status]');
        const scopeWrap = $('[data-ov-scope-wrap]');
        const scopeSelect = $('[data-ov-scope]');
        const jumpSelect = $('[data-ov-jump]');

        function savePrefs() {
            writePrefs({ period: state.period, scope: state.scope, scale: Math.round(state.scale * 100) / 100 });
        }

        function resetRange() {
            if (state.period === 'month') {
                state.from = monthStart(today);
                state.days = monthLength(today);
            } else {
                state.from = addDays(today, -1);
                state.days = 14;
            }
        }

        function shiftRange(direction) {
            if (state.period === 'month') {
                state.from = addMonths(state.from, direction);
                state.days = monthLength(state.from);
            } else {
                state.from = addDays(state.from, direction * 14);
            }
            load('range');
        }

        function rangeLabel() {
            const fmt = key => { const d = parseKey(key); return `${String(d.getUTCDate()).padStart(2, '0')}/${String(d.getUTCMonth() + 1).padStart(2, '0')}`; };
            if (state.period === 'month') {
                const d = parseKey(state.from);
                return `Tháng ${d.getUTCMonth() + 1}/${d.getUTCFullYear()}`;
            }
            return `${fmt(state.from)} → ${fmt(addDays(state.from, state.days - 1))}`;
        }

        function setStatus(message, kind) {
            statusBox.textContent = message || '';
            statusBox.className = `cal-ov-status${message ? ' show' : ''}${kind ? ` ${kind}` : ''}`;
        }

        function syncToolbar() {
            $('[data-ov-range-label]').textContent = rangeLabel();
            host.querySelectorAll('[data-ov-period]').forEach(button => button.setAttribute('aria-pressed', String(button.dataset.ovPeriod === state.period)));
            $('[data-ov-zoom-label]').textContent = `${Math.round(state.scale * 100)}%`;
            $('[data-ov-zoom-out]').disabled = state.scale <= MIN_SCALE + 0.001;
            $('[data-ov-zoom-in]').disabled = state.scale >= MAX_SCALE - 0.001;
        }

        // ---- data ----------------------------------------------------------------------------
        async function load(reason) {
            if (!state.active) return;
            if (state.loading) { state.queued = true; return; }
            const serial = ++state.serial;
            const foreground = reason !== 'poll' && reason !== 'realtime';
            state.loading = true;
            syncToolbar();
            if (foreground) { host.classList.add('loading'); setStatus('Đang tải tổng quan…'); }
            try {
                const query = new URLSearchParams({ from: state.from, days: String(state.days) });
                if (state.scope) query.set('propertyId', state.scope);
                let data;
                try {
                    data = await global.DeLongApi.get(`/api/admin/calendar/overview?${query}`);
                } catch (error) {
                    // A remembered branch may no longer be assigned to this account: fall back to all branches.
                    if (state.scope) { state.scope = ''; savePrefs(); state.loading = false; return load(reason); }
                    throw error;
                }
                if (serial !== state.serial) return;
                const signature = JSON.stringify(data);
                if (signature !== state.signature || foreground) {
                    state.signature = signature;
                    state.data = data;
                    buildModel();
                    renderScope();
                    renderJump();
                    renderTable();
                }
                const roomCount = state.model.length;
                if (!roomCount) setStatus('Không có phòng đang hoạt động trong phạm vi chi nhánh của tài khoản này.');
                else if (data?.truncated) setStatus('Có quá nhiều phòng để hiện cùng lúc. Hãy chọn từng chi nhánh để xem đủ.', 'warn');
                else setStatus('');
            } catch (error) {
                if (serial === state.serial) setStatus(error?.message || 'Không thể tải lịch tổng quan.', 'error');
            } finally {
                state.loading = false;
                host.classList.remove('loading');
                if (state.queued) { state.queued = false; setTimeout(() => load('queued'), 0); }
            }
        }

        function buildModel() {
            const model = [];
            let colorIndex = 0;
            (state.data?.properties || []).forEach((property, propertyIndex) => {
                (property.rooms || []).forEach((room, roomIndex) => {
                    const columns = [];
                    const seen = new Map();
                    (room.calendar || []).forEach(day => visibleSlots(day.slots).forEach(slot => {
                        if (seen.has(slot.rateId)) return;
                        const column = { rateId: slot.rateId, rateName: slot.rateName, price: slot.price, startUtc: slot.startUtc, endUtc: slot.endUtc,
                            sortKey: minutesOfDay(slot.startUtc, property.timeZoneId) };
                        seen.set(slot.rateId, column);
                        columns.push(column);
                    }));
                    columns.sort((a, b) => a.sortKey - b.sortKey);
                    const days = (room.calendar || []).map(day => {
                        const byRate = new Map(visibleSlots(day.slots).map(slot => [slot.rateId, slot]));
                        return { date: day.date, slots: columns.map(column => byRate.get(column.rateId) || null) };
                    });
                    model.push({
                        key: `${property.id}:${room.roomId}`,
                        property,
                        room,
                        columns,
                        days,
                        color: roomColor(colorIndex++),
                        firstOfProperty: roomIndex === 0 && propertyIndex > 0
                    });
                });
            });
            state.model = model;
        }

        function renderScope() {
            const accessible = state.data?.accessibleProperties || [];
            scopeWrap.hidden = accessible.length < 2;
            const fragment = document.createDocumentFragment();
            const all = document.createElement('option');
            all.value = '';
            all.textContent = 'Tất cả chi nhánh';
            fragment.appendChild(all);
            accessible.forEach(property => {
                const option = document.createElement('option');
                option.value = property.id;
                option.textContent = property.name;
                fragment.appendChild(option);
            });
            scopeSelect.replaceChildren(fragment);
            scopeSelect.value = state.scope;
        }

        function renderJump() {
            const fragment = document.createDocumentFragment();
            const placeholder = document.createElement('option');
            placeholder.value = '';
            placeholder.textContent = 'Đến phòng…';
            fragment.appendChild(placeholder);
            const multi = (state.data?.properties || []).length > 1;
            let group = null;
            let groupId = '';
            state.model.forEach(item => {
                if (multi && item.property.id !== groupId) {
                    group = document.createElement('optgroup');
                    group.label = item.property.name;
                    groupId = item.property.id;
                    fragment.appendChild(group);
                }
                const option = document.createElement('option');
                option.value = item.key;
                option.textContent = item.room.roomName;
                (multi ? group : fragment).appendChild(option);
            });
            jumpSelect.replaceChildren(fragment);
            jumpSelect.value = '';
        }

        // ---- rendering -----------------------------------------------------------------------
        function el(tag, className, text) {
            const node = document.createElement(tag);
            if (className) node.className = className;
            if (text !== undefined) node.textContent = text;
            return node;
        }

        function renderTable() {
            const position = { left: viewport.scrollLeft, top: viewport.scrollTop };
            const multi = (state.data?.properties || []).length > 1;
            const table = el('table', 'cal-ov-table');
            const thead = el('thead');
            const head1 = el('tr', 'cal-ov-h1');
            const head2 = el('tr', 'cal-ov-h2');
            const corner = el('th', 'cal-ov-corner');
            corner.rowSpan = 2;
            corner.append(el('b', '', 'Thứ'), el('small', '', 'Ngày'));
            head1.appendChild(corner);
            state.model.forEach(item => {
                const span = Math.max(1, item.columns.length);
                const th = el('th', 'cal-ov-room');
                th.colSpan = span;
                th.dataset.roomKey = item.key;
                th.style.setProperty('--room-color', item.color);
                if (item.firstOfProperty) th.classList.add('group-start');
                const name = el('span', 'cal-ov-room-name', item.room.roomName);
                th.appendChild(name);
                if (multi) th.appendChild(el('small', 'cal-ov-room-branch', item.property.name));
                if (item.room.isBookingLocked) th.appendChild(el('small', 'cal-ov-room-lock', 'Đang khóa'));
                head1.appendChild(th);
                if (!item.columns.length) {
                    const empty = el('th', 'cal-ov-slot room-start', '—');
                    if (item.firstOfProperty) empty.classList.add('group-start');
                    head2.appendChild(empty);
                }
                item.columns.forEach((column, columnIndex) => {
                    const th2 = el('th', 'cal-ov-slot');
                    if (columnIndex === 0) th2.classList.add('room-start');
                    if (columnIndex === 0 && item.firstOfProperty) th2.classList.add('group-start');
                    th2.appendChild(el('b', '', `${timeText(column.startUtc, item.property.timeZoneId)}–${timeText(column.endUtc, item.property.timeZoneId)}`));
                    const price = priceText(column.price);
                    if (price) th2.appendChild(el('small', '', `(${price})`));
                    th2.title = column.rateName || '';
                    head2.appendChild(th2);
                });
            });
            thead.append(head1, head2);
            table.appendChild(thead);

            const tbody = el('tbody');
            const dayCount = state.days;
            for (let dayIndex = 0; dayIndex < dayCount; dayIndex++) {
                const dateKey = addDays(state.from, dayIndex);
                const tr = el('tr');
                const date = parseKey(dateKey);
                const weekday = date.getUTCDay();
                if (dateKey === today) tr.classList.add('today');
                if (weekday === 0 || weekday === 6) tr.classList.add('weekend');
                const th = el('th', 'cal-ov-day');
                const weekdayLabel = el('b');
                weekdayLabel.append(el('span', 'wd-long', WEEKDAYS[weekday]), el('span', 'wd-short', WEEKDAYS_SHORT[weekday]));
                th.append(weekdayLabel, el('small', '', `${String(date.getUTCDate()).padStart(2, '0')}/${String(date.getUTCMonth() + 1).padStart(2, '0')}`));
                tr.appendChild(th);
                state.model.forEach((item, roomIndex) => {
                    const day = item.days.find(entry => entry.date === dateKey);
                    if (!item.columns.length) {
                        const td = el('td', 'cal-ov-cell room-start missing');
                        if (item.firstOfProperty) td.classList.add('group-start');
                        tr.appendChild(td);
                        return;
                    }
                    item.columns.forEach((column, columnIndex) => {
                        const slot = day?.slots[columnIndex] || null;
                        const td = el('td', 'cal-ov-cell');
                        if (columnIndex === 0) td.classList.add('room-start');
                        if (columnIndex === 0 && item.firstOfProperty) td.classList.add('group-start');
                        td.dataset.r = String(roomIndex);
                        td.dataset.c = String(columnIndex);
                        td.dataset.d = dateKey;
                        if (!slot) { td.classList.add('missing'); tr.appendChild(td); return; }
                        td.classList.add(`state-${slot.state}`);
                        if (item.room.isBookingLocked) td.classList.add('room-locked');
                        const previous = columnIndex > 0 ? day.slots[columnIndex - 1] : null;
                        (slot.occupied || []).forEach((range, rangeIndex) => {
                            const visual = bookingVisual(range, item.property.colors);
                            const position = segmentPosition(range.startUtc, range.endUtc, slot);
                            const segment = el('span', `cal-ov-booking booking-${visual.key}`);
                            segment.dataset.o = String(rangeIndex);
                            segment.style.left = `${position.left}%`;
                            segment.style.width = `${position.width}%`;
                            segment.style.backgroundColor = visual.color;
                            const continues = !!range.bookingId && !!previous && (previous.occupied || []).some(other => other.bookingId === range.bookingId);
                            const name = range.isRoomBlock ? (range.blockReason || 'Khóa') : (range.customerName || '');
                            segment.title = `${visual.label}${name ? ` · ${name}` : ''} · ${timeText(range.startUtc, item.property.timeZoneId)}–${timeText(range.endUtc, item.property.timeZoneId)}`;
                            if (name && !continues) segment.appendChild(el('span', 'cal-ov-guest', name));
                            td.appendChild(segment);
                        });
                        tr.appendChild(td);
                    });
                });
                tbody.appendChild(tr);
            }
            table.appendChild(tbody);
            stage.replaceChildren(table);
            applyScale();
            if (!state.restoredScroll) {
                state.restoredScroll = true;
                const todayRow = stage.querySelector('tr.today');
                if (todayRow && position.top === 0) viewport.scrollTop = Math.max(0, todayRow.offsetTop - headerHeight() - 4);
            } else {
                viewport.scrollLeft = position.left;
                viewport.scrollTop = position.top;
            }
        }

        function headerHeight() {
            return stage.querySelector('thead')?.offsetHeight || 0;
        }

        // ---- zoom ----------------------------------------------------------------------------
        function applyScale() {
            viewport.style.setProperty('--ov-s', String(state.scale));
            viewport.classList.toggle('is-compact', state.scale < COMPACT_SCALE);
            syncToolbar();
        }

        // Zoom around a point given in viewport coordinates, keeping that content point in place.
        function zoomTo(nextScale, focusX, focusY, extraDx, extraDy) {
            const previous = state.scale;
            nextScale = clamp(nextScale, MIN_SCALE, MAX_SCALE);
            const rect = viewport.getBoundingClientRect();
            const fx = focusX ?? rect.width / 2;
            const fy = focusY ?? rect.height / 2;
            const contentX = viewport.scrollLeft + fx;
            const contentY = viewport.scrollTop + fy;
            state.scale = nextScale;
            applyScale();
            const ratio = nextScale / previous;
            viewport.scrollLeft = contentX * ratio - fx - (extraDx || 0);
            viewport.scrollTop = contentY * ratio - fy - (extraDy || 0);
            savePrefs();
        }

        function fitScale() {
            const table = stage.querySelector('table');
            if (!table) return state.scale;
            const naturalWidth = table.offsetWidth / state.scale;
            return clamp((viewport.clientWidth - 4) / Math.max(1, naturalWidth), MIN_SCALE, MAX_SCALE);
        }

        let lastZoomAt = 0;
        let clickTimer = null;
        function toggleFit(focusX, focusY) {
            lastZoomAt = Date.now();
            clearTimeout(clickTimer);
            const fit = fitScale();
            const target = Math.abs(state.scale - fit) < 0.02 ? 1 : fit;
            zoomTo(target, focusX, focusY);
        }

        // Pinch: preview with a CSS transform for smoothness, then commit the real cell sizes once.
        let pinch = null;
        function touchPoint(touch) {
            const rect = viewport.getBoundingClientRect();
            return { x: touch.clientX - rect.left, y: touch.clientY - rect.top };
        }
        function pinchInfo(touches) {
            const a = touchPoint(touches[0]);
            const b = touchPoint(touches[1]);
            return { distance: Math.hypot(a.x - b.x, a.y - b.y), x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 };
        }
        viewport.addEventListener('touchstart', event => {
            if (event.touches.length !== 2) return;
            const info = pinchInfo(event.touches);
            pinch = { start: info, ratio: 1, dx: 0, dy: 0, scrollLeft: viewport.scrollLeft, scrollTop: viewport.scrollTop };
            stage.style.transformOrigin = `${viewport.scrollLeft + info.x}px ${viewport.scrollTop + info.y}px`;
            stage.style.willChange = 'transform';
        }, { passive: true });
        viewport.addEventListener('touchmove', event => {
            if (!pinch || event.touches.length !== 2) return;
            event.preventDefault();
            const info = pinchInfo(event.touches);
            const raw = info.distance / Math.max(1, pinch.start.distance);
            pinch.ratio = clamp(state.scale * raw, MIN_SCALE, MAX_SCALE) / state.scale;
            pinch.dx = info.x - pinch.start.x;
            pinch.dy = info.y - pinch.start.y;
            stage.style.transform = `translate(${pinch.dx}px, ${pinch.dy}px) scale(${pinch.ratio})`;
        }, { passive: false });
        function endPinch() {
            if (!pinch) return;
            const current = pinch;
            pinch = null;
            stage.style.transform = '';
            stage.style.transformOrigin = '';
            stage.style.willChange = '';
            viewport.scrollLeft = current.scrollLeft;
            viewport.scrollTop = current.scrollTop;
            zoomTo(state.scale * current.ratio, current.start.x, current.start.y, current.dx, current.dy);
            lastTap = 0;
        }
        viewport.addEventListener('touchend', event => { if (event.touches.length < 2) endPinch(); });
        viewport.addEventListener('touchcancel', endPinch);
        // Safari fires its own gesture events; keep the page from zooming instead of the grid.
        ['gesturestart', 'gesturechange'].forEach(type => viewport.addEventListener(type, event => event.preventDefault()));

        viewport.addEventListener('wheel', event => {
            if (!event.ctrlKey && !event.metaKey) return;
            event.preventDefault();
            const rect = viewport.getBoundingClientRect();
            zoomTo(state.scale * Math.exp(-event.deltaY * 0.0025), event.clientX - rect.left, event.clientY - rect.top);
        }, { passive: false });

        // Double tap (touch) / double click (mouse) toggles between overview and 100%.
        let lastTap = 0;
        let tapStart = null;
        viewport.addEventListener('touchstart', event => {
            tapStart = event.touches.length === 1 ? { ...touchPoint(event.touches[0]), at: Date.now() } : null;
        }, { passive: true });
        viewport.addEventListener('touchend', event => {
            if (!tapStart || event.touches.length || event.changedTouches.length !== 1) return;
            const end = touchPoint(event.changedTouches[0]);
            const moved = Math.hypot(end.x - tapStart.x, end.y - tapStart.y) > 10;
            if (moved || Date.now() - tapStart.at > 300) { lastTap = 0; return; }
            const now = Date.now();
            if (now - lastTap < 300) {
                event.preventDefault();
                lastTap = 0;
                toggleFit(end.x, end.y);
            } else lastTap = now;
        });
        viewport.addEventListener('dblclick', event => {
            const rect = viewport.getBoundingClientRect();
            toggleFit(event.clientX - rect.left, event.clientY - rect.top);
        });

        // ---- interactions ----------------------------------------------------------------------
        stage.addEventListener('click', event => {
            const td = event.target.closest('td.cal-ov-cell');
            if (!td || td.classList.contains('missing')) return;
            const item = state.model[Number(td.dataset.r)];
            const columnIndex = Number(td.dataset.c);
            const day = item?.days.find(entry => entry.date === td.dataset.d);
            const slot = day?.slots[columnIndex];
            if (!item || !slot) return;
            const segment = event.target.closest('.cal-ov-booking');
            // Wait briefly so a double tap (zoom) does not also open a dialog.
            clearTimeout(clickTimer);
            clickTimer = setTimeout(() => {
                if (Date.now() - lastZoomAt < 350) return;
                const context = { propertyId: item.property.id, roomId: item.room.roomId, room: item.room, slot, day: { date: day.date } };
                if (segment) {
                    const range = slot.occupied[Number(segment.dataset.o)];
                    if (!range) return;
                    if (range.isRoomBlock) options.onOpenBlock?.({ ...context, range });
                    else options.onOpenBooking?.({ ...context, bookingId: range.bookingId });
                    return;
                }
                const free = slot.free || [];
                if (!free.length) return;
                const rect = td.getBoundingClientRect();
                const ratio = clamp((event.clientX - rect.left) / Math.max(1, rect.width), 0, 1);
                const at = new Date(slot.startUtc).getTime() + ratio * (new Date(slot.endUtc).getTime() - new Date(slot.startUtc).getTime());
                const range = free.find(r => new Date(r.startUtc).getTime() <= at && at <= new Date(r.endUtc).getTime())
                    || free.reduce((best, r) => (new Date(r.endUtc) - new Date(r.startUtc)) > (new Date(best.endUtc) - new Date(best.startUtc)) ? r : best, free[0]);
                options.onOpenFree?.({ ...context, range });
            }, 300);
        });

        function jumpToRoom(key) {
            const header = [...stage.querySelectorAll('th.cal-ov-room')].find(th => th.dataset.roomKey === key);
            if (!header) return;
            const dayColumn = stage.querySelector('th.cal-ov-corner')?.offsetWidth || 0;
            viewport.scrollTo({ left: Math.max(0, header.offsetLeft - dayColumn - 6), behavior: 'smooth' });
            header.classList.remove('flash');
            void header.offsetWidth;
            header.classList.add('flash');
        }

        host.querySelector('[data-ov-prev]').addEventListener('click', () => shiftRange(-1));
        host.querySelector('[data-ov-next]').addEventListener('click', () => shiftRange(1));
        host.querySelector('[data-ov-today]').addEventListener('click', () => { resetRange(); state.restoredScroll = false; load('today'); });
        host.querySelectorAll('[data-ov-period]').forEach(button => button.addEventListener('click', () => {
            if (state.period === button.dataset.ovPeriod) return;
            state.period = button.dataset.ovPeriod;
            resetRange();
            state.restoredScroll = false;
            savePrefs();
            load('period');
        }));
        scopeSelect.addEventListener('change', () => {
            state.scope = scopeSelect.value;
            savePrefs();
            viewport.scrollLeft = 0;
            load('scope');
        });
        jumpSelect.addEventListener('change', () => {
            if (jumpSelect.value) jumpToRoom(jumpSelect.value);
            jumpSelect.value = '';
        });
        host.querySelector('[data-ov-zoom-out]').addEventListener('click', () => zoomTo(state.scale - 0.1));
        host.querySelector('[data-ov-zoom-in]').addEventListener('click', () => zoomTo(state.scale + 0.1));
        host.querySelector('[data-ov-fit]').addEventListener('click', () => zoomTo(fitScale()));

        document.addEventListener('delong:operations-change', () => {
            if (!state.active) return;
            clearTimeout(state.changeTimer);
            state.changeTimer = setTimeout(() => load('realtime'), 600);
        });
        document.addEventListener('visibilitychange', () => { if (!document.hidden && state.active) load('realtime'); });

        function activate() {
            if (state.active) return;
            state.active = true;
            applyScale();
            load('activate');
            clearInterval(state.pollTimer);
            state.pollTimer = setInterval(() => { if (!document.hidden) load('poll'); }, 30000);
        }
        function deactivate() {
            state.active = false;
            clearInterval(state.pollTimer);
            state.pollTimer = null;
        }

        return { activate, deactivate, refresh: () => load('realtime') };
    }

    global.DeLongCalendarOverview = { create };
})(window);
