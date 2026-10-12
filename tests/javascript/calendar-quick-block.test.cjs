const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const source = fs.readFileSync('src/DeLong.Web/wwwroot/js/pages/admin-calendar.js', 'utf8');
const methods = source.slice(source.indexOf('            openQuickBlock()'), source.indexOf('            async saveBooking()'));
function setup(allowed = true, failure) {
    const calls = [], events = [];
    const api = { post: async (...args) => { calls.push(args); if (failure) throw failure; } };
    const actions = new Function('root', 'utcOffset', 'DeLongApi', 'document', 'CustomEvent', `return {${methods}};`)(
        { dataset: { canLockRooms: String(allowed) } }, '+07:00', api, { dispatchEvent: event => events.push(event) }, class { constructor(type, value) { this.type = type; this.detail = value.detail; } });
    const vm = { ...actions, saving: false, propertyId: 'property', editor: { open: true, mode: 'create' },
        form: { roomId: 'blue', checkInLocal: '2026-10-11T21:00', checkOutLocal: '2026-10-12T09:30' }, notify() {} };
    vm.openQuickBlock();
    return { vm, calls, events };
}
test('quick lock uses selected overnight interval, no booking or payment is created', async () => {
    const { vm, calls, events } = setup(); vm.quickBlock.reason = ' Bảo trì ';
    await vm.saveQuickBlock();
    assert.equal(calls.length, 1);
    assert.match(calls[0][0], /room-blocks\/$/);
    assert.equal(calls[0][1].start, '2026-10-11T14:00:00.000Z');
    assert.equal(calls[0][1].end, '2026-10-12T02:30:00.000Z');
    assert.deepEqual(calls[0][1].roomIds, ['blue']);
    assert.equal(vm.editor.open, false);
    assert.equal(events[0].type, 'delong:operations-change');
});
test('invalid interval and missing reason never call the API', async () => {
    const { vm, calls } = setup(); await vm.saveQuickBlock();
    vm.quickBlock.reason = 'Sửa'; vm.quickBlock.end = vm.quickBlock.start;
    await vm.saveQuickBlock(); assert.equal(calls.length, 0);
});
test('server conflicts stay visible and preserve booking draft for explicit acknowledgement', async () => {
    const conflicts = [{ id: 'booking', code: 'BK1' }];
    const { vm, calls } = setup(true, { message: 'Có đơn cũ', problem: { conflicts } });
    vm.quickBlock.reason = 'Sửa'; await vm.saveQuickBlock();
    assert.equal(vm.editor.open, true); assert.equal(vm.quickBlock.open, true);
    assert.deepEqual(vm.quickBlock.conflicts, conflicts); assert.equal(vm.quickBlock.acknowledged, false);
    vm.quickBlock.acknowledged = true; await vm.saveQuickBlock();
    assert.deepEqual(calls[1][1].acknowledgedBookingIds, ['booking']);
});
test('staff cannot open quick lock', () => { assert.equal(setup(false).vm.quickBlock, undefined); });
