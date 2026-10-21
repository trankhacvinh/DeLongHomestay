window.DeLongBookingHistory = {
    data() { return { bookingHistory: { open: false, loading: false, error: '', rows: [], activity: null, selected: null, more: false, bookingId: null } }; },
    watch: {
        selectedBooking: {
            handler(booking) { if (booking?.id) this.loadBookingHistory(booking.id); else this.bookingHistory = { open: false, loading: false, error: '', rows: [], activity: null, selected: null, more: false, bookingId: null }; }
        }
    },
    methods: {
        async loadBookingHistory(id, append = false) {
            const state = this.bookingHistory;
            if (append && state.loading) return;
            state.loading = true; state.error = '';
            if (!append) { state.bookingId = id; state.rows = []; state.selected = null; state.activity = null; }
            try {
                const api = `/api/admin/properties/${this.propertyId}/audit/Booking/${id}`;
                const [rows, activity] = await Promise.all([
                    DeLongApi.get(`${api}?skip=${append ? state.rows.length : 0}`),
                    append ? Promise.resolve(state.activity) : DeLongApi.get(`${api}/activity`)
                ]);
                if (state.bookingId !== id) return;
                state.rows = append ? [...state.rows, ...rows] : rows;
                state.activity = activity; state.more = rows.length === 50;
            } catch (error) { if (state.bookingId === id) state.error = error.message || 'Không thể tải lịch sử'; }
            finally { if (state.bookingId === id) state.loading = false; }
        },
        bookingHistoryActor(row) {
            if (!row) return 'Chưa có thông tin';
            if (row.actorName) return row.actorName;
            if (row.actorUserId) return 'Tài khoản không còn hiển thị';
            if (row.action === 'Created' && String(this.selectedBooking?.source).toLowerCase() === 'website') return 'Khách đặt qua website';
            return 'Hệ thống / chưa ghi nhận tài khoản';
        },
        bookingHistoryTime(value) {
            if (!value) return 'Chưa có thông tin';
            const data = document.getElementById('calendar-page-data') || document.getElementById('bookings-page-data');
            let zone = 'Asia/Ho_Chi_Minh';
            try { zone = JSON.parse(data?.textContent || '{}').timeZoneId || zone; } catch {}
            return new Intl.DateTimeFormat('vi-VN', { timeZone: zone, dateStyle: 'short', timeStyle: 'medium' }).format(new Date(value));
        },
        bookingHistoryAction(action) {
            return ({ Created:'Tạo đặt phòng', Updated:'Sửa đặt phòng', StatusChanged:'Đổi trạng thái', Moved:'Chuyển phòng / thời gian',
                PaymentReceived:'Ghi nhận thanh toán', PaymentRefunded:'Hoàn tiền', PaymentVoided:'Vô hiệu giao dịch',
                GuestDetailsUpdated:'Sửa thông tin người đặt', IdentityUpdated:'Cập nhật giấy tờ', IdentityRemoved:'Xóa giấy tờ', LatePaymentResolved:'Xử lý thanh toán đến muộn', AutoPaymentConfirmed:'Xác nhận từ thanh toán', PaymentExpired:'Hết hạn thanh toán' })[action] || action;
        },
        bookingHistoryChanges(row) {
            const parse = value => { try { return JSON.parse(value || '{}') || {}; } catch { return {}; } };
            const before = parse(row?.beforeJson), after = parse(row?.afterJson);
            const labels = {code:'Mã đặt phòng',type:'Kiểu đặt',roomId:'Phòng',customerId:'Hồ sơ khách',rateName:'Mức giá',unitPrice:'Đơn giá',nightCount:'Số đêm',checkInUtc:'Nhận phòng',checkOutUtc:'Trả phòng',status:'Trạng thái',roomAmount:'Tiền phòng',specialSurchargeAmount:'Phụ thu ngày đặc biệt',extraAmount:'Phụ thu',discountAmount:'Giảm giá',source:'Nguồn',note:'Ghi chú',displayColor:'Màu lịch',paymentAmount:'Số tiền giao dịch',paymentType:'Loại giao dịch',paymentMethod:'Phương thức',isVoided:'Đã vô hiệu',occurredAtUtc:'Thời điểm giao dịch',guestCount:'Số khách',emailUpdated:'Email đã thay đổi',side:'Mặt giấy tờ'};
            const format = (key, value) => {
                if (value === null || value === undefined) return '—';
                if (key === 'roomId') return this.rooms?.find(room => room.id === value)?.name || value;
                if (key === 'customerId') return 'Hồ sơ ' + value;
                if (key.endsWith('Utc')) return this.bookingHistoryTime(value);
                if (key === 'status') return ({Requested:'Yêu cầu mới',Held:'Giữ phòng',Confirmed:'Đã xác nhận',CheckedIn:'Đã nhận phòng',Completed:'Hoàn tất',Cancelled:'Đã hủy',NoShow:'Không đến'})[value] || String(value);
                if (key.endsWith('Amount') || key === 'unitPrice') return this.money(Number(value));
                if (typeof value === 'boolean') return value ? 'Có' : 'Không';
                if (key === 'paymentType') return value === 'Receipt' ? 'Thu tiền' : 'Hoàn tiền';
                return String(value);
            };
            return Object.keys(labels).filter(key => (key in before || key in after) && JSON.stringify(before[key]) !== JSON.stringify(after[key]))
                .map(key => ({ key, label: labels[key], before: format(key, before[key]), after: format(key, after[key]) }));
        }
    }
};
