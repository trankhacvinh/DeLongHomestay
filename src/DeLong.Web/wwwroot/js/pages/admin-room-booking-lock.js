window.DeLongRoomBookingLock = {
    data() { return { canLockRooms: document.querySelector('#calendar-page, #rooms-page')?.dataset.canLockRooms === 'true', roomLock: { open: false, room: null, reason: '', saving: false, error: '' }, roomLockRefreshTimer: null }; },
    methods: {
        roomLockDetails(room) {
            if (!room?.isBookingLocked) return '';
            const when = room.bookingLockedAtUtc ? new Date(room.bookingLockedAtUtc + (room.bookingLockedAtUtc.endsWith('Z') ? '' : 'Z')).toLocaleString('vi-VN', { timeZone: this.timeZoneId || 'Asia/Ho_Chi_Minh' }) : '';
            return [room.bookingLockReason, room.bookingLockedByName, when].filter(Boolean).join(' · ');
        },
        openRoomBookingLock(room) {
            if (!this.canLockRooms || !room) return;
            this.roomLock = { open: true, room, reason: '', saving: false, error: '' };
        },
        async saveRoomBookingLock() {
            const state = this.roomLock;
            if (state.saving || !state.room) return;
            const isLocked = !state.room.isBookingLocked;
            const reason = state.reason.trim();
            if (isLocked && (!reason || reason.length > 500)) { state.error = 'Vui lòng nhập lý do từ 1 đến 500 ký tự.'; return; }
            state.saving = true;
            state.error = '';
            try {
                const updated = await DeLongApi.put(`/api/admin/properties/${this.propertyId}/rooms/${state.room.id}/booking-lock`, { isLocked, reason });
                const index = this.rooms.findIndex(x => x.id === updated.id);
                if (index >= 0) this.rooms.splice(index, 1, updated);
                state.open = false;
                document.dispatchEvent(new CustomEvent('delong:room-lock-updated', { detail: { propertyId: this.propertyId, roomId: updated.id } }));
                this.notify(isLocked ? 'Đã khóa nhận đặt phòng. Các đơn cũ được giữ nguyên.' : 'Đã mở khóa nhận đặt phòng.', 'success');
            } catch (error) { state.error = error.message || 'Không thể cập nhật khóa phòng.'; }
            finally { state.saving = false; }
        },
        async refreshRoomBookingLocks() {
            try {
                const rooms = await DeLongApi.get(`/api/admin/properties/${this.propertyId}/rooms/`);
                for (const room of rooms) {
                    const existing = this.rooms.find(x => x.id === room.id);
                    if (existing) for (const key of ['isBookingLocked', 'bookingLockReason', 'bookingLockedAtUtc', 'bookingLockedByUserId', 'bookingLockedByName']) existing[key] = room[key];
                }
            } catch { /* The next poll retries; booking APIs still enforce the lock. */ }
        }
    },
    mounted() {
        this.roomLockChangeHandler = event => {
            if (!event.detail?.propertyId || event.detail.propertyId === this.propertyId) this.refreshRoomBookingLocks();
        };
        document.addEventListener('delong:operations-change', this.roomLockChangeHandler);
        this.roomLockRefreshTimer = setInterval(() => { if (!document.hidden) this.refreshRoomBookingLocks(); }, 15000);
    },
    beforeUnmount() {
        document.removeEventListener('delong:operations-change', this.roomLockChangeHandler);
        clearInterval(this.roomLockRefreshTimer);
    }
};
