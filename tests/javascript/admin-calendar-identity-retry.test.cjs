const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const vm = require('node:vm');
const source = readFileSync('src/DeLong.Web/wwwroot/js/pages/admin-calendar.js', 'utf8');

function fixture(uploadFailures) {
    let options;
    const calls = [], notices = [];
    const created = { id: 'b', roomId: 'r', customerId: 'c', code: 'BKNEW', status: 2, type: 0, paidAmount: 0, balanceAmount: 250000 };
    let failuresLeft = uploadFailures;
    const api = {
        async post(url, body) { calls.push({ type: 'post', url }); return created; },
        async put(url, body) { calls.push({ type: 'put', url }); return url.endsWith('/guest-details') ? {} : created; },
        async get(url) { calls.push({ type: 'get', url }); return created; },
        async postForm(url) {
            calls.push({ type: 'upload', url });
            if (failuresLeft-- > 0) { const error = new Error('Failed to fetch'); throw error; }
            return { side: url.split('/').pop() };
        }
    };
    const vue = { createApp(config) { options = config; return { mixin() { return this; }, mount() { return {}; } }; } };
    class FakeFormData { append() {} }
    const context = {
        document: { getElementById: id => id === 'calendar-page' ? {} : { textContent: '{}' }, dispatchEvent() {} },
        window: { DeLongCalendarCanManage: true, innerWidth: 1000, Vue: vue }, Vue: vue,
        DeLongApi: api, FormData: FakeFormData, CustomEvent: class {}, Intl,
        URL: { createObjectURL: () => 'blob:x', revokeObjectURL() {} },
        setTimeout: fn => fn()
    };
    vm.runInNewContext(source, context);
    const app = { ...options.data(), ...options.methods, propertyId: 'prop', bookings: [], selectedBooking: null, editor: { open: true, mode: 'create' } };
    for (const [name, getter] of Object.entries(options.computed)) Object.defineProperty(app, name, { get: () => getter.call(app), configurable: true });
    app.form = { ...app.emptyForm(), roomId: 'r', customerName: 'Guest', customerPhone: '0900000000', checkInLocal: '2026-11-12T14:00', checkOutLocal: '2026-11-12T17:00', roomAmount: 250000, initialPaymentAmount: 0 };
    app.paymentEdited = true;
    app.rooms = [{ id: 'r', capacity: 2, rates: [] }];
    app.notify = (text, type) => notices.push({ text, type });
    app.identityFiles = { front: { name: 'front.jpg' }, back: { name: 'back.jpg' } };
    app.identityPreviews = { front: 'blob:f', back: 'blob:b' };
    return { app, calls, notices };
}

test('a dropped CCCD upload is retried once and the form closes when it succeeds', async () => {
    const { app, calls } = fixture(1);
    await app.saveBooking();
    assert.equal(calls.filter(c => c.type === 'upload').length, 3);
    assert.equal(app.editor.open, false);
});

test('when CCCD still fails the new booking stays open in edit mode with the photos kept', async () => {
    const { app, calls, notices } = fixture(99);
    await app.saveBooking();
    assert.equal(calls.filter(c => c.type === 'post' && c.url.endsWith('/bookings')).length, 1);
    assert.equal(app.editor.open, true);
    assert.equal(app.editor.mode, 'edit');
    assert.equal(app.selectedBooking.code, 'BKNEW');
    assert.equal(app.identityFiles.front.name, 'front.jpg');
    assert.match(notices.at(-1).text, /BKNEW.*CCCD.*Lưu thay đổi/);
});
