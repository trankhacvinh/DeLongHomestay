const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const source = readFileSync('src/DeLong.Web/wwwroot/js/pages/admin-calendar-v2.js', 'utf8');
const visualSource = source.slice(source.indexOf('    function bookingVisual('), source.indexOf('    function bookingGuestText('));
const visual = new Function('state', `${visualSource}; return bookingVisual;`)({ colors: {} });
test('completed debt stays red and paid history returns to completed style', () => {
    assert.equal(visual({ status: 4, balanceAmount: 100000, hasSpecialRequest: true }).key, 'unpaid');
    assert.equal(visual({ status: 4, balanceAmount: 0, hasSpecialRequest: true }).key, 'completed');
    assert.equal(visual({ status: 2, balanceAmount: 100000 }).key, 'unpaid');
});
const historySource = source.slice(source.indexOf('    function completedBookingsForSlot('), source.indexOf('    function renderSlot('));
test('completed occupancy is not drawn a second time by the history overlay', () => {
    const booking = { id: 'completed', roomId: 'blue', status: 4, checkInUtc: '2026-10-11T07:00:00Z', checkOutUtc: '2026-10-11T10:00:00Z' };
    const history = new Function('vm', 'currentRoom', `${historySource}; return completedBookingsForSlot;`)(
        () => ({ bookings: [booking] }), () => ({ id: 'blue' }));
    const slot = { startUtc: booking.checkInUtc, endUtc: booking.checkOutUtc, occupied: [{ bookingId: booking.id }] };
    assert.equal(history(slot).length, 0);
    assert.equal(history({ ...slot, occupied: [] }).length, 1);
});
