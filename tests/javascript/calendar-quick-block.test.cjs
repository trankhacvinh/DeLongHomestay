const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const source = readFileSync('src/DeLong.Web/wwwroot/js/pages/admin-calendar.js', 'utf8');
function setup(allowed = true, failure = null) {
    const methods = source.slice(source.indexOf('            openQuickBlock()'), source.indexOf('            async saveBooking()'));
    const calls = [], events = [];
    const actions = new Function('root', 'DeLongApi', 'document', 'CustomEvent', 'window', 'utcOffset', `return {${methods}};`)(
        { dataset: { canLockRooms: String(allowed) } }, { put: async (...args) => { calls.push(args); if(failure)throw failure; return {id:'blue',isBookingLocked:true}; },post:async(...args)=>{calls.push(args);if(failure)throw failure;return{success:true};} },
        { dispatchEvent: event => events.push(event) }, class { constructor(type, value) { this.type = type; this.detail = value.detail; } }, {crypto:{randomUUID:()=> 'stable-request-key'}}, '+07:00');
    const vm = { ...actions, saving:false, propertyId:'property',editor:{open:true,mode:'create'},rooms:[{id:'blue'}],form:{roomId:'blue',checkInLocal:'2031-01-10T21:00',checkOutLocal:'2031-01-11T09:30'},notify(){} };
    vm.openQuickBlock();return {vm,calls,events};
}
test('quick lock has no time requirement and uses indefinite lock API, not a booking', async()=>{
 const {vm,calls,events}=setup();vm.quickBlock.scope='room';vm.quickBlock.reason=' Bảo trì ';await vm.saveQuickBlock();
 assert.equal(calls.length,1);assert.equal(calls[0][0],'/api/admin/properties/property/rooms/blue/booking-lock');
 assert.deepEqual(calls[0][1],{isLocked:true,reason:'Bảo trì'});assert.equal(vm.rooms[0].isBookingLocked,true);assert.equal(vm.editor.open,false);assert.equal(events[0].type,'delong:operations-change');
});
test('missing reason does not call API',async()=>{const {vm,calls}=setup();await vm.saveQuickBlock();assert.equal(calls.length,0);});
test('failure preserves booking draft and lock dialog',async()=>{const {vm,calls}=setup(true,new Error('Failed'));vm.quickBlock.reason='Bảo trì';await vm.saveQuickBlock();assert.equal(calls.length,1);assert.equal(vm.editor.open,true);assert.equal(vm.quickBlock.open,true);assert.equal(vm.quickBlock.error,'Failed');});
test('staff cannot open quick lock',()=>{assert.equal(setup(false).vm.quickBlock,undefined);});

test('slot lock uses the selected overnight window and retains a stable retry key',async()=>{
 const {vm,calls}=setup();vm.quickBlock.reason='Bảo trì';await vm.saveQuickBlock();
 assert.equal(calls[0][0],'/api/admin/properties/property/room-blocks/');assert.equal(calls[0][1].start,'2031-01-10T21:00+07:00');assert.equal(calls[0][1].end,'2031-01-11T09:30+07:00');assert.equal(calls[0][1].requestKey,'stable-request-key');assert.equal(calls[0][1].repeatDaily,false);
});
test('selected full slot is preserved even when the booking form uses only its free remainder',()=>{
 const {vm}=setup();vm.selectedLockSlot={roomId:'blue',rateId:'r1',startUtc:'2031-01-10T07:00:00Z',endUtc:'2031-01-10T10:00:00Z'};vm.openQuickBlock();assert.equal(vm.quickBlock.start,vm.selectedLockSlot.startUtc);assert.equal(vm.quickBlock.end,vm.selectedLockSlot.endUtc);
});

test('expired slot is explained before submit, sends no request, and requires an explicit room-lock choice', async () => {
 const {vm,calls}=setup();
 vm.form.checkInLocal='2020-01-10T14:00';vm.form.checkOutLocal='2020-01-10T17:00';vm.openQuickBlock();
 assert.equal(vm.quickBlock.scope,'slot');assert.match(vm.quickBlock.error,/đã kết thúc/);
 vm.quickBlock.reason='Sửa';await vm.saveQuickBlock();assert.equal(calls.length,0);assert.equal(vm.quickBlock.open,true);
 vm.quickBlock.scope='room';vm.resetQuickBlockConflicts();assert.equal(vm.quickBlock.error,'');
 await vm.saveQuickBlock();assert.equal(calls.length,1);assert.match(calls[0][0],/booking-lock$/);
});
test('slot expiring while the modal is open is checked again before sending', async () => {
 const {vm,calls}=setup();vm.quickBlock.reason='Sửa';vm.quickBlock.end=new Date(Date.now()-1000).toISOString();
 vm.quickBlock.start=new Date(Date.now()-3600000).toISOString();await vm.saveQuickBlock();
 assert.equal(calls.length,0);assert.match(vm.quickBlock.error,/đã kết thúc/);assert.equal(vm.saving,false);
});
test('ongoing and overnight slots remain valid while invalid or excessive ranges are rejected', async () => {
 const {vm,calls}=setup();vm.quickBlock.reason='Sửa';
 vm.quickBlock.start=new Date(Date.now()-3600000).toISOString();vm.quickBlock.end=new Date(Date.now()+3600000).toISOString();
 assert.equal(vm.quickBlockSlotError(),'');await vm.saveQuickBlock();assert.equal(calls.length,1);
 vm.quickBlock.start='invalid';assert.match(vm.quickBlockSlotError(),/không có thời gian hợp lệ/);
 vm.quickBlock.start='2031-01-10T00:00:00Z';vm.quickBlock.end='2033-01-10T00:00:00Z';assert.match(vm.quickBlockSlotError(),/366/);
});
