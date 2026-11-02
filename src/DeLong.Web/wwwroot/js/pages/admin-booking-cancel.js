(function (global) {
    const money = value => new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(Number(value || 0));
    // Mixin dùng chung cho Lịch phòng, Lịch V2 và Đặt phòng: hủy booking phải có lý do và chọn cách xử lý tiền khách đã trả.
    // Trang chủ cần có: selectedBooking, saving, changeStatus(status, extraBody) trả về true/false.
    global.DeLongBookingCancel = {
        data() {
            return { cancelDialog: { open: false, reason: '', mode: '', amount: 0, method: 1, note: '', error: '', paid: 0 } };
        },
        computed: {
            cancelModeOptions() {
                return [
                    { value: 'now', label: 'Hoàn tiền ngay', hint: 'Ghi nhận khoản hoàn tiền vào booking ngay bây giờ.' },
                    { value: 'later', label: 'Hoàn sau', hint: 'Booking vào danh sách “Chờ hoàn”, nhập khoản hoàn khi đã chuyển tiền.' },
                    { value: 'keep', label: 'Không hoàn (giữ cọc)', hint: 'Giữ lại toàn bộ số tiền khách đã trả.' }
                ];
            }
        },
        methods: {
            cancelMoney: money,
            openCancelDialog() {
                const booking = this.selectedBooking;
                if (!booking) return;
                const paid = Math.max(0, Number(booking.paidAmount || 0));
                this.cancelDialog = { open: true, reason: '', mode: paid > 0 ? '' : 'none', amount: paid, method: 1, note: '', error: '', paid };
            },
            closeCancelDialog() {
                if (this.saving) return;
                this.cancelDialog.open = false;
            },
            cancelValidationMessage() {
                const d = this.cancelDialog;
                if (!d.reason.trim()) return 'Vui lòng nhập lý do hủy.';
                if (d.paid > 0) {
                    if (!d.mode) return 'Khách đã thanh toán, hãy chọn cách xử lý tiền.';
                    if (d.mode === 'now' || d.mode === 'later') {
                        const amount = Number(d.amount);
                        if (!(amount > 0)) return 'Nhập số tiền hoàn lớn hơn 0.';
                        if (amount > d.paid) return `Số tiền hoàn không được lớn hơn ${money(d.paid)}.`;
                    }
                }
                return '';
            },
            async submitCancelDialog() {
                const d = this.cancelDialog;
                const message = this.cancelValidationMessage();
                if (message) { d.error = message; return; }
                d.error = '';
                const refund = d.paid > 0
                    ? { mode: d.mode, amount: d.mode === 'keep' ? null : Number(d.amount), method: Number(d.method), note: d.note.trim() || null }
                    : { mode: 'none' };
                const ok = await this.changeStatus(5, { reason: d.reason.trim(), refund });
                if (ok) d.open = false;
            },
            reportCancelError(message) {
                if (this.cancelDialog?.open) this.cancelDialog.error = message;
            }
        }
    };
})(window);
