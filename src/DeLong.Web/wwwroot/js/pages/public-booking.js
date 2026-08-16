(function () {
    const root = document.getElementById('public-booking-page');
    if (!root) return;

    const initial = JSON.parse(document.getElementById('public-booking-data')?.textContent || '{}');
    const { createApp } = Vue;

    function parseIsoDate(value) {
        if (!value) return null;
        const [year, month, day] = value.split('-').map(Number);
        if (!year || !month || !day) return null;
        return new Date(year, month - 1, day);
    }

    function isoDate(value) {
        return `${value.getFullYear()}-${String(value.getMonth() + 1).padStart(2, '0')}-${String(value.getDate()).padStart(2, '0')}`;
    }

    function addDays(value, amount) {
        const date = parseIsoDate(value);
        if (!date) return value;
        date.setDate(date.getDate() + amount);
        return isoDate(date);
    }

    createApp({
        data() {
            const today = initial.today || initial.date;
            const arrival = initial.date || today;
            return {
                propertyName: initial.propertyName || 'Cơ sở',
                siteSlug: initial.siteSlug || '',
                scopePrefix: initial.scopePrefix || '',
                bookingType: 0,
                today,
                date: arrival,
                checkInDate: arrival,
                checkOutDate: addDays(arrival, 1),
                rooms: initial.rooms || [],
                stayRooms: [],
                roomMedia: {},
                selectedRoomId: initial.initialRoomId || null,
                selectedRateId: initial.initialRateId || null,
                loading: false,
                submitting: false,
                availabilityError: '',
                errorMessage: '',
                fieldErrors: { customerName: '', customerPhone: '' },
                deepLinkedRoom: !!initial.initialRoomId,
                deepLinkedRate: !!initial.initialRateId,
                form: {
                    customerName: '',
                    customerPhone: '',
                    note: '',
                    website: ''
                }
            };
        },
        computed: {
            roomList() {
                return this.bookingType === 0 ? this.rooms : this.stayRooms;
            },
            selectedRoom() {
                return this.roomList.find(x => x.id === this.selectedRoomId) || null;
            },
            selectedRate() {
                if (!this.selectedRoom) return null;
                if (this.bookingType === 1) return this.selectedRoom.nightlyRate || null;
                return this.timeSlotRates(this.selectedRoom).find(x => x.id === this.selectedRateId) || null;
            },
            stayNights() {
                if (this.selectedRoom?.nights) return Number(this.selectedRoom.nights);
                const arrival = parseIsoDate(this.checkInDate);
                const departure = parseIsoDate(this.checkOutDate);
                if (!arrival || !departure) return 0;
                return Math.max(0, Math.round((departure - arrival) / 86400000));
            },
            minimumCheckOut() {
                return addDays(this.checkInDate || this.today, 1);
            },
            selectableRoomCount() {
                return this.roomList.filter(room => this.roomSelectable(room)).length;
            },
            bookingStep() {
                if (this.loading) return 1;
                if (this.selectedRate) return 4;
                if (this.selectedRoom) return 3;
                return 2;
            },
            canSubmit() {
                const roomReady = !!this.selectedRoom && (this.bookingType === 0 ? !!this.selectedRate?.available : this.selectedRoom.available === true);
                const phoneDigits = this.form.customerPhone.replace(/\D/g, '').length;
                return roomReady && !!this.selectedRate && this.form.customerName.trim().length >= 2 && phoneDigits >= 8;
            },
            bookingTotalText() {
                if (!this.selectedRoom || !this.selectedRate) return '';
                return this.money(this.bookingType === 0 ? this.selectedRate.price : this.selectedRoom.totalAmount);
            },
            dateText() {
                return this.stayDateText(this.date);
            }
        },
        async mounted() {
            await Promise.all([this.loadRoomMedia(), this.loadAvailability()]);
            if (!this.selectedRoomId && this.rooms.length) {
                const first = this.rooms.find(x => this.availableCount(x) > 0) || this.rooms[0];
                if (first) this.chooseRoom(first);
            } else if (this.selectedRoom && !this.selectedRateId) {
                this.selectedRateId = this.timeSlotRates(this.selectedRoom).find(x => x.available)?.id || null;
            }
            await this.$nextTick();
            if (window.matchMedia('(max-width: 820px)').matches && this.deepLinkedRoom) {
                const target = document.getElementById(this.deepLinkedRate && this.selectedRate ? 'booking-contact-step' : 'booking-rate-step');
                target?.scrollIntoView({ behavior: 'smooth', block: 'start' });
            }
        },
        methods: {
            apiUrl(path, params = {}) {
                const query = new URLSearchParams(params);
                if (this.siteSlug) query.set('siteSlug', this.siteSlug);
                const suffix = query.toString();
                return suffix ? `${path}?${suffix}` : path;
            },
            money(value) {
                return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(value || 0);
            },
            stayDateText(value) {
                const date = parseIsoDate(value);
                if (!date) return '';
                return new Intl.DateTimeFormat('vi-VN', { weekday: 'short', day: '2-digit', month: '2-digit', year: 'numeric' }).format(date);
            },
            roomImage(room) {
                return room?.id ? this.roomMedia[room.id] || null : null;
            },
            async loadRoomMedia() {
                try {
                    const items = await DeLongApi.get(this.apiUrl('/api/public/room-media'));
                    this.roomMedia = Object.fromEntries((items || []).map(item => [item.roomId, item]));
                } catch {
                    this.roomMedia = {};
                }
            },
            timeSlotRates(room) {
                return (room?.rates || []).filter(x => Number(x.type) !== 2);
            },
            availableCount(room) {
                return this.timeSlotRates(room).filter(x => x.available).length;
            },
            roomSelectable(room) {
                return this.bookingType === 0 ? this.availableCount(room) > 0 : room?.available === true;
            },
            async switchMode(type) {
                if (this.bookingType === type) return;
                this.bookingType = type;
                this.selectedRoomId = null;
                this.selectedRateId = null;
                this.availabilityError = '';
                this.errorMessage = '';
                if (type === 1) {
                    if (!this.checkInDate) this.checkInDate = this.date || this.today;
                    if (!this.checkOutDate || this.checkOutDate <= this.checkInDate) this.checkOutDate = addDays(this.checkInDate, 1);
                    await this.loadStayAvailability();
                } else {
                    await this.loadAvailability();
                    const first = this.rooms.find(x => this.availableCount(x) > 0) || null;
                    if (first) this.chooseRoom(first);
                }
            },
            chooseRoom(room) {
                if (!this.roomSelectable(room)) return;
                this.selectedRoomId = room.id;
                if (this.bookingType === 1) {
                    this.selectedRateId = room.nightlyRate?.id || null;
                } else {
                    const rates = this.timeSlotRates(room);
                    const currentStillValid = rates.some(x => x.id === this.selectedRateId && x.available);
                    if (!currentStillValid) this.selectedRateId = rates.find(x => x.available)?.id || null;
                }
                this.errorMessage = '';
            },
            async loadAvailability() {
                if (!this.date || this.bookingType !== 0) return;
                this.loading = true;
                this.availabilityError = '';
                this.errorMessage = '';
                try {
                    const data = await DeLongApi.get(this.apiUrl('/api/public/availability', { date: this.date }));
                    this.rooms = data.rooms || [];
                    if (this.selectedRoomId) {
                        const room = this.rooms.find(x => x.id === this.selectedRoomId);
                        if (!room || this.availableCount(room) === 0) {
                            this.selectedRoomId = null;
                            this.selectedRateId = null;
                        } else if (!this.timeSlotRates(room).some(x => x.id === this.selectedRateId && x.available)) {
                            this.selectedRateId = this.timeSlotRates(room).find(x => x.available)?.id || null;
                        }
                    }
                } catch (error) {
                    this.rooms = [];
                    this.selectedRoomId = null;
                    this.selectedRateId = null;
                    this.availabilityError = error.message || 'Không thể kiểm tra phòng trống.';
                } finally {
                    this.loading = false;
                }
            },
            async stayDatesChanged() {
                if (!this.checkInDate) return;
                if (!this.checkOutDate || this.checkOutDate <= this.checkInDate) this.checkOutDate = addDays(this.checkInDate, 1);
                await this.loadStayAvailability();
            },
            async loadStayAvailability() {
                if (this.bookingType !== 1 || !this.checkInDate || !this.checkOutDate) return;
                if (this.checkOutDate <= this.checkInDate) {
                    this.errorMessage = 'Ngày trả phòng phải sau ngày nhận phòng.';
                    return;
                }
                this.loading = true;
                this.availabilityError = '';
                this.errorMessage = '';
                try {
                    const data = await DeLongApi.get(this.apiUrl('/api/public/stay-availability', { checkIn: this.checkInDate, checkOut: this.checkOutDate }));
                    this.stayRooms = data.rooms || [];
                    if (this.selectedRoomId) {
                        const room = this.stayRooms.find(x => x.id === this.selectedRoomId && x.available);
                        if (!room) {
                            this.selectedRoomId = null;
                            this.selectedRateId = null;
                        } else {
                            this.selectedRateId = room.nightlyRate?.id || null;
                        }
                    }
                    if (!this.selectedRoomId) {
                        const first = this.stayRooms.find(x => x.available);
                        if (first) this.chooseRoom(first);
                    }
                } catch (error) {
                    this.stayRooms = [];
                    this.selectedRoomId = null;
                    this.selectedRateId = null;
                    this.availabilityError = error.message || 'Không thể kiểm tra phòng trống cho khoảng lưu trú.';
                } finally {
                    this.loading = false;
                }
            },
            reloadAvailability() {
                return this.bookingType === 1 ? this.loadStayAvailability() : this.loadAvailability();
            },
            focusDateInput() {
                const selector = this.bookingType === 1 ? 'input[type="date"][v-model="checkInDate"]' : 'input[type="date"][v-model="date"]';
                const input = root.querySelector(selector) || root.querySelector('input[type="date"]');
                input?.scrollIntoView({ behavior: 'smooth', block: 'center' });
                input?.focus();
            },
            clearFieldError(field) {
                if (this.fieldErrors[field]) this.fieldErrors[field] = '';
                if (this.errorMessage) this.errorMessage = '';
            },
            validateContact() {
                this.fieldErrors.customerName = this.form.customerName.trim().length >= 2 ? '' : 'Vui lòng nhập tên khách (ít nhất 2 ký tự).';
                this.fieldErrors.customerPhone = this.form.customerPhone.replace(/\D/g, '').length >= 8 ? '' : 'Vui lòng nhập số điện thoại hợp lệ.';
                const firstField = this.fieldErrors.customerName ? 'customerName' : (this.fieldErrors.customerPhone ? 'customerPhone' : null);
                if (firstField) {
                    this.$nextTick(() => {
                        const input = this.$refs[firstField];
                        input?.scrollIntoView({ behavior: 'smooth', block: 'center' });
                        input?.focus();
                    });
                    return false;
                }
                return true;
            },
            mobilePrimaryAction() {
                if (this.canSubmit) return this.submitRequest();
                document.getElementById('booking-contact-step')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
                this.validateContact();
            },
            newRequestKey() {
                if (globalThis.crypto?.randomUUID) return globalThis.crypto.randomUUID();
                return `web-${Date.now()}-${Math.random().toString(36).slice(2, 14)}`;
            },
            async submitRequest() {
                if (this.submitting || !this.selectedRoom || !this.selectedRate) return;
                if (!this.validateContact()) return;
                this.submitting = true;
                this.errorMessage = '';
                if (!this.requestKey) this.requestKey = this.newRequestKey();
                try {
                    const payload = {
                        type: this.bookingType,
                        roomId: this.selectedRoomId,
                        rateId: this.selectedRate.id,
                        stayDate: this.bookingType === 0 ? this.date : '',
                        checkInDate: this.bookingType === 1 ? this.checkInDate : '',
                        checkOutDate: this.bookingType === 1 ? this.checkOutDate : '',
                        customerName: this.form.customerName,
                        customerPhone: this.form.customerPhone,
                        note: this.form.note,
                        website: this.form.website
                    };
                    const result = await DeLongApi.post(this.apiUrl('/api/public/booking-requests'), payload, { 'Idempotency-Key': this.requestKey });
                    const query = new URLSearchParams({ code: result.code, room: result.roomName, amount: String(result.totalAmount) });
                    window.location.assign(`${this.scopePrefix}/booking/success?${query.toString()}`);
                } catch (error) {
                    this.errorMessage = error.message || 'Không thể gửi yêu cầu lúc này.';
                    // Keep the same key only for network-level uncertainty. Any HTTP response means
                    // the server answered definitively and a corrected retry should get a fresh key.
                    if (error.status) this.requestKey = null;
                    if (error.status === 409) {
                        if (this.bookingType === 1) await this.loadStayAvailability();
                        else await this.loadAvailability();
                    }
                } finally {
                    this.submitting = false;
                }
            }
        }
    }).mount(root);
})();