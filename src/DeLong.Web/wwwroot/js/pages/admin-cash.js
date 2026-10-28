(function () {
    const root=document.getElementById('cash-page');if(!root)return;
    const initial=JSON.parse(document.getElementById('cash-data').textContent);const helpers=window.DeLongOperations;
    Vue.createApp({
        data:()=>({...initial,roomId:initial.roomId||'',requestVersion:0,loadedQuery:new URLSearchParams({propertyId:initial.propertyId,scope:initial.scope,month:initial.month,...(initial.roomId?{roomId:initial.roomId}:{})}).toString(),loading:false,saving:false,error:'',message:'',tab:'ledger',showVoided:false,editor:'',form:{},formError:'',detailGroup:null,detailEntry:null,voidReason:'',
            tabs:[{key:'ledger',name:'Giao dịch'},{key:'byProperty',name:'Theo chi nhánh'},{key:'byRoom',name:'Theo phòng'},{key:'byDay',name:'Theo ngày'},{key:'byExpense',name:'Theo nhóm chi'}]}),
        computed:{
            query(){return new URLSearchParams({propertyId:this.propertyId,scope:this.scope,month:this.month,...(this.roomId?{roomId:this.roomId}:{})}).toString();},
            exportUrl(){return '/api/admin/cash/export?'+this.query;},
            roomOptions(){return this.rooms.filter(x=>this.scope==='all'||x.propertyId===this.scope);},
            entries(){return this.snapshot.entries.filter(x=>this.showVoided||!x.isVoided);},
            groups(){return this.snapshot[this.tab]||[];},
            canVoidEntry(){return this.canManage && this.detailEntry && !this.detailEntry.isVoided && ['expense','other-receipt'].includes(this.detailEntry.source) && this.editableProperties.some(x=>x.id===this.detailEntry.propertyId);},
            detailEntries(){if(!this.detailGroup)return[];const g=this.detailGroup;return this.snapshot.entries.filter(e=>!e.isVoided && (g.type==='byProperty'?e.propertyId===g.key:g.type==='byRoom'?`${e.propertyId}:${e.roomId||''}`===g.key:g.type==='byDay'?helpers.localInput(e.occurredAtUtc,this.zone(e.propertyId)).slice(0,10)===g.key:e.source==='expense' && e.expenseCategory===g.key));}
        },
        methods:{
            money:helpers.money,zone(id){return this.properties.find(p=>p.id===id)?.timeZoneId||'Asia/Ho_Chi_Minh';},propertyName(id){return this.properties.find(p=>p.id===id)?.name||'';},
            dateText(e){return helpers.dateText(e.occurredAtUtc,this.zone(e.propertyId));},
            sourceText(source){return {'booking-receipt':'Thu booking','booking-refund':'Hoàn tiền booking','other-receipt':'Thu ngoài booking',expense:'Chi tiền mặt'}[source]||source;},
            scopeChanged(){this.roomId='';this.load();},
            async load(){const version=++this.requestVersion;const query=this.query;this.loading=true;this.error='';try{const data=await DeLongApi.get('/api/admin/cash/?'+query);if(version===this.requestVersion){this.snapshot=data;this.loadedQuery=query;this.detailGroup=null;}}catch(e){if(version===this.requestVersion){this.error=e.message;this.loadedQuery='';}}finally{if(version===this.requestVersion)this.loading=false;}},
            openEditor(kind){this.formError='';this.editor=kind;const property=this.editableProperties.find(p=>p.id===this.scope)||this.editableProperties.find(p=>p.id===this.propertyId)||this.editableProperties[0];if(!property){this.editor='';return;}const now=helpers.localInput(Date.now(),this.zone(property.id));this.form={requestKey:crypto.randomUUID(),propertyId:property.id,roomId:'',localAt:now.startsWith(this.month)?now:this.month+'-01T09:00',amount:0,description:'',category:'Vật tư',note:''};},
            formPropertyChanged(){this.form.roomId='';const now=helpers.localInput(Date.now(),this.zone(this.form.propertyId));this.form.localAt=now.startsWith(this.month)?now:this.month+'-01T09:00';},
            closeEditor(){if(!this.saving)this.editor='';},
            async save(){if(this.saving)return;this.saving=true;this.formError='';try{const f=this.form;const body={requestKey:f.requestKey,occurredAt:helpers.toUtc(f.localAt,this.zone(f.propertyId)),roomId:f.roomId||null,amount:Number(f.amount),description:f.description,note:f.note||null};const url=this.editor==='receipt'?`/api/admin/properties/${f.propertyId}/cash/receipts`:`/api/admin/properties/${f.propertyId}/expenses`;if(this.editor==='expense')Object.assign(body,{category:f.category,method:0});await DeLongApi.post(url,body);this.editor='';this.message='Đã ghi nhận giao dịch tiền mặt.';await this.load();}catch(e){this.formError=e.message;}finally{this.saving=false;}},
            openEntry(e){this.detailEntry=e;this.formError='';this.voidReason='';},closeEntry(){if(!this.saving)this.detailEntry=null;},
            async voidEntry(){if(this.saving||!this.canVoidEntry)return;this.saving=true;this.formError='';try{const e=this.detailEntry;const path=e.source==='expense'?`expenses/${e.id}/void`:`cash/receipts/${e.id}/void`;await DeLongApi.post(`/api/admin/properties/${e.propertyId}/${path}`,{reason:this.voidReason});this.detailEntry=null;this.message='Đã hủy giao dịch; tổng tiền đã được tính lại.';await this.load();}catch(e){this.formError=e.message;}finally{this.saving=false;}}
        }
    }).mount(root);
})();
