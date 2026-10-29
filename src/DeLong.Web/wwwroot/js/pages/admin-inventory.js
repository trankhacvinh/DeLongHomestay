(function () {
    const root = document.getElementById('inventory-page'); if (!root) return;
    const initial = JSON.parse(document.getElementById('inventory-data').textContent);
    const helpers = window.DeLongOperations;
    const normalizeRecipient = value => String(value || '').normalize('NFC').trim().replace(/\s+/g, ' ').toUpperCase();
    let activeDialog = null;
    let previousFocus = null;
    Vue.createApp({
        data: () => ({ ...initial, importForm:{},importFile:null,importPreview:null,importKey:'',requestVersion:0,loadedMonth:initial.month,historyVersion:0,historyItem:null,historyRows:[],historySkip:0,historyMore:false,historyLoading:false,loading:false, saving:false, error:'', message:'', tab:'stock', search:'', editor:'', detail:null, formError:'', ownerId:'', unitName:'', itemForm:{}, documentForm:{lines:[]}, voidReason:'', recipientFilter:'', typeFilter:'', showVoided:false,
            tabs:[{key:'stock',name:'Nhập–xuất–tồn'},{key:'documents',name:'Phiếu kho'},{key:'distribution',name:'Cấp chi nhánh'},{key:'catalog',name:'Mặt hàng / ĐVT'}], types:['Tồn đầu','Nhập kho','Xuất kho','Điều chỉnh tăng','Điều chỉnh giảm'] }),
        computed: {
            zone() { return this.snapshot.warehouse?.timeZoneId || 'Asia/Ho_Chi_Minh'; },
            exportUrl() { return '/api/admin/inventory/export?' + new URLSearchParams({month:this.month}); },
            stockRows() { const q=this.search.toLocaleLowerCase('vi'); return this.snapshot.stock.filter(x=>x.name.toLocaleLowerCase('vi').includes(q)); },
            items() { const q=this.search.toLocaleLowerCase('vi'); return this.snapshot.items.filter(x=>(x.code+' '+x.name).toLocaleLowerCase('vi').includes(q)); },
            documents() { return this.snapshot.documents.filter(x=>(this.showVoided || !x.isVoided) && (this.typeFilter==='' || Number(this.typeFilter)===x.type) && (!this.recipientFilter || this.recipientKey(x)===this.recipientFilter)); },
            distribution() { return this.snapshot.distribution.filter(x=>!this.recipientFilter || this.recipientKey(x)===this.recipientFilter); },
            recipientOptions() {
                const properties = this.properties || [];
                const options = properties.map(p => ({ key:p.id,propertyId:p.id,name:p.name,value:properties.filter(x=>normalizeRecipient(x.name)===normalizeRecipient(p.name)).length>1?`${p.name} · ${p.code}`:p.name }));
                for (const previous of this.snapshot.recipients || []) {
                    const key = previous.propertyId || 'text:' + normalizeRecipient(previous.name);
                    if (!options.some(x=>x.key===key)) options.push({key,propertyId:previous.propertyId,name:previous.name,value:previous.name});
                }
                return options.sort((a,b)=>a.value.localeCompare(b.value,'vi'));
            },
            quickSummary() {
                const live = (this.snapshot.documents || []).filter(x=>!x.isVoided);
                const active = this.snapshot.stock.filter(row=>this.snapshot.items.some(x=>x.id===row.itemId && x.isActive));
                return { items:active.length,empty:active.filter(x=>Number(x.closing)<=0).length,receipts:live.filter(x=>Number(x.type)===1).length,issues:live.filter(x=>Number(x.type)===2).length };
            },
            stockTotals() {
                return this.stockRows.reduce((total,row)=>({
                    received:total.received+Number(row.received||0),
                    issued:total.issued+Number(row.issued||0),
                    closing:total.closing+Number(row.closing||0)
                }),{received:0,issued:0,closing:0});
            }
        },
        methods: {
            money:helpers.money, qty:helpers.quantity,
            recipientKey(row){return row.recipientPropertyId || 'text:'+normalizeRecipient(row.recipientName);},
            movementClass(type){return [2,4].includes(Number(type))?'inventory-out':'inventory-in';},
            dateText(value) { return helpers.dateText(value,this.zone); }, typeText(type) { return this.types[Number(type)] || 'Phiếu'; },
            async load() { const version=++this.requestVersion;const month=this.month;this.loading=true;this.error='';try{const data=await DeLongApi.get('/api/admin/inventory/?'+new URLSearchParams({month}));if(version===this.requestVersion){this.snapshot=data;this.loadedMonth=month;}}catch(e){if(version===this.requestVersion){this.error=e.message;this.loadedMonth='';}}finally{if(version===this.requestVersion)this.loading=false;} },
            async openHistory(item){this.historyItem=item;this.historyRows=[];this.historySkip=0;this.historyMore=false;this.historyLoading=false;this.historyVersion++;this.formError='';await this.moreHistory();},
            async moreHistory(){if(this.historyLoading||!this.historyItem)return;this.historyLoading=true;const id=this.historyItem.itemId;const version=this.historyVersion;try{const rows=await DeLongApi.get(`/api/admin/inventory/items/${id}/history?skip=${this.historySkip}`);if(version===this.historyVersion && this.historyItem?.itemId===id){this.historyRows.push(...rows);this.historySkip+=rows.length;this.historyMore=rows.length===100;}}catch(e){if(version===this.historyVersion)this.formError=e.message;}finally{if(version===this.historyVersion)this.historyLoading=false;}},
            async initialize() { if(this.saving)return; this.saving=true; this.error=''; try{await DeLongApi.post('/api/admin/inventory/initialize',{propertyId:this.ownerId});await this.load();}catch(e){this.error=e.message;}finally{this.saving=false;} },
            closeEditor(){if(!this.saving)this.editor='';},
            closeModal(){if(this.saving)return;if(this.historyItem)this.historyItem=null;else if(this.detail)this.detail=null;else this.closeEditor();},
            openImport(mode){this.formError='';this.importPreview=null;this.importFile=null;this.importKey=crypto.randomUUID();this.importForm={mode,duplicates:'skip',localAt:this.month+'-01T00:00',sheet:''};this.editor='import';},
            clearPreview(){this.importPreview=null;this.formError='';this.importKey=crypto.randomUUID();},
            importFileChanged(event){this.importFile=event.target.files[0]||null;this.clearPreview();},
            async previewImport(){if(this.saving)return;this.saving=true;this.formError='';this.importPreview=null;try{if(!this.importFile)throw new Error('Chọn file Excel.');if(this.importFile.size>4*1024*1024)throw new Error('File tối đa 4 MB.');const f=this.importForm;const data=new FormData();data.append('file',this.importFile);data.append('mode',f.mode);data.append('duplicates',f.duplicates);data.append('sheet',f.sheet);if(f.mode==='opening')data.append('openingAt',helpers.toUtc(f.localAt,this.zone));this.importPreview=await DeLongApi.postForm('/api/admin/inventory/import/preview',data);}catch(e){this.formError=e.message;}finally{this.saving=false;}},
            async commitImport(){if(this.saving||!this.importPreview?.token||this.importPreview.errors)return;this.saving=true;this.formError='';try{const result=await DeLongApi.post('/api/admin/inventory/import/commit',{requestKey:this.importKey,token:this.importPreview.token});this.editor='';this.message=`Đã import: ${result.createdItems} mặt hàng mới, ${result.updatedItems} cập nhật, ${result.createdUnits} đơn vị mới, ${result.openingItems} tồn đầu, ${result.skipped} bỏ qua.`;await this.load();}catch(e){this.formError=e.message;if(e.problem?.code==='preview_stale')this.importPreview=null;}finally{this.saving=false;}},
            openItem(item){this.formError='';this.itemForm=item?{...item}:{code:'',name:'',unitId:'',referencePrice:0,isActive:true};this.editor='item';},
            async addUnit(){if(this.saving)return;this.saving=true;this.error='';try{await DeLongApi.post('/api/admin/inventory/units',{name:this.unitName});this.unitName='';await this.load();}catch(e){this.error=e.message;}finally{this.saving=false;}},
            async saveItem(){if(this.saving)return;this.saving=true;this.formError='';try{const f=this.itemForm;const body={name:f.name,unitId:f.unitId,referencePrice:Number(f.referencePrice),isActive:f.isActive};if(f.id)await DeLongApi.put('/api/admin/inventory/items/'+f.id,body);else await DeLongApi.post('/api/admin/inventory/items',body);this.editor='';this.message='Đã lưu mặt hàng.';await this.load();}catch(e){this.formError=e.message;}finally{this.saving=false;}},
            openDocument(type){this.formError='';const now=helpers.localInput(Date.now(),this.zone);this.documentForm={requestKey:crypto.randomUUID(),type,localAt:type===0?this.month+'-01T00:00':now.startsWith(this.month)?now:this.month+'-01T09:00',recipientPropertyId:'',recipientText:'',note:'',lines:[{itemId:'',quantity:1,unitPrice:0}]};this.editor='document';},
            documentRecipient(){
                const f=this.documentForm;
                if(Number(f.type)!==2)return {recipientPropertyId:null,recipientName:null};
                const text=String(f.recipientText||'').trim().replace(/\s+/g,' ');
                const match=this.recipientOptions.find(x=>normalizeRecipient(x.value)===normalizeRecipient(text));
                if(match?.propertyId)return {recipientPropertyId:match.propertyId,recipientName:null};
                return {recipientPropertyId:text?null:f.recipientPropertyId||null,recipientName:text||null};
            },
            lineChanged(line){line.unitPrice=Number(this.snapshot.items.find(x=>x.id===line.itemId)?.referencePrice || 0);},
            async saveDocument(){if(this.saving)return;this.saving=true;this.formError='';try{const f=this.documentForm;await DeLongApi.post('/api/admin/inventory/documents',{requestKey:f.requestKey,type:Number(f.type),occurredAt:helpers.toUtc(f.localAt,this.zone),...this.documentRecipient(),note:f.note||null,lines:f.lines.map(l=>({itemId:l.itemId,quantity:Number(l.quantity),unitPrice:Number(l.unitPrice)}))});this.editor='';this.message='Đã ghi nhận phiếu kho.';await this.load();}catch(e){this.formError=e.message;}finally{this.saving=false;}},
            async voidDocument(){if(this.saving)return;this.saving=true;this.formError='';try{await DeLongApi.post('/api/admin/inventory/documents/'+this.detail.id+'/void',{reason:this.voidReason});this.detail=null;this.voidReason='';this.message='Đã hủy phiếu, tồn kho đã được tính lại.';await this.load();}catch(e){this.formError=e.message;}finally{this.saving=false;}}
        }, watch:{detail(){this.formError='';this.voidReason='';}},
        mounted(){root.addEventListener('keydown',event=>{if(event.key==='Escape'&&activeDialog){event.preventDefault();event.stopPropagation();this.closeModal();return;}if(event.key!=='Tab'||!activeDialog)return;const controls=[...activeDialog.querySelectorAll('button:not(:disabled),a[href],input:not(:disabled),select:not(:disabled),textarea:not(:disabled),[tabindex="0"]')].filter(x=>x.getClientRects().length);if(!controls.length)return;const first=controls[0],last=controls.at(-1);if(event.shiftKey&&document.activeElement===first){event.preventDefault();last.focus();}else if(!event.shiftKey&&document.activeElement===last){event.preventDefault();first.focus();}});},
        updated(){const dialog=root.querySelector('[role="dialog"]');if(dialog===activeDialog)return;if(dialog){previousFocus=document.activeElement;activeDialog=dialog;this.$nextTick(()=>dialog.querySelector('input,select,textarea,button')?.focus());}else{activeDialog=null;if(previousFocus?.isConnected)previousFocus.focus();previousFocus=null;}}
    }).directive('item-search', {
        mounted(element) {
            if (!window.TomSelect) return;
            new window.TomSelect(element, {
                create:false,maxItems:1,maxOptions:null,searchField:['text'],
                placeholder:'Tìm và chọn mặt hàng',closeAfterSelect:true,
                onInitialize(){this.control_input.setAttribute('aria-label','Tìm mặt hàng');},
                render:{no_results:()=>'<div class="no-results">Không tìm thấy mặt hàng.</div>'}
            });
        },
        updated(element,binding){
            const control=element.tomselect;
            if(control && control.getValue()!==String(binding.value||''))control.setValue(binding.value||'',true);
        },
        beforeUnmount(element){element.tomselect?.destroy();}
    }).mount(root);
})();
