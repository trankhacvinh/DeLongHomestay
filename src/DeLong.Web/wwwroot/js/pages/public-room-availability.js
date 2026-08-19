(function () {
    const page = document.querySelector('.public-room-page');
    const rateBlock = document.querySelector('.public-room-rate-block');
    if (!page || !rateBlock) return;

    const path = window.location.pathname.split('/').filter(Boolean).map(decodeURIComponent);
    const hIndex = path.indexOf('h');
    const siteSlug = hIndex >= 0 && path.length > hIndex + 1 ? path[hIndex + 1] : '';
    const roomsIndex = path.lastIndexOf('rooms');
    const roomRef = roomsIndex >= 0 && path.length > roomsIndex + 1 ? path[roomsIndex + 1] : '';
    if (!roomRef) return;

    const todayInput = document.querySelector('.public-room-quick-book input[name="date"]');
    const today = todayInput?.value || new Date().toISOString().slice(0, 10);
    const bookingForm = document.querySelector('.public-room-quick-book');
    const bookingBase = bookingForm?.getAttribute('action') || (siteSlug ? `/h/${encodeURIComponent(siteSlug)}/booking` : '/booking');

    const state = {
        from: today,
        days: 10,
        roomCode: '',
        timeZone: 'Asia/Ho_Chi_Minh',
        selected: null,
        loading: false,
        queued: false,
        source: null,
        pollTimer: null
    };

    const section = document.createElement('section');
    section.className = 'public-room-content-block public-room-availability-block';
    section.innerHTML = [
        '<div class="public-room-availability-head">',
        '  <div><span class="public-eyebrow">Lịch phòng</span><h2>Chọn thời gian phù hợp</h2><p>Xem nhanh khung nào còn trống. Phần trống còn lại của một khung linh hoạt chỉ có thể đặt qua nhân viên.</p></div>',
        '  <div class="public-room-availability-date-actions"><button type="button" data-availability-prev>‹ 7 ngày</button><button type="button" data-availability-today>Hôm nay</button><button type="button" data-availability-next>7 ngày ›</button></div>',
        '</div>',
        '<div class="public-room-availability-legend"><span><i class="booked"></i>Đã đặt</span><span><i class="selected"></i>Đang chọn</span><span><i class="available"></i>Còn trống</span><span><i class="partial"></i>Trống một phần</span></div>',
        '<div class="public-room-availability-range" data-availability-range></div>',
        '<div class="public-room-availability-status" data-availability-status></div>',
        '<div class="public-room-availability-scroll" data-availability-scroll></div>',
        '<div class="public-room-availability-choice" data-availability-choice hidden><div><small>Đang chọn</small><strong data-availability-choice-title></strong><span data-availability-choice-note></span></div><a class="public-btn public-btn-primary" data-availability-book href="#">Đặt khung này</a></div>'
    ].join('');
    rateBlock.after(section);

    const scroll = section.querySelector('[data-availability-scroll]');
    const status = section.querySelector('[data-availability-status]');
    const range = section.querySelector('[data-availability-range]');
    const choice = section.querySelector('[data-availability-choice]');
    const choiceTitle = section.querySelector('[data-availability-choice-title]');
    const choiceNote = section.querySelector('[data-availability-choice-note]');
    const bookLink = section.querySelector('[data-availability-book]');

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

    function dateText(key) {
        const value = parseDateKey(key);
        const weekday = new Intl.DateTimeFormat('vi-VN', { weekday: 'short', timeZone: 'UTC' }).format(value);
        const date = new Intl.DateTimeFormat('vi-VN', { day: '2-digit', month: '2-digit', timeZone: 'UTC' }).format(value);
        return `${weekday} · ${date}`;
    }

    function timeText(value) {
        return new Intl.DateTimeFormat('vi-VN', {
            timeZone: state.timeZone,
            hour: '2-digit', minute: '2-digit', hour12: false
        }).format(new Date(value));
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

    function visibleSlots(slots) {
        return (slots || []).filter(slot => Number(slot.rateType) !== 2);
    }

    function clearChoice() {
        state.selected = null;
        choice.hidden = true;
        section.querySelectorAll('.public-room-availability-slot.is-selected').forEach(node => node.classList.remove('is-selected'));
    }

    function selectAvailable(slot, day, slotNode) {
        if (slot.state !== 'available') return;
        section.querySelectorAll('.public-room-availability-slot.is-selected').forEach(node => node.classList.remove('is-selected'));
        slotNode.classList.add('is-selected');
        state.selected = { slot, day };
        choiceTitle.textContent = `${dateText(day.date)} · ${timeText(slot.startUtc)}–${timeText(slot.endUtc)}`;
        choiceNote.textContent = `${slot.rateName} · ${Number(slot.price || 0).toLocaleString('vi-VN')}đ`;
        const query = new URLSearchParams({ date: day.date, room: state.roomCode, rate: slot.rateId });
        bookLink.href = `${bookingBase}?${query}`;
        bookLink.textContent = 'Đặt khung này';
        choice.hidden = false;
    }

    function explainPartial(slot, rangeItem) {
        clearChoice();
        choiceTitle.textContent = `Còn trống ${timeText(rangeItem.startUtc)}–${timeText(rangeItem.endUtc)}`;
        choiceNote.textContent = 'Khung này đã có khách ở một phần thời gian. Vui lòng liên hệ homestay nếu muốn đặt giờ linh hoạt.';
        bookLink.href = bookingBase;
        bookLink.textContent = 'Xem cách đặt';
        choice.hidden = false;
    }

    function renderSlot(slot, day) {
        const cell = document.createElement('div');
        cell.className = `public-room-availability-slot state-${slot.state}`;
        const track = document.createElement('div');
        track.className = 'public-room-availability-track';

        (slot.free || []).forEach(rangeItem => {
            const segment = document.createElement('button');
            segment.type = 'button';
            segment.className = 'public-room-availability-segment free';
            segment.setAttribute('style', segmentStyle(rangeItem.startUtc, rangeItem.endUtc, slot.startUtc, slot.endUtc));
            segment.title = slot.state === 'available'
                ? `Còn trống ${timeText(rangeItem.startUtc)}–${timeText(rangeItem.endUtc)}`
                : `Còn trống ${timeText(rangeItem.startUtc)}–${timeText(rangeItem.endUtc)} · liên hệ để đặt giờ linh hoạt`;
            segment.addEventListener('click', event => {
                event.stopPropagation();
                if (slot.state === 'available') selectAvailable(slot, day, cell);
                else explainPartial(slot, rangeItem);
            });
            track.appendChild(segment);
        });

        (slot.occupied || []).forEach(rangeItem => {
            const segment = document.createElement('span');
            segment.className = `public-room-availability-segment occupied ${rangeItem.kind === 'held' ? 'held' : 'booked'}`;
            segment.setAttribute('style', segmentStyle(rangeItem.startUtc, rangeItem.endUtc, slot.startUtc, slot.endUtc));
            segment.title = rangeItem.kind === 'held' ? 'Đang được khách khác giữ tạm' : 'Đã có khách đặt';
            track.appendChild(segment);
        });

        const caption = document.createElement('small');
        if (slot.state === 'available') caption.textContent = 'Còn trống';
        else if (slot.state === 'occupied') caption.textContent = 'Đã đặt';
        else {
            const freeText = (slot.free || []).map(item => `${timeText(item.startUtc)}–${timeText(item.endUtc)}`).join(', ');
            caption.textContent = freeText ? `Còn ${freeText}` : 'Trống một phần';
        }
        cell.append(track, caption);
        if (slot.state === 'available') cell.addEventListener('click', () => selectAvailable(slot, day, cell));
        return cell;
    }

    function render(data) {
        state.roomCode = data.roomCode || roomRef;
        state.timeZone = data.timeZoneId || state.timeZone;
        range.textContent = `${data.roomName || state.roomCode} · ${dateText(state.from)} → ${dateText(addDays(state.from, state.days - 1))}`;
        status.className = 'public-room-availability-status';
        status.textContent = '';
        scroll.replaceChildren();
        clearChoice();
        bookLink.textContent = 'Đặt khung này';

        const calendar = Array.isArray(data.calendar) ? data.calendar : [];
        const headerSlots = visibleSlots(calendar[0]?.slots);
        if (!calendar.length || !headerSlots.length) {
            status.textContent = 'Phòng này chưa có khung giờ / qua đêm đang hoạt động để hiển thị.';
            status.classList.add('show');
            return;
        }

        const table = document.createElement('table');
        table.className = 'public-room-availability-table';
        const thead = document.createElement('thead');
        const header = document.createElement('tr');
        const first = document.createElement('th');
        first.textContent = 'Ngày';
        header.appendChild(first);
        headerSlots.forEach(slot => {
            const th = document.createElement('th');
            const strong = document.createElement('strong');
            strong.textContent = `${timeText(slot.startUtc)}–${timeText(slot.endUtc)}`;
            const small = document.createElement('small');
            small.textContent = slot.rateName;
            th.append(strong, small);
            header.appendChild(th);
        });
        thead.appendChild(header);
        table.appendChild(thead);

        const tbody = document.createElement('tbody');
        calendar.forEach(day => {
            const tr = document.createElement('tr');
            const th = document.createElement('th');
            th.textContent = dateText(day.date);
            if (day.date === today) th.classList.add('today');
            tr.appendChild(th);
            const byRate = new Map(visibleSlots(day.slots).map(slot => [slot.rateId, slot]));
            headerSlots.forEach(headerSlot => {
                const td = document.createElement('td');
                const slot = byRate.get(headerSlot.rateId);
                if (slot) td.appendChild(renderSlot(slot, day));
                else td.textContent = '—';
                tr.appendChild(td);
            });
            tbody.appendChild(tr);
        });
        table.appendChild(tbody);
        scroll.appendChild(table);
    }

    async function refresh() {
        if (state.loading) {
            state.queued = true;
            return;
        }
        state.loading = true;
        section.classList.add('loading');
        try {
            const query = new URLSearchParams({ room: roomRef, from: state.from, days: String(state.days) });
            if (siteSlug) query.set('siteSlug', siteSlug);
            const response = await fetch(`/api/public/room-availability?${query}`, { credentials: 'same-origin', headers: { Accept: 'application/json' } });
            if (!response.ok) throw new Error(`HTTP ${response.status}`);
            render(await response.json());
        } catch {
            status.textContent = 'Chưa thể tải lịch phòng. Bạn vẫn có thể dùng nút Đặt phòng bên cạnh để gửi yêu cầu.';
            status.className = 'public-room-availability-status show error';
        } finally {
            state.loading = false;
            section.classList.remove('loading');
            if (state.queued) {
                state.queued = false;
                setTimeout(refresh, 0);
            }
        }
    }

    function startRealtime() {
        if (!('EventSource' in window)) return;
        const query = new URLSearchParams({ room: roomRef });
        if (siteSlug) query.set('siteSlug', siteSlug);
        state.source = new EventSource(`/api/public/room-availability/stream?${query}`);
        state.source.addEventListener('availability', () => {
            refresh();
            setTimeout(refresh, 500);
        });
        state.source.addEventListener('open', refresh);
    }

    section.querySelector('[data-availability-prev]').addEventListener('click', () => { state.from = addDays(state.from, -7); refresh(); });
    section.querySelector('[data-availability-next]').addEventListener('click', () => { state.from = addDays(state.from, 7); refresh(); });
    section.querySelector('[data-availability-today]').addEventListener('click', () => { state.from = today; refresh(); });
    window.addEventListener('focus', refresh);
    document.addEventListener('visibilitychange', () => { if (!document.hidden) refresh(); });
    state.pollTimer = setInterval(() => { if (!document.hidden) refresh(); }, 15000);
    window.addEventListener('beforeunload', () => {
        if (state.pollTimer) clearInterval(state.pollTimer);
        state.source?.close();
    }, { once: true });

    refresh();
    startRealtime();
})();