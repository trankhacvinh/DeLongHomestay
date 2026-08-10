(function () {
    const root = document.getElementById('calendar-page');
    if (!root) return;

    const initial = JSON.parse(document.getElementById('calendar-page-data').textContent || '{}');
    const { createApp } = Vue;
    const timeZone = initial.timeZoneId || 'Asia/Ho_Chi_Minh';
    const utcOffset = initial.utcOffset || '+07:00';

    function parseDateKey(key) {
        const [year, month, day] = key.split('-').map(Number);
        return new Date(Date.UTC(year, month - 1, day));
    }

    function dateKey(date) {
        return `${date.getUTCFullYear()}-${String(date.getUTCMonth() + 1).padStart(2, '0')}-${String(date.getUTCDate()).padStart(2, '0')}`;
    }

    function addDays(key, amount) {
        const date = parseDateKey(key);
        date.setUTCDate(date.getUTCDate() + amount);
        return dateKey(date);
    }

    function toInputValue(dayKey, time) {
        return `${dayKey}T${time}`;
    }

    createApp({
        data() {
            return {
                propertyId: initial.propertyId,
                startDate: initial.startDate,
                today: initial.today,
                rooms: initial.rooms || [],
                bookings: initial.bookings || [],
                canManage: window.DeLongCalendarCanManage === true,
                saving: false,
                editor: { open: false, mode: 'create' },
                selectedBooking: null,
                selectedDayKey: initial.today,
                form: this.emptyForm ? this.emptyForm() : {},
                toast: { show: false, message: '', type: 'success', timer: null }
            };
        },
        computed: {
            days() {
                return Array.from({ length: 7 }, (_, index) => {
                    const key = addDays(this.startDate, index);
                    const value = parseDateKey(key);
                    return {
                        key,
                        label: new Intl.DateTimeFormat('vi-VN', { day: '2-digit', month: '2-digit', timeZone: 'UTC' }).format(value),
                        weekday: new Intl.DateTimeFormat('vi-VN', { weekday: 'short', timeZone: 'UTC' }).format(value)
                    };
                });
            },
            rangeLabel() {
                const first = this.days[0];
                const last = this.days[this.days.length - 1];
                return `${first.label} – ${last.label}`;
            },
            activeRooms() {
                return this.rooms.filter(x => x.isActive).sort((a, b) => a.sortOrder - b.sortOrder || a.name.localeCompare(b.name));
            },
            selectedRoom() {
                return this.rooms.find(x => x.id === this.form.roomId) || null;
            },
            selectedRoomRates() {
                return (this.selectedRoom?.rates || []).filter(x => x.isActive).sort((a, b) => a.sortOrder - b.sortOrder);
            },
            totalAmount() {
                return Math.max(0, Number(this.form.roomAmount || 0) + Number(this.form.extraAmount || 0) - Number(this.form.discountAmount || 0));
            }
        },
        methods: {
            emptyForm() {
                return {
                    roomId: '', rateId: '', customerName: '', customerPhone: '',
                    checkInLocal: '', checkOutLocal: '', status: 1,
                    roomAmount: 0, extraAmount: 0, discountAmount: 0,
                    source: '', note: ''
                };
            },
            money(value) {
                return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(value || 0);
            },
            formatInProperty(utcValue, options) {
                return new Intl.DateTimeFormat('vi-VN', Object.assign({ timeZone }, options)).format(new Date(utcValue));
            },
            timeText(utcValue) {
                return this.formatInProperty(utcValue, { hour: '2-digit', minute: '2-digit', hour12: false });
            },
            dateTimeText(utcValue) {
                return this.formatInProperty(utcValue, { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit', hour12: false });
            },
            timeRange(booking) {
                return `${this.timeText(booking.checkInUtc)} → ${this.timeText(booking.checkOutUtc)}`;
            },
            statusText(status) {
                return ({ 0: 'Yêu cầu', 1: 'Giữ phòng', 2: 'Đã xác nhận', 3: 'Đang ở', 4: 'Hoàn tất', 5: 'Đã hủy', 6: 'No-show' })[status] || `#${status}`;
            },
            bookingClass(status) {
                if (status === 1) return 'booking-held';
                if (status === 2 || status === 3) return 'booking-confirmed';
                if (status === 5 || status === 6) return 'booking-cancelled';
                return 'booking-requested';
            },
            bookingsFor(roomId, dayKey) {
                const start = new Date(`${dayKey}T00:00:00${utcOffset}`);
                const end = new Date(`${addDays(dayKey, 1)}T00:00:00${utcOffset}`);
                return this.bookings
                    .filter(x => x.roomId === roomId && x.status !== 5 && x.status !== 6)
                    .filter(x => new Date(x.checkInUtc) < end && start < new Date(x.checkOutUtc))
                    .sort((a, b) => new Date(a.checkInUtc) - new Date(b.checkInUtc));
            },
            moveRange(amount) {
                const target = addDays(this.startDate, amount);
                window.location.assign(`/Admin/Calendar?from=${target}`);
            },
            goToday() {
                window.location.assign(`/Admin/Calendar?from=${this.today}`);
            },
            openCreate(room, day) {
                if (!this.canManage) return;
                const selectedRoom = room || this.activeRooms[0] || null;
                this.selectedDayKey = day?.key || this.today;
                this.form = this.emptyForm();
                if (selectedRoom) this.form.roomId = selectedRoom.id;
                this.editor = { open: true, mode: 'create' };
                this.selectedBooking = null;

                const firstRate = (selectedRoom?.rates || []).filter(x => x.isActive).sort((a, b) => a.sortOrder - b.sortOrder)[0];
                if (firstRate) {
                    this.form.rateId = firstRate.id;
                    this.$nextTick(() => this.applyRate());
                } else {
                    this.form.checkInLocal = toInputValue(this.selectedDayKey, '14:00');
                    this.form.checkOutLocal = toInputValue(this.selectedDayKey, '17:00');
                }
            },
            roomChanged() {
                this.form.rateId = '';
                const rate = this.selectedRoomRates[0];
                if (rate) {
                    this.form.rateId = rate.id;
                    this.applyRate();
                }
            },
            applyRate() {
                const rate = this.selectedRoomRates.find(x => x.id === this.form.rateId);
                if (!rate) return;
                this.form.checkInLocal = toInputValue(this.selectedDayKey, rate.startTime);
                const checkoutDay = rate.isOvernight ? addDays(this.selectedDayKey, 1) : this.selectedDayKey;
                this.form.checkOutLocal = toInputValue(checkoutDay, rate.endTime);
                this.form.roomAmount = Number(rate.price || 0);
            },
            openBooking(booking) {
                this.selectedBooking = booking;
                this.editor = { open: true, mode: 'view' };
            },
            closeEditor() {
                if (!this.saving) this.editor.open = false;
            },
            validateCreate() {
                if (!this.form.roomId) return 'Vui lòng chọn phòng.';
                if (!this.form.customerName.trim()) return 'Vui lòng nhập tên khách.';
                if (!this.form.customerPhone.trim()) return 'Vui lòng nhập số điện thoại.';
                if (!this.form.checkInLocal || !this.form.checkOutLocal) return 'Vui lòng nhập giờ nhận/trả phòng.';
                if (new Date(`${this.form.checkOutLocal}${utcOffset}`) <= new Date(`${this.form.checkInLocal}${utcOffset}`)) return 'Giờ trả phòng phải sau giờ nhận phòng.';
                return null;
            },
            async saveBooking() {
                const validation = this.validateCreate();
                if (validation) return this.notify(validation, 'error');
                this.saving = true;
                try {
                    const payload = {
                        roomId: this.form.roomId,
                        customerId: null,
                        customerName: this.form.customerName,
                        customerPhone: this.form.customerPhone,
                        checkIn: `${this.form.checkInLocal}${utcOffset}`,
                        checkOut: `${this.form.checkOutLocal}${utcOffset}`,
                        status: Number(this.form.status),
                        roomAmount: Number(this.form.roomAmount || 0),
                        extraAmount: Number(this.form.extraAmount || 0),
                        discountAmount: Number(this.form.discountAmount || 0),
                        source: this.form.source || null,
                        note: this.form.note || null
                    };
                    const booking = await DeLongApi.post(`/api/admin/properties/${this.propertyId}/bookings`, payload);
                    this.bookings.push(booking);
                    this.editor.open = false;
                    this.notify(`Đã tạo ${booking.code}.`, 'success');
                } catch (error) {
                    this.notify(error.message || 'Không thể tạo booking.', 'error');
                } finally {
                    this.saving = false;
                }
            },
            nextActions(booking) {
                if (!booking) return [];
                const actions = [];
                if (booking.status === 0) {
                    actions.push({ status: 1, label: 'Giữ phòng', className: 'btn-light' });
                    actions.push({ status: 2, label: 'Xác nhận', className: 'btn-primary' });
                    actions.push({ status: 5, label: 'Hủy', className: 'btn-danger' });
                } else if (booking.status === 1) {
                    actions.push({ status: 2, label: 'Xác nhận', className: 'btn-primary' });
                    actions.push({ status: 5, label: 'Hủy', className: 'btn-danger' });
                } else if (booking.status === 2) {
                    actions.push({ status: 3, label: 'Check-in', className: 'btn-primary' });
                    actions.push({ status: 6, label: 'No-show', className: 'btn-light' });
                    actions.push({ status: 5, label: 'Hủy', className: 'btn-danger' });
                } else if (booking.status === 3) {
                    actions.push({ status: 4, label: 'Check-out / Hoàn tất', className: 'btn-primary' });
                }
                return actions;
            },
            async changeStatus(status) {
                if (!this.selectedBooking) return;
                this.saving = true;
                try {
                    const updated = await DeLongApi.post(`/api/admin/properties/${this.propertyId}/bookings/${this.selectedBooking.id}/status`, { status });
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
                const timer = setTimeout(() => { this.toast.show = false; }, 3500);
                this.toast = { show: true, message, type, timer };
            }
        },
        created() {
            this.form = this.emptyForm();
        }
    }).mount(root);
})();
