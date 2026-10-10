const { readFileSync } = require('node:fs');
const { resolve } = require('node:path');
const assert = require('node:assert/strict');
const { test } = require('node:test');
const source = readFileSync(resolve(__dirname, '../../src/DeLong.Web/wwwroot/js/pages/admin-calendar.js'), 'utf8');
const actionsStart = source.indexOf('            nextActions(booking) {');
const actionsEnd = source.indexOf('            requestStatusChange(action)', actionsStart);
const actions = new Function(`return ({${source.slice(actionsStart, actionsEnd)}}).nextActions;`)();
test('calendar no longer exposes check-in/check-out or a second confirmation for held rooms', () => {
    for (const status of [1, 2, 3, 4]) {
        const result = actions({ status });
        assert.ok(result.every(action => ![2, 3, 4].includes(action.status)));
    }
    assert.equal(actions({ status: 0 })[0].status, 2);
    assert.equal(actions({ status: 6 })[0].status, 2);
});
const formStart = source.indexOf('            emptyForm() {');
const formEnd = source.indexOf('            },', formStart) + '            },'.length;
const emptyForm = new Function(`return ({${source.slice(formStart, formEnd)}}).emptyForm;`)();
const saveStart = source.indexOf('            async saveBooking() {');
const saveEnd = source.indexOf('            resetGuestEditor()', saveStart);
for (const payment of [0, 50000]) {
    test(`manual calendar admission records only an explicitly entered payment (${payment})`, async () => {
        const calls = [];
        const booking = { id: 'booking', code: 'BKTEST', paidAmount: 0 };
        const api = { post: async (url, body) => { calls.push({ url, body }); return booking; }, get: async () => ({ ...booking, paidAmount: payment }) };
        const save = new Function('DeLongApi', 'utcOffset', `return ({${source.slice(saveStart, saveEnd)}}).saveBooking;`)(api, '+07:00');
        const app = { selectedRoom: { id: 'room' }, selectedRoomRates: [], propertyId: 'branch', bookings: [], editor: { mode: 'create', open: true },
            validateForm: () => null, saveGuestDetails: async () => {}, notify() {}, money: String };
        app.form = emptyForm.call(app);
        app.form.initialPaymentAmount = payment;
        app.form.checkInLocal = '2026-10-09T14:00';
        app.form.checkOutLocal = '2026-10-09T17:00';
        await save.call(app);
        assert.equal(calls[0].body.status, 2);
        assert.equal(calls.length, payment > 0 ? 2 : 1);
        assert.equal(app.bookings[0].paidAmount, payment);
        if (payment > 0) assert.equal(calls[1].body.amount, payment);
        assert.equal(app.editor.open, false);
    });
}
