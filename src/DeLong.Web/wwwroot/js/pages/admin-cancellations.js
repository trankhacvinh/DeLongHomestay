(function () {
    const root = document.getElementById('cancellations-page');
    if (!root) return;
    const initial = JSON.parse(document.getElementById('cancellations-data').textContent);
    const helpers = window.DeLongOperations;
    let activeLoad = null;
    let searchTimer = null;
    const emptyTotals = { count: 0, totalAmount: 0, paidAmount: 0, refundedAmount: 0, remainingRefundAmount: 0, keptAmount: 0, pendingCount: 0 };

    Vue.createApp({
        data: () => ({
            propertyId: initial.propertyId,
            properties: initial.properties || [],
            canManage: initial.canManage === true,
            scope: initial.scope,
            from: initial.from,
            to: initial.to,
            dateField: 'cancelled',
            status: '',
            staffId: '',
            search: '',
            page: initial.result?.page || 1,
            pageSize: initial.result?.pageSize || 50,
            rows: initial.result?.rows || [],
            totals: initial.result?.totals || emptyTotals,
            totalRows: initial.result?.totalRows || 0,
            actors: initial.result?.actors || [],
            loading: false,
            saving: false,
            error: '',
            message: '',
            formError: '',
            requestVersion: 0,
            detail: null,
            dueForm: { amount: 0, note: '' },
            addForm: { amount: '', method: 1, localAt: '', reference: '', note: '' },
            editingId: null,
            editForm: { amount: 0, method: 1, localAt: '', reference: '', note: '' },
            voidTarget: null,
            voidReason: '',
            methods: [
                { value: 1, label: 'Chuyển khoản' },
                { value: 0, label: 'Tiền mặt' },
                { value: 2, label: 'Thẻ' },
                { value: 3, label: 'Khác' }
            ]
        }),
        computed: {
            query() {
                const params = new URLSearchParams({ propertyId: this.propertyId, scope: this.scope, from: this.from, to: this.to, dateField: this.dateField });
                if (this.status) params.set('status', this.status);
                if (this.staffId) params.set('staffId', this.staffId);
                if (this.search) params.set('search', this.search);
                return params.toString();
            },
            exportUrl() { return '/api/admin/cancellations/export?' + this.query; },
            pageCount() { return Math.max(1, Math.ceil(this.totalRows / this.pageSize)); }
        },
        methods: {
            money: helpers.money,
            zone(row) { return this.properties.find(p => p.id === row.propertyId)?.timeZoneId || 'Asia/Ho_Chi_Minh'; },
            dateText(row, value) { return value ? helpers.dateText(value, this.zone(row)) : ''; },
            statusText(status) {
                return ({ NotRequired: 'Không cần hoàn', Pending: 'Chờ hoàn', Partial: 'Hoàn một phần', Refunded: 'Đã hoàn', Kept: 'Không hoàn (giữ cọc)' })[status] || status;
            },
            methodText(method) { return ({ 0: 'Tiền mặt', 1: 'Chuyển khoản', 2: 'Thẻ', 3: 'Khác', 4: 'Pay2S', 5: 'SePay' })[method] || 'Khác'; },
            bookingUrl(row) { return `/Admin/Bookings?propertyId=${row.propertyId}&bookingId=${row.bookingId}`; },
            searchChanged() {
                clearTimeout(searchTimer);
                searchTimer = setTimeout(() => this.reload(true), 350);
            },
            goPage(page) { this.page = Math.min(Math.max(1, page), this.pageCount); this.reload(false); },
            async reload(resetPage) {
                if (resetPage) this.page = 1;
                if (this.from && this.to && this.to < this.from) { this.error = 'Ngày kết thúc phải sau ngày bắt đầu.'; return; }
                activeLoad?.abort();
                const controller = new AbortController();
                activeLoad = controller;
                const version = ++this.requestVersion;
                this.loading = true;
                this.error = '';
                const timer = setTimeout(() => controller.abort(), 30000);
                try {
                    const data = await DeLongApi.get(`/api/admin/cancellations/?${this.query}&page=${this.page}&pageSize=${this.pageSize}`, { signal: controller.signal });
                    if (version !== this.requestVersion) return;
                    this.rows = data.rows;
                    this.totals = data.totals;
                    this.totalRows = data.totalRows;
                    this.pageSize = data.pageSize;
                    if (this.page > this.pageCount) this.page = this.pageCount;
                    // Giữ danh sách nhân viên đầy đủ khi đang lọc theo một nhân viên.
                    if (!this.staffId || data.actors.length > this.actors.length) this.actors = data.actors;
                    if (this.detail) {
                        const fresh = data.rows.find(x => x.id === this.detail.id);
                        if (fresh) this.detail = fresh;
                    }
                } catch (e) {
                    if (version === this.requestVersion) this.error = controller.signal.aborted ? 'Tải danh sách mất quá lâu. Vui lòng bấm Tải lại.' : e.message;
                } finally {
                    clearTimeout(timer);
                    if (version === this.requestVersion) { this.loading = false; activeLoad = null; }
                }
            },
            nowLocal(row) { return helpers.localInput(Date.now(), this.zone(row)); },
            openRow(row) {
                this.detail = row;
                this.formError = '';
                this.editingId = null;
                this.voidTarget = null;
                this.voidReason = '';
                this.dueForm = { amount: row.refundDueAmount, note: row.refundNote || '' };
                this.addForm = { amount: row.remainingRefundAmount > 0 ? row.remainingRefundAmount : '', method: 1, localAt: this.nowLocal(row), reference: '', note: '' };
            },
            closeDetail() { if (!this.saving) this.detail = null; },
            startEdit(refund) {
                this.formError = '';
                this.voidTarget = null;
                this.editingId = refund.paymentId;
                this.editForm = {
                    amount: refund.amount, method: refund.method, localAt: helpers.localInput(refund.occurredAtUtc, this.zone(this.detail)),
                    reference: refund.reference || '', note: refund.note || ''
                };
            },
            startVoid(refund) { this.formError = ''; this.editingId = null; this.voidTarget = refund; this.voidReason = ''; },
            async run(action, success) {
                if (this.saving) return;
                this.saving = true;
                this.formError = '';
                try {
                    await action();
                    this.message = success;
                    setTimeout(() => { this.message = ''; }, 4000);
                    await this.reload(false);
                    if (this.detail) this.openRow(this.detail);
                } catch (e) {
                    this.formError = e.message || 'Không thể lưu. Vui lòng thử lại.';
                } finally {
                    this.saving = false;
                }
            },
            apiBase() { return `/api/admin/properties/${this.detail.propertyId}`; },
            saveDue() {
                return this.run(() => DeLongApi.post(`${this.apiBase()}/cancellations/${this.detail.id}/refund-due`,
                    { amount: Number(this.dueForm.amount || 0), note: this.dueForm.note || null }), 'Đã cập nhật số tiền cần hoàn.');
            },
            addRefund() {
                return this.run(() => DeLongApi.post(`${this.apiBase()}/cancellations/${this.detail.id}/refunds`, {
                    amount: Number(this.addForm.amount), method: Number(this.addForm.method),
                    occurredAt: helpers.toUtc(this.addForm.localAt, this.zone(this.detail)),
                    reference: this.addForm.reference || null, note: this.addForm.note || null
                }), 'Đã ghi nhận khoản hoàn tiền.');
            },
            saveEdit(refund) {
                return this.run(async () => {
                    await DeLongApi.put(`${this.apiBase()}/cancellations/${this.detail.id}/refunds/${refund.paymentId}`, {
                        amount: Number(this.editForm.amount), method: Number(this.editForm.method),
                        occurredAt: helpers.toUtc(this.editForm.localAt, this.zone(this.detail)),
                        reference: this.editForm.reference || null, note: this.editForm.note || null
                    });
                    this.editingId = null;
                }, 'Đã sửa khoản hoàn tiền.');
            },
            confirmVoid() {
                const target = this.voidTarget;
                return this.run(async () => {
                    await DeLongApi.post(`${this.apiBase()}/payments/${target.paymentId}/void`, { reason: this.voidReason });
                    this.voidTarget = null;
                }, 'Đã hủy khoản hoàn tiền.');
            }
        },
        beforeUnmount() { this.requestVersion++; activeLoad?.abort(); clearTimeout(searchTimer); }
    }).mount(root);
})();
