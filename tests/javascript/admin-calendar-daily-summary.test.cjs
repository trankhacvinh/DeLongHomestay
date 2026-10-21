const {test} = require('node:test');
const assert = require('node:assert/strict');
const {readFileSync} = require('node:fs');
const vm = require('node:vm');
const script = readFileSync('src/DeLong.Web/wwwroot/js/pages/admin-calendar-daily-summary.js', 'utf8');
function controller(get) {
    const document = { createElement: tag => ({ tag, children: [], textContent: '', attributes: {}, append(...children) { this.children.push(...children); },
        setAttribute(key, value) { this.attributes[key] = value; }, addEventListener() {} }) };
    const scope = { window: {}, document, Intl, URLSearchParams, DeLongApi: { get } };
    vm.runInNewContext(script, scope);
    return scope.window.DeLongCalendarDailySummary.create({ propertyId: 'branch', timeZone: 'Asia/Ho_Chi_Minh', openBooking() {} });
}
test('daily summary preserves separate booking and net receipt amounts and property scope', async () => {
    const urls = [];
    const c = controller(async url => { urls.push(url); return [{ date:'2026-10-17', bookingCount:2, bookingValue:550000, netReceipts:-50000 }]; });
    await c.load('2026-10-17',1);
    assert.equal(urls[0], '/api/admin/properties/branch/bookings/daily-summary?from=2026-10-17&days=1');
    const button = c.button('2026-10-17');
    assert.equal(button.children[0].textContent,'2 lượt');
    assert.match(button.children[1].textContent,/550\.000/);
    assert.match(button.children[2].textContent,/-50\.000/);
    assert.match(button.attributes['aria-label'],/toàn cơ sở/);
});
test('failed summary never displays a false zero and can be retried', async () => {
    let fail = true;
    const c = controller(async () => { if (fail) throw new Error('offline'); return [{date:'2026-10-17',bookingCount:3}]; });
    await c.load('2026-10-17',1);
    assert.equal(c.button('2026-10-17').textContent, 'Xem thống kê ngày');
    fail = false; await c.load('2026-10-17',1);
    assert.equal(c.button('2026-10-17').children[0].textContent, '3 lượt');
});
test('stale responses cannot replace the latest date range', async () => {
    const replies = [];
    const c = controller(() => new Promise(resolve => replies.push(resolve)));
    const first = c.load('2026-10-17',1), second = c.load('2026-10-18',1);
    replies[1]([{date:'2026-10-18',bookingCount:5}]); await second;
    replies[0]([{date:'2026-10-17',bookingCount:9}]); await first;
    assert.equal(c.button('2026-10-18').children[0].textContent,'5 lượt');
    assert.equal(c.button('2026-10-17').textContent,'Xem thống kê ngày');
});
