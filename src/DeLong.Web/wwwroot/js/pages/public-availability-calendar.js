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
    const isWeekend = value => [0, 6].includes(parseDate(value).getUTCDay());
    // Formatters are created once: building an Intl formatter per slot made large calendars freeze.
    const moneyFormat = new Intl.NumberFormat('vi-VN');
    const money = value => `${moneyFormat.format(Number(value || 0))}đ`;
    const timeFormats = new Map();
    const element = (tag, className, text) => {
        const node = document.createElement(tag);
        if (className) node.className = className;
        if (text !== undefined && text !== null) node.textContent = text;
        return node;
    };

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

        // Rooms are grouped by property (branch). Several branches => the guest picks which ones to show.
        const groups = [];
        rooms.forEach(room => {
            room.groupKey = room.siteSlug || room.propertyName || '';
            let group = groups.find(item => item.key === room.groupKey);
            if (!group) { group = { key: room.groupKey, name: room.propertyName || 'Cơ sở', rooms: [] }; groups.push(group); }
            group.rooms.push(room);
        });

        const batchSize = 14;
        const rowHeight = 52;
        const overscan = 5;
        const scrollGestureGapMs = 240;
        const requestTimeoutMs = 20000;
        const maxRoomsPerRequest = 24;
        const multiColumnWidth = 92;
        const dateColumnWidth = 104;
        const branchStorageKey = 'delong.publicCalendar.branches';
        const wideQuery = window.matchMedia('(min-width: 761px)');
        const defaultPolicy = { publicMaxConsecutiveSlotDays: 3, multiSlotDiscountTiers: [] };
        const state = {
            roomIndex: 0,
            multi: false,
            activeGroups: new Set(),
            roomData: new Map(),
            timeZone: 'Asia/Ho_Chi_Minh',
            dates: [],
            nextFrom: today,
            loading: false,
            requestVersion: 0,
            renderQueued: false,
            batchRequestedInGesture: false,
            lastScrollInputAt: 0,
            selected: [],
            selectedRoomId: null,
            renderVersion: 0,
            rowCache: new Map(),
            rowCacheVersion: -1,
            renderedRange: '',
            abort: null,
            policies: new Map(),
            policy: defaultPolicy
        };

        host.innerHTML = `
            <fieldset class="public-v2-branches" data-calendar-branches hidden><legend>Chọn cơ sở để xem lịch</legend><div class="public-v2-branch-actions"><button type="button" data-branch-all>Chọn tất cả</button><button type="button" data-branch-none>Bỏ chọn</button></div><div class="public-v2-branch-list" data-branch-list></div></fieldset>
            <div class="public-v2-roombar" data-calendar-roombar><img class="public-v2-room-cover" data-room-cover alt="" aria-hidden="true" hidden><button type="button" data-room-prev aria-label="Phòng trước">‹</button><div><small>PHÒNG</small><strong data-room-name>—</strong><span data-room-meta></span></div><button type="button" data-room-next aria-label="Phòng tiếp theo">›</button></div>
            <div class="public-v2-legend"><span><i class="available"></i>Còn trống</span><span><i class="selected"></i>Đang chọn</span><span><i class="partial"></i>Trống một phần</span><span><i class="occupied"></i>Đã có khách</span><span class="public-v2-legend-hint" data-calendar-hint hidden>Chọn một khung, rồi nối tiếp các khung liền sau của cùng phòng.</span></div>
            <div class="public-v2-status" data-calendar-status></div>
            <div class="public-v2-viewport-shell">
                <div class="public-v2-viewport" data-calendar-viewport tabindex="0" aria-label="Lịch phòng, kéo xuống để xem thêm ngày"><div class="public-v2-grid-head" data-calendar-head></div><div class="public-v2-virtual-spacer" data-calendar-top></div><div class="public-v2-virtual-rows" data-calendar-rows></div><div class="public-v2-virtual-spacer" data-calendar-bottom></div></div>
                <div class="public-v2-load-sentinel" data-calendar-sentinel role="status" aria-live="polite"><span>Đang tải thêm ngày…</span></div>
            </div>
            <div class="public-v2-selection" data-calendar-selection hidden><div><small data-selection-label></small><strong data-selection-total></strong></div><button type="button" class="clear" data-selection-clear>Xóa</button><button type="button" class="book" data-selection-book>Đặt phòng</button></div>`;
        const branchBox = host.querySelector('[data-calendar-branches]');
        const branchList = host.querySelector('[data-branch-list]');
        const roombar = host.querySelector('[data-calendar-roombar]');
        const name = host.querySelector('[data-room-name]');
        const meta = host.querySelector('[data-room-meta]');
        const cover = host.querySelector('[data-room-cover]');
        const hint = host.querySelector('[data-calendar-hint]');
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

        const timeLabel = value => {
            let format = timeFormats.get(state.timeZone);
            if (!format) {
                format = new Intl.DateTimeFormat('vi-VN', { timeZone: state.timeZone, hour: '2-digit', minute: '2-digit', hour12: false });
                timeFormats.set(state.timeZone, format);
            }
            return format.format(new Date(value));
        };
        const groupRooms = () => groups.length > 1 ? rooms.filter(room => state.activeGroups.has(room.groupKey)) : rooms;
        // Single-room mode (phones, or only one room) shows one room at a time with the prev/next bar.
        const visibleRooms = () => {
            const list = groupRooms();
            if (state.multi) return list;
            return list.length ? [list[Math.min(state.roomIndex, list.length - 1)]] : [];
        };
        const roomById = id => rooms.find(room => String(room.id) === String(id));
        const daysOf = room => state.roomData.get(String(room?.id))?.days || [];
        const currentRoom = () => (state.selectedRoomId ? roomById(state.selectedRoomId) : null) || visibleRooms()[0];
        const visibleSlots = day => (day?.slots || []).filter(slot => Number(slot.rateType) !== 2);
        const slotCountOf = room => visibleSlots(daysOf(room)[0]).length;
        const gridTemplate = count => `clamp(74px, 18%, 104px) repeat(${Math.max(1, count)}, minmax(0, 1fr))`;
        const gridMinWidth = count => state.multi ? `${dateColumnWidth + (Math.max(1, count) * multiColumnWidth)}px` : '';
        const policyFor = room => state.policies.get(room?.siteSlug || '') || state.policies.get('') || defaultPolicy;

        function bookingUrl(room) {
            const url = new URL(room.bookingUrl, window.location.origin);
            const first = state.selected[0];
            url.searchParams.set('date', first.date);
            url.searchParams.set('room', room.code);
            url.searchParams.set('rate', first.slot.rateId);
            url.searchParams.set('slots', state.selected.map(x => `${x.date}:${x.slot.rateId}`).join(','));
            const timeWindow = selectedTimeWindow();
            if (timeWindow) {
                url.searchParams.set('effectiveCheckIn', timeWindow.startUtc);
                url.searchParams.set('effectiveCheckOut', timeWindow.endUtc);
                if (timeWindow.adjusted) url.searchParams.set('scheduleNote', 'Thời gian được điều chỉnh để dành 30 phút dọn phòng giữa hai lượt khách.');
            }
            url.searchParams.set('embed', '1');
            return `${url.pathname}${url.search}`;
        }

        const slotKey = (date, slot) => `${date}:${slot.rateId}`;
        const isSelected = (date, slot) => state.selected.some(x => slotKey(x.date, x.slot) === slotKey(date, slot));
        // Consecutive-slot rule: the selection belongs to one room and only grows by the next slot in time order.
        function flatSlots() {
            return daysOf(currentRoom()).flatMap(day => visibleSlots(day).map(slot => ({ date: day.date, slot })));
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
            const roomDays = daysOf(room);
            const selected = state.selected.map(x => {
                const day = roomDays.find(day => day.date === x.date);
                const surchargePercent = Number(day?.surchargePercent || 0);
                const displayed = Number(x.slot.price || 0);
                const base = surchargePercent > 0 ? displayed / (1 + surchargePercent / 100) : displayed;
                return { ...x, day, base, surchargePercent, amount: displayed, rule: 'standard' };
            });
            const slotsPerDay = visibleSlots(roomDays[0]).length;
            if (room?.fullDayPricingEnabled && slotsPerDay > 0) {
                const dates = [...new Set(selected.map(x => x.date))];
                dates.forEach(date => {
                    const group = selected.filter(x => x.date === date);
                    if (group.length !== slotsPerDay) return;
                    const fullDayBase = Number(group[0].day?.effectiveFullDayPrice || 0);
                    if (fullDayBase <= 0) return;
                    const total = fullDayBase * (1 + Number(group[0].surchargePercent || 0) / 100);
                    const list = group.reduce((sum, x) => sum + x.base, 0);
                    group.forEach(x => { x.amount = list > 0 ? total * x.base / list : total / group.length; x.rule = 'full-day'; });
                });
            }
            [...new Set(selected.map(x => x.date))].forEach(date => {
                const group = selected.filter(x => x.date === date && x.rule === 'standard');
                const count = Number(room?.threeSlotCount || 3);
                const discount = Number(room?.threeSlotDiscountPercent || 0);
                if (room?.threeSlotDiscountEnabled === false || group.length !== count || group[0].day?.allowThreeSlotCombo === false) return;
                group.forEach(x => { x.amount = x.base * (100 - discount) / 100 + x.base * x.surchargePercent / 100; x.rule = `combo-${count}`; });
            });
            return Math.round(selected.reduce((sum, x) => sum + x.amount, 0));
        }
        function selectedTimeWindow() {
            if (!state.selected.length) return null;
            const first = state.selected[0].slot;
            const last = state.selected[state.selected.length - 1].slot;
            const startUtc = first.bookableStartUtc || first.startUtc;
            const endUtc = last.bookableEndUtc || last.endUtc;
            return {
                startUtc,
                endUtc,
                adjusted: startUtc !== first.startUtc || endUtc !== last.endUtc,
                text: `Nhận ${timeLabel(startUtc)} · Trả ${timeLabel(endUtc)}`
            };
        }
        function selectionSummary() {
            const first = state.selected[0];
            const last = state.selected[state.selected.length - 1];
            const window = selectedTimeWindow();
            return `${state.selected.length} khung · ${dateLabel(first.date)}${last.date !== first.date ? ` → ${dateLabel(last.date)}` : ''}${window ? ` · ${window.text}` : ''}`;
        }
        function updateSelectionBar() {
            if (!state.selected.length) state.selectedRoomId = null;
            selectionBar.hidden = state.selected.length === 0;
            hint.hidden = !state.multi || state.selected.length > 0;
            if (!state.selected.length) return;
            const window = selectedTimeWindow();
            const room = currentRoom();
            selectionLabel.textContent = `${state.multi && room ? `${room.name} · ` : ''}${selectionSummary()}`;
            selectionTotal.textContent = money(estimatedTotal());
            selectionBar.classList.toggle('is-adjusted', window?.adjusted === true);
        }
        function beginSelection(day) {
            const room = roomById(day.roomId);
            state.selectedRoomId = String(day.roomId);
            state.policy = policyFor(room);
        }
        function rejectOtherRoom(day) {
            if (!state.selected.length || String(day.roomId) === String(state.selectedRoomId)) return false;
            const room = currentRoom();
            status.textContent = `Bạn đang chọn khung của phòng ${room?.name || ''}. Mỗi lần đặt chỉ chọn các khung liền nhau của một phòng; bấm Xóa để chọn phòng khác.`;
            status.className = 'public-v2-status show';
            return true;
        }
        function selectSlot(day, slot, trigger) {
            if (rejectOtherRoom(day)) return;
            if (Number(day.bookingMode) === 1) {
                const daySlots = visibleSlots(day);
                if (daySlots.some(item => item.state !== 'available')) {
                    status.textContent = 'Ngày này chỉ nhận combo cả ngày nhưng hiện không còn đủ tất cả khung giờ.';
                    status.className = 'public-v2-status show error';
                    return;
                }
                const selectedForDay = state.selected.filter(x => x.date === day.date);
                if (selectedForDay.length === daySlots.length && state.selected.slice(-daySlots.length).every(x => x.date === day.date)) {
                    state.selected.splice(-daySlots.length, daySlots.length);
                } else {
                    const firstSlot = daySlots[0];
                    const next = nextCandidate();
                    if (state.selected.length && (!next || slotKey(next.date, next.slot) !== slotKey(day.date, firstSlot))) return;
                    const firstDate = parseDate(state.selected[0]?.date || day.date);
                    const daySpan = Math.round((parseDate(day.date) - firstDate) / 86400000) + 1;
                    if (daySpan > Number(state.policy.publicMaxConsecutiveSlotDays || 3)) return;
                    if (!state.selected.length) beginSelection(day);
                    daySlots.forEach(item => state.selected.push({ date: day.date, slot: item }));
                }
                status.className = 'public-v2-status';
                updateSelectionBar();
                refreshRows();
                return;
            }
            const existing = state.selected.findIndex(x => slotKey(x.date, x.slot) === slotKey(day.date, slot));
            if (existing >= 0 && existing === state.selected.length - 1) state.selected.pop();
            else if (state.selected.length === 0) { beginSelection(day); state.selected.push({ date: day.date, slot }); }
            else {
                const next = nextCandidate();
                if (!next || slotKey(next.date, next.slot) !== slotKey(day.date, slot)) return;
                const previousSlot = state.selected[state.selected.length - 1].slot;
                if ((previousSlot.bookableEndUtc || previousSlot.endUtc) !== previousSlot.endUtc ||
                    (slot.bookableStartUtc || slot.startUtc) !== slot.startUtc) {
                    status.textContent = 'Hai khung này không còn liền nhau sau khi dành thời gian dọn phòng. Vui lòng chỉ chọn khoảng thời gian đang hiển thị hoặc đổi khung khác.';
                    status.className = 'public-v2-status show error';
                    return;
                }
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
            refreshRows();
        }

        // Header: [branch row] / [room row] / slot row. One CSS grid; the date cell spans every header row.
        function columnCount() {
            return visibleRooms().reduce((sum, room) => sum + slotCountOf(room), 0);
        }
        function renderHeader() {
            const list = visibleRooms().filter(room => slotCountOf(room) > 0);
            const count = columnCount();
            const showRooms = state.multi;
            const showBranches = state.multi && new Set(list.map(room => room.groupKey)).size > 1;
            const headerRows = 1 + (showRooms ? 1 : 0) + (showBranches ? 1 : 0);
            head.style.gridTemplateColumns = gridTemplate(count);
            head.style.minWidth = gridMinWidth(count);
            head.classList.toggle('is-multi', showRooms);
            head.replaceChildren();
            const dateCell = element('strong', 'public-v2-date-column', 'Ngày');
            dateCell.style.gridRow = `1 / span ${headerRows}`;
            head.appendChild(dateCell);
            let column = 2;
            if (showBranches) {
                groups.forEach(group => {
                    const span = list.filter(room => room.groupKey === group.key).reduce((sum, room) => sum + slotCountOf(room), 0);
                    if (!span) return;
                    const cell = element('div', 'public-v2-head-branch');
                    cell.style.gridRow = '1';
                    cell.style.gridColumn = `${column} / span ${span}`;
                    cell.appendChild(element('strong', null, group.name));
                    head.appendChild(cell);
                    column += span;
                });
            }
            column = 2;
            if (showRooms) {
                list.forEach(room => {
                    const span = slotCountOf(room);
                    const cell = element('div', 'public-v2-head-room');
                    cell.style.gridRow = String(showBranches ? 2 : 1);
                    cell.style.gridColumn = `${column} / span ${span}`;
                    const coverUrl = room.coverImageUrl?.trim();
                    if (coverUrl) {
                        const image = element('img');
                        image.src = coverUrl;
                        image.alt = '';
                        image.loading = 'lazy';
                        cell.appendChild(image);
                    }
                    const label = element('span');
                    label.appendChild(element('strong', null, room.name));
                    if (Number(room.fromPrice) > 0) label.appendChild(element('small', null, `từ ${money(room.fromPrice)}`));
                    cell.appendChild(label);
                    head.appendChild(cell);
                    column += span;
                });
            }
            column = 2;
            list.forEach(room => {
                visibleSlots(daysOf(room)[0]).forEach((slot, index) => {
                    const cell = document.createElement('div');
                    cell.style.gridRow = String(headerRows);
                    cell.style.gridColumn = String(column);
                    if (index === 0 && showRooms) cell.classList.add('is-room-start');
                    cell.innerHTML = `<strong>${timeLabel(slot.startUtc)}–${timeLabel(slot.endUtc)}</strong><small></small>`;
                    cell.querySelector('small').textContent = slot.rateName;
                    head.appendChild(cell);
                    column += 1;
                });
            });
        }

        function renderSlotButton(day, slot, next) {
            const button = document.createElement('button');
            button.type = 'button';
            button.className = `public-v2-slot-bar state-${slot.state}`;
            const selected = isSelected(day.date, slot);
            const eligible = state.selected.length === 0 || selected || (next && slotKey(next.date, next.slot) === slotKey(day.date, slot));
            if (selected) button.classList.add('is-selected');
            if (slot.state === 'available' && !eligible) button.classList.add('is-ineligible');
            if (next && slotKey(next.date, next.slot) === slotKey(day.date, slot)) button.classList.add('is-next');
            const canBook = !!slot.bookableStartUtc && !!slot.bookableEndUtc;
            const adjustedTime = canBook && (slot.bookableStartUtc !== slot.startUtc || slot.bookableEndUtc !== slot.endUtc)
                ? `Nhận ${timeLabel(slot.bookableStartUtc)} · Trả ${timeLabel(slot.bookableEndUtc)}`
                : '';
            const selectedIndex = state.selected.findIndex(item => slotKey(item.date, item.slot) === slotKey(day.date, slot));
            const isFirstSelected = selectedIndex === 0;
            const isLastSelected = selectedIndex === state.selected.length - 1;
            const selectedLabels = [];
            if (selected && canBook) {
                if (isFirstSelected) selectedLabels.push(`Nhận ${timeLabel(slot.bookableStartUtc)}`);
                if (isLastSelected) selectedLabels.push(`Trả ${timeLabel(slot.bookableEndUtc)}`);
                if (!selectedLabels.length) selectedLabels.push('Ở tiếp');
            }
            const selectedTime = selectedLabels.join(' · ');
            const stateText = selectedTime || (slot.state === 'available' ? (Number(day.bookingMode) === 1 ? 'Chọn cả ngày' : 'Còn trống') : slot.state === 'partial' && canBook ? adjustedTime : 'Đã có khách');
            const selectedSchedule = selected && canBook
                ? `<span class="public-v2-selected-schedule">${selectedLabels.map(label => `<b>${label}</b>`).join('')}</span>`
                : `<span>${stateText}</span><small>${money(slot.price)}</small>`;
            button.innerHTML = selectedSchedule;
            const roomName = state.multi ? `${roomById(day.roomId)?.name || ''}, ` : '';
            button.title = `${roomName}${stateText} · ${money(slot.price)}`;
            button.setAttribute('aria-label', `${roomName}${dateLabel(day.date)}, ${slot.rateName}, ${stateText}, ${money(slot.price)}`);
            button.setAttribute('aria-pressed', String(selected));
            if (canBook) {
                button.addEventListener('click', () => selectSlot(day, slot, button));
            } else button.disabled = true;
            return button;
        }

        function renderVirtualRows() {
            state.renderQueued = false;
            if (!state.dates.length) return;
            const first = Math.max(0, Math.floor(viewport.scrollTop / rowHeight) - overscan);
            const visibleCount = Math.ceil(viewport.clientHeight / rowHeight) + (overscan * 2);
            const last = Math.min(state.dates.length, first + visibleCount);
            // Scrolling inside the same window of rows does not rebuild hundreds of buttons.
            const rangeKey = `${first}:${last}:${state.dates.length}:${state.renderVersion}`;
            if (rangeKey === state.renderedRange) return;
            state.renderedRange = rangeKey;
            const list = visibleRooms().filter(room => slotCountOf(room) > 0);
            const slotCount = columnCount();
            const next = nextCandidate();
            topSpacer.style.height = `${first * rowHeight}px`;
            bottomSpacer.style.height = `${Math.max(0, state.dates.length - last) * rowHeight}px`;
            // Rows are cached per date until data/selection changes, so scrolling only builds the rows that come into view.
            if (state.rowCacheVersion !== state.renderVersion) { state.rowCache = new Map(); state.rowCacheVersion = state.renderVersion; }
            const fragment = document.createDocumentFragment();
            state.dates.slice(first, last).forEach(date => {
                const cached = state.rowCache.get(date);
                if (cached) { fragment.appendChild(cached); return; }
                const row = document.createElement('div');
                row.className = `public-v2-grid-row${date === today ? ' is-today' : ''}${isWeekend(date) ? ' is-weekend' : ''}`;
                row.style.gridTemplateColumns = gridTemplate(slotCount);
                row.style.minWidth = gridMinWidth(slotCount);
                const dateCell = document.createElement('div');
                dateCell.className = 'public-v2-date-column';
                dateCell.innerHTML = `<strong>${dateLabel(date)}</strong>${date === today ? '<small>Hôm nay</small>' : ''}`;
                row.appendChild(dateCell);

                list.forEach(room => {
                    const day = state.roomData.get(String(room.id))?.byDate.get(date);
                    const template = slotCountOf(room);
                    const slots = day ? visibleSlots(day) : [];
                    for (let index = 0; index < template; index += 1) {
                        const slot = slots[index];
                        const cell = slot ? renderSlotButton(day, slot, next) : element('span', 'public-v2-slot-empty');
                        if (!state.multi) { row.appendChild(cell); continue; }
                        // Multi-room grid: wrap each slot so room boundaries can be drawn on the cell, not the button.
                        const wrapper = element('div', `public-v2-slot-cell${index === 0 ? ' is-room-start' : ''}`);
                        wrapper.appendChild(cell);
                        row.appendChild(wrapper);
                    }
                });
                state.rowCache.set(date, row);
                fragment.appendChild(row);
            });
            rows.replaceChildren(fragment);
        }

        function refreshRows() {
            state.renderVersion += 1;
            queueRender();
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

        // Every request is aborted when the room/branch selection changes and times out after 20s,
        // so a slow server can never leave the calendar (or the page) stuck.
        async function getJson(url, signal) {
            const timeout = new AbortController();
            const timer = window.setTimeout(() => timeout.abort(), requestTimeoutMs);
            const abortOnReset = () => timeout.abort();
            signal?.addEventListener('abort', abortOnReset, { once: true });
            try {
                const response = await fetch(url, { credentials: 'same-origin', headers: { Accept: 'application/json' }, signal: timeout.signal });
                if (!response.ok) throw new Error(`HTTP ${response.status}`);
                return await response.json();
            } finally {
                window.clearTimeout(timer);
                signal?.removeEventListener('abort', abortOnReset);
            }
        }

        async function fetchRoomBatch(room, from, signal) {
            const query = new URLSearchParams({ roomId: room.id, from, days: String(batchSize) });
            if (room.siteSlug) query.set('siteSlug', room.siteSlug);
            return getJson(`/api/public/room-availability?${query}`, signal);
        }

        // Several rooms: one request per property (rooms-availability), properties loaded one after another.
        async function fetchManyRooms(list, from, signal) {
            const bySite = new Map();
            list.forEach(room => {
                const key = room.siteSlug || '';
                if (!bySite.has(key)) bySite.set(key, []);
                bySite.get(key).push(room);
            });
            const results = [];
            for (const [siteSlug, siteRooms] of bySite) {
                for (let index = 0; index < siteRooms.length; index += maxRoomsPerRequest) {
                    const chunk = siteRooms.slice(index, index + maxRoomsPerRequest);
                    const query = new URLSearchParams({ roomIds: chunk.map(room => room.id).join(','), from, days: String(batchSize) });
                    if (siteSlug) query.set('siteSlug', siteSlug);
                    let rows = [];
                    try { rows = (await getJson(`/api/public/rooms-availability?${query}`, signal))?.rooms || []; }
                    catch (error) { if (signal?.aborted) throw error; rows = null; }
                    chunk.forEach(room => {
                        const data = rows ? rows.find(row => String(row.roomId) === String(room.id)) || { calendar: [] } : null;
                        results.push({ room, data });
                    });
                }
            }
            return results;
        }

        // Loads the next batch of days for every visible room, then extends the rows.
        async function loadNextBatch() {
            const list = visibleRooms();
            if (!list.length || state.loading) return;
            const version = state.requestVersion;
            const signal = state.abort?.signal;
            const loadingStartedAt = window.performance.now();
            const from = state.nextFrom;
            state.loading = true;
            sentinelText.textContent = 'Đang tải thêm ngày…';
            sentinel.classList.remove('ready');
            sentinel.classList.add('show');
            try {
                const results = list.length > 1
                    ? await fetchManyRooms(list, from, signal)
                    : [await fetchRoomBatch(list[0], from, signal).then(data => ({ room: list[0], data })).catch(error => { if (signal?.aborted) throw error; return { room: list[0], data: null }; })];
                if (version !== state.requestVersion) return;
                let lastDate = null;
                let failed = 0;
                results.forEach(({ room, data }) => {
                    const entry = state.roomData.get(String(room.id)) || { days: [], byDate: new Map(), failed: false };
                    if (!data) { entry.failed = true; failed += 1; state.roomData.set(String(room.id), entry); return; }
                    room.fullDayPricingEnabled = data.fullDayPricingEnabled === true;
                    room.fullDayPrice = data.fullDayPrice;
                    room.threeSlotDiscountEnabled = data.threeSlotDiscountEnabled === true;
                    room.threeSlotCount = data.threeSlotCount;
                    room.threeSlotDiscountPercent = data.threeSlotDiscountPercent;
                    state.timeZone = data.timeZoneId || state.timeZone;
                    const calendar = Array.isArray(data.calendar) ? data.calendar : [];
                    calendar.forEach(day => { day.roomId = room.id; entry.byDate.set(day.date, day); });
                    entry.days.push(...calendar);
                    entry.failed = false;
                    state.roomData.set(String(room.id), entry);
                    if (calendar.length) {
                        const end = calendar[calendar.length - 1].date;
                        if (!lastDate || end > lastDate) lastDate = end;
                    }
                });
                if (!lastDate) {
                    status.textContent = failed
                        ? 'Chưa thể tải thêm lịch phòng. Kéo lại để thử lần nữa.'
                        : (list.length > 1 ? 'Các phòng đang chọn chưa có khung giờ đang hoạt động.' : 'Phòng này chưa có khung giờ đang hoạt động.');
                    status.className = `public-v2-status show${failed ? ' error' : ''}`;
                    return;
                }
                const firstLoad = !state.dates.length;
                for (let date = from; date <= lastDate; date = addDays(date, 1)) state.dates.push(date);
                state.nextFrom = addDays(lastDate, 1);
                status.className = failed ? 'public-v2-status show error' : 'public-v2-status';
                if (failed) status.textContent = 'Một vài phòng chưa tải được lịch. Kéo lại để thử lần nữa.';
                if (firstLoad) renderHeader();
                refreshRows();
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

        function renderRoombar() {
            const list = groupRooms();
            roombar.hidden = state.multi;
            const room = state.multi ? null : list[Math.min(state.roomIndex, list.length - 1)];
            name.textContent = room?.name || '—';
            meta.textContent = room ? `${room.propertyName || ''} · ${room.code}` : '';
            const coverUrl = room?.coverImageUrl?.trim();
            cover.hidden = !coverUrl;
            if (coverUrl) cover.src = coverUrl;
            else cover.removeAttribute('src');
            const single = list.length < 2;
            host.querySelector('[data-room-prev]').disabled = single;
            host.querySelector('[data-room-next]').disabled = single;
        }

        function resetRoom() {
            state.multi = wideQuery.matches && groupRooms().length > 1;
            state.requestVersion += 1;
            state.abort?.abort();
            state.abort = new AbortController();
            state.renderedRange = '';
            state.renderVersion += 1;
            state.roomData = new Map();
            state.dates = [];
            state.nextFrom = today;
            state.loading = false;
            state.renderQueued = false;
            state.batchRequestedInGesture = false;
            state.lastScrollInputAt = 0;
            state.selected = [];
            state.selectedRoomId = null;
            updateSelectionBar();
            viewport.scrollTop = 0;
            viewport.scrollLeft = 0;
            viewport.classList.toggle('is-multi', state.multi);
            head.replaceChildren();
            rows.replaceChildren();
            topSpacer.style.height = '0';
            bottomSpacer.style.height = '0';
            renderRoombar();
            if (!visibleRooms().length) {
                status.textContent = groups.length > 1 ? 'Hãy chọn ít nhất một cơ sở để xem lịch phòng.' : 'Chưa có phòng được xuất bản để hiển thị.';
                status.className = 'public-v2-status show';
                section.classList.remove('loading');
                return;
            }
            status.textContent = 'Đang tải lịch phòng…';
            status.className = 'public-v2-status show';
            section.classList.add('loading');
            void loadNextBatch();
        }

        function readStoredGroups() {
            try {
                const stored = JSON.parse(window.localStorage.getItem(branchStorageKey) || 'null');
                if (Array.isArray(stored)) return stored.filter(key => groups.some(group => group.key === key));
            } catch { /* storage may be unavailable */ }
            return null;
        }
        function storeGroups() {
            try { window.localStorage.setItem(branchStorageKey, JSON.stringify([...state.activeGroups])); } catch { /* ignore */ }
        }
        function renderBranches() {
            branchBox.hidden = groups.length < 2;
            if (groups.length < 2) return;
            branchList.replaceChildren();
            groups.forEach(group => {
                const label = element('label', state.activeGroups.has(group.key) ? 'is-checked' : '');
                const input = element('input');
                input.type = 'checkbox';
                input.checked = state.activeGroups.has(group.key);
                input.addEventListener('change', () => {
                    if (input.checked) state.activeGroups.add(group.key);
                    else state.activeGroups.delete(group.key);
                    label.classList.toggle('is-checked', input.checked);
                    state.roomIndex = 0;
                    storeGroups();
                    resetRoom();
                });
                label.appendChild(input);
                label.appendChild(element('span', null, group.name));
                label.appendChild(element('small', null, String(group.rooms.length)));
                branchList.appendChild(label);
            });
        }
        function setAllGroups(on) {
            state.activeGroups = new Set(on ? groups.map(group => group.key) : []);
            state.roomIndex = 0;
            storeGroups();
            renderBranches();
            resetRoom();
        }

        const changeRoom = amount => {
            const list = groupRooms();
            if (!list.length || state.multi) return;
            state.roomIndex = (state.roomIndex + amount + list.length) % list.length;
            resetRoom();
        };
        host.querySelector('[data-room-prev]').addEventListener('click', () => changeRoom(-1));
        host.querySelector('[data-room-next]').addEventListener('click', () => changeRoom(1));
        host.querySelector('[data-branch-all]').addEventListener('click', () => setAllGroups(true));
        host.querySelector('[data-branch-none]').addEventListener('click', () => setAllGroups(false));
        viewport.addEventListener('wheel', () => beginScrollGesture(), { passive: true });
        viewport.addEventListener('touchstart', () => beginScrollGesture(true), { passive: true });
        viewport.addEventListener('pointerdown', event => {
            if (event.pointerType !== 'touch') beginScrollGesture(true);
        }, { passive: true });
        viewport.addEventListener('keydown', event => {
            if (['ArrowDown', 'PageDown', 'End', ' '].includes(event.key)) beginScrollGesture(true);
        });
        viewport.addEventListener('scroll', handleViewportScroll, { passive: true });
        host.querySelector('[data-selection-clear]').addEventListener('click', () => { state.selected = []; status.className = 'public-v2-status'; updateSelectionBar(); refreshRows(); });
        host.querySelector('[data-selection-book]').addEventListener('click', () => {
            if (!state.selected.length) return;
            const room = currentRoom();
            bookingModal.open(bookingUrl(room), `${room?.name || ''} · ${selectionSummary()}`);
        });
        window.addEventListener('message', event => {
            if (event.origin !== window.location.origin || event.data?.type !== 'delong-booking-conflict') return;
            resetRoom();
            status.textContent = 'Một khung vừa được khách khác giữ. Lịch đã được cập nhật, vui lòng chọn lại.';
            status.className = 'public-v2-status show error';
        });
        const handleWidthChange = () => { if ((wideQuery.matches && groupRooms().length > 1) !== state.multi) resetRoom(); };
        if (wideQuery.addEventListener) wideQuery.addEventListener('change', handleWidthChange);
        else if (wideQuery.addListener) wideQuery.addListener(handleWidthChange);
        if ('ResizeObserver' in window) new ResizeObserver(queueRender).observe(viewport);
        else window.addEventListener('resize', queueRender, { passive: true });

        if (!rooms.length) {
            roombar.hidden = true;
            status.textContent = 'Chưa có phòng được xuất bản để hiển thị.';
            status.className = 'public-v2-status show';
        } else {
            // Default: remembered branches, else every branch when the total stays readable, else the first branch.
            const stored = readStoredGroups();
            state.activeGroups = new Set(stored && stored.length ? stored : (rooms.length <= 12 ? groups.map(group => group.key) : [groups[0].key]));
            renderBranches();
            [...new Set(rooms.map(room => room.siteSlug || ''))].forEach(siteSlug => {
                const policyUrl = siteSlug ? `/api/public/booking-policy?siteSlug=${encodeURIComponent(siteSlug)}` : '/api/public/booking-policy';
                fetch(policyUrl, { credentials: 'same-origin', headers: { Accept: 'application/json' } }).then(x => x.ok ? x.json() : null).then(x => { if (x) state.policies.set(siteSlug, x); }).catch(() => {});
            });
            resetRoom();
        }
    });
})();
