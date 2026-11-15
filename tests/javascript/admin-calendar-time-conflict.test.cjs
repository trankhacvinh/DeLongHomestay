const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');

const source = readFileSync('src/DeLong.Web/wwwroot/js/pages/admin-calendar.js', 'utf8');
const helper = source.slice(source.indexOf('    function utcToLocalInput('), source.indexOf('\n    }\n', source.indexOf('    function utcToLocalInput(')) + 6);
const computedStart = source.indexOf('            conflictCheckKey() {');
const computed = source.slice(computedStart, source.indexOf('            },\n', source.indexOf('            conflictMoveTo() {')) + 15);
const methods = source.slice(source.indexOf('            scheduleConflictCheck() {'), source.indexOf('            closeEditor() {'));

function setup(items) {
    const calls = [];
    const make = new Function('DeLongApi', 'utcOffset', 'timeZone', `${helper}\nreturn { computed: {${computed}}, methods: {${methods}} };`);
    const parts = make({ get: async url => { calls.push(url); return items; } }, '+07:00', 'Asia/Ho_Chi_Minh');
    const vm = {
        propertyId: 'p', editor: { open: true, mode: 'create' }, selectedBooking: null,
        form: { roomId: 'moon4', checkInLocal: '2026-11-16T10:00', checkOutLocal: '2026-11-16T17:00' },
        timeConflicts: { key: '', items: [], timer: null, serial: 0 },
        ...parts.methods
    };
    for (const [name, fn] of Object.entries(parts.computed)) Object.defineProperty(vm, name, { get: fn.bind(vm) });
    return { vm, calls };
}

const overnight = { bookingId: 'b1', code: 'BKV78WVU5E', customerName: 'Anh trung', startUtc: '2026-11-15T15:00:00Z', endUtc: '2026-11-16T03:30:00Z', isRoomBlock: false };

test('picked time overlapping a stay is reported before saving, and check-in can move to its checkout', async () => {
    const { vm, calls } = setup([overnight]);
    await vm.checkTimeConflicts(vm.conflictCheckKey);
    assert.equal(calls.length, 1);
    assert.match(calls[0], /booking-conflicts\/rooms\/moon4\?from=2026-11-16T03%3A00%3A00.000Z&to=2026-11-16T10%3A00%3A00.000Z/);
    assert.equal(vm.timeConflicts.items.length, 1);
    assert.equal(vm.conflictMoveTo, '2026-11-16T10:30');
    vm.moveCheckInAfterConflict();
    assert.equal(vm.form.checkInLocal, '2026-11-16T10:30');
});

test('no move offered when a conflict starts after check-in', async () => {
    const later = { ...overnight, startUtc: '2026-11-16T05:00:00Z', endUtc: '2026-11-16T08:00:00Z' };
    const { vm } = setup([later]);
    await vm.checkTimeConflicts(vm.conflictCheckKey);
    assert.equal(vm.timeConflicts.items.length, 1);
    assert.equal(vm.conflictMoveTo, '');
});

test('edit mode excludes the booking being edited', async () => {
    const { vm, calls } = setup([]);
    vm.editor.mode = 'edit';
    vm.selectedBooking = { id: 'self' };
    await vm.checkTimeConflicts(vm.conflictCheckKey);
    assert.match(calls[0], /excludeBookingId=self/);
    assert.equal(vm.timeConflicts.items.length, 0);
});
