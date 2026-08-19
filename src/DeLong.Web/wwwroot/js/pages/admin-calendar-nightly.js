(function () {
    if (!window.Vue?.createApp) return;

    const pageData = (() => {
        try { return JSON.parse(document.getElementById('calendar-page-data')?.textContent || '{}'); }
        catch { return {}; }
    })();
    const utcOffset = pageData.utcOffset || '+07:00';
    const originalCreateApp = window.Vue.createApp.bind(window.Vue);

    function parseDateKey(value) {
        if (!value) return null;
        const key = value.slice(0, 10);
        const [year, month, day] = key.split('-').map(Number);
        if (!year || !month || !day) return null;
        return new Date(Date.UTC(year, month - 1, day));
    }

    function dateKey(value) {
        return `${value.getUTCFullYear()}-${String(value.getUTCMonth() + 1).padStart(2, '0')}-${String(value.getUTCDate()).padStart(2, '0')}`;
    }

    function addDays(value, amount) {
        const date = parseDateKey(value);
        if (!date) return value?.slice(0, 10) || '';
        date.setUTCDate(date.getUTCDate() + amount);
        return dateKey(date);
    }

    function dayDistance(fromValue, toValue) {
        const from = parseDateKey(fromValue);
        const to = parseDateKey(toValue);
        if (!from || !to) return 0;
        return Math.round((to - from) / 86400000);
    }

    function withTime(value, time) {
        if (!value) return value;
        return `${value.slice(0, 10)}T${time}`;
    }

    function patchCalendarComponent(options) {
        if (!options?.methods?.saveBooking || !document.getElementById('calendar-page')) return options;
        if (options.__nightlyPatched) return options;
        options.__nightlyPatched = true;

        const originalData = options.data;
        options.data = function () {
            const data = originalData.call(this);
            return { ...data, nightlySyncing: false };
        };

        options.computed = options.computed || {};
        options.computed.selectedRoomRates = function () {
            return (this.selectedRoom?.rates || [])
                .filter(x => x.isActive)
                .sort((a, b) => a.sortOrder - b.sortOrder || Number(a.type) - Number(b.type));
        };

        const methods = options.methods;
        const originalOpenBooking = methods.openBooking;
        const originalOpenEditBooking = methods.openEditBooking;

        methods.nightlyRateForRoom = function () {
            return this.selectedRoomRates.find(x => Number(x.type) === 2) || null;
        };

        methods.bookingSpansCalendarDays = function (booking) {
            if (!booking?.checkInUtc || !booking?.checkOutUtc) return false;
            return this.localDateKey(booking.checkInUtc) !== this.localDateKey(booking.checkOutUtc);
        };

        methods.calendarNightCount = function (booking) {
            const distance = dayDistance(this.localDateKey(booking.checkInUtc), this.localDateKey(booking.checkOutUtc));
            return Math.max(1, Number(booking.nightCount || 0), distance);
        };

        methods.bookingsToRender = function (roomId, dayKeyValue) {
            return this.activeBookingRows(roomId)
                .filter(booking => {
                    const checkInKey = this.localDateKey(booking.checkInUtc);
                    const spansDays = this.bookingSpansCalendarDays(booking);
                    if (!spansDays || Number(booking.status) === 0) return checkInKey === dayKeyValue;
                    const visibleStart = checkInKey < this.startDate ? this.startDate : checkInKey;
                    const lastVisible = this.days[this.days.length - 1].key;
                    const checkOutKey = this.localDateKey(booking.checkOutUtc);
                    return dayKeyValue === visibleStart && visibleStart <= lastVisible && checkOutKey >= this.startDate;
                })
                .sort((a, b) => new Date(a.checkInUtc) - new Date(b.checkInUtc))
                .map(booking => {
                    if (!this.bookingSpansCalendarDays(booking)) return booking;
                    return { ...booking, __actualType: booking.type, type: 1, nightCount: this.calendarNightCount(booking) };
                });
        };

        methods.bookingSpan = function (booking, dayKeyValue) {
            if (!this.bookingSpansCalendarDays(booking) || Number(booking.status) === 0) return 1;
            const lastVisible = this.days[this.days.length - 1].key;
            const checkoutKey = this.localDateKey(booking.checkOutUtc);
            const endKey = checkoutKey > lastVisible ? lastVisible : checkoutKey;
            return Math.max(1, Math.min(7, dayDistance(dayKeyValue, endKey) + 1));
        };

        methods.openBooking = function (booking) {
            const original = this.bookings.find(x => x.id === booking?.id) || booking;
            return originalOpenBooking.call(this, original);
        };

        methods.canEditBooking = function (booking) {
            const original = this.bookings.find(x => x.id === booking?.id) || booking;
            return this.canManage && original && ![4, 5, 6].includes(Number(original.status));
        };

        methods.openEditBooking = function (booking) {
            const original = this.bookings.find(x => x.id === booking?.id) || booking;
            originalOpenEditBooking.call(this, original);
            if (this.editor.mode === 'edit') this.form.rateId = original.roomRateId || '';
        };

        methods.roomChanged = function () {
            this.form.rateId = '';
            if (this.editor.mode !== 'create') return;
            const rate = this.selectedRoomRates.find(x => Number(x.type) !== 2) || this.selectedRoomRates[0];
            if (rate) {
                this.form.rateId = rate.id;
                this.applyRate();
            }
        };

        methods.applyRate = function () {
            const rate = this.selectedRoomRates.find(x => x.id === this.form.rateId);
            if (!rate) return;
            const type = Number(rate.type);
            this.nightlySyncing = true;
            try {
                this.form.checkInLocal = `${this.selectedDayKey}T${rate.startTime}`;
                if (type === 2) {
                    const existingDistance = dayDistance(this.form.checkInLocal, this.form.checkOutLocal);
                    const nights = Math.max(1, existingDistance || 1);
                    this.form.checkOutLocal = `${addDays(this.selectedDayKey, nights)}T${rate.endTime}`;
                    this.form.roomAmount = Number(rate.price || 0) * nights;
                } else {
                    const checkoutDay = type === 1 || rate.isOvernight ? addDays(this.selectedDayKey, 1) : this.selectedDayKey;
                    this.form.checkOutLocal = `${checkoutDay}T${rate.endTime}`;
                    this.form.roomAmount = Number(rate.price || 0);
                }
            } finally {
                this.nightlySyncing = false;
            }
        };

        methods.syncNightlyFromDates = function () {
            if (this.nightlySyncing || !this.form?.checkInLocal || !this.form?.checkOutLocal) return;
            const nights = dayDistance(this.form.checkInLocal, this.form.checkOutLocal);
            if (nights <= 0) return;
            const currentRate = this.selectedRoomRates.find(x => x.id === this.form.rateId) || null;
            const nightly = this.nightlyRateForRoom();

            if (Number(currentRate?.type) === 1) return;
            if (Number(currentRate?.type) !== 2 && nights < 2) return;
            if (!nightly) return;

            this.nightlySyncing = true;
            try {
                this.form.rateId = nightly.id;
                this.form.checkInLocal = withTime(this.form.checkInLocal, nightly.startTime);
                this.form.checkOutLocal = withTime(this.form.checkOutLocal, nightly.endTime);
                this.form.roomAmount = Number(nightly.price || 0) * nights;
            } finally {
                this.nightlySyncing = false;
            }
        };

        methods.saveBooking = async function () {
            this.syncNightlyFromDates();
            const validation = this.validateForm();
            if (validation) return this.notify(validation, 'error');

            const selectedRate = this.selectedRoomRates.find(x => x.id === this.form.rateId) || null;
            const isNightly = Number(selectedRate?.type) === 2;
            const nights = isNightly ? dayDistance(this.form.checkInLocal, this.form.checkOutLocal) : null;
            if (isNightly && (!nights || nights < 1)) return this.notify('Lưu trú theo đêm phải có ngày trả sau ngày nhận.', 'error');

            this.saving = true;
            try {
                const roomAmount = isNightly ? Number(selectedRate.price || 0) * nights : Number(this.form.roomAmount || 0);
                const common = {
                    roomId: this.form.roomId,
                    customerName: this.form.customerName,
                    customerPhone: this.form.customerPhone,
                    type: isNightly ? 1 : 0,
                    roomRateId: selectedRate?.id || null,
                    rateName: selectedRate?.name || null,
                    unitPrice: selectedRate ? Number(selectedRate.price || 0) : null,
                    nightCount: isNightly ? nights : null,
                    checkIn: `${this.form.checkInLocal}${utcOffset}`,
                    checkOut: `${this.form.checkOutLocal}${utcOffset}`,
                    roomAmount,
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
                this.editor.open = false;
            } catch (error) {
                this.notify(error.message || 'Không thể lưu lượt đặt.', 'error');
            } finally {
                this.saving = false;
            }
        };

        const originalMounted = options.mounted;
        options.mounted = async function () {
            if (typeof originalMounted === 'function') await originalMounted.call(this);
            try {
                const rooms = await DeLongApi.get(`/api/admin/properties/${this.propertyId}/rooms`);
                if (Array.isArray(rooms)) this.rooms = rooms;
            } catch {
                // Keep the page snapshot if refresh fails; normal time-slot booking still works.
            }
        };

        options.watch = options.watch || {};
        options.watch['form.checkInLocal'] = function () { this.$nextTick(() => this.syncNightlyFromDates()); };
        options.watch['form.checkOutLocal'] = function () { this.$nextTick(() => this.syncNightlyFromDates()); };

        return options;
    }

    window.Vue.createApp = function (options, ...args) {
        return originalCreateApp(patchCalendarComponent(options), ...args);
    };
})();
