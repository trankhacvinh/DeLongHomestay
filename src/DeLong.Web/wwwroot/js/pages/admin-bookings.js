(function () {
    const root = document.getElementById('bookings-page');
    if (!root) return;

    const initial = JSON.parse(document.getElementById('bookings-page-data').textContent || '{}');
    const { createApp } = Vue;
    const timeZone = initial.timeZoneId || 'Asia/Ho_Chi_Minh';

    createApp({
        data() {
            return {
                propertyId: initial.propertyId,
                bookings: initial.bookings || [],
                canManage: window.DeLongBookingsCanManage === true,
                search: '',
                statusFilter: '',
                selectedBooking: null,
                detail: { open: false },
                payments: [],
                loadingPayments: false,
                paymentEditor: { open: false },
                paymentForm: { type: 0, method: 1, amount: 0, reference: '', note: '' },
                voidEditor: { open: false, payment: null, reason: '' },
                saving: false,
                toast: { show: false, message: '', type: 'success', timer: null }
            };
        },
        computed: {
            filteredBookings() {
                const q = this.search.toLowerCase();
                return this.bookings.filter(booking => {
                    if (this.statusFilter !== '' && String(booking.status) !== this.statusFilter) return false;
                    if (!q) return true;
                    return [booking.code, booking.customerName, booking.customerPhone, booking.roomName, booking.roomCode]
                        .some(value => String(value || '').toLowerCase().includes(q));
                });
            }
        },
        methods: {
            money(value) {
                return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(value || 0);
            },
            dateTimeText(value) {
                return new Intl.DateTimeFormat('vi-VN', {
                    timeZone,
                    day: '2-digit', month: '2-digit', year: 'numeric',
                    hour: '2-digit', minute: '2-digit', hour12: false
                }).format(new Date(value));
            },
            statusText(status) {
                return ({ 0: 'Yêu cầu', 1: 'Giữ phòng', 2: 'Đã xác nhận', 3: 'Đang ở', 4: 'Hoàn tất', 5: 'Đã hủy', 6: 'No-show' })[status] || `#${status}`;
            },
            statusClass(status) {
                if (status === 1) return 'warning';
                if (status === 2 || status === 3 || status === 4) return 'success';
                if (status === 5 || status === 6) return 'danger';
                return 'info';
            },
            paymentMethodText(method) {
                return ({ 0: 'Tiền mặt', 1: 'Chuyển khoản', 2: 'Thẻ', 3: 'Khác' })[method] || 'Khác';
            },
            async openBooking(booking) {
                this.selectedBooking = booking;
                this.detail.open = true;
                await this.loadPayments();
            },
            closeDetail() {
                if (this.saving) return;
                this.detail.open = false;
                this.paymentEditor.open = false;
                this.voidEditor.open = false;
            },
            async loadPayments() {
                if (!this.selectedBooking) return;
                this.loadingPayments = true;
                try {
                    this.payments = await DeLongApi.get(
                        `/api/admin/properties/${this.propertyId}/bookings/${this.selectedBooking.id}/payments`);
                } catch (error) {
                    this.notify(error.message || 'Không thể tải thanh toán.', 'error');
                } finally {
                    this.loadingPayments = false;
                }
            },
            async reloadSelectedBooking() {
                if (!this.selectedBooking) return;
                const updated = await DeLongApi.get(
                    `/api/admin/properties/${this.propertyId}/bookings/${this.selectedBooking.id}`);
                const index = this.bookings.findIndex(x => x.id === updated.id);
                if (index >= 0) this.bookings.splice(index, 1, updated);
                this.selectedBooking = updated;
            },
            openPayment() {
                const suggested = Math.max(0, Number(this.selectedBooking?.balanceAmount || 0));
                this.paymentForm = { type: 0, method: 1, amount: suggested, reference: '', note: '' };
                this.paymentEditor.open = true;
            },
            closePayment() { if (!this.saving) this.paymentEditor.open = false; },
            async savePayment() {
                if (!this.selectedBooking) return;
                if (Number(this.paymentForm.amount || 0) <= 0) return this.notify('Số tiền phải lớn hơn 0.', 'error');
                this.saving = true;
                try {
                    const payment = await DeLongApi.post(
                        `/api/admin/properties/${this.propertyId}/bookings/${this.selectedBooking.id}/payments`,
                        {
                            type: Number(this.paymentForm.type),
                            method: Number(this.paymentForm.method),
                            amount: Number(this.paymentForm.amount),
                            occurredAt: null,
                            reference: this.paymentForm.reference || null,
                            note: this.paymentForm.note || null
                        });
                    this.payments.unshift(payment);
                    await this.reloadSelectedBooking();
                    this.paymentEditor.open = false;
                    this.notify(payment.type === 0 ? 'Đã ghi nhận khoản thu.' : 'Đã ghi nhận hoàn tiền.', 'success');
                } catch (error) {
                    this.notify(error.message || 'Không thể ghi nhận thanh toán.', 'error');
                } finally {
                    this.saving = false;
                }
            },
            openVoid(payment) {
                this.voidEditor = { open: true, payment, reason: '' };
            },
            closeVoid() { if (!this.saving) this.voidEditor.open = false; },
            async confirmVoid() {
                if (!this.voidEditor.payment) return;
                if (!this.voidEditor.reason.trim()) return this.notify('Vui lòng nhập lý do void.', 'error');
                this.saving = true;
                try {
                    const updated = await DeLongApi.post(
                        `/api/admin/properties/${this.propertyId}/payments/${this.voidEditor.payment.id}/void`,
                        { reason: this.voidEditor.reason });
                    const index = this.payments.findIndex(x => x.id === updated.id);
                    if (index >= 0) this.payments.splice(index, 1, updated);
                    await this.reloadSelectedBooking();
                    this.voidEditor.open = false;
                    this.notify('Đã void giao dịch, lịch sử vẫn được giữ.', 'success');
                } catch (error) {
                    this.notify(error.message || 'Không thể void giao dịch.', 'error');
                } finally {
                    this.saving = false;
                }
            },
            nextActions(booking) {
                if (!booking) return [];
                if (booking.status === 0) return [
                    { status: 1, label: 'Giữ phòng', className: 'btn-light' },
                    { status: 2, label: 'Xác nhận', className: 'btn-primary' },
                    { status: 5, label: 'Hủy', className: 'btn-danger' }
                ];
                if (booking.status === 1) return [
                    { status: 2, label: 'Xác nhận', className: 'btn-primary' },
                    { status: 5, label: 'Hủy', className: 'btn-danger' }
                ];
                if (booking.status === 2) return [
                    { status: 3, label: 'Check-in', className: 'btn-primary' },
                    { status: 6, label: 'No-show', className: 'btn-light' },
                    { status: 5, label: 'Hủy', className: 'btn-danger' }
                ];
                if (booking.status === 3) return [
                    { status: 4, label: 'Check-out / Hoàn tất', className: 'btn-primary' }
                ];
                return [];
            },
            async changeStatus(status) {
                if (!this.selectedBooking) return;
                this.saving = true;
                try {
                    const updated = await DeLongApi.post(
                        `/api/admin/properties/${this.propertyId}/bookings/${this.selectedBooking.id}/status`,
                        { status });
                    const index = this.bookings.findIndex(x => x.id === updated.id);
                    if (index >= 0) this.bookings.splice(index, 1, updated);
                    this.selectedBooking = updated;
                    this.notify(`Đã chuyển sang ${this.statusText(updated.status)}.`, 'success');
                } catch (error) {
                    this.notify(error.message || 'Không thể đổi trạng thái booking.', 'error');
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
