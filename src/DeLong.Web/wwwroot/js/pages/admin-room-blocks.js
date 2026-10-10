(function () {
    const root = document.getElementById('room-blocks-page');
    const dataNode = document.getElementById('room-blocks-data');
    if (!root || !dataNode || !window.Vue) return;
    const initial = JSON.parse(dataNode.textContent);
    const zone = initial.timeZoneId;
    function local(value) {
        const parts = new Intl.DateTimeFormat('en-CA', { timeZone: zone, year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }).formatToParts(new Date(value));
        const part = key => parts.find(x => x.type === key)?.value;
        return `${part('year')}-${part('month')}-${part('day')}T${part('hour')}:${part('minute')}`;
    }
    function utc(value) {
        if (!/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/.test(value)) throw new Error('Vui lòng nhập thời gian hợp lệ.');
        const nominal = Date.parse(`${value}:00Z`);
        let result = nominal;
        for (let i = 0; i < 3; i++) result += nominal - Date.parse(`${local(result)}:00Z`);
        if (local(result) !== value) throw new Error('Thời gian không hợp lệ trong múi giờ của chi nhánh.');
        return new Date(result).toISOString();
    }
    Vue.createApp({
        data: () => ({ ...initial, filter: 'all', now: Date.now(), editor: false, saving: false, error: '', formError: '', conflicts: [], acknowledged: false, form: {}, timer: null,
            filters: [{key:'all',label:'Tất cả'},{key:'upcoming',label:'Sắp diễn ra'},{key:'active',label:'Đang khóa'},{key:'ended',label:'Đã kết thúc'}] }),
        computed: {
            activeRooms() { return this.rooms.filter(x => x.isActive); },
            presets() { return this.rooms.find(x => x.id === this.form.roomIds?.[0])?.rates.filter(x => x.type !== 2) || []; },
            groups() {
                const grouped = new Map();
                for (const block of this.blocks) {
                    if (!grouped.has(block.batchId)) grouped.set(block.batchId, []);
                    grouped.get(block.batchId).push(block);
                }
                const result = [...grouped].map(([batchId, all]) => {
                    const valid = all.filter(x => !x.cancelledAtUtc);
                    const latestCreated = all.map(x=>x.createdAtUtc).sort().at(-1);
                    const rows = valid.length ? valid : all.filter(x=>x.createdAtUtc===latestCreated);
                    const first = rows.reduce((a,b) => a.startUtc < b.startUtc ? a : b);
                    const last = rows.reduce((a,b) => a.endUtc > b.endUtc ? a : b);
                    const state = valid.some(x => Date.parse(x.startUtc) <= this.now && this.now < Date.parse(x.endUtc)) ? 'active' : valid.some(x => Date.parse(x.endUtc) > this.now) ? 'upcoming' : 'ended';
                    const windows = [...new Map(rows.map(x => [local(x.startUtc).slice(11) + local(x.endUtc).slice(11), {start:local(x.startUtc).slice(11),end:local(x.endUtc).slice(11)}])).values()];
                    return {batchId,rows,state,roomIds:[...new Set(rows.map(x=>x.roomId))],roomNames:[...new Set(rows.map(x=>x.roomName))].join(', '),reason:first.reason,actor:first.createdByName || 'Quản lý',createdAtUtc:first.createdAtUtc,repeatDaily:first.repeatDaily,windows,
                        start:local(first.startUtc),end:local(last.endUtc),fromDate:local(first.startUtc).slice(0,10),toDate:rows.map(x=>local(x.startUtc).slice(0,10)).sort().at(-1),
                        summary:first.repeatDaily ? `${local(first.startUtc).slice(0,10)} → ${rows.map(x=>local(x.startUtc).slice(0,10)).sort().at(-1)} · mỗi ngày ${windows.map(x=>`${x.start}–${x.end}${x.end<=x.start?' (+1 ngày)':''}`).join(', ')}` : `${this.dateText(first.startUtc)} → ${this.dateText(last.endUtc)}`};
                });
                for (const room of this.rooms.filter(x=>x.isBookingLocked)) result.unshift({batchId:room.id,roomIds:[room.id],roomNames:room.name,reason:room.bookingLockReason,actor:room.bookingLockedByName || 'Quản lý',createdAtUtc:room.bookingLockedAtUtc,state:'active',unlimited:true,summary:'Không thời hạn · mở khóa thủ công'});
                return result;
            },
            visible() { return this.groups.filter(x => this.filter === 'all' || x.state === this.filter); }
        },
        watch: { form: { deep: true, handler() { this.resetConflicts(); } } },
        mounted() {
            const query = new URLSearchParams(location.search);
            const roomId = query.get('roomId');
            const block = this.blocks.find(x=>x.id===query.get('blockId'));
            const group = block ? this.groups.find(x=>x.batchId===block.batchId && x.state !== 'ended') : null;
            if (group) this.open(group);
            else if (this.activeRooms.some(x=>x.id===roomId)) this.open(null, roomId);
            this.timer = setInterval(()=>{this.now=Date.now();},30000);
        },
        beforeUnmount() { clearInterval(this.timer); },
        methods: {
            dateText(value) { return value ? new Intl.DateTimeFormat('vi-VN',{timeZone:zone,dateStyle:'short',timeStyle:'short'}).format(new Date(value)) : ''; },
            statusLabel(item) { return ({active:'Đang khóa',upcoming:'Sắp diễn ra',ended:'Đã kết thúc'})[item.state]; },
            resetConflicts() { this.conflicts=[];this.acknowledged=false; },
            open(item = null, roomId = '') {
                this.form = item ? {batchId:item.batchId,roomIds:[...item.roomIds],reason:item.reason,mode:item.repeatDaily?'daily':'continuous',start:item.start,end:item.end,fromDate:item.fromDate,toDate:item.toDate,windows:item.windows.map(x=>({...x}))}
                    : {batchId:null,roomIds:roomId?[roomId]:[],reason:'',mode:'continuous',start:local(Date.now()),end:local(Date.now()+3600000),fromDate:local(Date.now()).slice(0,10),toDate:local(Date.now()).slice(0,10),windows:[{start:'10:30',end:'13:30'}]};
                this.formError='';this.resetConflicts();this.editor=true;
            },
            close() { if (!this.saving) this.editor=false; },
            addWindow(rate) { this.form.windows.push({start:rate?.startTime || '00:00',end:rate?.endTime || '00:00'}); },
            async reload() {
                const [blocks,rooms] = await Promise.all([DeLongApi.get(`/api/admin/properties/${this.propertyId}/room-blocks/`),DeLongApi.get(`/api/admin/properties/${this.propertyId}/rooms/`)]);
                this.blocks=blocks;this.rooms=rooms;this.now=Date.now();
            },
            async save() {
                if (!this.form.roomIds.length || !this.form.reason.trim()) {this.formError='Chọn phòng và nhập lý do.';return;}
                this.saving=true;this.formError='';
                try {
                    if (this.form.mode === 'unlimited') {
                        for (const roomId of this.form.roomIds) await DeLongApi.put(`/api/admin/properties/${this.propertyId}/rooms/${roomId}/booking-lock`,{isLocked:true,reason:this.form.reason});
                    } else {
                        const request = {roomIds:this.form.roomIds,reason:this.form.reason,start:utc(this.form.start),end:utc(this.form.end),repeatDaily:this.form.mode==='daily',fromDate:this.form.fromDate,toDate:this.form.toDate,windows:this.form.windows.map(x=>({start:`${x.start.slice(0,5)}:00`,end:`${x.end.slice(0,5)}:00`})),acknowledgeExistingBookings:this.acknowledged,acknowledgedBookingIds:this.acknowledged?this.conflicts.map(x=>x.id):[]};
                        const url=`/api/admin/properties/${this.propertyId}/room-blocks/${this.form.batchId || ''}`;
                        if (this.form.batchId) await DeLongApi.put(url,request); else await DeLongApi.post(url,request);
                    }
                    this.editor=false;
                    await this.reload();
                } catch(error) { this.formError=error.message || 'Không thể lưu lịch khóa.';this.conflicts=error.problem?.conflicts || []; }
                finally {this.saving=false;}
            },
            async end(item) {
                this.saving=true;this.error='';
                try {
                    if(item.unlimited) await DeLongApi.put(`/api/admin/properties/${this.propertyId}/rooms/${item.roomIds[0]}/booking-lock`,{isLocked:false,reason:null});
                    else await DeLongApi.post(`/api/admin/properties/${this.propertyId}/room-blocks/${item.batchId}/end`,{});
                    await this.reload();
                } catch(error) { this.error=error.message || 'Không thể kết thúc khóa.'; }
                finally {this.saving=false;}
            }
        }
    }).mount(root);
})();
