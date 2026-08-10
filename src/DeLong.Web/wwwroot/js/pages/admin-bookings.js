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
            openBooking(booking) {
                this.selectedBooking = booking;
                this.detail.open = true;
            },
            closeDetail() { if (!this.saving) this.detail.open = false; },
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
