const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const vm = require('node:vm');
const source = readFileSync('src/DeLong.Web/wwwroot/js/pages/admin-calendar.js', 'utf8');
function fixture() {
    let options;
    const calls = [], events = [], notices = [];
    const old = { id: 'b', roomId: 'r', code: 'BK123', status: 2, type: 0, paidAmount: 100000, balanceAmount: 150000, specialSurchargeAmount: 0 };
    const updated = { ...old, paidAmount: 250000, balanceAmount: 0 };
    const api = {
        async put(url, body) { calls.push({ type: 'put', url, body }); return old; },
        async post(url, body) { calls.push({ type: 'post', url, body }); return url.endsWith('/payments') ? { id: 'p' } : old; },
        async get(url) { calls.push({ type: 'get', url }); return updated; }
    };
    vm.runInNewContext(source, {
        document: { getElementById: id => id === 'calendar-page' ? {} : { textContent: '{}' }, dispatchEvent: event => events.push(event) },
        window: { DeLongCalendarCanManage: true, innerWidth: 1000 },
        Vue: { createApp(config) { options = config; return { mixin() { return this; }, mount() { return {}; } }; } },
        DeLongApi: api, CustomEvent: class { constructor(type, data) { this.type = type; this.detail = data.detail; } }, Intl, URL
    });
    const app = { ...options.data(), ...options.methods, propertyId: 'prop', bookings: [old], selectedBooking: old, editor: { open: true, mode: 'edit' } };
    for (const [name, getter] of Object.entries(options.computed)) Object.defineProperty(app, name, { get: () => getter.call(app), configurable: true });
    app.form = { ...app.emptyForm(), roomId: 'r', customerName: 'Guest', customerPhone: '0900000000', checkInLocal: '2026-10-12T14:00', checkOutLocal: '2026-10-12T17:00', roomAmount: 250000, initialPaymentAmount: 150000 };
    app.rooms = [{ id: 'r', capacity: 2, rates: [] }];
    app.notify = (text, type) => notices.push({ text, type });
    app.saveGuestDetails = async () => {};
    return { app, api, calls, events, notices, old, updated };
}
test('editing a booking appends a receipt and refreshes its outstanding balance', async () => {
    const { app, calls, events } = fixture();
    await app.saveBooking();
    assert.deepEqual(calls.map(c => c.type), ['put', 'post', 'get']);
    assert.match(calls[1].url, /\/bookings\/b\/payments$/);
    assert.equal(calls[1].body.amount, 150000);
    assert.equal(calls[1].body.type, 0);
    assert.equal(app.selectedBooking.balanceAmount, 0);
    assert.equal(app.bookings[0].paidAmount, 250000);
    assert.equal(app.form.initialPaymentAmount, 0);
    assert.equal(events[0].type, 'delong:operations-change');
});
test('completed bookings can receive outstanding money without editing their stay', async () => {
    const { app, calls } = fixture();
    app.selectedBooking.status = 4;
    app.additionalPayment = { amount: 150000, method: 0 };
    await app.saveAdditionalPayment();
    assert.deepEqual(calls.map(c => c.type), ['post', 'get']);
    assert.equal(calls[0].body.method, 0);
    assert.equal(app.additionalPayment.amount, 0);
    assert.equal(app.selectedBooking.balanceAmount, 0);
});
test('edit validates payment against the remaining amount including existing surcharge', () => {
    const { app } = fixture();
    app.selectedBooking.specialSurchargeAmount = 50000;
    assert.equal(app.paymentLimit, 200000);
    app.form.initialPaymentAmount = 200001;
    assert.match(app.validateForm(), /không được lớn hơn/);
    app.form.initialPaymentAmount = 200000;
    assert.equal(app.validateForm(), null);
});
test('a successful receipt is cleared even if reading the refreshed booking fails', async () => {
    const { app, api, calls, events } = fixture();
    api.get = async () => { throw new Error('Read failed'); };
    await app.saveBooking();
    assert.equal(calls.filter(c => c.type === 'post').length, 1);
    assert.equal(app.form.initialPaymentAmount, 0);
    assert.equal(app.editor.open, false);
    assert.equal(events.length, 1);
});
test('payment failure reports partial success and never pretends the debt is settled', async () => {
    const { app, api, notices } = fixture();
    api.post = async () => { throw new Error('Payment denied'); };
    await app.saveBooking();
    assert.equal(app.selectedBooking.balanceAmount, 150000);
    assert.ok(notices.some(n => n.type === 'error' && n.text.includes('Kiểm tra lịch sử thanh toán')));
    assert.equal(app.editor.open, false);
});
test('invalid amounts, cancelled bookings and double clicks do not send another receipt', async () => {
    const { app, calls } = fixture();
    for (const amount of [0, -1, Infinity, 150001]) {
        app.additionalPayment.amount = amount;
        await app.saveAdditionalPayment();
    }
    app.additionalPayment.amount = 150000;
    app.selectedBooking.status = 5;
    await app.saveAdditionalPayment();
    app.selectedBooking.status = 2;
    app.saving = true;
    await app.saveAdditionalPayment();
    await app.saveBooking();
    assert.equal(calls.length, 0);
});
