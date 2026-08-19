(function () {
    if (!window.Vue?.createApp) return;

    const originalCreateApp = window.Vue.createApp.bind(window.Vue);

    function parseDateKey(value) {
        if (!value) return null;
        const key = value.slice(0, 10);
        const [year, month, day] = key.split('-').map(Number);
        if (!year || !month || !day) return null;
        return new Date(Date.UTC(year, month - 1, day));
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

        methods.bookingsToRender = function (roomId, dayKey) {
            return this.activeBookingRows(roomId)
                .filter(booking => {
                    const checkInKey = this.localDateKey(booking.checkInUtc);
                    const spansDays = this.bookingSpansCalendarDays(booking);
                    if (!spansDays || Number(booking.status) === 0) return checkInKey === dayKey;
                    const visibleStart = checkInKey < this.startDate ? this.startDate : checkInKey;
                    const lastVisible = this.days[this.days.length - 1].key;
                    const checkOutKey = this.localDateKey(booking.checkOutUtc);
                    return dayKey === visibleStart && visibleStart <= lastVisible && checkOutKey >= this.startDate;
                })
                .sort((a, b) => new Date(a.checkInUtc) - new Date(b.checkInUtc))
                .map(booking => {
                    if (!this.bookingSpansCalendarDays(booking)) return booking;
                    return {
                        ...booking,
                        __actualType: booking.type,
                        type: 1,
                        nightCount: this.calendarNightCount(booking)
                    };
                });
        };

        methods.bookingSpan = function (booking, dayKey) {
            if (!this.bookingSpansCalendarDays(booking) || Number(booking.status) === 0) return 1;
            const lastVisible = this.days[this.days.length - 1].key;
            const checkoutKey = this.localDateKey(booking.checkOutUtc);
            const endKey = checkoutKey > lastVisible ? lastVisible : checkoutKey;
            return Math.max(1, Math.min(7, dayDistance(dayKey, endKey) + 1));
        };

        methods.openBooking = function (booking) {
            const original = this.bookings.find(x => x.id === booking?.id) || booking;
            return originalOpenBooking.call(this, original);
        };

        methods.openEditBooking = function (booking) {
            if (!this.canEditBooking(booking)) return;
            const original = this.bookings.find(x => x.id === booking?.id) || booking;
            const checkInLocal = this.localInputForUtc ? this.localInputForUtc(original.checkInUtc) : null;
            const actualCheckIn = checkInLocal || (() => {
                const parts = new Intl.DateTimeFormat('en-CA', {
                    timeZone: this.timeZoneId || undefined,
                    year: 'numeric', month: '2-digit', day: '2-digit',
                    hour: '2-digit', minute: '2-digit', hourCycle: 'h23'
                }).formatToParts(new Date(original.checkInUtc));
                const get = type => parts.find(part => part.type === type)?.value || '';
                return `${get('year')}-${get('month')}-${get('day')}T${get('hour')}:${get('minute')}`;
            })();
            const actualCheckOut = (() => {
                const value = new Date(original.checkOutUtc);
                const formatter = new Intl.DateTimeFormat('en-CA', {
                    timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone,
                    year: 'numeric', month: '2-digit', day: '2-digit',
                    hour: '2-digit', minute: '2-digit', hourCycle: 'h23'
                });
                const parts = formatter.formatToParts(value);
                const get = type => parts.find(part => part.type === type)?.value || '';
                return `${get('year')}-${get('month')}-${get('day')}T${get('hour')}:${get('minute')}`;
            })();

            this.selectedDayKey = actualCheckIn.slice(0, 10);
            this.selectedBooking = original;
            this.form = {
                roomId: original.roomId,
                rateId: original.roomRateId || '',
                customerId: original.customerId,
                customerName: original.customerName,
                customerPhone: original.customerPhone,
                checkInLocal: actualCheckIn,
                checkOutLocal: actualCheckOut,
                status: original.status,
                roomAmount: Number(original.roomAmount || 0),
                extraAmount: Number(original.extraAmount || 0),
                discountAmount: Number(original.discountAmount || 0),
                source: original.source || '',
                note: original.note || ''
            };
            this.editor = { open: true, mode: 'edit' };
        };

        methods.canEditBooking = function (booking) {
            const original = this.bookings.find(x => x.id === booking?.id) || booking;
            return this.canManage && original && ![4, 5, 6].includes(Number(original.status));
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
                    const checkoutDate = (() => {
                        const base = parseDateKey(this.selectedDayKey);
                        base.setUTCDate(base.getUTCDate() + nights);
                        return `${base.getUTCFullYear()}-${String(base.getUTCMonth() + 1).padStart(2, '0')}-${String(base.getUTCDate()).padStart(2, '0')}`;
                    })();
                    this.form.checkOutLocal = `${checkoutDate}T${rate.endTime}`;
                    this.form.roomAmount = Number(rate.price || 0) * nights;
                } else {
                    const checkoutDay = type === 1 || rate.isOvernight ? (() => {
                        const base = parseDateKey(this.selectedDayKey);
                        base.setUTCDate(base.getUTCDate() + 1);
                        return `${base.getUTCFullYear()}-${String(base.getUTCMonth() + 1).padStart(2, '0')}-${String(base.getUTCDate()).padStart(2, '0')}`;
                    })() : this.selectedDayKey;
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
                if (this.form.rateId !== nightly.id) this.form.rateId = nightly.id;
                this.form.checkInLocal = withTime(this.form.checkInLocal, nightly.startTime);
                this.form.checkOutLocal = withTime(this.form.checkOutLocal, nightly.endTime);
                this.form.roomAmount = Number(nightly.price || 0) * Math.max(1, nights);
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
            const nights = isNightly ? Math.max(1, dayDistance(this.form.checkInLocal, this.form.checkOutLocal)) : null;
            if (isNightly && nights < 1) return this.notify('Ngày trả phòng phải sau ngày nhận phòng.', 'error');

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
                    checkIn: `${this.form.checkInLocal}${this.$root?.utcOffset || ''}`,
                    checkOut: `${this.form.checkOutLocal}${this.$root?.utcOffset || ''}`,
                    roomAmount,
                    extraAmount: Number(this.form.extraAmount || 0),
                    discountAmount: Number(this.form.discountAmount || 0),
                    source: this.form.source || null,
                    note: this.form.note || null
                };

                // The calendar page currently uses +07:00. Keep the existing behavior if the
                // component does not expose an offset property.
                if (!common.checkIn.match(/[+-]\d\d:\d\d$/)) common.checkIn = `${this.form.checkInLocal}+07:00`;
                if (!common.checkOut.match(/[+-]\d\d:\d\d$/)) common.checkOut = `${this.form.checkOutLocal}+07:00`;

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
                // Keep the page snapshot if the refresh fails; time-slot booking still works.
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
