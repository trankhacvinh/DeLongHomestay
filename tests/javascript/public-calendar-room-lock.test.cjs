const { readFileSync } = require('node:fs');
const { resolve } = require('node:path');
const assert = require('node:assert/strict');
const { test } = require('node:test');
const source = readFileSync(resolve(__dirname, '../../src/DeLong.Web/wwwroot/js/pages/public-availability-calendar.js'), 'utf8');
const renderSource = source.slice(source.indexOf('        function renderSlotButton('), source.indexOf('        function renderVirtualRows('));
function fixture() {
    const rooms = [{ id: 'open', name: 'Open', isBookingLocked: false }, { id: 'locked', name: 'Locked', isBookingLocked: true }];
    const state = { multi: true, selected: [] };
    const render = new Function('document', 'state', 'roomById', 'isSelected', 'slotKey', 'timeLabel', 'money', 'dateLabel', 'selectSlot', `${renderSource}; return renderSlotButton;`)(
        { createElement: () => ({ classList: { add() {} }, setAttribute() {}, listeners: [], addEventListener(type, callback) { this.listeners.push({ type, callback }); } }) },
        state, id => rooms.find(r => r.id === id), () => false, (date, slot) => date + slot.rateId, x => x, x => x, x => x, () => {}
    );
    return { render, rooms };
}
test('multi-room calendar disables only the locked room even with stale bookable times', () => {
    const { render } = fixture();
    const slot = { state: 'available', rateId: 'morning', price: 250000, startUtc: '10:30', endUtc: '13:30', bookableStartUtc: '10:30', bookableEndUtc: '13:30' };
    const locked = render({ date: '2026-10-07', roomId: 'locked' }, slot, null);
    assert.equal(locked.disabled, true);
    assert.match(locked.className, /state-locked/);
    assert.match(locked.innerHTML, /Tạm ngừng nhận đặt phòng/);
    assert.equal(locked.listeners.length, 0);
    const open = render({ date: '2026-10-07', roomId: 'open' }, slot, null);
    assert.equal(open.listeners.length, 1);
    assert.match(open.className, /state-available/);
});
test('unlock restores selection using the same gallery/calendar room object', () => {
    const { render, rooms } = fixture();
    rooms[1].isBookingLocked = false;
    const button = render({ date: '2026-10-07', roomId: 'locked' }, { state: 'available', bookableStartUtc: '10:30', bookableEndUtc: '13:30' }, null);
    assert.equal(button.listeners.length, 1);
    assert.doesNotMatch(button.className, /state-locked/);
});
const refreshSource = source.slice(source.indexOf('        async function refreshAdmission('), source.indexOf('        function startAdmissionStream('));
test('admission refresh checks every visible room and rebuilds when another room becomes locked', async () => {
    const rooms = [{ id: 'a', isBookingLocked: false }, { id: 'b', isBookingLocked: false }];
    let resets = 0;
    const refresh = new Function('visibleRooms', 'document', 'state', 'fetchManyRooms', 'today', 'resetRoom', `let admissionRefreshing = false; ${refreshSource}; return refreshAdmission;`)(
        () => rooms, { hidden: false }, { requestVersion: 1 }, async (list, from, signal, days) => {
            assert.equal(list.length, 2);
            assert.equal(days, 1);
            return list.map(room => ({ room, data: { isBookingLocked: room.id === 'b' } }));
        }, '2026-10-07', () => resets++
    );
    await refresh();
    assert.equal(rooms[0].isBookingLocked, false);
    assert.equal(rooms[1].isBookingLocked, true);
    assert.equal(resets, 1);
});

test('scheduled closure refresh invalidates future calendar even when indefinite lock stays off', async () => {
    const room = { id: 'a', isBookingLocked: false, bookingScheduleUpdatedAtUtc: null };
    let resets = 0;
    let revision = '2026-10-09T15:00:00Z';
    const refresh = new Function('visibleRooms', 'document', 'state', 'fetchManyRooms', 'today', 'resetRoom', `let admissionRefreshing = false; ${refreshSource}; return refreshAdmission;`)(
        () => [room], { hidden: false }, { requestVersion: 1 }, async () => [{ room, data: { isBookingLocked: false, bookingScheduleUpdatedAtUtc: revision } }],
        '2026-10-09', () => resets++
    );
    await refresh();
    assert.equal(resets, 1);
    assert.equal(room.isBookingLocked, false);
    await refresh();
    assert.equal(resets, 1);
    revision = '2026-10-09T16:00:00Z';
    await refresh();
    assert.equal(resets, 2);
});
