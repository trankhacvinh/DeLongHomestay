(function () {
    const calendars = document.querySelectorAll('[data-public-availability-calendar]');
    if (!calendars.length) return;

    const parseDate = value => {
        const [year, month, day] = String(value || '').split('-').map(Number);
        return new Date(Date.UTC(year, month - 1, day));
    };
    const dateKey = value => `${value.getUTCFullYear()}-${String(value.getUTCMonth() + 1).padStart(2, '0')}-${String(value.getUTCDate()).padStart(2, '0')}`;
    const addDays = (value, amount) => { const date = parseDate(value); date.setUTCDate(date.getUTCDate() + amount); return dateKey(date); };
    const localDateKey = (value, timeZone) => {
        const parts = new Intl.DateTimeFormat('en-US', {
            timeZone,
            year: 'numeric',
            month: '2-digit',
            day: '2-digit'
        }).formatToParts(value).reduce((result, part) => {
            result[part.type] = part.value;
            return result;
        }, {});
        return `${parts.year}-${parts.month}-${parts.day}`;
    };
    const today = localDateKey(new Date(), 'Asia/Ho_Chi_Minh');
    const dateLabel = value => {
        const date = parseDate(value);
        const weekday = date.getUTCDay() === 0 ? 'CN' : `T${date.getUTCDay() + 1}`;
        return `${weekday} - ${String(date.getUTCDate()).padStart(2, '0')}/${String(date.getUTCMonth() + 1).padStart(2, '0')}`;
    };
    const money = value => `${Number(value || 0).toLocaleString('vi-VN')}đ`;

    function createBookingModal() {
        const backdrop = document.createElement('div');
        backdrop.className = 'public-slot-booking-backdrop';
        backdrop.hidden = true;
        backdrop.innerHTML = '<section class="public-slot-booking-modal" role="dialog" aria-modal="true" aria-labelledby="public-slot-booking-title"><header><div><span>ĐẶT PHÒNG</span><h2 id="public-slot-booking-title">Hoàn tất thông tin đặt phòng</h2><p data-booking-modal-summary></p></div><button type="button" data-booking-modal-close aria-label="Đóng form đặt phòng">×</button></header><div class="public-slot-booking-frame-wrap"><div class="public-slot-booking-loading">Đang tải form đặt phòng…</div><iframe data-booking-modal-frame title="Form đặt phòng" loading="eager"></iframe></div></section>';
        document.body.appendChild(backdrop);
        const frame = backdrop.querySelector('[data-booking-modal-frame]');
        const summary = backdrop.querySelector('[data-booking-modal-summary]');
        const loading = backdrop.querySelector('.public-slot-booking-loading');
        const close = () => {
            backdrop.hidden = true;
            document.body.classList.remove('public-booking-modal-open');
            frame.removeAttribute('src');
        };
        backdrop.querySelector('[data-booking-modal-close]').addEventListener('click', close);
        frame.addEventListener('load', () => { loading.hidden = true; });
        return {
            open(url, text) {
                summary.textContent = text;
                loading.hidden = false;
                backdrop.hidden = false;
                document.body.classList.add('public-booking-modal-open');
                frame.src = url;
                backdrop.querySelector('[data-booking-modal-close]').focus();
            }
        };
    }

    const bookingModal = createBookingModal();

    calendars.forEach(section => {
        const host = section.querySelector('[data-availability-calendar-host]');
        let rooms = [];
        try { rooms = JSON.parse(section.querySelector('[data-availability-rooms]')?.textContent || '[]'); } catch { rooms = []; }

        const batchSize = 14;
        const rowHeight = 52;
        const overscan = 5;
        const scrollGestureGapMs = 240;
        const state = {
            roomIndex: 0,
            timeZone: 'Asia/Ho_Chi_Minh',
            days: [],
            nextFrom: today,
            loading: false,
            requestVersion: 0,
            renderQueued: false,
            batchRequestedInGesture: false,
            lastScrollInputAt: 0,
            selected: [],
            policy: { publicMaxConsecutiveSlotDays: 3, multiSlotDiscountTiers: [] }
        };

        host.innerHTML = `
            <div class="public-v2-roombar"><button type="button" data-room-prev aria-label="Phòng trước">‹</button><div><small>PHÒNG</small><strong data-room-name>—</strong><span data-room-meta></span></div><button type="button" data-room-next aria-label="Phòng tiếp theo">›</button></div>
            <div class="public-v2-legend"><span><i class="available"></i>Còn trống</span><span><i class="selected"></i>Đang chọn</span><span><i class="partial"></i>Trống một phần</span><span><i class="occupied"></i>Đã có khách</span></div>
            <div class="public-v2-status" data-calendar-status></div>
            <div class="public-v2-viewport-shell">
                <div class="public-v2-viewport" data-calendar-viewport tabindex="0" aria-label="Lịch phòng, kéo xuống để xem thêm ngày"><div class="public-v2-grid-head" data-calendar-head></div><div class="public-v2-virtual-spacer" data-calendar-top></div><div class="public-v2-virtual-rows" data-calendar-rows></div><div class="public-v2-virtual-spacer" data-calendar-bottom></div></div>
                <div class="public-v2-load-sentinel" data-calendar-sentinel role="status" aria-live="polite"><span>Đang tải thêm ngày…</span></div>
            </div>
            <div class="public-v2-selection" data-calendar-selection hidden><div><small data-selection-label></small><strong data-selection-total></strong></div><button type="button" class="clear" data-selection-clear>Xóa</button><button type="button" class="book" data-selection-book>Đặt phòng</button></div>`;
        const name = host.querySelector('[data-room-name]');
        const meta = host.querySelector('[data-room-meta]');
        const status = host.querySelector('[data-calendar-status]');
        const viewport = host.querySelector('[data-calendar-viewport]');
        const head = host.querySelector('[data-calendar-head]');
        const topSpacer = host.querySelector('[data-calendar-top]');
        const rows = host.querySelector('[data-calendar-rows]');
        const bottomSpacer = host.querySelector('[data-calendar-bottom]');
        const sentinel = host.querySelector('[data-calendar-sentinel]');
        const sentinelText = sentinel.querySelector('span');
        const selectionBar = host.querySelector('[data-calendar-selection]');
        const selectionLabel = host.querySelector('[data-selection-label]');
        const selectionTotal = host.querySelector('[data-selection-total]');

        const timeLabel = value => new Intl.DateTimeFormat('vi-VN', { timeZone: state.timeZone, hour: '2-digit', minute: '2-digit', hour12: false }).format(new Date(value));
        const currentRoom = () => rooms[state.roomIndex];
        const visibleSlots = day => (day?.slots || []).filter(slot => Number(slot.rateType) !== 2);
        const gridTemplate = count => `clamp(74px, 18%, 104px) repeat(${Math.max(1, count)}, minmax(0, 1fr))`;

        function bookingUrl(room) {
            const url = new URL(room.bookingUrl, window.location.origin);
            const first = state.selected[0];
            url.searchParams.set('date', first.date);
            url.searchParams.set('room', room.code);
            url.searchParams.set('rate', first.slot.rateId);
            url.searchParams.set('slots', state.selected.map(x => `${x.date}:${x.slot.rateId}`).join(','));
            url.searchParams.set('embed', '1');
            return `${url.pathname}${url.search}`;
        }

        const slotKey = (date, slot) => `${date}:${slot.rateId}`;
        const isSelected = (date, slot) => state.selected.some(x => slotKey(x.date, x.slot) === slotKey(date, slot));
        function flatSlots() {
            return state.days.flatMap(day => visibleSlots(day).map(slot => ({ date: day.date, slot })));
        }
        function nextCandidate() {
            if (!state.selected.length) return null;
            const all = flatSlots();
            const last = state.selected[state.selected.length - 1];
            const index = all.findIndex(x => slotKey(x.date, x.slot) === slotKey(last.date, last.slot));
            return index >= 0 ? all[index + 1] || null : null;
        }
        function estimatedTotal() {
            const room = currentRoom();
            const selected = state.selected.map(x => ({ ...x, amount: Number(x.slot.price || 0), rule: 'standard' }));
            const slotsPerDay = visibleSlots(state.days[0]).length;
            if (room?.fullDayPricingEnabled && Number(room.fullDayPrice || 0) > 0 && slotsPerDay > 0) {
                const dates = [...new Set(selected.map(x => x.date))];
                dates.forEach(date => {
                    const group = selected.filter(x => x.date === date);
                    if (group.length !== slotsPerDay) return;
                    const list = group.reduce((sum, x) => sum + x.amount, 0);
                    group.forEach(x => { x.amount = list > 0 ? Number(room.fullDayPrice) * Number(x.slot.price || 0) / list : Number(room.fullDayPrice) / group.length; x.rule = 'full-day'; });
                });
            }
            const remaining = selected.filter(x => x.rule === 'standard');
            const tier = (state.policy.multiSlotDiscountTiers || []).filter(x => Number(x.minimumSlots) <= remaining.length).sort((a, b) => Number(b.minimumSlots) - Number(a.minimumSlots))[0];
            if (tier) remaining.forEach(x => { x.amount *= (100 - Number(tier.discountPercent || 0)) / 100; });
            return Math.round(selected.reduce((sum, x) => sum + x.amount, 0));
        }
        function updateSelectionBar() {
            selectionBar.hidden = state.selected.length === 0;
            if (!state.selected.length) return;
            const first = state.selected[0];
            const last = state.selected[state.selected.length - 1];
            selectionLabel.textContent = `${state.selected.length} khung · ${dateLabel(first.date)}${last.date !== first.date ? ` → ${dateLabel(last.date)}` : ''}`;
            selectionTotal.textContent = money(estimatedTotal());
        }
        function selectSlot(day, slot, trigger) {
            const existing = state.selected.findIndex(x => slotKey(x.date, x.slot) === slotKey(day.date, slot));
            if (existing >= 0 && existing === state.selected.length - 1) state.selected.pop();
            else if (state.selected.length === 0) state.selected.push({ date: day.date, slot });
            else {
                const next = nextCandidate();
                if (!next || slotKey(next.date, next.slot) !== slotKey(day.date, slot)) return;
                const firstDate = parseDate(state.selected[0].date);
                const currentDate = parseDate(day.date);
                const daySpan = Math.round((currentDate - firstDate) / 86400000) + 1;
                if (daySpan > Number(state.policy.publicMaxConsecutiveSlotDays || 3)) {
                    status.textContent = `Chỉ được chọn tối đa ${Number(state.policy.publicMaxConsecutiveSlotDays || 3)} ngày liên tiếp.`;
                    status.className = 'public-v2-status show error';
                    return;
                }
                state.selected.push({ date: day.date, slot });
            }
            const selected = isSelected(day.date, slot);
            trigger?.classList.toggle('is-selected', selected);
            trigger?.setAttribute('aria-pressed', String(selected));
            status.className = 'public-v2-status';
            updateSelectionBar();
            queueRender();
        }

        function renderHeader() {
            const slots = visibleSlots(state.days[0]);
            head.style.gridTemplateColumns = gridTemplate(slots.length);
            head.replaceChildren();
            const dateCell = document.createElement('strong');
            dateCell.className = 'public-v2-date-column';
            dateCell.textContent = 'Ngày';
            head.appendChild(dateCell);
            slots.forEach(slot => {
                const cell = document.createElement('div');
                cell.innerHTML = `<strong>${timeLabel(slot.startUtc)}–${timeLabel(slot.endUtc)}</strong><small>${slot.rateName}</small>`;
                head.appendChild(cell);
            });
        }

        function renderVirtualRows() {
            state.renderQueued = false;
            if (!state.days.length) return;
            const first = Math.max(0, Math.floor(viewport.scrollTop / rowHeight) - overscan);
            const visibleCount = Math.ceil(viewport.clientHeight / rowHeight) + (overscan * 2);
            const last = Math.min(state.days.length, first + visibleCount);
            const slotCount = visibleSlots(state.days[0]).length;
            topSpacer.style.height = `${first * rowHeight}px`;
            bottomSpacer.style.height = `${Math.max(0, state.days.length - last) * rowHeight}px`;
            rows.replaceChildren();

            state.days.slice(first, last).forEach(day => {
                const row = document.createElement('div');
                row.className = `public-v2-grid-row${day.date === today ? ' is-today' : ''}`;
                row.style.gridTemplateColumns = gridTemplate(slotCount);
                const dateCell = document.createElement('div');
                dateCell.className = 'public-v2-date-column';
                dateCell.innerHTML = `<strong>${dateLabel(day.date)}</strong>${day.date === today ? '<small>Hôm nay</small>' : ''}`;
                row.appendChild(dateCell);

                visibleSlots(day).forEach(slot => {
                    const button = document.createElement('button');
                    button.type = 'button';
                    button.className = `public-v2-slot-bar state-${slot.state}`;
                    const selected = isSelected(day.date, slot);
                    const next = nextCandidate();
                    const eligible = state.selected.length === 0 || selected || (next && slotKey(next.date, next.slot) === slotKey(day.date, slot));
                    if (selected) button.classList.add('is-selected');
                    if (slot.state === 'available' && !eligible) button.classList.add('is-ineligible');
                    const freeText = (slot.free || []).map(item => `${timeLabel(item.startUtc)}–${timeLabel(item.endUtc)}`).join(', ');
                    const stateText = slot.state === 'available' ? 'Còn trống' : slot.state === 'partial' ? `Còn ${freeText}` : 'Đã có khách';
                    button.innerHTML = `<span>${stateText}</span><small>${money(slot.price)}</small>`;
                    button.title = `${stateText} · ${money(slot.price)}`;
                    button.setAttribute('aria-label', `${dateLabel(day.date)}, ${slot.rateName}, ${stateText}, ${money(slot.price)}`);
                    button.setAttribute('aria-pressed', String(selected));
                    if (slot.state === 'available') {
                        button.addEventListener('click', () => selectSlot(day, slot, button));
                    } else button.disabled = true;
                    row.appendChild(button);
                });
                rows.appendChild(row);
            });

        }

        function queueRender() {
            if (state.renderQueued) return;
            state.renderQueued = true;
            window.requestAnimationFrame(renderVirtualRows);
        }

        function isNearLoadedEnd() {
            return viewport.scrollTop + viewport.clientHeight >= viewport.scrollHeight - (rowHeight * 5);
        }

        function beginScrollGesture(forceNew = false) {
            const now = window.performance.now();
            const isNewGesture = forceNew || now - state.lastScrollInputAt > scrollGestureGapMs;
            if (isNewGesture) {
                state.batchRequestedInGesture = false;
                sentinel.classList.remove('ready');
                if (isNearLoadedEnd() && !state.loading) window.requestAnimationFrame(handleViewportScroll);
            }
            state.lastScrollInputAt = now;
        }

        function handleViewportScroll() {
            queueRender();
            if (!isNearLoadedEnd()) {
                if (!state.loading) sentinel.classList.remove('ready');
                return;
            }
            if (state.loading) return;
            if (state.batchRequestedInGesture) {
                sentinelText.textContent = 'Vuốt thêm để tải tiếp';
                sentinel.classList.add('ready');
                return;
            }
            state.batchRequestedInGesture = true;
            void loadNextBatch();
        }

        async function loadNextBatch() {
            const room = currentRoom();
            if (!room || state.loading) return;
            const version = state.requestVersion;
            const loadingStartedAt = window.performance.now();
            state.loading = true;
            sentinelText.textContent = 'Đang tải thêm ngày…';
            sentinel.classList.remove('ready');
            sentinel.classList.add('show');
            try {
                const query = new URLSearchParams({ roomId: room.id, from: state.nextFrom, days: String(batchSize) });
                if (room.siteSlug) query.set('siteSlug', room.siteSlug);
                const response = await fetch(`/api/public/room-availability?${query}`, { credentials: 'same-origin', headers: { Accept: 'application/json' } });
                if (!response.ok) throw new Error(`HTTP ${response.status}`);
                const data = await response.json();
                if (version !== state.requestVersion) return;
                room.fullDayPricingEnabled = data.fullDayPricingEnabled === true;
                room.fullDayPrice = data.fullDayPrice;
                state.timeZone = data.timeZoneId || state.timeZone;
                const calendar = Array.isArray(data.calendar) ? data.calendar : [];
                if (!calendar.length) {
                    status.textContent = 'Phòng này chưa có khung giờ đang hoạt động.';
                    status.className = 'public-v2-status show';
                    return;
                }
                state.days.push(...calendar);
                state.nextFrom = addDays(calendar[calendar.length - 1].date, 1);
                status.className = 'public-v2-status';
                if (state.days.length === calendar.length) renderHeader();
                queueRender();
            } catch {
                if (version !== state.requestVersion) return;
                status.textContent = 'Chưa thể tải thêm lịch phòng. Kéo lại để thử lần nữa.';
                status.className = 'public-v2-status show error';
            } finally {
                const remainingIndicatorTime = 350 - (window.performance.now() - loadingStartedAt);
                if (remainingIndicatorTime > 0) {
                    await new Promise(resolve => window.setTimeout(resolve, remainingIndicatorTime));
                }
                if (version === state.requestVersion) {
                    state.loading = false;
                    sentinel.classList.remove('show');
                    if (isNearLoadedEnd() && state.batchRequestedInGesture) {
                        sentinelText.textContent = 'Vuốt thêm để tải tiếp';
                        sentinel.classList.add('ready');
                    }
                    section.classList.remove('loading');
                }
            }
        }

        function resetRoom() {
            const room = currentRoom();
            state.requestVersion += 1;
            state.days = [];
            state.nextFrom = today;
            state.loading = false;
            state.renderQueued = false;
            state.batchRequestedInGesture = false;
            state.lastScrollInputAt = 0;
            state.selected = [];
            updateSelectionBar();
            viewport.scrollTop = 0;
            head.replaceChildren();
            rows.replaceChildren();
            topSpacer.style.height = '0';
            bottomSpacer.style.height = '0';
            name.textContent = room?.name || '—';
            meta.textContent = room ? `${room.propertyName || ''} · ${room.code}` : '';
            status.textContent = 'Đang tải lịch phòng…';
            status.className = 'public-v2-status show';
            section.classList.add('loading');
            void loadNextBatch();
        }

        const changeRoom = amount => {
            if (!rooms.length) return;
            state.roomIndex = (state.roomIndex + amount + rooms.length) % rooms.length;
            resetRoom();
        };
        host.querySelector('[data-room-prev]').addEventListener('click', () => changeRoom(-1));
        host.querySelector('[data-room-next]').addEventListener('click', () => changeRoom(1));
        viewport.addEventListener('wheel', () => beginScrollGesture(), { passive: true });
        viewport.addEventListener('touchstart', () => beginScrollGesture(true), { passive: true });
        viewport.addEventListener('pointerdown', event => {
            if (event.pointerType !== 'touch') beginScrollGesture(true);
        }, { passive: true });
        viewport.addEventListener('keydown', event => {
            if (['ArrowDown', 'PageDown', 'End', ' '].includes(event.key)) beginScrollGesture(true);
        });
        viewport.addEventListener('scroll', handleViewportScroll, { passive: true });
        host.querySelector('[data-selection-clear]').addEventListener('click', () => { state.selected = []; updateSelectionBar(); queueRender(); });
        host.querySelector('[data-selection-book]').addEventListener('click', () => {
            if (!state.selected.length) return;
            const first = state.selected[0];
            const last = state.selected[state.selected.length - 1];
            bookingModal.open(bookingUrl(currentRoom()), `${name.textContent} · ${state.selected.length} khung · ${dateLabel(first.date)}${last.date !== first.date ? ` → ${dateLabel(last.date)}` : ''}`);
        });
        window.addEventListener('message', event => {
            if (event.origin !== window.location.origin || event.data?.type !== 'delong-booking-conflict') return;
            resetRoom();
            status.textContent = 'Một khung vừa được khách khác giữ. Lịch đã được cập nhật, vui lòng chọn lại.';
            status.className = 'public-v2-status show error';
        });
        if ('ResizeObserver' in window) new ResizeObserver(queueRender).observe(viewport);
        else window.addEventListener('resize', queueRender, { passive: true });

        if (!rooms.length) {
            status.textContent = 'Chưa có phòng được xuất bản để hiển thị.';
            status.className = 'public-v2-status show';
        } else {
            const policyUrl = rooms[0]?.siteSlug ? `/api/public/booking-policy?siteSlug=${encodeURIComponent(rooms[0].siteSlug)}` : '/api/public/booking-policy';
            fetch(policyUrl, { credentials: 'same-origin', headers: { Accept: 'application/json' } }).then(x => x.ok ? x.json() : null).then(x => { if (x) state.policy = x; }).catch(() => {});
            resetRoom();
        }
    });
})();
