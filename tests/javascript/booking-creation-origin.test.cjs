const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const vm = require('node:vm');

function fixture() {
    let options;
    vm.runInNewContext(readFileSync('src/DeLong.Web/wwwroot/js/pages/admin-bookings.js', 'utf8'), {
        document: { getElementById: id => id === 'bookings-page-data' ? { textContent: JSON.stringify({ propertyId: 'p', timeZoneId: 'Asia/Ho_Chi_Minh' }) } : {} },
        Vue: { createApp: value => { options = value; return { mixin() { return this; }, mount() {} }; } },
        window: {}, Intl
    });
    const state = options.data();
    for (const [name, method] of Object.entries(options.methods)) state[name] = method.bind(state);
    for (const [name, computed] of Object.entries(options.computed)) Object.defineProperty(state, name, { get: () => computed.call(state) });
    const row = (id, creationOrigin, extra = {}) => ({ id, creationOrigin, code: id, customerName: 'Khách mẫu', customerPhone: '0900000000', roomName: 'Blue', status: 2,
        createdAtUtc: '2026-11-07T03:00:00Z', checkInUtc: '2026-11-20T07:00:00Z', balanceAmount: 100, ...extra });
    state.todayDateKey = '2026-11-07';
    state.bookings = [row('customer', 1, { source: 'Zalo' }), row('staff', 2, { source: 'Website' }), row('unknown', 0, { source: 'Website' }), row('missing', undefined)];
    return { state, row };
}

test('creator filter uses recorded origin rather than mutable booking source', () => {
    const { state } = fixture();
    assert.equal(state.creationOriginFilter, '');
    state.creationOriginFilter = '1';
    assert.deepEqual(Array.from(state.filteredBookings, row => row.id), ['customer']);
    state.creationOriginFilter = '2';
    assert.deepEqual(Array.from(state.filteredBookings, row => row.id), ['staff']);
    state.creationOriginFilter = '0';
    assert.deepEqual(Array.from(state.filteredBookings, row => row.id), ['unknown', 'missing']);
    state.creationOriginFilter = '';
    assert.equal(state.filteredBookings.length, 4);
});

test('creator filter composes with date, status, search and debt without changing daily total', () => {
    const { state, row } = fixture();
    state.bookings.push(row('paid', 1, { balanceAmount: 0 }), row('cancelled', 1, { status: 5 }), row('old', 1, { createdAtUtc: '2026-11-06T03:00:00Z' }));
    Object.assign(state, { creationOriginFilter: '1', createdFrom: '2026-11-07', createdTo: '2026-11-07', dateFrom: '2026-11-20', dateTo: '2026-11-20', statusFilter: '2', debtOnly: true, search: 'blue' });
    assert.deepEqual(Array.from(state.filteredBookings, row => row.id), ['customer']);
    assert.equal(state.filteredDebtSummary.count, 1);
    assert.equal(state.filteredDebtSummary.amount, 100);
    assert.equal(state.createdTodayCount, 6);
    state.creationOriginFilter = '2';
    assert.deepEqual(Array.from(state.filteredBookings, row => row.id), ['staff']);
    assert.equal(state.createdTodayCount, 6);
    state.search = 'no match';
    assert.equal(state.filteredBookings.length, 0);
});

test('creator labels handle known origins and missing legacy values', () => {
    const { state } = fixture();
    assert.equal(state.creationOriginText(1), 'Khách tự đặt');
    assert.equal(state.creationOriginText(2), 'Nhân viên tạo');
    for (const value of [0, null, undefined]) assert.equal(state.creationOriginText(value), 'Chưa xác định');
});
