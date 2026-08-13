(function () {
    const root = document.getElementById('public-booking-page');
    if (!root) return;

    const initial = JSON.parse(document.getElementById('public-booking-data')?.textContent || '{}');
    const { createApp } = Vue;

    createApp({
        data() {
            return {
                date: initial.date,
                rooms: initial.rooms || [],
                selectedRoomId: initial.initialRoomId || null,
                selectedRateId: initial.initialRateId || null,
                loading: false,
                submitting: false,
                errorMessage: '',
                form: {
                    customerName: '',
                    customerPhone: '',
                    note: '',
                    website: ''
                }
            };
        },
        computed: {
            selectedRoom() {
                return this.rooms.find(x => x.id === this.selectedRoomId) || null;
            },
            selectedRate() {
                return this.selectedRoom?.rates?.find(x => x.id === this.selectedRateId) || null;
            },
            canSubmit() {
                return !!this.selectedRoom && !!this.selectedRate && this.selectedRate.available &&
                    this.form.customerName.trim().length >= 2 && this.form.customerPhone.trim().length >= 8;
            },
            dateText() {
                if (!this.date) return '';
                const [year, month, day] = this.date.split('-').map(Number);
                return new Intl.DateTimeFormat('vi-VN', { weekday: 'long', day: '2-digit', month: '2-digit', year: 'numeric' })
                    .format(new Date(year, month - 1, day));
            }
        },
        async mounted() {
            await this.loadAvailability();
            if (!this.selectedRoomId && this.rooms.length) {
                const first = this.rooms.find(x => this.availableCount(x) > 0) || this.rooms[0];
                this.chooseRoom(first);
            } else if (this.selectedRoom && !this.selectedRateId) {
                this.selectedRateId = this.selectedRoom.rates.find(x => x.available)?.id || null;
            }
        },
        methods: {
            money(value) {
                return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(value || 0);
            },
            availableCount(room) {
                return (room?.rates || []).filter(x => x.available).length;
            },
            chooseRoom(room) {
                this.selectedRoomId = room.id;
                const currentStillValid = room.rates.some(x => x.id === this.selectedRateId && x.available);
                if (!currentStillValid) this.selectedRateId = room.rates.find(x => x.available)?.id || null;
                this.errorMessage = '';
            },
            async loadAvailability() {
                if (!this.date) return;
                this.loading = true;
                this.errorMessage = '';
                try {
                    const data = await DeLongApi.get(`/api/public/availability?date=${encodeURIComponent(this.date)}`);
                    this.rooms = data.rooms || [];
                    if (this.selectedRoomId) {
                        const room = this.rooms.find(x => x.id === this.selectedRoomId);
                        if (!room) {
                            this.selectedRoomId = null;
                            this.selectedRateId = null;
                        } else if (!room.rates.some(x => x.id === this.selectedRateId && x.available)) {
                            this.selectedRateId = room.rates.find(x => x.available)?.id || null;
                        }
                    }
                } catch (error) {
                    this.errorMessage = error.message || 'Không thể kiểm tra phòng trống.';
                } finally {
                    this.loading = false;
                }
            },
            async submitRequest() {
                if (!this.canSubmit || this.submitting) return;
                this.submitting = true;
                this.errorMessage = '';
                try {
                    const result = await DeLongApi.post('/api/public/booking-requests', {
                        roomId: this.selectedRoomId,
                        rateId: this.selectedRateId,
                        stayDate: this.date,
                        customerName: this.form.customerName,
                        customerPhone: this.form.customerPhone,
                        note: this.form.note,
                        website: this.form.website
                    });
                    const query = new URLSearchParams({ code: result.code, room: result.roomName, amount: String(result.totalAmount) });
                    window.location.assign(`/booking/success?${query.toString()}`);
                } catch (error) {
                    this.errorMessage = error.message || 'Không thể gửi yêu cầu lúc này.';
                    if (error.status === 409) await this.loadAvailability();
                } finally {
                    this.submitting = false;
                }
            }
        }
    }).mount(root);
})();
