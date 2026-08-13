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
                statusConfirm: { open: false, action: null, title: '', message: '' },
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
                return ({
                    0: 'Yêu cầu',
                    1: 'Giữ phòng',
                    2: 'Đã xác nhận',
                    3: 'Đang ở',
                    4: 'Hoàn tất',
                    5: 'Đã hủy',
                    6: 'Không đến'
                })[status] || `#${status}`;
            },
            statusClass(status) {
                if (status === 1) return 'warning';
                if (status === 2 || status === 3 || status === 4) return 'success';
                if (status === 5 || status === 6) return 'danger';
                return 'info';
            },
            sourceText(source) {
                if (!source) return 'Không rõ nguồn';
                const value = String(source).trim();
                return ({ Website: 'Trang web', website: 'Trang web' })[value] || value;
            },
            paymentMethodText(method) {
                return ({ 0: 'Tiền mặt', 1: 'Chuyển khoản', 2: 'Thẻ', 3: 'Khác' })[method] || 'Khác';
            },
            friendlyError(error, fallback) {
                const problem = error?.problem || {};
                const marker = `${problem.code || ''} ${problem.type || ''}`.toLowerCase();
                if (error?.status === 409 || marker.includes('booking_conflict')) {
                    return 'Phòng này đã có lượt đặt trùng thời gian. Vui lòng kiểm tra lịch hoặc chọn phòng/khung giờ khác.';
                }
                if (error?.status === 429) {
                    return 'Bạn thao tác quá nhanh. Vui lòng chờ một chút rồi thử lại.';
                }
                return error?.message || fallback;
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
                this.statusConfirm.open = false;
            },
            async loadPayments() {
                if (!this.selectedBooking) return;
                this.loadingPayments = true;
                try {
                    this.payments = await DeLongApi.get(
                        `/api/admin/properties/${this.propertyId}/bookings/${this.selectedBooking.id}/payments`);
                } catch (error) {
                    this.notify(this.friendlyError(error, 'Không thể tải thanh toán.'), 'error');
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
                    this.notify(payment.type === 0 ? 'Đã ghi nhận khoản thu.' : 'Đã ghi nhận khoản hoàn tiền.', 'success');
                } catch (error) {
                    this.notify(this.friendlyError(error, 'Không thể ghi nhận thanh toán.'), 'error');
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
                if (!this.voidEditor.reason.trim()) return this.notify('Vui lòng nhập lý do vô hiệu giao dịch.', 'error');
                this.saving = true;
                try {
                    const updated = await DeLongApi.post(
                        `/api/admin/properties/${this.propertyId}/payments/${this.voidEditor.payment.id}/void`,
                        { reason: this.voidEditor.reason });
                    const index = this.payments.findIndex(x => x.id === updated.id);
                    if (index >= 0) this.payments.splice(index, 1, updated);
                    await this.reloadSelectedBooking();
                    this.voidEditor.open = false;
                    this.notify('Đã vô hiệu giao dịch. Lịch sử vẫn được giữ lại.', 'success');
                } catch (error) {
                    this.notify(this.friendlyError(error, 'Không thể vô hiệu giao dịch.'), 'error');
                } finally {
                    this.saving = false;
                }
            },
            nextActions(booking) {
                if (!booking) return [];
                if (booking.status === 0) return [
                    { status: 1, label: 'Giữ phòng', className: 'btn-light' },
                    { status: 2, label: 'Xác nhận', className: 'btn-primary' },
                    {
                        status: 5,
                        label: 'Hủy yêu cầu',
                        className: 'btn-danger',
                        confirmTitle: 'Hủy yêu cầu đặt phòng?',
                        confirmMessage: 'Yêu cầu sẽ chuyển sang trạng thái Đã hủy. Bạn vẫn có thể khôi phục nếu thao tác nhầm.'
                    }
                ];
                if (booking.status === 1) return [
                    { status: 2, label: 'Xác nhận', className: 'btn-primary' },
                    {
                        status: 5,
                        label: 'Hủy giữ phòng',
                        className: 'btn-danger',
                        confirmTitle: 'Hủy giữ phòng?',
                        confirmMessage: 'Phòng sẽ được giải phóng khỏi lượt đặt này. Bạn vẫn có thể khôi phục yêu cầu nếu thao tác nhầm.'
                    }
                ];
                if (booking.status === 2) return [
                    { status: 3, label: 'Nhận phòng', className: 'btn-primary' },
                    {
                        status: 6,
                        label: 'Khách không đến',
                        className: 'btn-light',
                        confirmTitle: 'Đánh dấu khách không đến?',
                        confirmMessage: 'Trạng thái này sẽ giải phóng phòng. Nếu bấm nhầm, bạn có thể khôi phục lại Đã xác nhận.'
                    },
                    {
                        status: 5,
                        label: 'Hủy đặt phòng',
                        className: 'btn-danger',
                        confirmTitle: 'Hủy lượt đặt phòng?',
                        confirmMessage: 'Lượt đặt sẽ bị hủy và phòng được giải phóng. Bạn vẫn có thể khôi phục yêu cầu nếu thao tác nhầm.'
                    }
                ];
                if (booking.status === 3) return [
                    { status: 4, label: 'Trả phòng / Hoàn tất', className: 'btn-primary' }
                ];
                if (booking.status === 5) return [
                    {
                        status: 0,
                        label: 'Khôi phục yêu cầu',
                        className: 'btn-light',
                        confirmTitle: 'Khôi phục lượt đặt?',
                        confirmMessage: 'Lượt đặt sẽ quay về trạng thái Yêu cầu. Sau đó bạn có thể giữ hoặc xác nhận phòng lại.'
                    }
                ];
                if (booking.status === 6) return [
                    {
                        status: 2,
                        label: 'Khôi phục đặt phòng',
                        className: 'btn-light',
                        confirmTitle: 'Khôi phục lượt đặt?',
                        confirmMessage: 'Lượt đặt sẽ quay lại Đã xác nhận. Hệ thống sẽ kiểm tra trùng phòng trước khi khôi phục.'
                    }
                ];
                return [];
            },
            requestStatus(action) {
                if (!action) return;
                if (!action.confirmTitle) {
                    this.changeStatus(action.status);
                    return;
                }
                this.statusConfirm = {
                    open: true,
                    action,
                    title: action.confirmTitle,
                    message: action.confirmMessage || 'Bạn có chắc muốn thay đổi trạng thái này?'
                };
            },
            closeStatusConfirm() {
                if (!this.saving) this.statusConfirm.open = false;
            },
            async confirmStatusChange() {
                const action = this.statusConfirm.action;
                if (!action) return;
                this.statusConfirm.open = false;
                await this.changeStatus(action.status);
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
                    this.notify(`Đã chuyển sang trạng thái “${this.statusText(updated.status)}”.`, 'success');
                } catch (error) {
                    this.notify(this.friendlyError(error, 'Không thể đổi trạng thái đặt phòng.'), 'error');
                } finally {
                    this.saving = false;
                }
            },
            notify(message, type) {
                if (this.toast.timer) clearTimeout(this.toast.timer);
                const duration = type === 'error' ? 5200 : 3200;
                const timer = setTimeout(() => { this.toast.show = false; }, duration);
                this.toast = { show: true, message: String(message || ''), type, timer };
            }
        }
    }).mount(root);
})();
