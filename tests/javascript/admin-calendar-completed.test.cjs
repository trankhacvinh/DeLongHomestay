const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const source = readFileSync('src/DeLong.Web/wwwroot/js/pages/admin-calendar-v2.js', 'utf8');
const visualSource = source.slice(source.indexOf('    function bookingVisual('), source.indexOf('    function bookingGuestText('));
const visual = new Function('state', `${visualSource}; return bookingVisual;`)({ colors: {} });
test('completed debt stays red and paid history uses configured colors without a completed override', () => {
    assert.equal(visual({ status: 4, balanceAmount: 100000, hasSpecialRequest: true }).key, 'unpaid');
    assert.equal(visual({ status: 4, balanceAmount: 0, hasSpecialRequest: true }).key, 'special');
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

test('manual colors override appearance while retaining real state and room block color', () => {
    const debt = visual({ status: 2, balanceAmount: 100000, displayColor: '#397967' });
    assert.equal(debt.key, 'unpaid');
    assert.equal(debt.color, '#C94B4B');
    assert.equal(visual({ status: 2, balanceAmount: 100000, displayColor: null }).color, '#C94B4B');
    assert.equal(visual({ isRoomBlock: true, displayColor: '#397967' }).color, '#64748B');
    assert.equal(visual({ status: 4, displayColor: '#7C5CC4' }).color, '#7C5CC4');
});

test('debt overrides notes and manual color; paid notes use special request color', () => {
    const result = visual({status:2,balanceAmount:100000,displayColor:'#397967',hasSpecialRequest:true});
    assert.equal(result.key,'unpaid');assert.equal(result.color,'#C94B4B');
    const emptyNote = visual({status:2,balanceAmount:100000,displayColor:'#397967',hasSpecialRequest:false});
    assert.equal(emptyNote.key,'unpaid');assert.equal(emptyNote.color,'#C94B4B');
    assert.equal(visual({status:2,balanceAmount:0,displayColor:'#397967',hasSpecialRequest:true}).color,'#7C5CC4');
});

test('configured debt and automatic colors apply to completed and active bookings', () => {
    const configured = new Function('state', `${visualSource}; return bookingVisual;`)({colors:{unpaidColor:'#FF0000',confirmedColor:'#003399',specialRequestColor:'#880088'}});
    for (const status of [0,1,2,3,4]) {
        assert.equal(configured({status,balanceAmount:1,displayColor:'#00FF00',hasSpecialRequest:true}).color,'#FF0000');
    }
    assert.equal(configured({status:4,balanceAmount:0}).color,'#003399');
    assert.equal(configured({status:4,balanceAmount:0,displayColor:'#ABCDEF'}).color,'#ABCDEF');
    assert.equal(configured({status:4,balanceAmount:0,hasSpecialRequest:true}).color,'#880088');
});

test('configured mixed and checked-in colors are used, with debt and notes retaining priority', () => {
    const configured = new Function('state', `${visualSource}; return bookingVisual;`)({ colors: {
        mixedSlotColor: '#FF8800', checkedInColor: '#123456', confirmedColor: '#FFA55F',
        unpaidColor: '#FF2929', specialRequestColor: '#F34DFF', flexibleTimeColor: '#FAFE01'
    } });
    assert.equal(configured({ status: 2, isMixedSlot: true }).color, '#FF8800');
    assert.equal(configured({ status: 3 }).color, '#123456');
    assert.equal(configured({ status: 2 }).color, '#FFA55F');
    assert.equal(configured({ status: 4, isMixedSlot: true }).color, '#FF8800');
    assert.equal(configured({ status: 3, isMixedSlot: true }).color, '#FF8800');
    assert.equal(configured({ isMixedSlot: true, balanceAmount: 1 }).color, '#FF2929');
    assert.equal(configured({ isMixedSlot: true, hasSpecialRequest: true }).color, '#F34DFF');
    assert.equal(configured({ isMixedSlot: true, isFlexibleTime: true }).color, '#FAFE01');
});

test('history overlay respects server classification even when old rate metadata disagrees', () => {
    const base = { id: 'old', roomId: 'r', type: 0, status: 4, roomRateId: 'first-preset', rateSegmentCount: 0,
        checkInUtc: '2026-10-26T03:30:00Z', checkOutUtc: '2026-10-26T10:00:00Z', isFlexibleTime: false, isMixedSlot: true };
    const history = new Function('vm', 'currentRoom', `${historySource}; return completedBookingsForSlot;`)(
        () => ({ bookings: [base] }), () => ({ id: 'r' }));
    const row = history({ startUtc: base.checkInUtc, endUtc: base.checkOutUtc, occupied: [] })[0];
    assert.equal(row.isMixedSlot, true);
    assert.equal(row.isFlexibleTime, false);
});
