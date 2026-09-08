(function () {
    const root = document.getElementById('vouchers-page');
    if (!root) return;

    const initial = JSON.parse(document.getElementById('vouchers-page-data').textContent || '{}');
    const { createApp } = Vue;
    const propertyTimeZone = initial.timeZoneId || 'Asia/Ho_Chi_Minh';
    let filterPickers = [];
    let editorPickers = [];

    const emptyForm = () => {
        const now = new Date();
        const end = new Date(now.getTime() + 30 * 86400000);
        return {
            code: '', description: '', discountPercent: 10,
            startsAtLocal: zonedInput(now, propertyTimeZone), endsAtLocal: zonedInput(end, propertyTimeZone),
            timeSlot: true, overnight: true, fullDay: true,
            totalUsageLimit: null, perCustomerUsageLimit: 1,
            customerIds: [], status: 0
        };
    };

    function zonedInput(value, timeZone) {
        const parts = Object.fromEntries(new Intl.DateTimeFormat('en-CA', {
            timeZone, year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', hour12: false
        }).formatToParts(new Date(value)).filter(part => part.type !== 'literal').map(part => [part.type, part.value]));
        return `${parts.year}-${parts.month}-${parts.day}T${parts.hour === '24' ? '00' : parts.hour}:${parts.minute}`;
    }

    function zonedToIso(value, timeZone) {
        const match = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})$/.exec(value || '');
        if (!match) throw new Error('Thời gian không hợp lệ.');
        const [, year, month, day, hour, minute] = match.map(Number);
        const guess = Date.UTC(year, month - 1, day, hour, minute);
        const displayed = Object.fromEntries(new Intl.DateTimeFormat('en-CA', {
            timeZone, year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: false
        }).formatToParts(new Date(guess)).filter(part => part.type !== 'literal').map(part => [part.type, Number(part.value)]));
        const displayedHour = displayed.hour === 24 ? 0 : displayed.hour;
        const offset = Date.UTC(displayed.year, displayed.month - 1, displayed.day, displayedHour, displayed.minute, displayed.second) - guess;
        return new Date(guess - offset).toISOString();
    }

    function normalizeSearch(value) {
        return String(value || '').normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().trim();
    }

    createApp({
        data() {
            const first = new Date();
            first.setDate(1);
            const last = new Date(first.getFullYear(), first.getMonth() + 1, 0);
            return {
                propertyId: initial.propertyId,
                propertyName: initial.propertyName,
                timeZoneId: initial.timeZoneId || 'Asia/Ho_Chi_Minh',
                canManage: initial.canManage === true,
                customers: initial.customers || [],
                vouchers: [], loading: false, saving: false,
                filters: { q: '', status: 'Active', from: zonedInput(first, propertyTimeZone).slice(0, 10), to: zonedInput(last, propertyTimeZone).slice(0, 10) },
                editor: { open: false, mode: 'create', voucherId: null, error: '' },
                form: emptyForm(),
                customerSearch: '',
                customerPickerOpen: false,
                detail: { open: false, loading: false, voucher: null, history: [], emails: [] },
                mailer: { open: false, voucher: null, email: '', customerName: '', sending: false, error: '' },
                toast: { show: false, type: 'success', message: '', timer: null }
            };
        },
        computed: {
            filteredCustomers() {
                const query = normalizeSearch(this.customerSearch);
                const list = query
                    ? this.customers.filter(customer => normalizeSearch(`${customer.name} ${customer.phone} ${customer.email || ''}`).includes(query))
                    : this.customers;
                return list.slice(0, 100);
            },
            selectedCustomers() {
                const selected = new Set(this.form.customerIds || []);
                return this.customers.filter(customer => selected.has(customer.id));
            }
        },
        mounted() {
            this.load();
            this.$nextTick(() => this.mountFilterPickers());
        },
        beforeUnmount() {
            filterPickers.forEach(picker => picker.destroy());
            editorPickers.forEach(picker => picker.destroy());
            filterPickers = [];
            editorPickers = [];
        },
        methods: {
            number(value) { return new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 2 }).format(value || 0); },
            money(value) { return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(value || 0); },
            dateText(value) { return new Intl.DateTimeFormat('vi-VN', { timeZone: this.timeZoneId, day: '2-digit', month: '2-digit', year: 'numeric' }).format(new Date(value)); },
            dateTimeText(value) { return new Intl.DateTimeFormat('vi-VN', { timeZone: this.timeZoneId, day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit', hour12: false }).format(new Date(value)); },
            statusText(value) { return ({ 0: 'Nháp', 1: 'Hoạt động', 2: 'Tạm dừng', 3: 'Lưu trữ' })[Number(value)] || String(value); },
            statusClass(value) { return ({ 0: 'info', 1: 'success', 2: 'warning', 3: 'danger' })[Number(value)] || 'info'; },
            redemptionText(value) { return ({ 0: 'Đang giữ', 1: 'Đã dùng', 2: 'Đã giải phóng', 3: 'Đã hoàn lượt' })[Number(value)] || String(value); },
            redemptionClass(value) { return ({ 0: 'warning', 1: 'success', 2: 'info', 3: 'info' })[Number(value)] || 'info'; },
            applicabilityText(value) {
                const numeric = Number(value);
                const parts = [];
                if ((numeric & 1) === 1) parts.push('Khung giờ');
                if ((numeric & 2) === 2) parts.push('Qua đêm');
                if ((numeric & 4) === 4) parts.push('Cả ngày');
                return parts.join(', ') || '—';
            },
            api(path = '') { return `/api/admin/properties/${this.propertyId}/vouchers${path}`; },
            mountFilterPickers() {
                filterPickers.forEach(picker => picker.destroy());
                filterPickers = [];
                if (typeof flatpickr !== 'function') return;
                const locale = flatpickr.l10ns?.vn || flatpickr.l10ns?.default;
                const options = { locale, dateFormat: 'Y-m-d', altInput: true, altFormat: 'd/m/Y', allowInput: true, disableMobile: true };
                if (this.$refs.filterFromPicker) filterPickers.push(flatpickr(this.$refs.filterFromPicker, {
                    ...options,
                    defaultDate: this.filters.from,
                    onChange: (_dates, value) => { this.filters.from = value; }
                }));
                if (this.$refs.filterToPicker) filterPickers.push(flatpickr(this.$refs.filterToPicker, {
                    ...options,
                    defaultDate: this.filters.to,
                    onChange: (_dates, value) => { this.filters.to = value; }
                }));
            },
            mountEditorPickers() {
                editorPickers.forEach(picker => picker.destroy());
                editorPickers = [];
                if (typeof flatpickr !== 'function') return;
                const locale = flatpickr.l10ns?.vn || flatpickr.l10ns?.default;
                const options = {
                    locale,
                    enableTime: true,
                    time_24hr: true,
                    minuteIncrement: 1,
                    dateFormat: 'Y-m-d\\TH:i',
                    altInput: true,
                    altFormat: 'd/m/Y H:i',
                    allowInput: true,
                    disableMobile: true
                };
                if (this.$refs.startsAtPicker) editorPickers.push(flatpickr(this.$refs.startsAtPicker, {
                    ...options,
                    defaultDate: this.form.startsAtLocal,
                    onChange: (_dates, value) => { this.form.startsAtLocal = value; }
                }));
                if (this.$refs.endsAtPicker) editorPickers.push(flatpickr(this.$refs.endsAtPicker, {
                    ...options,
                    defaultDate: this.form.endsAtLocal,
                    onChange: (_dates, value) => { this.form.endsAtLocal = value; }
                }));
            },
            async load() {
                this.loading = true;
                try {
                    const query = new URLSearchParams();
                    if (this.filters.q) query.set('search', this.filters.q);
                    if (this.filters.status) query.set('status', this.filters.status);
                    if (this.filters.from) query.set('fromUtc', zonedToIso(`${this.filters.from}T00:00`, this.timeZoneId));
                    if (this.filters.to) {
                        const nextDay = new Date(`${this.filters.to}T12:00:00Z`);
                        nextDay.setUTCDate(nextDay.getUTCDate() + 1);
                        query.set('toUtc', zonedToIso(`${nextDay.toISOString().slice(0, 10)}T00:00`, this.timeZoneId));
                    }
                    this.vouchers = await DeLongApi.get(`${this.api('/')}?${query}`);
                } catch (error) { this.notify(error.message || 'Không thể tải voucher.', 'error'); }
                finally { this.loading = false; }
            },
            openCreate() {
                this.form = emptyForm();
                this.customerSearch = '';
                this.customerPickerOpen = false;
                this.editor = { open: true, mode: 'create', voucherId: null, error: '' };
                this.$nextTick(() => this.mountEditorPickers());
            },
            openEdit(voucher) {
                this.form = {
                    code: voucher.code, description: voucher.description || '', discountPercent: voucher.discountPercent,
                    startsAtLocal: zonedInput(voucher.startsAtUtc, this.timeZoneId), endsAtLocal: zonedInput(voucher.endsAtUtc, this.timeZoneId),
                    timeSlot: (Number(voucher.appliesTo) & 1) === 1, overnight: (Number(voucher.appliesTo) & 2) === 2,
                    fullDay: (Number(voucher.appliesTo) & 4) === 4,
                    totalUsageLimit: voucher.totalUsageLimit, perCustomerUsageLimit: voucher.perCustomerUsageLimit,
                    customerIds: voucher.customerId ? [voucher.customerId] : [], status: Number(voucher.status)
                };
                this.customerSearch = '';
                this.customerPickerOpen = false;
                this.editor = { open: true, mode: 'edit', voucherId: voucher.id, error: '' };
                this.$nextTick(() => this.mountEditorPickers());
            },
            closeEditor() { if (!this.saving) { this.editor.open = false; this.customerPickerOpen = false; } },
            generateCode() {
                const bytes = new Uint8Array(4);
                crypto.getRandomValues(bytes);
                this.form.code = `DL${Array.from(bytes, value => value.toString(16).padStart(2, '0')).join('').toUpperCase()}`;
            },
            toggleCustomer(customerId) {
                const current = Array.from(this.form.customerIds || []);
                const index = current.indexOf(customerId);
                if (index >= 0) current.splice(index, 1);
                else if (this.editor.mode === 'edit') current.splice(0, current.length, customerId);
                else current.push(customerId);
                this.form.customerIds = current;
            },
            clearCustomers() { this.form.customerIds = []; this.customerPickerOpen = false; },
            selectFilteredCustomers() {
                if (this.editor.mode !== 'create') return;
                this.form.customerIds = Array.from(new Set([...(this.form.customerIds || []), ...this.filteredCustomers.map(customer => customer.id)]));
            },
            batchCode(baseCode, index, count) {
                const width = Math.max(2, String(count).length);
                const suffix = `-${String(index + 1).padStart(width, '0')}`;
                const base = String(baseCode || '').trim().toUpperCase().slice(0, 50 - suffix.length);
                return `${base}${suffix}`;
            },
            payload(customerId = null, code = this.form.code) {
                return {
                    code,
                    description: this.form.description || null,
                    discountPercent: Number(this.form.discountPercent),
                    appliesTo: (this.form.timeSlot ? 1 : 0) | (this.form.overnight ? 2 : 0) | (this.form.fullDay ? 4 : 0),
                    startsAtUtc: zonedToIso(this.form.startsAtLocal, this.timeZoneId),
                    endsAtUtc: zonedToIso(this.form.endsAtLocal, this.timeZoneId),
                    totalUsageLimit: this.form.totalUsageLimit ? Number(this.form.totalUsageLimit) : null,
                    perCustomerUsageLimit: this.form.perCustomerUsageLimit ? Number(this.form.perCustomerUsageLimit) : null,
                    customerId: customerId || null,
                    status: Number(this.form.status)
                };
            },
            async save() {
                this.editor.error = '';
                if (!this.form.code || !this.form.startsAtLocal || !this.form.endsAtLocal) { this.editor.error = 'Vui lòng nhập mã và thời gian hiệu lực.'; return; }
                if (this.editor.mode === 'edit' && this.form.customerIds.length > 1) { this.editor.error = 'Voucher hiện có chỉ có thể gắn cho một khách.'; return; }
                this.saving = true;
                try {
                    if (this.editor.mode === 'create' && this.form.customerIds.length > 1) {
                        const customerIds = [...this.form.customerIds];
                        let created = 0;
                        try {
                            for (let index = 0; index < customerIds.length; index++) {
                                await DeLongApi.post(this.api('/'), this.payload(customerIds[index], this.batchCode(this.form.code, index, customerIds.length)));
                                created++;
                            }
                        } catch (error) {
                            throw new Error(created > 0
                                ? `Đã tạo ${created}/${customerIds.length} voucher. Các voucher còn lại chưa tạo: ${error.message || 'có lỗi xảy ra'}`
                                : (error.message || 'Không thể tạo voucher hàng loạt.'));
                        }
                        this.editor.open = false;
                        await this.load();
                        this.notify(`Đã tạo ${created} voucher cho ${created} khách.`);
                        return;
                    }

                    const customerId = this.form.customerIds[0] || null;
                    if (this.editor.mode === 'create') await DeLongApi.post(this.api('/'), this.payload(customerId));
                    else await DeLongApi.put(this.api(`/${this.editor.voucherId}`), this.payload(customerId));
                    this.editor.open = false;
                    await this.load();
                    this.notify('Đã lưu voucher.');
                } catch (error) { this.editor.error = error.message || 'Không thể lưu voucher.'; }
                finally { this.saving = false; }
            },
            async archiveVoucher() {
                if (!confirm('Lưu trữ voucher này? Mã sẽ không thể sử dụng và lịch sử vẫn được giữ.')) return;
                this.saving = true;
                try { await DeLongApi.delete(this.api(`/${this.editor.voucherId}`)); this.editor.open = false; await this.load(); this.notify('Đã lưu trữ voucher.'); }
                catch (error) { this.editor.error = error.message || 'Không thể lưu trữ voucher.'; }
                finally { this.saving = false; }
            },
            async openDetail(voucher) {
                this.detail = { open: true, loading: true, voucher, history: [], emails: [] };
                try {
                    const result = await DeLongApi.get(this.api(`/${voucher.id}/history`));
                    if (!this.detail.open || this.detail.voucher?.id !== voucher.id) return;
                    this.detail.history = result.redemptions || [];
                    this.detail.emails = result.emailDeliveries || [];
                } catch (error) { this.notify(error.message || 'Không thể tải lịch sử.', 'error'); }
                finally { this.detail.loading = false; }
            },
            closeDetail() { this.detail.open = false; },
            async restore(item) {
                const reason = prompt('Nhập lý do hoàn lại lượt voucher:');
                if (!reason?.trim()) return;
                try {
                    await DeLongApi.post(this.api(`/redemptions/${item.id}/restore`), { reason: reason.trim() });
                    await this.openDetail(this.detail.voucher);
                    await this.load();
                    this.notify('Đã hoàn lại lượt voucher và lưu audit.');
                } catch (error) { this.notify(error.message || 'Không thể hoàn lượt voucher.', 'error'); }
            },
            openEmail(voucher) {
                if (!voucher) return;
                this.detail.open = false;
                this.mailer = { open: true, voucher, email: voucher.customerEmail || '', customerName: voucher.customerName || '', sending: false, error: '' };
            },
            closeEmail() { if (!this.mailer.sending) this.mailer.open = false; },
            async sendEmail() {
                if (!this.mailer.email) { this.mailer.error = 'Vui lòng nhập email người nhận.'; return; }
                this.mailer.sending = true; this.mailer.error = '';
                try {
                    await DeLongApi.post(this.api(`/${this.mailer.voucher.id}/send-email`), { recipientEmail: this.mailer.email, customerName: this.mailer.customerName || null });
                    this.mailer.open = false;
                    this.notify('Đã xếp email voucher vào hàng gửi.');
                } catch (error) { this.mailer.error = error.message || 'Không thể gửi email voucher.'; }
                finally { this.mailer.sending = false; }
            },
            async copy(value) {
                if (!value) return;
                try { await navigator.clipboard.writeText(value); this.notify('Đã sao chép mã voucher.'); }
                catch { this.notify('Trình duyệt không cho phép sao chép tự động.', 'error'); }
            },
            notify(message, type = 'success') {
                clearTimeout(this.toast.timer);
                this.toast = { show: true, message, type, timer: setTimeout(() => { this.toast.show = false; }, 3200) };
            }
        }
    }).mount(root);
})();
