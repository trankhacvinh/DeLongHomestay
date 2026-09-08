(function () {
    const root = document.getElementById('pricing-page');
    if (!root) return;
    const initial = JSON.parse(document.getElementById('pricing-page-data').textContent || '{}');
    const clone = value => JSON.parse(JSON.stringify(value));
    let specialDatePickers = [];

    Vue.createApp({
        data() { return { propertyId: initial.propertyId, canManage: initial.canManage === true, rooms: clone(initial.rooms || []), tab: 'rates', saving: false, settings: { threeSlotDiscountEnabled: true, threeSlotCount: 3, threeSlotDiscountPercent: 10, weekendDayMask: 65 }, specialDays: [], weekDays: [{value:1,label:'Thứ 2'},{value:2,label:'Thứ 3'},{value:3,label:'Thứ 4'},{value:4,label:'Thứ 5'},{value:5,label:'Thứ 6'},{value:6,label:'Thứ 7'},{value:0,label:'Chủ nhật'}], editor: {open:false,id:null,error:'',form:{}}, toast:{show:false,type:'success',message:'',timer:null} }; },
        mounted() { this.load(); },
        beforeUnmount() { specialDatePickers.forEach(picker => picker.destroy()); specialDatePickers = []; },
        methods: {
            api(path='') { return `/api/admin/properties/${this.propertyId}/pricing${path}`; },
            async load() { try { const data = await DeLongApi.get(this.api('/')); this.settings = data.settings; this.specialDays = data.specialDays || []; } catch(e) { this.notify(e.message || 'Không tải được cấu hình.', 'error'); } },
            isWeekend(day) { return (Number(this.settings.weekendDayMask) & (1 << day)) !== 0; },
            toggleWeekend(day) { this.settings.weekendDayMask = Number(this.settings.weekendDayMask) ^ (1 << day); },
            async saveSettings() { await this.run(async()=>{ this.settings = await DeLongApi.put(this.api('/settings'), this.settings); this.notify('Đã lưu quy tắc combo.'); }); },
            async saveRates() { await this.run(async()=>{ await DeLongApi.put(this.api('/room-rates'), { rooms:this.rooms.map(room=>({roomId:room.id,fullDayPricingEnabled:room.fullDayPricingEnabled,fullDayPrice:room.fullDayPrice,useWeekdayFullDayPriceOnWeekend:room.useWeekdayFullDayPriceOnWeekend,weekendFullDayPrice:room.weekendFullDayPrice,rates:room.rates.map(rate=>({rateId:rate.id,weekdayPrice:rate.price,useWeekdayPriceOnWeekend:rate.useWeekdayPriceOnWeekend,weekendPrice:rate.weekendPrice}))})) }); this.notify('Đã lưu toàn bộ bảng giá trong một giao dịch.'); }); },
            openDay(day) {
                const today = new Date().toISOString().slice(0,10);
                this.editor = {open:true,id:day?.id || null,error:'',form: day ? clone(day) : {startDate:today,endDate:today,name:'',category:0,basePriceProfile:0,surchargePercent:10,bookingMode:0,allowThreeSlotCombo:true,note:'',isActive:true}};
                this.$nextTick(() => this.mountSpecialDatePickers());
            },
            mountSpecialDatePickers() {
                specialDatePickers.forEach(picker => picker.destroy());
                specialDatePickers = [];
                if (typeof flatpickr !== 'function') return;
                const locale = flatpickr.l10ns?.vn || flatpickr.l10ns?.default;
                const baseOptions = {
                    locale,
                    dateFormat: 'Y-m-d',
                    altInput: true,
                    altFormat: 'd/m/Y',
                    allowInput: true,
                    disableMobile: true
                };
                if (this.$refs.startDatePicker) specialDatePickers.push(flatpickr(this.$refs.startDatePicker, {
                    ...baseOptions,
                    defaultDate: this.editor.form.startDate,
                    onChange: (_dates, value) => { this.editor.form.startDate = value; }
                }));
                if (this.$refs.endDatePicker) specialDatePickers.push(flatpickr(this.$refs.endDatePicker, {
                    ...baseOptions,
                    defaultDate: this.editor.form.endDate,
                    onChange: (_dates, value) => { this.editor.form.endDate = value; }
                }));
            },
            async saveDay() { await this.run(async()=>{ this.editor.error=''; try { if(this.editor.id) await DeLongApi.put(this.api(`/special-days/${this.editor.id}`),this.editor.form); else await DeLongApi.post(this.api('/special-days'),this.editor.form); this.editor.open=false; await this.load(); this.notify('Đã lưu ngày đặc biệt.'); } catch(e){ this.editor.error=e.message || 'Không thể lưu.'; throw e; } }, false); },
            async archiveDay() { if(!confirm('Xóa ngày đặc biệt này? Lịch sử booking cũ vẫn được giữ.')) return; await this.run(async()=>{ await DeLongApi.delete(this.api(`/special-days/${this.editor.id}`)); this.editor.open=false; await this.load(); this.notify('Đã lưu trữ ngày đặc biệt.'); }); },
            async run(action, notifyError=true) { if(this.saving) return; this.saving=true; try { await action(); } catch(e) { if(notifyError) this.notify(e.message || 'Không thể lưu dữ liệu.','error'); } finally { this.saving=false; } },
            formatDate(v){ if(!v)return '—'; const [y,m,d]=String(v).slice(0,10).split('-'); return `${d}/${m}/${y}`; },
            categoryText(v){ return ['Ngày đặc biệt','Ngày lễ','Ngày lễ trọng đại'][Number(v)] || v; },
            profileText(v){ return ['Tự động theo thứ','Giá ngày thường','Giá cuối tuần'][Number(v)] || v; },
            notify(message,type='success'){ clearTimeout(this.toast.timer); this.toast={show:true,type,message,timer:setTimeout(()=>this.toast.show=false,3200)}; }
        }
    }).mount(root);
})();
