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
