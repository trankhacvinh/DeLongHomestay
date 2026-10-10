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
    assert.equal(app.roomLock.open, false);
    assert.equal(calls.length, 2);
    assert.equal(app.rooms[0].isBookingLocked, false);
    assert.equal(calls[1].body.isLocked, false);
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
