(function () {
    const root = document.getElementById('finance-page');
    if (!root) return;

    const initial = JSON.parse(document.getElementById('finance-page-data').textContent || '{}');
    const { createApp } = Vue;
    const defaultTimeZone = initial.timeZoneId || 'Asia/Ho_Chi_Minh';

    function isoDate(value) {
        return `${value.getUTCFullYear()}-${String(value.getUTCMonth() + 1).padStart(2, '0')}-${String(value.getUTCDate()).padStart(2, '0')}`;
    }

    function shiftPeriod(value, period, delta) {
        const date = new Date(`${value}T00:00:00Z`);
        if (period === 'day') date.setUTCDate(date.getUTCDate() + delta);
        else if (period === 'week') date.setUTCDate(date.getUTCDate() + delta * 7);
        else {
            date.setUTCDate(1);
            date.setUTCMonth(date.getUTCMonth() + delta * (period === 'quarter' ? 3 : 1));
        }
        return isoDate(date);
    }

    createApp({
        data() {
            return {
                propertyId: initial.propertyId,
                scope: initial.scope || initial.propertyId,
                scopeName: initial.scopeName || initial.propertyName || '',
                properties: initial.properties || [],
                canMutateScope: initial.canMutateScope === true,
                period: initial.period || 'month',
                anchorDate: initial.anchorDate,
                rangeStart: initial.rangeStart,
                rangeEnd: initial.rangeEnd,
                payments: initial.payments || [],
                expenses: initial.expenses || [],
                outstanding: Number(initial.summary?.outstanding || 0),
                ledgerFilter: 'all',
                canManage: window.DeLongFinanceCanManage === true,
                saving: false,
                expenseEditor: { open: false },
                expenseForm: { category: 'Dọn phòng', description: '', amount: 0, method: 1, vendor: '', reference: '', note: '' },
                voidEditor: { open: false, expense: null, reason: '' },
                toast: { show: false, message: '', type: 'success', timer: null }
            };
        },
        computed: {
            periodLabel() {
                const start = new Date(`${this.rangeStart}T00:00:00Z`);
                const end = new Date(`${this.rangeEnd}T00:00:00Z`);
                const fullDate = new Intl.DateTimeFormat('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric', timeZone: 'UTC' });
                if (this.period === 'day') return fullDate.format(start);
                if (this.period === 'week') return `${fullDate.format(start)} – ${fullDate.format(end)}`;
                if (this.period === 'quarter') return `Quý ${Math.floor(start.getUTCMonth() / 3) + 1}/${start.getUTCFullYear()}`;
                return new Intl.DateTimeFormat('vi-VN', { month: 'long', year: 'numeric', timeZone: 'UTC' }).format(start);
            },
            periodName() {
                return ({ day: 'ngày', week: 'tuần', month: 'tháng', quarter: 'quý' })[this.period] || 'kỳ';
            },
            exportUrl() {
                const query = new URLSearchParams({ handler: 'Export', propertyId: this.propertyId, date: this.anchorDate, period: this.period, scope: this.scope });
                return `/Admin/Finance?${query.toString()}`;
            },
            ledgerEntries() {
                const paymentRows = this.payments.map(payment => ({
                    id: `payment-${payment.id}`,
                    occurredAtUtc: payment.occurredAtUtc,
                    propertyId: payment.propertyId,
                    kind: payment.type === 0 ? 'receipt' : 'refund',
                    typeLabel: payment.type === 0 ? 'Thu booking' : 'Hoàn tiền',
                    title: payment.bookingCode,
                    description: payment.customerName,
                    method: payment.method,
                    reference: payment.reference,
                    receipt: payment.type === 0 && !payment.isVoided ? Number(payment.amount || 0) : 0,
                    outflow: payment.type === 1 && !payment.isVoided ? Number(payment.amount || 0) : 0,
                    isVoided: payment.isVoided === true
                }));
                const expenseRows = this.expenses.map(expense => ({
                    id: `expense-${expense.id}`,
                    occurredAtUtc: expense.occurredAtUtc,
                    propertyId: expense.propertyId,
                    kind: 'expense',
                    typeLabel: 'Chi phí',
                    title: expense.category,
                    description: expense.description,
                    method: expense.method,
                    reference: expense.reference,
                    receipt: 0,
                    outflow: expense.isVoided ? 0 : Number(expense.amount || 0),
                    isVoided: expense.isVoided === true
                }));
                return [...paymentRows, ...expenseRows].sort((left, right) => new Date(right.occurredAtUtc) - new Date(left.occurredAtUtc));
            },
            filteredLedgerEntries() {
                if (this.ledgerFilter === 'in') return this.ledgerEntries.filter(x => x.receipt > 0);
                if (this.ledgerFilter === 'out') return this.ledgerEntries.filter(x => x.outflow > 0);
                if (this.ledgerFilter === 'void') return this.ledgerEntries.filter(x => x.isVoided);
                return this.ledgerEntries;
            },
            ledgerNet() { return this.ledgerEntries.reduce((sum, item) => sum + item.receipt - item.outflow, 0); },
            receipts() { return this.payments.filter(x => !x.isVoided && x.type === 0).reduce((sum, x) => sum + Number(x.amount || 0), 0); },
            refunds() { return this.payments.filter(x => !x.isVoided && x.type === 1).reduce((sum, x) => sum + Number(x.amount || 0), 0); },
            expenseTotal() { return this.expenses.filter(x => !x.isVoided).reduce((sum, x) => sum + Number(x.amount || 0), 0); },
            netCash() { return this.receipts - this.refunds - this.expenseTotal; }
        },
        methods: {
            money(value) {
                return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(value || 0);
            },
            propertyFor(propertyId) {
                return this.properties.find(x => x.id === propertyId) || null;
            },
            propertyLabel(propertyId) {
                return this.propertyFor(propertyId)?.name || '';
            },
            dateTimeText(value, propertyId) {
                const timeZone = this.propertyFor(propertyId)?.timeZoneId || defaultTimeZone;
                return new Intl.DateTimeFormat('vi-VN', {
                    timeZone,
                    day: '2-digit', month: '2-digit',
                    hour: '2-digit', minute: '2-digit', hour12: false
                }).format(new Date(value));
            },
            paymentMethodText(method) {
                return ({ 0: 'Tiền mặt', 1: 'Chuyển khoản', 2: 'Thẻ', 3: 'Khác', 4: 'Pay2S' })[method] || 'Khác';
            },
            navigate(anchorDate, scope, period) {
                const query = new URLSearchParams({ propertyId: this.propertyId, date: anchorDate, period, scope });
                window.location.assign(`/Admin/Finance?${query.toString()}`);
            },
            movePeriod(delta) {
                this.navigate(shiftPeriod(this.anchorDate, this.period, delta), this.scope, this.period);
            },
            changeScope() {
                this.navigate(this.anchorDate, this.scope, this.period);
            },
            changePeriod() {
                this.navigate(this.anchorDate, this.scope, this.period);
            },
            changeAnchor() {
                this.navigate(this.anchorDate, this.scope, this.period);
            },
            isInActivePeriod(value, propertyId) {
                const timeZone = this.propertyFor(propertyId)?.timeZoneId || defaultTimeZone;
                const parts = new Intl.DateTimeFormat('en-CA', { timeZone, year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(new Date(value));
                const get = type => parts.find(part => part.type === type)?.value || '';
                const localDate = `${get('year')}-${get('month')}-${get('day')}`;
                return localDate >= this.rangeStart && localDate <= this.rangeEnd;
            },
            openExpense() {
                if (!this.canManage || !this.canMutateScope) return;
                this.expenseForm = { category: 'Dọn phòng', description: '', amount: 0, method: 1, vendor: '', reference: '', note: '' };
                this.expenseEditor.open = true;
            },
            closeExpense() { if (!this.saving) this.expenseEditor.open = false; },
            async saveExpense() {
                if (!this.canMutateScope) return this.notify('Hãy chuyển cơ sở làm việc trên thanh trên trước khi ghi chi phí.', 'error');
                if (!this.expenseForm.description.trim()) return this.notify('Vui lòng nhập nội dung chi phí.', 'error');
                if (Number(this.expenseForm.amount || 0) <= 0) return this.notify('Số tiền phải lớn hơn 0.', 'error');
                this.saving = true;
                try {
                    const expense = await DeLongApi.post(
                        `/api/admin/properties/${this.propertyId}/expenses`,
                        {
                            occurredAt: null,
                            category: this.expenseForm.category,
                            description: this.expenseForm.description,
                            amount: Number(this.expenseForm.amount),
                            method: Number(this.expenseForm.method),
                            vendor: this.expenseForm.vendor || null,
                            reference: this.expenseForm.reference || null,
                            note: this.expenseForm.note || null
                        });
                    if (this.isInActivePeriod(expense.occurredAtUtc, expense.propertyId)) this.expenses.unshift(expense);
                    this.expenseEditor.open = false;
                    this.notify(this.isInActivePeriod(expense.occurredAtUtc, expense.propertyId)
                        ? 'Đã ghi chi phí.'
                        : `Đã ghi chi phí vào ngày hiện tại; khoản này nằm ngoài ${this.periodName} đang xem.`, 'success');
                } catch (error) {
                    this.notify(error.message || 'Không thể ghi chi phí.', 'error');
                } finally {
                    this.saving = false;
                }
            },
            openVoid(expense) {
                if (!this.canMutateScope) return;
                this.voidEditor = { open: true, expense, reason: '' };
            },
            closeVoid() { if (!this.saving) this.voidEditor.open = false; },
            async confirmVoid() {
                if (!this.canMutateScope) return;
                if (!this.voidEditor.expense) return;
                if (!this.voidEditor.reason.trim()) return this.notify('Vui lòng nhập lý do void.', 'error');
                this.saving = true;
                try {
                    const updated = await DeLongApi.post(
                        `/api/admin/properties/${this.propertyId}/expenses/${this.voidEditor.expense.id}/void`,
                        { reason: this.voidEditor.reason });
                    const index = this.expenses.findIndex(x => x.id === updated.id);
                    if (index >= 0) this.expenses.splice(index, 1, updated);
                    this.voidEditor.open = false;
                    this.notify('Đã void khoản chi.', 'success');
                } catch (error) {
                    this.notify(error.message || 'Không thể void khoản chi.', 'error');
                } finally {
                    this.saving = false;
                }
            },
            notify(message, type) {
                if (this.toast.timer) clearTimeout(this.toast.timer);
                const timer = setTimeout(() => { this.toast.show = false; }, 3200);
                this.toast = { show: true, message, type, timer };
            }
        }
    }).mount(root);
})();
