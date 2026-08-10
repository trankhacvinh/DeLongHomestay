(function () {
    const root = document.getElementById('finance-page');
    if (!root) return;

    const initial = JSON.parse(document.getElementById('finance-page-data').textContent || '{}');
    const { createApp } = Vue;
    const timeZone = initial.timeZoneId || 'Asia/Ho_Chi_Minh';

    function shiftMonth(value, delta) {
        const [year, month] = value.split('-').map(Number);
        const date = new Date(Date.UTC(year, month - 1 + delta, 1));
        return `${date.getUTCFullYear()}-${String(date.getUTCMonth() + 1).padStart(2, '0')}`;
    }

    createApp({
        data() {
            return {
                propertyId: initial.propertyId,
                month: initial.month,
                payments: initial.payments || [],
                expenses: initial.expenses || [],
                outstanding: Number(initial.summary?.outstanding || 0),
                canManage: window.DeLongFinanceCanManage === true,
                saving: false,
                expenseEditor: { open: false },
                expenseForm: { category: 'Dọn phòng', description: '', amount: 0, method: 1, vendor: '', reference: '', note: '' },
                voidEditor: { open: false, expense: null, reason: '' },
                toast: { show: false, message: '', type: 'success', timer: null }
            };
        },
        computed: {
            monthLabel() {
                const [year, month] = this.month.split('-').map(Number);
                return new Intl.DateTimeFormat('vi-VN', { month: 'long', year: 'numeric', timeZone: 'UTC' })
                    .format(new Date(Date.UTC(year, month - 1, 1)));
            },
            receipts() { return this.payments.filter(x => !x.isVoided && x.type === 0).reduce((sum, x) => sum + Number(x.amount || 0), 0); },
            refunds() { return this.payments.filter(x => !x.isVoided && x.type === 1).reduce((sum, x) => sum + Number(x.amount || 0), 0); },
            expenseTotal() { return this.expenses.filter(x => !x.isVoided).reduce((sum, x) => sum + Number(x.amount || 0), 0); },
            netCash() { return this.receipts - this.refunds - this.expenseTotal; }
        },
        methods: {
            money(value) {
                return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(value || 0);
            },
            dateTimeText(value) {
                return new Intl.DateTimeFormat('vi-VN', {
                    timeZone,
                    day: '2-digit', month: '2-digit',
                    hour: '2-digit', minute: '2-digit', hour12: false
                }).format(new Date(value));
            },
            paymentMethodText(method) {
                return ({ 0: 'Tiền mặt', 1: 'Chuyển khoản', 2: 'Thẻ', 3: 'Khác' })[method] || 'Khác';
            },
            moveMonth(delta) {
                const month = shiftMonth(this.month, delta);
                window.location.assign(`/Admin/Finance?propertyId=${this.propertyId}&month=${month}`);
            },
            openExpense() {
                if (!this.canManage) return;
                this.expenseForm = { category: 'Dọn phòng', description: '', amount: 0, method: 1, vendor: '', reference: '', note: '' };
                this.expenseEditor.open = true;
            },
            closeExpense() { if (!this.saving) this.expenseEditor.open = false; },
            async saveExpense() {
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
                    this.expenses.unshift(expense);
                    this.expenseEditor.open = false;
                    this.notify('Đã ghi chi phí.', 'success');
                } catch (error) {
                    this.notify(error.message || 'Không thể ghi chi phí.', 'error');
                } finally {
                    this.saving = false;
                }
            },
            openVoid(expense) {
                this.voidEditor = { open: true, expense, reason: '' };
            },
            closeVoid() { if (!this.saving) this.voidEditor.open = false; },
            async confirmVoid() {
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
