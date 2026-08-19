(function () {
    const root = document.getElementById('housekeeping-page');
    if (!root) return;

    const initial = JSON.parse(document.getElementById('housekeeping-page-data').textContent || '{}');
    const { createApp } = Vue;
    const timeZone = initial.timeZoneId || 'Asia/Ho_Chi_Minh';

    const vm = createApp({
        data() {
            return {
                propertyId: initial.propertyId,
                rooms: initial.rooms || [],
                canManage: window.DeLongHousekeepingCanManage === true,
                savingId: null,
                toast: { show: false, message: '', type: 'success', timer: null },
                refreshInFlight: false,
                refreshQueued: false
            };
        },
        computed: {
            cleanRooms() { return this.rooms.filter(x => x.status === 0); },
            dirtyRooms() { return this.rooms.filter(x => x.status === 1); },
            cleaningRooms() { return this.rooms.filter(x => x.status === 2); }
        },
        methods: {
            updatedText(room) {
                if (!room.updatedAtUtc) return 'Chưa cập nhật';
                return new Intl.DateTimeFormat('vi-VN', {
                    timeZone,
                    day: '2-digit', month: '2-digit',
                    hour: '2-digit', minute: '2-digit', hour12: false
                }).format(new Date(room.updatedAtUtc));
            },
            statusText(status) {
                return ({ 0: 'Sạch', 1: 'Bẩn', 2: 'Đang dọn' })[status] || 'Không xác định';
            },
            async refreshRooms() {
                if (this.refreshInFlight) {
                    this.refreshQueued = true;
                    return;
                }
                this.refreshInFlight = true;
                try {
                    const rooms = await DeLongApi.get(`/api/admin/properties/${this.propertyId}/housekeeping`);
                    this.rooms = Array.isArray(rooms) ? rooms : [];
                } catch {
                    // SSE reconnect/focus/fallback poll will retry. Keep the last known board.
                } finally {
                    this.refreshInFlight = false;
                    if (this.refreshQueued) {
                        this.refreshQueued = false;
                        setTimeout(() => this.refreshRooms(), 0);
                    }
                }
            },
            async setStatus(room, status) {
                if (!this.canManage) return;
                this.savingId = room.roomId;
                try {
                    const updated = await DeLongApi.post(
                        `/api/admin/properties/${this.propertyId}/housekeeping/rooms/${room.roomId}/status`,
                        { status });
                    const index = this.rooms.findIndex(x => x.roomId === updated.roomId);
                    if (index >= 0) this.rooms.splice(index, 1, updated);
                    this.notify(`${updated.roomName}: ${this.statusText(updated.status)}.`, 'success');
                } catch (error) {
                    this.notify(error.message || 'Không thể cập nhật trạng thái phòng.', 'error');
                } finally {
                    this.savingId = null;
                }
            },
            notify(message, type) {
                if (this.toast.timer) clearTimeout(this.toast.timer);
                const timer = setTimeout(() => { this.toast.show = false; }, 3000);
                this.toast = { show: true, message, type, timer };
            }
        }
    }).mount(root);

    document.addEventListener('delong:operations-change', event => {
        const detail = event.detail || {};
        if (detail.propertyId && detail.propertyId !== vm.propertyId) return;
        const type = String(detail.type || '');
        if (type === 'stream.reconnected' || type === 'housekeeping.changed' || type.startsWith('booking.')) {
            vm.refreshRooms();
        }
    });
    window.addEventListener('focus', () => vm.refreshRooms());
    document.addEventListener('visibilitychange', () => {
        if (!document.hidden) vm.refreshRooms();
    });

    const fallbackPoll = setInterval(() => {
        if (!document.hidden) vm.refreshRooms();
    }, 15000);
    window.addEventListener('beforeunload', () => clearInterval(fallbackPoll), { once: true });
})();
