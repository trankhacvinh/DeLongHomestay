const { chromium }=require('playwright');
const fs=require('node:fs');const assert=require('node:assert/strict');
const base='src/DeLong.Web/';const output='docs/qa/room-lock-scopes-2026-10-28';
(async()=>{
 fs.mkdirSync(output,{recursive:true});
 const browser=await chromium.launch({headless:true,...(process.env.CHROME_EXECUTABLE?{executablePath:process.env.CHROME_EXECUTABLE}:{})});
 try{
  for(const width of [390,1280]){
   const page=await browser.newPage({viewport:{width,height:844}});const errors=[];page.on('pageerror',e=>errors.push(e.message));
   await page.route('http://localhost/**',route=>route.fulfill({contentType:'text/html',body:'<html><meta name="viewport" content="width=device-width,initial-scale=1"><body class="admin-body"></body></html>'}));await page.goto('http://localhost/Admin/CalendarV2');
   const source=fs.readFileSync(base+'Pages/Admin/CalendarV2.cshtml','utf8');const start=source.indexOf('    <div class="modal-backdrop modal-top" v-if="quickBlock.open"');const end=source.indexOf('    <div class="modal-backdrop modal-top" v-if="statusConfirm.open"',start);
   const propertyId='01985a50-1000-7000-8000-000000000001';const room={id:'blue',name:'Phòng mẫu',code:'BLUE',capacity:2,isActive:true,isBookingLocked:false,rates:[]};
   await page.setContent(`<script id="calendar-page-data" type="application/json">${JSON.stringify({propertyId,rooms:[room]})}</script><main id="calendar-page" data-calendar-v2-page="true" data-can-lock-rooms="true" data-property-id="${propertyId}" data-start-date="2031-01-10" data-today="2031-01-10" data-range-days="1"><div class="calendar-page-head"></div><button id="fixture-lock" class="btn btn-light" v-on:click="openQuickBlock">Khóa chọn</button>${source.slice(start,end)}${fs.readFileSync(base+'Pages/Shared/_RoomBookingLockDialog.cshtml','utf8')}</main>`);
   await page.evaluate(()=>document.body.classList.add('admin-body'));
   for(const css of ['site.css','calendar.css','admin-calendar-v2.css','admin-shell-fixes.css'])await page.addStyleTag({path:base+'wwwroot/css/'+css});
   await page.addScriptTag({path:process.env.VUE_RUNTIME_PATH});await page.addScriptTag({path:base+'wwwroot/js/pages/admin-room-booking-lock.js'});
   const calendar=fs.readFileSync(base+'wwwroot/js/pages/admin-calendar.js','utf8');const methods=calendar.slice(calendar.indexOf('            openQuickBlock()'),calendar.indexOf('            async saveBooking()'));
   await page.evaluate(({propertyId,room,methods})=>{
    window.calls=[];window.blocks=[];window.fullLock=false;
    window.DeLongApi={
      get:async url=>{
       if(url.endsWith('/bookings/existing-booking'))return{id:'existing-booking',roomId:room.id,customerName:'Khách mẫu',status:2};
       if(url.endsWith('/room-blocks/'))return structuredClone(window.blocks);
       if(url.endsWith('/rooms/'))return[{...room,isBookingLocked:window.fullLock}];
       if(url.includes('booking-calendar-settings'))return{};
       if(url.includes('/availability/')){
        const spans=[['2031-01-10T03:30:00Z','2031-01-10T06:30:00Z'],['2031-01-10T07:00:00Z','2031-01-10T10:00:00Z'],['2031-01-10T10:30:00Z','2031-01-10T13:30:00Z'],['2031-01-10T14:00:00Z','2031-01-11T02:30:00Z']];
        return{roomName:room.name,roomCode:room.code,timeZoneId:'Asia/Ho_Chi_Minh',isBookingLocked:window.fullLock,calendar:[{date:'2031-01-10',slots:spans.map(([startUtc,endUtc],i)=>{
         const blocks=window.blocks.filter(b=>!b.cancelledAtUtc&&new Date(b.startUtc)<new Date(endUtc)&&new Date(startUtc)<new Date(b.endUtc));
         return{rateId:'rate'+i,rateName:i===3?'Qua đêm':'Khung '+(i+1),rateType:i===3?1:0,startUtc,endUtc,state:blocks.length||window.fullLock&&i===0?'occupied':'available',occupied:blocks.map(b=>({bookingId:b.id,startUtc,endUtc,isRoomBlock:true,blockReason:b.reason,status:2})).concat(window.fullLock&&i===0?[{bookingId:'existing-booking',startUtc,endUtc,status:2,customerName:'Khách mẫu',balanceAmount:0}]:[]),free:blocks.length||window.fullLock&&i===0?[]:[{startUtc,endUtc}]};})}]};
       }throw new Error('Unexpected URL '+url);
      },
      post:async(url,body)=>{window.calls.push({url,body});if(url.endsWith('/room-blocks/'))window.blocks.push({id:'interval',roomId:room.id,startUtc:body.start,endUtc:body.end,reason:body.reason});else if(url.endsWith('/intervals/interval/end'))window.blocks[0].cancelledAtUtc=new Date().toISOString();else throw new Error('Unexpected post '+url);return{success:true};},
      put:async(url,body)=>{window.calls.push({url,body});window.fullLock=body.isLocked;return{...room,isBookingLocked:body.isLocked,bookingLockReason:body.reason};}
    };
    const actions=new Function('root','DeLongApi','document','CustomEvent','window','utcOffset',`return {${methods}}`)(document.getElementById('calendar-page'),DeLongApi,document,CustomEvent,window,'+07:00');
    const app=Vue.createApp({data:()=>({propertyId,rooms:[room],bookings:[],canManage:true,saving:false,form:{roomId:room.id},editor:{open:false,mode:'create'},quickBlock:{open:false},timeZoneId:'Asia/Ho_Chi_Minh'}),computed:{activeRooms(){return this.rooms;}},methods:{...actions,notify(){},openBooking(booking){window.viewedBooking=booking.id;},dateTimeText(value){return new Intl.DateTimeFormat('vi-VN',{timeZone:'Asia/Ho_Chi_Minh',dateStyle:'short',timeStyle:'short'}).format(new Date(value));},openCreate(){this.form={roomId:room.id,rateId:''};this.editor={open:true,mode:'create'};},applyRate(){},}}).mixin(DeLongRoomBookingLock).mount('#calendar-page');
    document.getElementById('calendar-page').__delongCalendarVm=app;
   },{propertyId,room,methods});
   await page.addScriptTag({path:base+'wwwroot/js/pages/admin-calendar-v2.js'});await page.locator('.calendar-v2-slot').first().waitFor();
   await page.locator('.calendar-v2-slot').last().locator('button').click();await page.locator('#fixture-lock').click();
   let dialog=page.getByRole('dialog');await dialog.waitFor();await page.waitForFunction(()=>[...document.querySelectorAll('.modal-backdrop,.modal-card')].every(el=>Number(getComputedStyle(el).opacity)>=.999));await page.screenshot({path:`${output}/choices-${width}.png`,animations:'disabled'});
   assert.equal(await dialog.getByRole('radio',{name:/Khóa khung đang chọn/}).isChecked(),true);await dialog.locator('textarea').fill('Bảo trì mẫu');await dialog.getByRole('button',{name:'Khóa phòng',exact:true}).click();await dialog.waitFor({state:'detached'});
   const request=await page.evaluate(()=>window.calls[0]);assert.equal(request.body.start,'2031-01-10T14:00:00Z');assert.equal(request.body.end,'2031-01-11T02:30:00Z');
   await page.locator('[data-booking-id="interval"]').click();await dialog.getByRole('button',{name:'Mở khóa',exact:true}).waitFor({state:'visible'});await page.waitForFunction(()=>document.querySelector('[aria-labelledby="room-booking-lock-title"] button.btn-primary:not(:disabled)'));
   assert.equal(await page.evaluate(()=>window.calls.length),1);await page.waitForFunction(()=>[...document.querySelectorAll('.modal-backdrop,.modal-card')].every(el=>Number(getComputedStyle(el).opacity)>=.999));await page.screenshot({path:`${output}/unlock-slot-${width}.png`,animations:'disabled'});
   await dialog.getByRole('button',{name:'Mở khóa',exact:true}).click();await dialog.waitFor({state:'detached'});await page.locator('[data-booking-id="interval"]').waitFor({state:'detached'});
   await page.locator('.calendar-v2-slot').first().locator('button').click();await page.locator('#fixture-lock').click();await dialog.getByRole('radio',{name:/Khóa toàn phòng/}).check();await dialog.locator('textarea').fill('Khóa toàn phòng mẫu');await dialog.getByRole('button',{name:'Khóa phòng',exact:true}).click();await dialog.waitFor({state:'detached'});
   await page.waitForSelector('.room-booking-locked');
   const booked=page.locator('[data-booking-id="existing-booking"]');
   await booked.locator('span').click();await page.waitForFunction(()=>window.viewedBooking==='existing-booking');
   assert.equal(await dialog.count(),0);assert.equal(await page.evaluate(()=>window.fullLock),true);
   await page.evaluate(()=>window.viewedBooking=null);await booked.focus();await booked.press('Enter');
   await page.waitForFunction(()=>window.viewedBooking==='existing-booking');assert.equal(await dialog.count(),0);
   await page.locator('.calendar-v2-slot').nth(1).locator('button').click();await dialog.getByRole('heading',{name:'Mở khóa toàn phòng'}).waitFor();await dialog.getByRole('button',{name:'Quay lại'}).click();await dialog.waitFor({state:'detached'});
   assert.equal(await page.evaluate(()=>window.fullLock),true);
   await page.locator('.calendar-v2-slot').last().click();await dialog.getByRole('button',{name:'Mở khóa',exact:true}).click();await dialog.waitFor({state:'detached'});assert.equal(await page.evaluate(()=>window.fullLock),false);
   await page.evaluate(()=>{const app=document.getElementById('calendar-page').__delongCalendarVm;app.form={roomId:'blue',rateId:'',checkInLocal:'2020-10-27T14:00',checkOutLocal:'2020-10-27T17:00'};app.editor={open:true,mode:'create'};app.selectedLockSlot=null;});
   await page.locator('#fixture-lock').click();await dialog.getByRole('alert').filter({hasText:'đã kết thúc'}).waitFor();
   await dialog.locator('textarea').fill('Bảo trì mẫu');assert.equal(await dialog.getByRole('button',{name:'Khóa phòng',exact:true}).isDisabled(),true);
   await dialog.getByRole('radio',{name:/Khóa toàn phòng/}).check();assert.equal(await dialog.getByRole('button',{name:'Khóa phòng',exact:true}).isEnabled(),true);assert.equal(await dialog.getByRole('alert').count(),0);
   await dialog.getByRole('button',{name:'Quay lại'}).click();
   assert.deepEqual(errors,[]);await page.close();console.log('PASS slot/global lock, booked click/keyboard and expired slot '+width+'px');
  }
 }finally{await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});
