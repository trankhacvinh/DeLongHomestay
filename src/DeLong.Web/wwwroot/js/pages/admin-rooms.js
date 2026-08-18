(function () {
    const root = document.getElementById('rooms-page');
    if (!root) return;

    const initial = JSON.parse(document.getElementById('rooms-page-data').textContent || '{}');
    const { createApp } = Vue;

    createApp({
        data() {
            return {
                propertyId: initial.propertyId,
                propertyName: initial.propertyName || '',
                rooms: initial.rooms || [],
                search: '',
                showInactive: false,
                saving: false,
                statusSavingId: null,
                editor: { open: false, mode: 'create', roomId: null },
                form: { code: '', name: '', capacity: 2, sortOrder: 1, isActive: true, isPublished: false },
                toast: { show: false, message: '', type: 'success', timer: null }
            };
        },
        computed: {
            filteredRooms() {
                const q = this.search.toLowerCase();
                return this.rooms.filter(room => {
                    if (!this.showInactive && !room.isActive) return false;
                    return !q || room.name.toLowerCase().includes(q) || room.code.toLowerCase().includes(q);
                });
            }
        },
        methods: {
            money(value) {
                return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(value || 0);
            },
            openCreate() {
                this.form = { code: '', name: '', capacity: 2, sortOrder: this.rooms.length + 1, isActive: true, isPublished: false };
                this.editor = { open: true, mode: 'create', roomId: null };
            },
            openEdit(room) {
                this.form = {
                    code: room.code,
                    name: room.name,
                    capacity: room.capacity,
                    sortOrder: room.sortOrder,
                    isActive: room.isActive,
                    isPublished: room.isPublished === true
                };
                this.editor = { open: true, mode: 'edit', roomId: room.id };
            },
            closeEditor() { if (!this.saving) this.editor.open = false; },
            validate() {
                if (!this.form.code.trim()) return 'Vui lòng nhập mã phòng.';
                if (!this.form.name.trim()) return 'Vui lòng nhập tên phòng.';
                if (this.form.capacity < 1 || this.form.capacity > 50) return 'Sức chứa không hợp lệ.';
                return null;
            },
            async saveRoom() {
                const validation = this.validate();
                if (validation) return this.notify(validation, 'error');
                this.saving = true;
                try {
                    const base = `/api/admin/properties/${this.propertyId}/rooms`;
                    let room;
                    if (this.editor.mode === 'create') {
                        room = await DeLongApi.post(base, { code: this.form.code, name: this.form.name, capacity: this.form.capacity, sortOrder: this.form.sortOrder });
                        this.rooms.push(room);
                    } else {
                        room = await DeLongApi.put(`${base}/${this.editor.roomId}`, this.form);
                        const index = this.rooms.findIndex(x => x.id === room.id);
                        if (index >= 0) this.rooms.splice(index, 1, room);
                    }
                    this.rooms.sort((a, b) => a.sortOrder - b.sortOrder || a.name.localeCompare(b.name));
                    this.editor.open = false;
                    this.notify('Đã lưu phòng.', 'success');
                } catch (error) {
                    this.notify(error.message || 'Không thể lưu phòng.', 'error');
                } finally { this.saving = false; }
            },
            async updateRoomStatus(room, changes, successMessage) {
                if (!room || this.statusSavingId) return;
                this.statusSavingId = room.id;
                try {
                    const updated = await DeLongApi.put(`/api/admin/properties/${this.propertyId}/rooms/${room.id}`, {
                        code: room.code,
                        name: room.name,
                        capacity: room.capacity,
                        sortOrder: room.sortOrder,
                        isActive: changes.isActive ?? room.isActive,
                        isPublished: changes.isPublished ?? room.isPublished
                    });
                    const index = this.rooms.findIndex(x => x.id === updated.id);
                    if (index >= 0) this.rooms.splice(index, 1, updated);
                    this.notify(successMessage, 'success');
                } catch (error) {
                    this.notify(error.message || 'Không thể cập nhật trạng thái phòng.', 'error');
                } finally {
                    this.statusSavingId = null;
                }
            },
            toggleActive(room) {
                const next = !room.isActive;
                return this.updateRoomStatus(room, { isActive: next }, next ? 'Đã bật hoạt động phòng.' : 'Đã ngừng hoạt động phòng.');
            },
            togglePublished(room) {
                if (!room.isActive && !room.isPublished) {
                    this.notify('Hãy bật hoạt động phòng trước khi đưa lên website.', 'error');
                    return;
                }
                const next = !room.isPublished;
                return this.updateRoomStatus(room, { isPublished: next }, next ? 'Đã hiển thị phòng trên website và trang đặt phòng.' : 'Đã ẩn phòng khỏi website và trang đặt phòng.');
            },
            notify(message, type) {
                if (this.toast.timer) clearTimeout(this.toast.timer);
                this.toast = { show: true, message, type, timer: setTimeout(() => { this.toast.show = false; }, 3200) };
            }
        }
    }).mount(root);
})();
