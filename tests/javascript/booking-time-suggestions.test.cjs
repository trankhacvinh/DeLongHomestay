const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const vm = require('node:vm');

function fixture() {
    let options;
    const api = { get: async () => ({ rates: [suggestion()] }) };
    const vue = { createApp(config) { options = config; return { mixin() { return this; }, mount() { return {}; } }; } };
    const context = {
        document: { getElementById: id => id === 'calendar-page' ? {} : { textContent: '{}' } },
        window: { Vue: vue, DeLongCalendarCanManage: true, innerWidth: 1000 }, Vue: vue, DeLongApi: api, Intl, URL
    };
    vm.runInNewContext(readFileSync('src/DeLong.Web/wwwroot/js/pages/admin-calendar-nightly.js', 'utf8'), context);
    vm.runInNewContext(readFileSync('src/DeLong.Web/wwwroot/js/pages/admin-calendar.js', 'utf8'), context);
    const app = { ...options.data(), ...options.methods, propertyId: 'prop', selectedDayKey: '2031-01-10', editor: { open: true, mode: 'create' } };
    app.rooms = [{ id: 'r', capacity: 2, rates: [{ id: 'third', name: 'Khung 3', startTime: '18:00', endTime: '21:00', type: 0, price: 210000, isActive: true, sortOrder: 3 }] }];
    for (const [name, getter] of Object.entries(options.computed)) Object.defineProperty(app, name, { get: () => getter.call(app) });
    app.form = { ...app.emptyForm(), roomId: 'r', rateId: 'third', customerName: 'Synthetic', customerPhone: '0900000000', guestCount: 2 };
    app.applyRate();
    return { app, api };
}
function suggestion(start = '2031-01-10T11:20:00Z') {
    return { rateId: 'third', defaultStartUtc: '2031-01-10T11:00:00Z', suggestedStartUtc: start, endUtc: '2031-01-10T14:00:00Z', isAdjusted: true };
}

test('fresh actual checkout suggestions update both option label and preset inputs without changing price or end', async () => {
    const { app, api } = fixture();
    await app.loadRateSuggestions();
    assert.equal(app.selectedRoomRateOptions[0].labelStart, '18:20');
    assert.equal(app.form.checkInLocal, '2031-01-10T18:20');
    assert.equal(app.form.checkOutLocal, '2031-01-10T21:00');
    assert.equal(app.form.roomAmount, 210000);
    api.get = async () => ({ rates: [suggestion('2031-01-10T11:40:00Z')] });
    await app.loadRateSuggestions();
    assert.equal(app.selectedRoomRateOptions[0].labelStart, '18:40');
    assert.equal(app.form.checkInLocal, '2031-01-10T18:40');
});
test('async response never overwrites a staff member typing custom dates', async () => {
    const { app, api } = fixture();
    let resolve;
    api.get = () => new Promise(done => { resolve = done; });
    const pending = app.loadRateSuggestions();
    app.form.checkInLocal = '2031-01-10T19:00';
    app.bookingTimeChanged();
    resolve({ rates: [suggestion()] }); await pending;
    assert.equal(app.form.checkInLocal, '2031-01-10T19:00');
    assert.equal(app.selectedRoomRateOptions[0].labelStart, '18:20');
    app.applyRate(); assert.equal(app.form.checkInLocal, '2031-01-10T18:20');
});
test('stale room/day responses and responses after closing/editing are discarded', async () => {
    for (const change of [app => { app.form.roomId = 'other'; }, app => { app.selectedDayKey = '2031-01-11'; }, app => { app.editor.open = false; }, app => { app.editor.mode = 'edit'; }]) {
        const { app, api } = fixture(); let resolve;
        api.get = () => new Promise(done => { resolve = done; });
        const pending = app.loadRateSuggestions(); change(app); resolve({ rates: [suggestion()] }); await pending;
        assert.equal(app.rateSuggestions.scope, '');
        assert.equal(app.form.checkInLocal, '2031-01-10T18:00');
    }
});
test('edit mode uses existing rate times and never fetches or applies suggestions', async () => {
    const { app, api } = fixture();
    await app.loadRateSuggestions(); app.editor.mode = 'edit';
    api.get = () => { throw Error('Must not fetch'); };
    await app.loadRateSuggestions(); app.applyRate();
    assert.equal(app.selectedRoomRateOptions[0].labelStart, '18:00');
    assert.equal(app.form.checkInLocal, '2031-01-10T18:00');
});
test('room and day changes refresh suggestions through the production nightly extension', async () => {
    const { app, api } = fixture(); const calls = [];
    api.get = async url => { calls.push(url); return { rates: [suggestion()] }; };
    app.roomChanged();
    await new Promise(setImmediate);
    assert.equal(app.form.checkInLocal, '2031-01-10T18:20');
    app.form.checkInLocal = '2031-01-11T19:00'; app.bookingTimeChanged();
    await new Promise(setImmediate);
    assert.equal(calls.length, 2);
    assert.match(calls[1], /date=2031-01-11$/);
    assert.equal(app.form.checkInLocal, '2031-01-11T19:00');
});
test('unavailable presets and pending requests cannot submit; failures are visible and retryable', async () => {
    const { app, api } = fixture();
    app.rateSuggestions.loading = true;
    assert.match(app.validateForm(), /Đang cập nhật/);
    api.get = async () => { throw Error('Network failure'); };
    await app.loadRateSuggestions();
    assert.equal(app.rateSuggestions.error, 'Network failure'); assert.equal(app.rateSuggestions.loading, false);
    api.get = async () => ({ rates: [suggestion(null)] }); await app.loadRateSuggestions();
    assert.equal(app.rateSuggestions.error, ''); assert.equal(app.selectedRoomRateOptions[0].unavailable, true);
    assert.match(app.validateForm(), /không còn khoảng trống/);
    app.form.rateId = ''; assert.equal(app.validateForm(), null);
});
