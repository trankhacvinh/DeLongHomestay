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

    function dayDistance(fromKey, toKey) {
        return Math.round((parseDateKey(toKey) - parseDateKey(fromKey)) / 86400000);
    }

    function toInputValue(dayKey, time) {
        return `${dayKey}T${time}`;
    }

    function utcToLocalInput(value) {
        const parts = new Intl.DateTimeFormat('en-CA', {
            timeZone,
            year: 'numeric', month: '2-digit', day: '2-digit',
            hour: '2-digit', minute: '2-digit', hourCycle: 'h23'
        }).formatToParts(new Date(value));
        const get = type => parts.find(part => part.type === type)?.value || '';
        return `${get('year')}-${get('month')}-${get('day')}T${get('hour')}:${get('minute')}`;
    }

    createApp({
        data() {
            const finePointer = window.matchMedia ? window.matchMedia('(pointer: fine)').matches : true;
            return {
                propertyId: initial.propertyId,
                startDate: initial.startDate,
                today: initial.today,
                rooms: initial.rooms || [],
                bookings: initial.bookings || [],
                canManage: window.DeLongCalendarCanManage === true,
                dragEnabled: window.DeLongCalendarCanManage === true && finePointer && window.innerWidth > 820,
                drag: { bookingId: null, overKey: '' },
                moveConfirm: { open: false, booking: null, room: null, day: null },
                saving: false,
                editor: { open: false, mode: 'create' },
                selectedBooking: null,
                guestDetails: { loading: false, error: '', customerEmail: '', guestCount: 1, documents: [] },
                identityFiles: { front: null, back: null },
                identityPreviews: { front: '', back: '' },
                selectedDayKey: initial.today,
                form: {},
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
                return (this.selectedRoom?.rates || []).filter(x => x.isActive && Number(x.type) !== 2).sort((a, b) => a.sortOrder - b.sortOrder);
            },
            totalAmount() {
                return Math.max(0, Number(this.form.roomAmount || 0) + Number(this.form.extraAmount || 0) - Number(this.form.discountAmount || 0));
            }
        },
        methods: {
            emptyForm() {
                return {
                    roomId: '', rateId: '', customerId: null, customerName: '', customerPhone: '',
                    checkInLocal: '', checkOutLocal: '', status: 1,
                    roomAmount: 0, extraAmount: 0, discountAmount: 0,
                    source: '', note: '', customerEmail: '', guestCount: 1
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
            shortDateText(utcValue) {
                return this.formatInProperty(utcValue, { day: '2-digit', month: '2-digit' });
            },
            dateKeyText(value) {
                if (!value) return '—';
                return new Intl.DateTimeFormat('vi-VN', {
                    day: '2-digit', month: '2-digit', year: 'numeric', weekday: 'short', timeZone: 'UTC'
                }).format(parseDateKey(value));
            },
            localDateKey(utcValue) {
                return utcToLocalInput(utcValue).slice(0, 10);
            },
            timeRange(booking) {
                return `${this.timeText(booking.checkInUtc)} → ${this.timeText(booking.checkOutUtc)}`;
            },
            multiDayLabel(booking) {
                const nights = Number(booking.nightCount || 0);
                return `${nights || '?'} đêm · ${this.shortDateText(booking.checkInUtc)} → ${this.shortDateText(booking.checkOutUtc)}`;
            },
            statusText(status) {
                return ({ 0: 'Yêu cầu', 1: 'Giữ phòng', 2: 'Đã xác nhận', 3: 'Đang ở', 4: 'Hoàn tất', 5: 'Đã hủy', 6: 'Không đến' })[status] || `#${status}`;
            },
            bookingClass(status) {
                if (status === 1) return 'booking-held';
                if (status === 2 || status === 3) return 'booking-confirmed';
                if (status === 5 || status === 6) return 'booking-cancelled';
                return 'booking-requested';
            },
            activeBookingRows(roomId) {
                return this.bookings.filter(x => x.roomId === roomId && [0, 1, 2, 3].includes(Number(x.status)));
            },
            lockingBookingRows(roomId) {
                return this.bookings.filter(x => x.roomId === roomId && [1, 2, 3].includes(Number(x.status)));
            },
            cellHasBooking(roomId, dayKey) {
                const start = new Date(`${dayKey}T00:00:00${utcOffset}`);
                const end = new Date(`${addDays(dayKey, 1)}T00:00:00${utcOffset}`);
                return this.lockingBookingRows(roomId).some(x => new Date(x.checkInUtc) < end && start < new Date(x.checkOutUtc));
            },
            bookingsToRender(roomId, dayKey) {
                return this.activeBookingRows(roomId)
                    .filter(booking => {
                        const checkInKey = this.localDateKey(booking.checkInUtc);
                        if (Number(booking.type) !== 1 || Number(booking.status) === 0) return checkInKey === dayKey;
                        const visibleStart = checkInKey < this.startDate ? this.startDate : checkInKey;
                        const lastVisible = this.days[this.days.length - 1].key;
                        const checkOutKey = this.localDateKey(booking.checkOutUtc);
                        return dayKey === visibleStart && visibleStart <= lastVisible && checkOutKey >= this.startDate;
                    })
                    .sort((a, b) => new Date(a.checkInUtc) - new Date(b.checkInUtc));
            },
            bookingSpan(booking, dayKey) {
                if (Number(booking.type) !== 1 || Number(booking.status) === 0) return 1;
                const lastVisible = this.days[this.days.length - 1].key;
                const checkoutKey = this.localDateKey(booking.checkOutUtc);
                const endKey = checkoutKey > lastVisible ? lastVisible : checkoutKey;
                return Math.max(1, Math.min(7, dayDistance(dayKey, endKey) + 1));
            },
            canDragBooking(booking) {
                return this.dragEnabled && booking && [0, 1, 2].includes(Number(booking.status));
            },
            dragKey(roomId, dayKey) {
                return `${roomId}:${dayKey}`;
            },
            startBookingDrag(booking, event) {
                if (!this.canDragBooking(booking)) {
                    event.preventDefault();
                    return;
                }
                this.drag.bookingId = booking.id;
                this.drag.overKey = '';
                if (event.dataTransfer) {
                    event.dataTransfer.effectAllowed = 'move';
                    event.dataTransfer.setData('text/plain', booking.id);
                }
                const element = event.currentTarget;
                requestAnimationFrame(() => element?.classList.add('is-dragging'));
            },
            endBookingDrag(event) {
                event.currentTarget?.classList.remove('is-dragging');
                this.drag.bookingId = null;
                this.drag.overKey = '';
            },
            dragOverCell(room, day, event) {
                if (!this.dragEnabled || !this.drag.bookingId) return;
                event.preventDefault();
                if (event.dataTransfer) event.dataTransfer.dropEffect = 'move';
                this.drag.overKey = this.dragKey(room.id, day.key);
            },
            dragLeaveCell(room, day, event) {
                if (event.currentTarget?.contains(event.relatedTarget)) return;
                const key = this.dragKey(room.id, day.key);
                if (this.drag.overKey === key) this.drag.overKey = '';
            },
            dropBooking(room, day, event) {
                if (!this.dragEnabled) return;
                event.preventDefault();
                const bookingId = this.drag.bookingId || event.dataTransfer?.getData('text/plain');
                const booking = this.bookings.find(x => x.id === bookingId);
                this.drag.overKey = '';
                if (!booking || !this.canDragBooking(booking)) return;

                const sameRoom = booking.roomId === room.id;
                const sameDate = this.localDateKey(booking.checkInUtc) === day.key;
                if (sameRoom && sameDate) {
                    this.notify('Lượt đặt đang ở đúng vị trí này.', 'success');
                    return;
                }

                this.moveConfirm = { open: true, booking, room, day };
            },
            closeMoveConfirm() {
                if (this.saving) return;
                this.moveConfirm = { open: false, booking: null, room: null, day: null };
            },
            async confirmBookingMove() {
                const booking = this.moveConfirm.booking;
                const room = this.moveConfirm.room;
                const day = this.moveConfirm.day;
                if (!booking || !room || !day) return;

                this.saving = true;
                try {
                    const updated = await DeLongApi.post(
                        `/api/admin/properties/${this.propertyId}/bookings/${booking.id}/move`,
                        { roomId: room.id, targetDate: day.key });
                    const index = this.bookings.findIndex(x => x.id === updated.id);
                    if (index >= 0) this.bookings.splice(index, 1, updated);
                    if (this.selectedBooking?.id === updated.id) this.selectedBooking = updated;
                    this.moveConfirm = { open: false, booking: null, room: null, day: null };
                    this.notify(`Đã chuyển ${updated.code} sang ${updated.roomName} · ${this.dateKeyText(this.localDateKey(updated.checkInUtc))}.`, 'success');
                } catch (error) {
                    const marker = `${error?.problem?.code || ''} ${error?.problem?.type || ''}`.toLowerCase();
                    if (error?.status === 409 || marker.includes('booking_conflict')) {
                        this.notify('Không thể chuyển: phòng đích đã có lượt đặt trùng thời gian.', 'error');
                    } else if (marker.includes('target_room_no_nightly_rate')) {
                        this.notify('Phòng đích chưa có giá “Lưu trú theo đêm”. Hãy cấu hình giá trước khi chuyển.', 'error');
                    } else {
                        this.notify(error?.message || 'Không thể chuyển lượt đặt.', 'error');
                    }
                } finally {
                    this.saving = false;
                }
            },
            moveRange(amount) {
                const target = addDays(this.startDate, amount);
                window.location.assign(`/Admin/Calendar?propertyId=${this.propertyId}&from=${target}`);
            },
            goToday() {
                window.location.assign(`/Admin/Calendar?propertyId=${this.propertyId}&from=${this.today}`);
            },
            openCreate(room, day) {
                if (!this.canManage) return;
                const selectedRoom = room || this.activeRooms[0] || null;
                this.selectedDayKey = day?.key || this.today;
                this.form = this.emptyForm();
                if (selectedRoom) this.form.roomId = selectedRoom.id;
                this.editor = { open: true, mode: 'create' };
                this.selectedBooking = null;
                this.resetGuestEditor();

                const firstRate = (selectedRoom?.rates || []).filter(x => x.isActive && Number(x.type) !== 2).sort((a, b) => a.sortOrder - b.sortOrder)[0];
                if (firstRate) {
                    this.form.rateId = firstRate.id;
                    this.$nextTick(() => this.applyRate());
                } else {
                    this.form.checkInLocal = toInputValue(this.selectedDayKey, '14:00');
                    this.form.checkOutLocal = toInputValue(this.selectedDayKey, '17:00');
                }
            },
            async openEditBooking(booking) {
                if (!this.canEditBooking(booking)) return;
                const checkInLocal = utcToLocalInput(booking.checkInUtc);
                this.selectedDayKey = checkInLocal.slice(0, 10);
                this.selectedBooking = booking;
                this.form = {
                    roomId: booking.roomId,
                    rateId: '',
                    customerId: booking.customerId,
                    customerName: booking.customerName,
                    customerPhone: booking.customerPhone,
                    checkInLocal,
                    checkOutLocal: utcToLocalInput(booking.checkOutUtc),
                    status: booking.status,
                    roomAmount: Number(booking.roomAmount || 0),
                    extraAmount: Number(booking.extraAmount || 0),
                    discountAmount: Number(booking.discountAmount || 0),
                    source: booking.source || '',
                    note: booking.note || '', customerEmail: '', guestCount: 1
                };
                this.editor = { open: true, mode: 'edit' };
                await this.loadGuestDetails(booking.id, true);
                this.form.customerEmail = this.guestDetails.customerEmail || '';
                this.form.guestCount = Number(this.guestDetails.guestCount || 1);
            },
            roomChanged() {
                this.form.rateId = '';
                if (this.editor.mode !== 'create') return;
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
                const checkoutDay = Number(rate.type) === 1 || rate.isOvernight ? addDays(this.selectedDayKey, 1) : this.selectedDayKey;
                this.form.checkOutLocal = toInputValue(checkoutDay, rate.endTime);
                this.form.roomAmount = Number(rate.price || 0);
            },
            async openBooking(booking) {
                this.selectedBooking = booking;
                this.editor = { open: true, mode: 'view' };
                await this.loadGuestDetails(booking.id);
            },
            closeEditor() {
                if (!this.saving) this.editor.open = false;
            },
            canEditBooking(booking) {
                return this.canManage && booking && Number(booking.type) !== 1 && ![4, 5, 6].includes(booking.status);
            },
            validateForm() {
                if (!this.form.roomId) return 'Vui lòng chọn phòng.';
                if (!this.form.customerName.trim()) return 'Vui lòng nhập tên khách.';
                if (!this.form.customerPhone.trim()) return 'Vui lòng nhập số điện thoại.';
                if (this.form.customerEmail && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(this.form.customerEmail)) return 'Email khách không hợp lệ.';
                if (!Number.isInteger(Number(this.form.guestCount)) || Number(this.form.guestCount) < 1 || Number(this.form.guestCount) > Number(this.selectedRoom?.capacity || 1)) return `Phòng này tối đa ${this.selectedRoom?.capacity || 1} khách.`;
                if (!this.form.checkInLocal || !this.form.checkOutLocal) return 'Vui lòng nhập giờ nhận/trả phòng.';
                if (new Date(`${this.form.checkOutLocal}${utcOffset}`) <= new Date(`${this.form.checkInLocal}${utcOffset}`)) return 'Giờ trả phòng phải sau giờ nhận phòng.';
                return null;
            },
            async saveBooking() {
                const validation = this.validateForm();
                if (validation) return this.notify(validation, 'error');
                this.saving = true;
                try {
                    const selectedRate = this.selectedRoomRates.find(x => x.id === this.form.rateId) || null;
                    const common = {
                        roomId: this.form.roomId,
                        customerName: this.form.customerName,
                        customerPhone: this.form.customerPhone,
                        type: 0,
                        roomRateId: selectedRate?.id || null,
                        rateName: selectedRate?.name || null,
                        unitPrice: selectedRate ? Number(selectedRate.price || 0) : null,
                        nightCount: null,
                        checkIn: `${this.form.checkInLocal}${utcOffset}`,
                        checkOut: `${this.form.checkOutLocal}${utcOffset}`,
                        roomAmount: Number(this.form.roomAmount || 0),
                        extraAmount: Number(this.form.extraAmount || 0),
                        discountAmount: Number(this.form.discountAmount || 0),
                        source: this.form.source || null,
                        note: this.form.note || null
                    };

                    let booking;
                    if (this.editor.mode === 'edit') {
                        booking = await DeLongApi.put(
                            `/api/admin/properties/${this.propertyId}/bookings/${this.selectedBooking.id}`,
                            { ...common, customerId: this.form.customerId });
                        const index = this.bookings.findIndex(x => x.id === booking.id);
                        if (index >= 0) this.bookings.splice(index, 1, booking);
                        this.selectedBooking = booking;
                        this.notify(`Đã cập nhật ${booking.code}.`, 'success');
                    } else {
                        booking = await DeLongApi.post(
                            `/api/admin/properties/${this.propertyId}/bookings`,
                            { ...common, customerId: null, status: Number(this.form.status) });
                        this.bookings.push(booking);
                        this.notify(`Đã tạo ${booking.code}.`, 'success');
                    }
                    await this.saveGuestDetails(booking);
                    this.editor.open = false;
                } catch (error) {
                    this.notify(error.message || 'Không thể lưu lượt đặt.', 'error');
                } finally {
                    this.saving = false;
                }
            },
            resetGuestEditor() {
                for (const side of ['front', 'back']) {
                    if (this.identityPreviews[side]) URL.revokeObjectURL(this.identityPreviews[side]);
                }
                this.identityFiles = { front: null, back: null };
                this.identityPreviews = { front: '', back: '' };
                this.guestDetails = { loading: false, error: '', customerEmail: '', guestCount: 1, documents: [] };
            },
            async loadGuestDetails(bookingId, editing = false) {
                this.resetGuestEditor();
                this.guestDetails.loading = true;
                try {
                    const details = await DeLongApi.get(`/api/admin/properties/${this.propertyId}/bookings/${bookingId}/guest-details`);
                    this.guestDetails = { ...details, loading: false, error: '', documents: Array.isArray(details.documents) ? details.documents : [] };
                } catch (error) {
                    this.guestDetails = { loading: false, error: error.message || 'Không thể tải dữ liệu khách.', customerEmail: '', guestCount: 1, documents: [] };
                    if (editing) this.notify(this.guestDetails.error, 'error');
                }
            },
            hasIdentity(side) {
                return this.guestDetails.documents.some(document => document.side === side);
            },
            identityUrl(bookingId, side) {
                return `/api/admin/properties/${this.propertyId}/bookings/${bookingId}/identity-documents/${side}`;
            },
            identityPreview(side) {
                if (this.identityPreviews[side]) return this.identityPreviews[side];
                if (this.selectedBooking?.id && this.hasIdentity(side)) return this.identityUrl(this.selectedBooking.id, side);
                return '';
            },
            selectIdentity(side, event) {
                const file = event.target.files?.[0] || null;
                if (file && file.size > 8 * 1024 * 1024) {
                    event.target.value = '';
                    return this.notify('Mỗi ảnh CCCD tối đa 8 MB.', 'error');
                }
                if (this.identityPreviews[side]) URL.revokeObjectURL(this.identityPreviews[side]);
                this.identityFiles[side] = file;
                this.identityPreviews[side] = file ? URL.createObjectURL(file) : '';
            },
            async saveGuestDetails(booking) {
                await DeLongApi.put(`/api/admin/properties/${this.propertyId}/bookings/${booking.id}/guest-details`, {
                    customerEmail: this.form.customerEmail || null,
                    guestCount: Number(this.form.guestCount || 1)
                });
                for (const side of ['front', 'back']) {
                    const file = this.identityFiles[side];
                    if (!file) continue;
                    const data = new FormData();
                    data.append('file', file, file.name);
                    await DeLongApi.postForm(this.identityUrl(booking.id, side), data);
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
                    { status: 3, label: 'Nhận phòng', className: 'btn-primary' },
                    { status: 6, label: 'Không đến', className: 'btn-light' },
                    { status: 5, label: 'Hủy', className: 'btn-danger' }
                ];
                if (booking.status === 3) return [
                    { status: 4, label: 'Trả phòng / Hoàn tất', className: 'btn-primary' }
                ];
                if (booking.status === 5) return [
                    { status: 0, label: 'Khôi phục yêu cầu', className: 'btn-light' }
                ];
                if (booking.status === 6) return [
                    { status: 2, label: 'Khôi phục đặt phòng', className: 'btn-light' }
                ];
                return [];
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
                    this.notify(error.message || 'Không thể đổi trạng thái lượt đặt.', 'error');
                } finally {
                    this.saving = false;
                }
            },
            notify(message, type) {
                if (this.toast.timer) clearTimeout(this.toast.timer);
                const timer = setTimeout(() => { this.toast.show = false; }, type === 'error' ? 5200 : 3500);
                this.toast = { show: true, message, type, timer };
            }
        },
        created() {
            this.form = this.emptyForm();
        }
    }).mount(root);
})();
