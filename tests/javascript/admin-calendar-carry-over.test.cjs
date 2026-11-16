const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const vm = require('node:vm');

const source = readFileSync('src/DeLong.Web/wwwroot/js/pages/admin-calendar-overview.js', 'utf8');
const block = source.slice(source.indexOf('    const timeFormatters ='), source.indexOf('    function minutesOfDay('));
const context = { Intl, Date, Map, Set };
vm.runInNewContext(`${block}\nthis.carryOverRanges = carryOverRanges;`, context);
const tz = 'Asia/Ho_Chi_Minh';

// 15/11 overnight 22:00 → 16/11 10:30 (local) = 15:00Z → 03:30Z
const overnight = { bookingId: 'b1', customerName: 'Anh trung', startUtc: '2026-11-15T15:00:00Z', endUtc: '2026-11-16T03:30:00Z' };
const previous = { date: '2026-11-15', slots: [{ occupied: [] }, { occupied: [overnight] }] };

test('An overnight stay ending this morning is carried to the next row', () => {
    const day = { date: '2026-11-16', slots: [{ occupied: [] }, { occupied: [] }] };
    const carried = context.carryOverRanges(previous, day, tz);
    assert.equal(carried.length, 1);
    assert.equal(carried[0].bookingId, 'b1');
});

test('A stay already drawn on the row is not repeated', () => {
    const day = { date: '2026-11-16', slots: [{ occupied: [overnight] }, { occupied: [] }] };
    assert.equal(context.carryOverRanges(previous, day, tz).length, 0);
});

test('A same-day stay and a stay ending at midnight are not carried', () => {
    const sameDay = { bookingId: 'b2', startUtc: '2026-11-15T04:30:00Z', endUtc: '2026-11-15T07:30:00Z' };
    const toMidnight = { bookingId: 'b3', startUtc: '2026-11-15T14:00:00Z', endUtc: '2026-11-15T17:00:00Z' };
    const prev = { date: '2026-11-15', slots: [{ occupied: [sameDay, toMidnight] }] };
    const day = { date: '2026-11-16', slots: [{ occupied: [] }] };
    assert.equal(context.carryOverRanges(prev, day, tz).length, 0);
    assert.equal(context.carryOverRanges(null, day, tz).length, 0);
});

test('The carry-over strip lets taps through while the first slot still has free time', () => {
    const passiveBlock = source.slice(source.indexOf('    function carryIsPassive('), source.indexOf('    function minutesOfDay('));
    const ctx = {};
    vm.runInNewContext(`${passiveBlock}\nthis.carryIsPassive = carryIsPassive;`, ctx);
    assert.equal(ctx.carryIsPassive({ free: [{ startUtc: '2026-11-16T04:30:00Z', endUtc: '2026-11-16T07:30:00Z' }] }), true);
    assert.equal(ctx.carryIsPassive({ free: [] }), false);
    assert.equal(ctx.carryIsPassive(null), false);
    const css = readFileSync('src/DeLong.Web/wwwroot/css/admin-calendar-overview.css', 'utf8');
    assert.match(css, /\.cal-ov-carry\.is-passive\{pointer-events:none\}/);
});
