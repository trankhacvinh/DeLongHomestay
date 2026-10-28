const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const source = fs.readFileSync(path.join(__dirname, '../../src/DeLong.Web/wwwroot/js/pages/admin-room-booking-lock.js'), 'utf8');
function fixture(canLock = true) {
    const events = [], calls = [];
    const context = { window: {}, document: { querySelector: () => ({ dataset: { canLockRooms: String(canLock) } }), dispatchEvent: e => events.push(e), addEventListener() {}, removeEventListener() {} },
        CustomEvent: class { constructor(type, options) { this.type = type; this.detail = options.detail; } },
        setInterval: () => 1, clearInterval() {}, DeLongApi: { put: async (url, body) => { calls.push({ url, body }); return { id: 'room', isBookingLocked: body.isLocked, bookingLockReason: body.reason }; } } };
    vm.runInNewContext(source, context);
    const mixin = context.window.DeLongRoomBookingLock;
    const app = { ...mixin.data(), propertyId: 'branch', rooms: [{ id: 'room', name: 'Blue', isBookingLocked: false }],
        $el: { dataset: { canLockRooms: String(canLock) } }, notify() {} };
    Object.assign(app, mixin.methods);
    return { app, events, calls, context };
}
test('unauthorized staff cannot open lock dialog', () => {
    const { app } = fixture(false);
    app.openRoomBookingLock(app.rooms[0]);
    assert.equal(app.roomLock.open, false);
});
test('empty or oversized reasons keep modal open without sending API mutation', async () => {
    const { app, calls } = fixture();
    app.openRoomBookingLock(app.rooms[0]);
    for (const reason of [' ', 'x'.repeat(501)]) {
        app.roomLock.reason = reason;
        await app.saveRoomBookingLock();
        assert.ok(app.roomLock.error);
        assert.equal(app.roomLock.open, true);
    }
    assert.equal(calls.length, 0);
});
test('lock and unlock reconcile the room and signal calendar without reloading', async () => {
    const { app, calls, events } = fixture();
    app.openRoomBookingLock(app.rooms[0]);
    app.roomLock.reason = '  Bảo trì  ';
    await app.saveRoomBookingLock();
    assert.equal(app.rooms[0].isBookingLocked, true);
    assert.equal(calls[0].body.reason, 'Bảo trì');
    assert.equal(app.roomLock.open, false);
    assert.equal(events[0].detail.roomId, 'room');
    await app.openRoomBookingLock(app.rooms[0]);
    assert.equal(app.roomLock.open, true);
    assert.equal(calls.length, 1);
    await app.saveRoomBookingLock();
    assert.equal(app.roomLock.open, false);
    assert.equal(calls.length, 2);
    assert.equal(app.rooms[0].isBookingLocked, false);
    assert.equal(calls[1].body.isLocked, false);
});

test('slot unlock requires confirmation and targets one interval rather than an entire batch',async()=>{
    const {app,calls,context}=fixture();
    context.DeLongApi.post=async(url,body)=>{calls.push({url,body});};
    const range={bookingId:'interval-1',blockReason:'Bảo trì',startUtc:'2031-01-10T07:00:00Z',endUtc:'2031-01-10T10:00:00Z'};
    context.DeLongApi.get=async()=>[{id:'interval-1',roomId:'room',startUtc:range.startUtc,endUtc:range.endUtc,reason:'Bảo trì'}];
    await app.openRoomSlotUnlock(app.rooms[0],range);assert.equal(app.roomLock.open,true);assert.equal(calls.length,0);
    await app.saveRoomBookingLock();assert.match(calls[0].url,/room-blocks\/intervals\/interval-1\/end$/);
    assert.equal(app.rooms[0].isBookingLocked,false);assert.equal(app.roomLock.open,false);
});
test('failed request retains dialog and releases saving state', async () => {
    const { app, context } = fixture();
    context.DeLongApi.put = async () => { throw new Error('Không có quyền chi nhánh'); };
    app.openRoomBookingLock(app.rooms[0]);
    app.roomLock.reason = 'Sửa phòng';
    await app.saveRoomBookingLock();
    assert.equal(app.roomLock.open, true);
    assert.equal(app.roomLock.saving, false);
    assert.equal(app.roomLock.error, 'Không có quyền chi nhánh');
    assert.equal(app.rooms[0].isBookingLocked, false);
});

test('unlock waits for original interval bounds and does not mutate when reading fails',async()=>{
    const {app,calls,context}=fixture();context.DeLongApi.get=async()=>{throw new Error('Read failed');};
    context.DeLongApi.post=async(...args)=>calls.push(args);
    await app.openRoomSlotUnlock(app.rooms[0],{bookingId:'interval',startUtc:'2031-01-10T07:00:00Z',endUtc:'2031-01-10T08:00:00Z'});
    await app.saveRoomBookingLock();assert.equal(calls.length,0);assert.equal(app.roomLock.open,true);assert.equal(app.roomLock.unlockReady,false);
    context.DeLongApi.get=async()=>[{id:'interval',roomId:'room',startUtc:'2031-01-10T07:00:00Z',endUtc:'2031-01-10T14:00:00Z',reason:'Long interval'}];
    await app.openRoomSlotUnlock(app.rooms[0],{bookingId:'interval',startUtc:'2031-01-10T07:00:00Z',endUtc:'2031-01-10T08:00:00Z'});
    assert.equal(app.roomLock.range.endUtc,'2031-01-10T14:00:00Z');assert.equal(app.roomLock.unlockReady,true);
});
