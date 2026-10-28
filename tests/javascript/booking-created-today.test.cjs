const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');

function fixture(timeZoneId = 'Asia/Ho_Chi_Minh') {
    let options, tick, cleared;
    const vue = { createApp(config) { options = config; return { mixin() { return this; }, mount() {} }; } };
    vm.runInNewContext(fs.readFileSync('src/DeLong.Web/wwwroot/js/pages/admin-bookings.js', 'utf8'), {
        document: { getElementById: id => id === 'bookings-page' ? {} : { textContent: JSON.stringify({ propertyId: 'p', timeZoneId }) } },
        Vue: vue, window: {}, Intl,
        setInterval(callback, ms) { tick = callback; assert.equal(ms, 60000); return 7; },
        clearInterval(id) { cleared = id; }
    });
    const app = { ...options.data(), todayDateKey: '2026-10-28' };
    Object.defineProperty(app, 'createdTodayCount', { get: () => options.computed.createdTodayCount.call(app) });
    return { app, options, tick: () => tick(), cleared: () => cleared };
}

test('counts persisted bookings by local creation day, including online/staff and any stay dates/statuses', () => {
    const { app } = fixture();
    app.bookings = [
        { createdAtUtc: '2026-10-27T16:59:59Z' },
        { createdAtUtc: '2026-10-27T17:00:00Z', checkInUtc: '2027-01-01T11:00:00Z', source: 'Website', status: 1 },
        { createdAtUtc: '2026-10-28T16:59:59Z', checkInUtc: '2026-10-27T11:00:00Z', source: 'Phone', status: 2 },
        { createdAtUtc: '2026-10-28T10:00:00Z', source: null, status: 5 },
        { createdAtUtc: '2026-10-28T17:00:00Z' },
        { checkInUtc: '2026-10-28T11:00:00Z' }, { createdAtUtc: 'invalid' }, { createdAtUtc: null }
    ];
    assert.equal(app.createdTodayCount, 3);
    Object.assign(app, { debtOnly: true, search: 'no match', dateFrom: '2027-01-01', dateTo: '2027-01-02', statusFilter: '4' });
    assert.equal(app.createdTodayCount, 3);
});
test('count updates when refreshed booking list changes and when the local day rolls over', () => {
    const { app } = fixture();
    app.bookings = [{ createdAtUtc: '2026-10-28T02:00:00Z' }];
    assert.equal(app.createdTodayCount, 1);
    app.bookings.splice(0, app.bookings.length, { createdAtUtc: '2026-10-28T10:00:00Z' }, { createdAtUtc: '2026-10-29T10:00:00Z' });
    assert.equal(app.createdTodayCount, 1);
    app.todayDateKey = '2026-10-29'; assert.equal(app.createdTodayCount, 1);
    app.bookings.push({ createdAtUtc: '2026-10-29T11:00:00Z' }); assert.equal(app.createdTodayCount, 2);
});
test('day boundaries follow the property timezone rather than browser/UTC dates', () => {
    const { app } = fixture('America/New_York');
    app.bookings = [{ createdAtUtc: '2026-10-28T03:59:59Z' }, { createdAtUtc: '2026-10-28T04:00:00Z' }, { createdAtUtc: '2026-10-29T03:59:59Z' }, { createdAtUtc: '2026-10-29T04:00:00Z' }];
    assert.equal(app.createdTodayCount, 2);
});
test('an idle page refreshes its local day and releases its timer on unmount', () => {
    const f = fixture();
    f.options.mounted.call(f.app); f.app.todayDateKey = '1900-01-01'; f.tick();
    assert.match(f.app.todayDateKey, /^\d{4}-\d{2}-\d{2}$/);
    assert.notEqual(f.app.todayDateKey, '1900-01-01');
    f.options.beforeUnmount.call(f.app); assert.equal(f.cleared(), 7);
});
