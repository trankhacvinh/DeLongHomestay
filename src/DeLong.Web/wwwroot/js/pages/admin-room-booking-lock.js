window.DeLongRoomBookingLock = {
    data() { return { canLockRooms: document.querySelector('#calendar-page, #rooms-page')?.dataset.canLockRooms === 'true', roomLock: { open: false, room: null, reason: '', saving: false, error: '' }, roomLockRefreshTimer: null }; },
    methods: {
        roomLockDetails(room) {
            if (!room?.isBookingLocked) return '';
            const when = room.bookingLockedAtUtc ? new Date(room.bookingLockedAtUtc + (room.bookingLockedAtUtc.endsWith('Z') ? '' : 'Z')).toLocaleString('vi-VN', { timeZone: this.timeZoneId || 'Asia/Ho_Chi_Minh' }) : '';
            return [room.bookingLockReason, room.bookingLockedByName, when].filter(Boolean).join(' · ');
        },
        async openRoomBookingLock(room) {
            if (!this.canLockRooms || !room) return;
            if (this.roomLock.saving) return;
            this.roomLock = { open: true, room, blockId: null, range: null, reason: '', saving: false, error: '' };
        },
        async openRoomSlotUnlock(room, range) {
            if (!this.canLockRooms || !room || !range?.bookingId || this.roomLock.saving) return;
            this.roomLock={open:true,room,blockId:range.bookingId,range,reason:range.blockReason||'',saving:false,loading:true,unlockReady:false,error:''};const state=this.roomLock;
            try{
                const blocks=await DeLongApi.get(`/api/admin/properties/${this.propertyId}/room-blocks/`);
                const block=blocks.find(x=>x.id===range.bookingId&&x.roomId===room.id&&!x.cancelledAtUtc);
                if(!block)throw new Error('Khoảng khóa đã thay đổi. Hãy tải lại lịch.');
                state.range={startUtc:block.startUtc,endUtc:block.endUtc};state.reason=block.reason;state.unlockReady=true;
            }catch(error){state.error=error.message||'Không thể đọc khoảng khóa.';}finally{state.loading=false;}
        },
        roomLockRangeText(range) {
            if(!range)return '';
            const format=value=>new Intl.DateTimeFormat('vi-VN',{timeZone:this.timeZoneId||this.timeZone||'Asia/Ho_Chi_Minh',dateStyle:'short',timeStyle:'short'}).format(new Date(value));
            return `${format(range.startUtc)} → ${format(range.endUtc)}`;
        },
        async saveRoomBookingLock() {
            const state = this.roomLock;
            if (state.saving || !state.room) return;
            if(state.blockId&&!state.unlockReady){state.error='Chưa đọc được khoảng khóa. Hãy mở lại.';return;}
            const isLocked = !state.room.isBookingLocked;
            const reason = state.reason.trim();
            if (!state.blockId && isLocked && (!reason || reason.length > 500)) { state.error = 'Vui lòng nhập lý do từ 1 đến 500 ký tự.'; return; }
            state.saving = true;
            state.error = '';
            try {
                if(state.blockId){
                    await DeLongApi.post(`/api/admin/properties/${this.propertyId}/room-blocks/intervals/${state.blockId}/end`,{});
                }else{
                    const updated = await DeLongApi.put(`/api/admin/properties/${this.propertyId}/rooms/${state.room.id}/booking-lock`, { isLocked, reason });
                    const index = this.rooms.findIndex(x => x.id === updated.id);
                    if (index >= 0) this.rooms.splice(index, 1, updated);
                }
                state.open = false;
                document.dispatchEvent(new CustomEvent('delong:room-lock-updated', { detail: { propertyId: this.propertyId, roomId: state.room.id } }));
                this.notify(state.blockId?'Đã mở khóa khoảng đang chọn.':isLocked ? 'Đã khóa nhận đặt phòng. Các đơn cũ được giữ nguyên.' : 'Đã mở khóa nhận đặt phòng.', 'success');
            } catch (error) {
                state.error = error.message || 'Không thể cập nhật khóa phòng.';
                if (!state.open) this.notify(state.error, 'error');
            }
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
