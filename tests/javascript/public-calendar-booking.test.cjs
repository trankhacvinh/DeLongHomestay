const { readFileSync } = require('node:fs');
const { resolve } = require('node:path');
const assert = require('node:assert/strict');
const { test } = require('node:test');

const source = readFileSync(resolve(__dirname, '../../src/DeLong.Web/wwwroot/js/pages/public-availability-calendar.js'), 'utf8');
const start = source.indexOf('        function bookingUrl(room) {');
const end = source.indexOf('\n        const slotKey', start);
assert.ok(start >= 0 && end > start, 'Calendar booking URL function must exist');
const createBookingUrl = new Function('window', 'state', 'selectedTimeWindow', `${source.slice(start, end)}\nreturn bookingUrl;`);

for (const adjusted of [false, true]) {
    test(`Selected slots open the embedded booking with adjusted=${adjusted}`, () => {
        const state = { selected: [
            { date: '2026-10-01', slot: { rateId: 'evening' } },
            { date: '2026-10-01', slot: { rateId: 'overnight' } }
        ] };
        const schedule = { startUtc: '2026-10-01T10:30:00Z', endUtc: '2026-10-02T02:30:00Z', adjusted };
        const bookingUrl = createBookingUrl({ location: { origin: 'https://delonghomestay.com' } }, state, () => schedule);
        const url = new URL(bookingUrl({ bookingUrl: '/booking', code: 'COCO-01' }), 'https://delonghomestay.com');
        assert.equal(url.pathname, '/booking');
        assert.equal(url.searchParams.get('embed'), '1');
        assert.equal(url.searchParams.get('room'), 'COCO-01');
        assert.equal(url.searchParams.get('slots'), '2026-10-01:evening,2026-10-01:overnight');
        assert.equal(url.searchParams.get('effectiveCheckIn'), schedule.startUtc);
        assert.equal(url.searchParams.get('effectiveCheckOut'), schedule.endUtc);
        assert.equal(url.searchParams.has('scheduleNote'), adjusted);
    });
}
