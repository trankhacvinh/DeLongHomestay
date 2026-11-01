(function () {
    'use strict';
    const root = document.getElementById('payroll-page');
    if (!root) return;
    const initial = JSON.parse(document.getElementById('payroll-data').textContent);

    // Khớp enum phía server.
    const STATUS = { Present: 0, UnpaidLeave: 1, PaidLeave: 2, HolidayOff: 3, MadeUp: 4 };
    const CODES = [
        { key: 'x', status: 0, cls: 'v-x', short: 'Đi làm', label: 'Đi làm' },
        { key: 'K', status: 1, cls: 'v-K', short: 'Nghỉ không lương', label: 'Nghỉ không lương (trừ tiền ca)' },
        { key: 'P', status: 2, cls: 'v-P', short: 'Nghỉ có lương', label: 'Nghỉ có lương (không trừ, mất chuyên cần)' },
        { key: 'L', status: 3, cls: 'v-L', short: 'Nghỉ lễ', label: 'Nghỉ lễ (không trừ)' },
        { key: 'B', status: 4, cls: 'v-B', short: 'Nghỉ đã làm bù', label: 'Nghỉ đã làm bù (không trừ, mất chuyên cần)' }
    ];
    const CODE_BY_STATUS = Object.fromEntries(CODES.map(c => [c.status, c]));
    const KEY_TO_STATUS = { x: 0, X: 0, k: 1, K: 1, p: 2, P: 2, l: 3, L: 3, b: 4, B: 4 };
    // Bộ gõ tiếng Việt (Telex/VNI) có thể trả ev.key = 'Process' hoặc ký tự khác; ev.code là phím vật lý nên luôn đúng.
    const CODE_TO_STATUS = { KeyX: 0, KeyK: 1, KeyP: 2, KeyL: 3, KeyB: 4 };
    const EDITABLE = 'input,select,textarea,[contenteditable="true"],[contenteditable=""]';
    const NEGATIVE_TYPES = ['phạt', 'trừ khác', 'truy thu', 'bồi thường'];
    const DOW = ['CN', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7'];
    const SLOT_NAMES = { 1: ['Ca 1'], 2: ['Sáng', 'Chiều'], 3: ['Ca 1', 'Ca 2', 'Ca 3'] };
    const fmt = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 0 });

    function pad(n) { return String(n).padStart(2, '0'); }
    function slotName(shifts, slot) { return (SLOT_NAMES[shifts] || [])[slot] || ('Ca ' + (slot + 1)); }
    function cellKey(empId, date, slot) { return empId + '|' + date + '|' + slot; }
    function fold(value) {
        return String(value || '').normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/đ/g, 'd').replace(/Đ/g, 'D').toLowerCase().trim();
    }
    function matchEmployee(e, query) {
        const q = fold(query); if (!q) return true;
        const hay = fold([e.code, e.fullName, e.position, e.phone].filter(Boolean).join(' '));
        return q.split(/\s+/).every(part => hay.includes(part));
    }

    // Ô chọn nhân viên có tìm kiếm (gõ mã, tên, chức vụ hoặc SĐT; không cần dấu).
    const EmpPicker = {
        props: { modelValue: { type: String, default: '' }, employees: { type: Array, default: () => [] }, disabled: Boolean, placeholder: { type: String, default: 'Tìm nhân viên…' } },
        emits: ['update:modelValue', 'change'],
        data: () => ({ open: false, q: '', hi: 0, pos: {} }),
        computed: {
            current() { return this.employees.find(e => e.id === this.modelValue) || null; },
            label() { return this.current ? this.current.code + ' · ' + this.current.fullName : ''; },
            filtered() { return this.employees.filter(e => matchEmployee(e, this.q)).slice(0, 100); }
        },
        methods: {
            show() {
                if (this.disabled) return;
                const r = this.$refs.input.getBoundingClientRect();
                const below = window.innerHeight - r.bottom;
                this.pos = below < 260 && r.top > below
                    ? { left: r.left + 'px', bottom: (window.innerHeight - r.top + 2) + 'px', minWidth: Math.max(r.width, 240) + 'px' }
                    : { left: r.left + 'px', top: (r.bottom + 2) + 'px', minWidth: Math.max(r.width, 240) + 'px' };
                this.q = ''; this.hi = Math.max(0, this.filtered.findIndex(e => e.id === this.modelValue)); this.open = true;
                this.$nextTick(() => this.$refs.input?.select());
            },
            hide() { this.open = false; this.q = ''; },
            type(ev) { this.q = ev.target.value; this.hi = 0; if (!this.open) this.show(); },
            move(step) { if (!this.open) { this.show(); return; } const n = this.filtered.length; if (n) this.hi = (this.hi + step + n) % n; this.$nextTick(() => this.$refs.list?.querySelector('.hi')?.scrollIntoView({ block: 'nearest' })); },
            choose(e) {
                if (!e) return;
                const changed = e.id !== this.modelValue;
                this.$emit('update:modelValue', e.id); this.hide(); this.$refs.input?.blur();
                if (changed) this.$nextTick(() => this.$emit('change', e.id));
            }
        },
        template: `<div class="pr-pick">
            <input ref="input" type="text" autocomplete="off" spellcheck="false" :disabled="disabled" :placeholder="current ? label : placeholder"
                :value="open ? q : label" @focus="show" @click="open || show()" @input="type" @blur="hide"
                @keydown.down.prevent="move(1)" @keydown.up.prevent="move(-1)" @keydown.enter.prevent="choose(filtered[hi])" @keydown.esc.prevent="hide(); $refs.input.blur()" />
            <ul v-if="open" ref="list" class="pr-pick-list" :style="pos" role="listbox">
                <li v-for="(e, i) in filtered" :key="e.id" :class="{ hi: i === hi, cur: e.id === modelValue }" role="option" @mousedown.prevent="choose(e)" @mouseenter="hi = i">
                    <b>{{ e.code }}</b> {{ e.fullName }}<small v-if="e.position"> · {{ e.position }}</small>
                </li>
                <li v-if="!filtered.length" class="none">Không tìm thấy nhân viên</li>
            </ul>
        </div>`
    };
    function localNow(zone) {
        const parts = new Intl.DateTimeFormat('en-CA', { timeZone: zone, year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' })
            .formatToParts(new Date()).reduce((a, p) => (a[p.type] = p.value, a), {});
        return `${parts.year}-${parts.month}-${parts.day}T${parts.hour}:${parts.minute}`;
    }
    function localToUtcIso(local, zone) {
        // Tìm thời điểm UTC sao cho giờ địa phương tại zone bằng local (đủ chính xác cho múi giờ không DST như Việt Nam).
        const guess = new Date(local + ':00Z');
        const shown = new Date(guess.toLocaleString('en-US', { timeZone: zone }));
        const asUtc = new Date(guess.toLocaleString('en-US', { timeZone: 'UTC' }));
        return new Date(guess.getTime() - (shown - asUtc)).toISOString();
    }

    let saveTimer = null;
    let activeLoad = null;

    Vue.createApp({
        data: () => ({
            ...initial,
            sheet: null, loading: false, busy: false, error: '', message: '',
            tab: 'att', cells: {}, pending: {}, inflight: false, saveState: '',
            sel: null, dragging: false, empQuery: '',
            formula: { ref: 'A1', value: 'Chọn một ô để xem nội dung và cách tính' },
            drawer: null, dialog: null,
            adjRows: [], newAdj: { employeeId: '', type: 'Thưởng', amount: 0, reason: '' },
            newTxn: {}, empRows: [], policyForm: {}, newHoliday: { date: '', name: '', kind: 0 },
            codeButtons: CODES,
            adjTypes: ['Thưởng', 'Tăng ca', 'Phụ cấp', 'Phạt', 'Trừ khác', 'Bồi thường', 'Truy lĩnh', 'Truy thu']
        }),
        computed: {
            locked() { return !!this.sheet && this.sheet.period.status === 1; },
            monthLabel() { const [y, m] = this.month.split('-'); return `tháng ${m}/${y}`; },
            policy() { return this.sheet?.policy || { salaryDaysBase: 30, attendanceBonus: 0, holidayMultiplier: 2, tetMultiplier: 3 }; },
            lines() { return this.sheet?.lines || []; },
            transactions() { return this.sheet?.transactions || []; },
            visibleTabs() {
                const all = [
                    { key: 'att', name: 'Chấm công' }, { key: 'pay', name: 'Bảng lương', admin: true },
                    { key: 'adj', name: 'Tăng / Giảm', admin: true }, { key: 'txn', name: 'Ứng & Trả lương', admin: true },
                    { key: 'emp', name: 'Nhân viên' }, { key: 'set', name: 'Cài đặt', admin: true }
                ];
                return all.filter(t => !t.admin || this.canManagePayroll);
            },
            days() {
                if (!this.sheet) return [];
                const [y, m] = this.month.split('-').map(Number);
                const holidays = Object.fromEntries((this.sheet.holidays || []).map(h => [h.date, h]));
                const out = [];
                for (let d = 1; d <= this.sheet.daysInMonth; d++) {
                    const date = `${y}-${pad(m)}-${pad(d)}`;
                    const dow = new Date(y, m - 1, d).getDay();
                    out.push({ day: d, date, dow, dowText: DOW[dow], holiday: holidays[date] || null });
                }
                return out;
            },
            monthEmployees() {
                if (!this.sheet) return [];
                const first = this.month + '-01', last = this.days.length ? this.days[this.days.length - 1].date : first;
                return this.sheet.employees.filter(e => e.startDate <= last && (!e.endDate || e.endDate >= first));
            },
            filteredEmployees() { return this.monthEmployees.filter(e => matchEmployee(e, this.empQuery)); },
            visibleLines() {
                if (!this.empQuery) return this.lines;
                const ids = new Set(this.sheet ? this.sheet.employees.filter(e => matchEmployee(e, this.empQuery)).map(e => e.id) : []);
                return this.lines.filter(l => ids.has(l.employeeId));
            },
            attRows() {
                const rows = [];
                this.filteredEmployees.forEach(emp => {
                    for (let s = 0; s < emp.shiftsPerDay; s++) rows.push({ key: emp.id + '-' + s, emp, slot: s, slotName: slotName(emp.shiftsPerDay, s) });
                });
                return rows;
            },
            stats() {
                const out = {};
                this.monthEmployees.forEach(emp => {
                    const st = { present: 0, unpaid: 0, paid: 0, madeUp: 0, holidayWorked: 0, blank: 0 };
                    this.days.forEach(d => {
                        if (!this.employed(emp, d.date)) return;
                        for (let s = 0; s < emp.shiftsPerDay; s++) {
                            const c = this.cells[cellKey(emp.id, d.date, s)];
                            if (!c) { st.blank++; continue; }
                            if (c.status === 0) { st.present++; if (d.holiday) st.holidayWorked++; }
                            else if (c.status === 1) st.unpaid++;
                            else if (c.status === 2) st.paid++;
                            else if (c.status === 4) st.madeUp++;
                        }
                    });
                    out[emp.id] = st;
                });
                return out;
            },
            blankTotal() { return Object.values(this.stats).reduce((s, x) => s + x.blank, 0); },
            pendingCount() { return Object.keys(this.pending).length; },
            saveStateText() { return { saving: 'Đang lưu…', saved: 'Đã lưu', failed: 'Chưa lưu được' }[this.saveState] || ''; },
            normSel() {
                if (!this.sel) return null;
                const s = this.sel;
                return { r1: Math.min(s.r1, s.r2), r2: Math.max(s.r1, s.r2), d1: Math.min(s.d1, s.d2), d2: Math.max(s.d1, s.d2) };
            },
            singleCell() {
                const n = this.normSel;
                if (!n || n.r1 !== n.r2 || n.d1 !== n.d2) return null;
                const row = this.attRows[n.r1]; const d = this.days[n.d1 - 1];
                if (!row || !d || !this.employed(row.emp, d.date)) return null;
                return { row, d, key: cellKey(row.emp.id, d.date, row.slot) };
            },
            singleCellNote() { return this.singleCell ? (this.cells[this.singleCell.key]?.note || '') : ''; },
            singleCellStatusSet() { return !!(this.singleCell && this.cells[this.singleCell.key]); },
            fillUntilText() { return this.today.startsWith(this.month) ? 'đến hôm nay' : 'cả tháng'; },
            totals() {
                const keys = ['basePay', 'unpaidDeduction', 'holidayPremium', 'attendanceBonus', 'additions', 'deductions', 'gross', 'advances', 'paid', 'remaining'];
                return Object.fromEntries(keys.map(k => [k, this.lines.reduce((s, l) => s + Number(l[k] || 0), 0)]));
            },
            statusItems() {
                if (this.tab === 'att' && this.normSel) {
                    const n = this.normSel; const tally = {}; let count = 0;
                    for (let r = n.r1; r <= n.r2; r++) for (let d = n.d1; d <= n.d2; d++) {
                        const row = this.attRows[r]; const day = this.days[d - 1];
                        if (!row || !day || !this.employed(row.emp, day.date)) continue;
                        count++; const c = this.cells[cellKey(row.emp.id, day.date, row.slot)];
                        const label = c ? CODE_BY_STATUS[c.status].key : 'chưa chấm'; tally[label] = (tally[label] || 0) + 1;
                    }
                    return ['Đang chọn: ' + count + ' ô', ...Object.entries(tally).map(([k, v]) => k + ': ' + v)];
                }
                if (this.canManagePayroll && this.lines.length) return ['Tổng được hưởng: ' + this.money(this.totals.gross), 'Còn phải trả: ' + this.money(this.totals.remaining)];
                return [];
            }
        },
        methods: {
            money(v) { const n = Math.round(Number(v || 0)); return (n < 0 ? '−' : '') + fmt.format(Math.abs(n)); },
            dash(v) { return Number(v || 0) === 0 ? '–' : this.money(v); },
            moneyInput(v) { const n = Math.round(Number(v || 0)); return n === 0 ? '' : (n < 0 ? '-' : '') + fmt.format(Math.abs(n)); },
            parseMoney(v) { const s = String(v || '').trim(); const neg = /^[-−]/.test(s); const n = parseInt(s.replace(/[^\d]/g, ''), 10); return isNaN(n) ? 0 : (neg ? -n : n); },
            dateText(v) { if (!v) return ''; const [y, m, d] = v.split('-'); return `${d}/${m}/${y}`; },
            dateTime(v) { return new Date(v).toLocaleString('vi-VN', { timeZone: this.timeZoneId, day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' }); },
            methodText(m) { return { 0: 'Tiền mặt', 1: 'Chuyển khoản', 2: 'Thẻ', 3: 'Khác' }[m] || 'Khác'; },
            employed(emp, date) { return date >= emp.startDate && (!emp.endDate || date <= emp.endDate); },
            empById(id) { return this.sheet?.employees.find(e => e.id === id); },
            empLabel(id) { const e = this.empById(id); return e ? e.code + ' · ' + e.fullName : '(đã xoá)'; },
            slotList(slots, emp) { return (slots || []).map(s => this.dateText(s.date).slice(0, 5) + ' ' + slotName(emp.shiftsPerDay, s.slot)).join(', '); },
            lineState(l) {
                if (l.blankShifts > 0) return { cls: 'block', text: 'Chưa chấm đủ' };
                if (!this.locked) return { cls: 'draft', text: 'Nháp' };
                if (l.remaining <= 0) return { cls: 'done', text: 'Đã trả đủ' };
                return l.paid > 0 ? { cls: 'part', text: 'Trả một phần' } : { cls: 'draft', text: 'Chưa trả' };
            },

            // ---------- tải dữ liệu ----------
            changeMonth() { this.sel = null; this.drawer = null; this.load(); },
            switchTab(key) { this.tab = key; this.drawer = null; this.formula = { ref: 'A1', value: 'Chọn một ô để xem nội dung và cách tính' }; },
            async load(options) {
                const quiet = options && options.quiet;
                if (!/^\d{4}-\d{2}$/.test(this.month)) return;
                if (this.pendingCount || this.inflight) await this.flush();
                activeLoad?.abort();
                const controller = new AbortController(); activeLoad = controller;
                if (!quiet) this.loading = true;
                try {
                    const data = await DeLongApi.get(`/api/admin/properties/${this.propertyId}/payroll?month=${this.month}`, { signal: controller.signal });
                    if (activeLoad !== controller) return;
                    this.applySheet(data);
                    const url = new URL(location.href); url.searchParams.set('month', this.month); url.searchParams.set('propertyId', this.propertyId);
                    history.replaceState(null, '', url);
                } catch (e) {
                    if (!controller.signal.aborted) this.error = e.message;
                } finally {
                    if (activeLoad === controller) { this.loading = false; activeLoad = null; }
                }
            },
            applySheet(data) {
                this.sheet = data;
                const cells = {};
                data.attendance.forEach(a => { cells[cellKey(a.employeeId, a.date, a.slot)] = { status: a.status, note: a.note || '' }; });
                Object.entries(this.pending).forEach(([k, v]) => { if (v.status === null) delete cells[k]; else cells[k] = { status: v.status, note: v.note || '' }; });
                this.cells = cells;
                this.adjRows = (data.adjustments || []).map(a => ({ ...a }));
                this.empRows = data.employees.map(e => this.toEmpRow(e));
                if (data.policy) this.policyForm = { ...data.policy };
                if (!this.newTxn.requestKey) this.resetTxn();
                if (this.drawer) { const emp = this.empById(this.drawer.emp.id); this.drawer = emp ? this.buildDrawer(emp) : null; }
            },

            // ---------- chấm công ----------
            cellText(row, d) { if (!this.employed(row.emp, d.date)) return ''; const c = this.cells[cellKey(row.emp.id, d.date, row.slot)]; return c ? CODE_BY_STATUS[c.status].key : ''; },
            cellTitle(row, d) { const c = this.cells[cellKey(row.emp.id, d.date, row.slot)]; return c && c.note ? c.note : ''; },
            cellClass(ri, row, d) {
                const out = [];
                if (!this.employed(row.emp, d.date)) out.push('off');
                else { const c = this.cells[cellKey(row.emp.id, d.date, row.slot)]; out.push(c ? CODE_BY_STATUS[c.status].cls : 'v-blank'); if (c && c.note) out.push('has-note'); }
                if (d.holiday) out.push(d.holiday.kind === 1 ? 'tet' : 'hol');
                const n = this.normSel;
                if (n && ri >= n.r1 && ri <= n.r2 && d.day >= n.d1 && d.day <= n.d2) out.push(ri === this.sel.r1 && d.day === this.sel.d1 ? 'sel' : 'inrange');
                return out;
            },
            cellDown(ev) {
                const td = ev.target.closest('td.d'); if (!td) return;
                ev.preventDefault();
                // preventDefault giữ focus ở phần tử cũ (ô tháng, nút…), khiến phím tắt bị bỏ qua: nhả focus ra.
                const active = document.activeElement;
                if (active && active !== document.body && !root.querySelector('.pr-att')?.contains(active)) active.blur();
                const r = Number(td.dataset.r), d = Number(td.dataset.d);
                if (ev.shiftKey && this.sel) { this.sel.r2 = r; this.sel.d2 = d; } else this.sel = { r1: r, d1: d, r2: r, d2: d };
                this.dragging = true; this.describeSel();
            },
            cellOver(ev) {
                if (!this.dragging) return;
                const td = ev.target.closest('td.d'); if (!td) return;
                this.sel.r2 = Number(td.dataset.r); this.sel.d2 = Number(td.dataset.d); this.describeSel();
            },
            describeSel() {
                const row = this.attRows[this.sel.r1], d = this.days[this.sel.d1 - 1]; if (!row || !d) return;
                const n = this.normSel, count = (n.r2 - n.r1 + 1) * (n.d2 - n.d1 + 1);
                const c = this.cells[cellKey(row.emp.id, d.date, row.slot)];
                let value = !this.employed(row.emp, d.date) ? 'Ngoài thời gian làm việc' : c ? CODE_BY_STATUS[c.status].key + ' — ' + CODE_BY_STATUS[c.status].label : '(trống) — chưa chấm';
                if (c && c.note) value += ' · ' + c.note;
                if (d.holiday) value += ' · ' + (d.holiday.kind === 1 ? 'Tết' : 'Lễ') + ': ' + d.holiday.name + (c && c.status === 0 ? ` → ca này được trả tổng ×${d.holiday.kind === 1 ? this.policy.tetMultiplier : this.policy.holidayMultiplier}` : '');
                this.formula = { ref: `${row.emp.code} · ${row.slotName} · ${pad(d.day)}/${this.month.slice(5)}` + (count > 1 ? ` (+${count - 1} ô)` : ''), value };
            },
            setCell(emp, date, slot, status, note) {
                const key = cellKey(emp.id, date, slot);
                const current = this.cells[key];
                if (status === null) { if (!current) return; delete this.cells[key]; }
                else {
                    const nextNote = note !== undefined ? note : (current?.note || '');
                    if (current && current.status === status && current.note === nextNote) return;
                    this.cells[key] = { status, note: nextNote };
                }
                this.pending[key] = { employeeId: emp.id, date, slot, status, note: status === null ? null : this.cells[key].note || null };
            },
            applyCode(status) {
                if (this.locked) { this.error = 'Bảng lương tháng này đã chốt, không sửa được chấm công.'; return; }
                const n = this.normSel; if (!n) { this.error = 'Chọn ô trước.'; return; }
                for (let r = n.r1; r <= n.r2; r++) for (let d = n.d1; d <= n.d2; d++) {
                    const row = this.attRows[r], day = this.days[d - 1];
                    if (row && day && this.employed(row.emp, day.date)) this.setCell(row.emp, day.date, row.slot, status);
                }
                this.describeSel(); this.scheduleSave();
            },
            setNote(value) {
                const c = this.singleCell; if (!c || this.locked) return;
                const current = this.cells[c.key]; if (!current) return;
                this.setCell(c.row.emp, c.d.date, c.row.slot, current.status, String(value || '').trim().slice(0, 500));
                this.describeSel(); this.scheduleSave();
            },
            fillBlanks() {
                if (this.locked) return;
                const until = this.today.startsWith(this.month) ? this.today : null;
                this.attRows.forEach(row => this.days.forEach(d => {
                    if (until && d.date > until) return;
                    if (this.employed(row.emp, d.date) && !this.cells[cellKey(row.emp.id, d.date, row.slot)]) this.setCell(row.emp, d.date, row.slot, STATUS.Present);
                }));
                this.scheduleSave();
            },
            scheduleSave() { clearTimeout(saveTimer); this.saveState = 'saving'; saveTimer = setTimeout(() => this.flush(), 700); },
            async flush() {
                clearTimeout(saveTimer);
                if (this.inflight) { saveTimer = setTimeout(() => this.flush(), 300); return; }
                const batch = this.pending; const keys = Object.keys(batch);
                if (!keys.length) { if (this.saveState === 'saving') this.saveState = 'saved'; return; }
                this.pending = {}; this.inflight = true; this.saveState = 'saving';
                try {
                    await DeLongApi.put(`/api/admin/properties/${this.propertyId}/payroll/attendance`, { month: this.month, cells: keys.map(k => batch[k]) });
                    this.saveState = 'saved';
                    if (this.canManagePayroll && !Object.keys(this.pending).length) this.load({ quiet: true });
                } catch (e) {
                    this.pending = { ...batch, ...this.pending };
                    this.saveState = 'failed'; this.error = 'Chưa lưu được chấm công: ' + e.message;
                } finally { this.inflight = false; }
            },
            onKey(ev) {
                if (this.tab !== 'att' || !this.sel || this.dialog) return;
                if (ev.target.closest && ev.target.closest(EDITABLE)) return;
                if (ev.ctrlKey || ev.metaKey || ev.altKey) return;
                const k = ev.key;
                const status = k in KEY_TO_STATUS ? KEY_TO_STATUS[k] : CODE_TO_STATUS[ev.code];
                if (status !== undefined) { ev.preventDefault(); this.applyCode(status); return; }
                // Enter/Tab trên nút đang focus vẫn là bấm nút, không phải di chuyển ô.
                if ((k === 'Enter' || k === ' ') && ev.target.closest && ev.target.closest('button,a')) return;
                if (k === 'Delete' || k === 'Backspace') { ev.preventDefault(); this.applyCode(null); return; }
                const moves = { ArrowUp: [-1, 0], ArrowDown: [1, 0], ArrowLeft: [0, -1], ArrowRight: [0, 1], Enter: [1, 0], Tab: [0, ev.shiftKey ? -1 : 1] };
                if (!moves[k]) return;
                ev.preventDefault();
                const [dr, dd] = moves[k]; const maxR = this.attRows.length - 1, maxD = this.days.length;
                const clamp = (v, lo, hi) => Math.max(lo, Math.min(hi, v));
                if (ev.shiftKey && k.startsWith('Arrow')) { this.sel.r2 = clamp(this.sel.r2 + dr, 0, maxR); this.sel.d2 = clamp(this.sel.d2 + dd, 1, maxD); }
                else { const r = clamp(this.sel.r1 + dr, 0, maxR), d = clamp(this.sel.d1 + dd, 1, maxD); this.sel = { r1: r, d1: d, r2: r, d2: d }; }
                this.describeSel();
                this.$nextTick(() => this.$refs.sheetEl?.querySelector('td.sel')?.scrollIntoView({ block: 'nearest', inline: 'nearest' }));
            },

            // ---------- bảng lương ----------
            payClick(ev) {
                const td = ev.target.closest('td'); const tr = ev.target.closest('tr[data-emp]'); if (!td || !tr) return;
                const emp = this.empById(tr.dataset.emp); const line = this.lines.find(l => l.employeeId === tr.dataset.emp); if (!emp || !line) return;
                this.$refs.sheetEl.querySelectorAll('td.sel').forEach(x => x.classList.remove('sel')); td.classList.add('sel');
                const f = td.dataset.f;
                if (f === 'open') { this.drawer = this.buildDrawer(emp); this.formula = { ref: emp.code, value: 'Mở phiếu lương chi tiết' }; return; }
                const x = this.payFormula(emp, line, f); if (x) this.formula = { ref: emp.code + ' · ' + x[0], value: x[1] };
            },
            payFormula(emp, l, f) {
                const p = this.policy, m = v => this.money(v);
                const adj = this.adjRows.filter(a => a.employeeId === emp.id);
                const map = {
                    salary: ['Lương tháng', m(l.monthlySalary) + ' (sửa ở trang Nhân viên)'],
                    perDay: ['Đơn giá ngày', `= ${m(l.monthlySalary)} ÷ ${p.salaryDaysBase} = ${m(l.salaryPerDay)}`],
                    perShift: ['Đơn giá ca', `= ${m(l.salaryPerDay)} ÷ ${emp.shiftsPerDay} ca = ${m(l.salaryPerShift)}`],
                    sched: ['Ca chuẩn', `= số ngày làm trong tháng × ${emp.shiftsPerDay} ca = ${l.scheduledShifts}`],
                    base: ['Lương cơ bản', `= ${m(l.salaryPerShift)} × ${l.scheduledShifts} ca = ${m(l.basePay)}`],
                    unpaid: ['Ca nghỉ không lương', this.slotList(l.unpaidSlots, emp) || 'Không có'],
                    deduct: ['Trừ ca nghỉ', `= ${m(l.salaryPerShift)} × ${l.unpaidShifts} ca = ${m(l.unpaidDeduction)}`],
                    hol: ['Ca làm lễ/Tết', this.slotList(l.holidayWorkedSlots, emp) || 'Không có'],
                    premium: ['Phụ trội lễ/Tết', `= đơn giá ca × (hệ số − 1) × ${l.holidayWorkedShifts} ca = ${m(l.holidayPremium)}`],
                    bonus: ['Chuyên cần', l.attendanceBonus ? 'Đủ điều kiện = ' + m(l.attendanceBonus) : 'Không được: ' + (l.attendanceBonusLostReason || '')],
                    plus: ['Khoản tăng', adj.filter(a => a.amount > 0).map(a => `${a.type} ${m(a.amount)} (${a.reason})`).join('; ') || 'Không có'],
                    minus: ['Khoản giảm', adj.filter(a => a.amount < 0).map(a => `${a.type} ${m(-a.amount)} (${a.reason})`).join('; ') || 'Không có'],
                    gross: ['Được hưởng', `= ${m(l.basePay)} − ${m(l.unpaidDeduction)} + ${m(l.holidayPremium)} + ${m(l.attendanceBonus)} + ${m(l.additions)} − ${m(l.deductions)} = ${m(l.gross)}`],
                    adv: ['Đã ứng', m(l.advances)], paid: ['Đã trả', m(l.paid)],
                    remain: ['Còn phải trả', `= ${m(l.gross)} − ${m(l.advances)} − ${m(l.paid)} = ${m(l.remaining)}`]
                };
                return map[f];
            },
            buildDrawer(emp) {
                const line = this.lines.find(l => l.employeeId === emp.id);
                if (!line) return null;
                const notes = [];
                Object.entries(this.cells).forEach(([k, c]) => {
                    if (!c.note) return; const [id, date, slot] = k.split('|');
                    if (id === emp.id) notes.push(`${this.dateText(date).slice(0, 5)} ${slotName(emp.shiftsPerDay, Number(slot))}: ${c.note}`);
                });
                return { emp, line, notes: notes.sort(), adjustments: this.adjRows.filter(a => a.employeeId === emp.id), transactions: this.transactions.filter(t => t.employeeId === emp.id && !t.isVoided) };
            },
            async copyPayslip() {
                const d = this.drawer; if (!d) return; const l = d.line, m = v => this.money(v) + ' đ';
                const lines = [`PHIẾU LƯƠNG ${this.monthLabel.toUpperCase()} — ${d.emp.code} ${d.emp.fullName}`,
                    `Lương tháng: ${m(l.monthlySalary)} · Đơn giá ca: ${m(l.salaryPerShift)} · Ca chuẩn: ${l.scheduledShifts}`,
                    `Lương cơ bản: ${m(l.basePay)}`, `Trừ ${l.unpaidShifts} ca nghỉ không lương: -${m(l.unpaidDeduction)}`,
                    `Phụ trội ${l.holidayWorkedShifts} ca lễ/Tết: ${m(l.holidayPremium)}`, `Chuyên cần: ${m(l.attendanceBonus)}`,
                    ...d.adjustments.map(a => `${a.type} (${a.reason}): ${m(a.amount)}`),
                    `ĐƯỢC HƯỞNG: ${m(l.gross)}`, `Đã ứng: ${m(l.advances)} · Đã trả: ${m(l.paid)}`, `CÒN PHẢI TRẢ: ${m(l.remaining)}`];
                try { await navigator.clipboard.writeText(lines.join('\n')); this.flash('Đã sao chép phiếu lương, có thể dán gửi nhân viên.'); }
                catch { this.error = 'Trình duyệt không cho sao chép.'; }
            },

            // ---------- tăng / giảm ----------
            normalizeSign(a) { const neg = NEGATIVE_TYPES.includes(String(a.type || '').toLowerCase()); a.amount = neg ? -Math.abs(a.amount) : a.amount; },
            async saveAdj(a) {
                if (!a.employeeId || !a.type || !a.reason || !a.amount) { this.error = 'Dòng tăng/giảm cần đủ nhân viên, loại, số tiền và lý do.'; return; }
                await this.mutate(() => DeLongApi.put(`/api/admin/properties/${this.propertyId}/payroll/adjustments/${a.id}`, { month: this.month, employeeId: a.employeeId, type: a.type, amount: a.amount, reason: a.reason }), 'Đã lưu khoản tăng/giảm.');
            },
            async addAdj() {
                const a = this.newAdj;
                if (!a.employeeId || !a.type || !a.reason || !a.amount) { this.error = 'Nhập đủ nhân viên, loại, số tiền và lý do.'; return; }
                const ok = await this.mutate(() => DeLongApi.post(`/api/admin/properties/${this.propertyId}/payroll/adjustments`, { month: this.month, employeeId: a.employeeId, type: a.type, amount: a.amount, reason: a.reason }), 'Đã thêm khoản tăng/giảm.');
                if (ok) this.newAdj = { employeeId: a.employeeId, type: 'Thưởng', amount: 0, reason: '' };
            },
            async deleteAdj(a) { await this.mutate(() => DeLongApi.delete(`/api/admin/properties/${this.propertyId}/payroll/adjustments/${a.id}`), 'Đã xoá khoản tăng/giảm.'); },
            startAdj(emp) { this.drawer = null; this.tab = 'adj'; this.newAdj = { employeeId: emp.id, type: 'Thưởng', amount: 0, reason: '' }; },

            // ---------- ứng / trả ----------
            resetTxn(employeeId, kind, amount) {
                this.newTxn = { requestKey: crypto.randomUUID(), employeeId: employeeId || '', kind: kind ?? (this.locked ? 1 : 0), amount: amount || 0, method: 0, localAt: localNow(this.timeZoneId), note: '' };
            },
            startTxn(emp, kind, amount) { this.drawer = null; this.tab = 'txn'; this.resetTxn(emp.id, kind, amount); },
            async addTxn() {
                const t = this.newTxn;
                if (!t.employeeId || !t.amount || !t.localAt) { this.error = 'Nhập đủ ngày, nhân viên và số tiền.'; return; }
                const body = { requestKey: t.requestKey, employeeId: t.employeeId, month: this.month, kind: t.kind, amount: t.amount, method: t.method, occurredAt: localToUtcIso(t.localAt, this.timeZoneId), note: t.note || null };
                const ok = await this.mutate(() => DeLongApi.post(`/api/admin/properties/${this.propertyId}/payroll/transactions`, body), t.kind === 0 ? 'Đã ghi ứng lương và tạo phiếu chi.' : 'Đã ghi trả lương và tạo phiếu chi.');
                if (ok) this.resetTxn();
            },
            openVoid(t) {
                this.dialog = { title: 'Huỷ khoản ' + (t.kind === 0 ? 'ứng' : 'trả') + ' lương', text: `${this.empLabel(t.employeeId)} · ${this.money(t.amount)} đ. Phiếu chi liên kết cũng bị huỷ.`, label: 'Lý do huỷ', value: '', action: 'Huỷ khoản này',
                    confirm: async () => { const reason = this.dialog.value; const ok = await this.mutate(() => DeLongApi.post(`/api/admin/properties/${this.propertyId}/payroll/transactions/${t.id}/void`, { reason }), 'Đã huỷ khoản và phiếu chi liên kết.'); if (ok) this.dialog = null; } };
            },

            // ---------- nhân viên ----------
            toEmpRow(e) {
                return { key: e.id, id: e.id, code: e.code, fullName: e.fullName, position: e.position || '', phone: e.phone || '', shiftsPerDay: e.shiftsPerDay,
                    startDate: e.startDate, endDate: e.endDate || '', attendanceBonusEligible: e.attendanceBonusEligible, isActive: e.isActive, sortOrder: e.sortOrder,
                    note: e.note, monthlySalary: Number(e.monthlySalary || 0), originalSalary: Number(e.monthlySalary || 0), salaryEffectiveFrom: this.month + '-01', dirty: false };
            },
            addEmpRow() {
                const n = this.empRows.length + 1;
                this.empRows.push({ key: 'new-' + Date.now(), id: null, code: 'NV' + pad(n), fullName: '', position: '', phone: '', shiftsPerDay: 4, startDate: this.month + '-01', endDate: '',
                    attendanceBonusEligible: true, isActive: true, sortOrder: n, note: null, monthlySalary: 0, originalSalary: 0, salaryEffectiveFrom: this.month + '-01', dirty: true });
            },
            async saveEmp(e) {
                if (!e.code || !e.fullName || !e.startDate) { this.error = 'Nhân viên cần mã, họ tên và ngày vào làm.'; return; }
                const body = { code: e.code, fullName: e.fullName, position: e.position || null, phone: e.phone || null, shiftsPerDay: e.shiftsPerDay, startDate: e.startDate, endDate: e.endDate || null,
                    attendanceBonusEligible: e.attendanceBonusEligible, isActive: e.isActive, sortOrder: Number(e.sortOrder || 0), note: e.note || null, monthlySalary: Number(e.monthlySalary || 0),
                    salaryEffectiveFrom: e.id ? (e.monthlySalary !== e.originalSalary ? e.salaryEffectiveFrom : null) : e.startDate };
                const url = `/api/admin/properties/${this.propertyId}/payroll/employees` + (e.id ? '/' + e.id : '');
                await this.mutate(() => e.id ? DeLongApi.put(url, body) : DeLongApi.post(url, body), e.id ? 'Đã lưu ' + e.code + '.' : 'Đã thêm ' + e.code + '.');
            },

            // ---------- cài đặt ----------
            async savePolicy() { await this.mutate(() => DeLongApi.put(`/api/admin/properties/${this.propertyId}/payroll/policy`, this.policyForm), 'Đã lưu chính sách; bảng lương nháp đã tính lại.'); },
            async addHoliday() {
                const h = this.newHoliday; if (!h.date || !h.name) { this.error = 'Nhập ngày và tên ngày lễ.'; return; }
                const ok = await this.mutate(() => DeLongApi.post(`/api/admin/properties/${this.propertyId}/payroll/holidays`, h), 'Đã thêm ngày lễ.');
                if (ok) this.newHoliday = { date: '', name: '', kind: h.kind };
            },
            async deleteHoliday(h) { await this.mutate(() => DeLongApi.delete(`/api/admin/properties/${this.propertyId}/payroll/holidays/${h.id}`), 'Đã xoá ngày lễ.'); },

            // ---------- chốt ----------
            async lockMonth() {
                await this.flush();
                await this.mutate(() => DeLongApi.post(`/api/admin/properties/${this.propertyId}/payroll/lock`, { month: this.month }), 'Đã chốt bảng lương ' + this.monthLabel + '. Giờ có thể trả lương.');
                this.resetTxn();
            },
            openUnlock() {
                this.dialog = { title: 'Mở chốt bảng lương ' + this.monthLabel, text: 'Chỉ mở được khi chưa có khoản trả lương nào của tháng này.', label: 'Lý do mở chốt', value: '', action: 'Mở chốt',
                    confirm: async () => { const reason = this.dialog.value; const ok = await this.mutate(() => DeLongApi.post(`/api/admin/properties/${this.propertyId}/payroll/unlock`, { month: this.month, reason }), 'Đã mở chốt, bảng lương quay về nháp.'); if (ok) { this.dialog = null; this.resetTxn(); } } };
            },

            async mutate(fn, okMessage) {
                if (this.busy) return false;
                this.busy = true; this.error = '';
                try { await fn(); await this.load({ quiet: true }); this.flash(okMessage); return true; }
                catch (e) { this.error = e.message; await this.load({ quiet: true }); return false; }
                finally { this.busy = false; }
            },
            clearEmpQuery() { this.empQuery = ''; },
            flash(text) { this.message = text; clearTimeout(this._flashTimer); this._flashTimer = setTimeout(() => { this.message = ''; }, 3500); }
        },
        watch: {
            empQuery() { this.sel = null; this.dragging = false; }
        },
        mounted() {
            this._onKey = ev => this.onKey(ev);
            this._onUp = () => { this.dragging = false; };
            this._onLeave = ev => { if (this.pendingCount) { this.flush(); ev.preventDefault(); ev.returnValue = ''; } };
            document.addEventListener('keydown', this._onKey);
            document.addEventListener('mouseup', this._onUp);
            window.addEventListener('beforeunload', this._onLeave);
            this.load();
        },
        beforeUnmount() {
            document.removeEventListener('keydown', this._onKey);
            document.removeEventListener('mouseup', this._onUp);
            window.removeEventListener('beforeunload', this._onLeave);
            activeLoad?.abort();
        }
    }).component('emp-picker', EmpPicker).mount(root);
})();
