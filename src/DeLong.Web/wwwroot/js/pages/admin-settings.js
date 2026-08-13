(function () {
    const root = document.getElementById('settings-page');
    if (!root) return;

    const initial = JSON.parse(document.getElementById('settings-page-data').textContent || '{}');
    const { createApp } = Vue;

    createApp({
        data() {
            return {
                propertyId: initial.propertyId,
                rooms: initial.rooms || [],
                saving: false,
                editor: { open: false, mode: 'create', rateId: null },
                archiveEditor: { open: false, room: null, rate: null },
                form: { roomId: '', type: 0, name: '', startTime: '14:00', endTime: '17:00', price: 0, sortOrder: 0, isActive: true },
                toast: { show: false, message: '', type: 'success', timer: null }
            };
        },
        computed: {
            activeRooms() {
                return this.rooms.filter(x => x.isActive).sort((a, b) => a.sortOrder - b.sortOrder || a.name.localeCompare(b.name));
            }
        },
        methods: {
            money(value) {
                return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(value || 0);
            },
            sortedRates(room) {
                return [...(room.rates || [])].sort((a, b) => a.sortOrder - b.sortOrder || a.name.localeCompare(b.name));
            },
            openCreate(room) {
                this.form = {
                    roomId: room?.id || this.activeRooms[0]?.id || '',
                    type: 0,
                    name: '',
                    startTime: '14:00',
                    endTime: '17:00',
                    price: 0,
                    sortOrder: 0,
                    isActive: true
                };
                this.editor = { open: true, mode: 'create', rateId: null };
            },
            openEdit(room, rate) {
                this.form = {
                    roomId: room.id,
                    type: Number(rate.type ?? (rate.isOvernight ? 1 : 0)),
                    name: rate.name,
                    startTime: rate.startTime,
                    endTime: rate.endTime,
                    price: Number(rate.price || 0),
                    sortOrder: Number(rate.sortOrder || 0),
                    isActive: rate.isActive
                };
                this.editor = { open: true, mode: 'edit', rateId: rate.id };
            },
            onTypeChanged() {
                if (this.form.type === 2 && !this.form.name.trim()) this.form.name = 'Lưu trú theo đêm';
                if (this.form.type === 1 && !this.form.name.trim()) this.form.name = 'Qua đêm';
            },
            closeEditor() { if (!this.saving) this.editor.open = false; },
            validate() {
                if (!this.form.roomId) return 'Vui lòng chọn phòng.';
                if (![0, 1, 2].includes(Number(this.form.type))) return 'Vui lòng chọn loại giá.';
                if (!this.form.name.trim()) return 'Vui lòng nhập tên mức giá.';
                if (!this.form.startTime || !this.form.endTime) return 'Vui lòng nhập giờ nhận/bắt đầu và giờ trả/kết thúc.';
                if (Number(this.form.price || 0) < 0) return 'Giá không được âm.';
                if (Number(this.form.type) === 2 && Number(this.form.price || 0) <= 0) return 'Giá lưu trú theo đêm phải lớn hơn 0.';
                return null;
            },
            async saveRate() {
                const validation = this.validate();
                if (validation) return this.notify(validation, 'error');
                this.saving = true;
                try {
                    const base = `/api/admin/properties/${this.propertyId}/rooms/${this.form.roomId}/rates`;
                    const payload = {
                        name: this.form.name,
                        startTime: this.form.startTime,
                        endTime: this.form.endTime,
                        type: Number(this.form.type),
                        price: Number(this.form.price || 0),
                        sortOrder: Number(this.form.sortOrder || 0)
                    };
                    let rate;
                    if (this.editor.mode === 'create') {
                        rate = await DeLongApi.post(base, payload);
                        const room = this.rooms.find(x => x.id === this.form.roomId);
                        if (room) room.rates.push(rate);
                    } else {
                        rate = await DeLongApi.put(`${base}/${this.editor.rateId}`, { ...payload, isActive: this.form.isActive });
                        const room = this.rooms.find(x => x.id === this.form.roomId);
                        const index = room?.rates.findIndex(x => x.id === rate.id) ?? -1;
                        if (room && index >= 0) room.rates.splice(index, 1, rate);
                    }
                    this.editor.open = false;
                    this.notify('Đã lưu mức giá.', 'success');
                } catch (error) {
                    this.notify(error.message || 'Không thể lưu mức giá.', 'error');
                } finally {
                    this.saving = false;
                }
            },
            openArchive(room, rate) {
                this.archiveEditor = { open: true, room, rate };
            },
            closeArchive() { if (!this.saving) this.archiveEditor.open = false; },
            async confirmArchive() {
                const room = this.archiveEditor.room;
                const rate = this.archiveEditor.rate;
                if (!room || !rate) return;
                this.saving = true;
                try {
                    await DeLongApi.delete(`/api/admin/properties/${this.propertyId}/rooms/${room.id}/rates/${rate.id}`);
                    const index = room.rates.findIndex(x => x.id === rate.id);
                    if (index >= 0) room.rates[index] = { ...rate, isActive: false };
                    this.archiveEditor.open = false;
                    this.notify('Đã ngừng mức giá.', 'success');
                } catch (error) {
                    this.notify(error.message || 'Không thể ngừng mức giá.', 'error');
                } finally {
                    this.saving = false;
                }
            },
            notify(message, type) {
                if (this.toast.timer) clearTimeout(this.toast.timer);
                const timer = setTimeout(() => { this.toast.show = false; }, 3000);
                this.toast = { show: true, message, type, timer };
            }
        }
    }).mount(root);
})();
