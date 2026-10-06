const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const vm = require('node:vm');
const source = readFileSync('src/DeLong.Web/wwwroot/js/pages/sepay-payment.js', 'utf8');
function setup(status = 'Pending', expired = false) {
    const initial = { amount: 250000, qrUrl: 'https://vietqr.app/img?acc=123',
        transferContent: 'DH123', bank: 'VCB', accountNumber: '123', status,
        expiresAtUtc: new Date(Date.now() + (expired ? -1000 : 300000)).toISOString(), successUrl: '/booking/success' };
    let fetches = 0, srcWrites = 0, redirect;
    const nodes = new Map();
    const root = { dataset: { orderId: 'DH123' }, querySelector(selector) {
        if (!nodes.has(selector)) nodes.set(selector, { textContent: '', hidden: true,
            classList: { add() {} }, addEventListener() {},
            getAttribute: () => initial.qrUrl, removeAttribute() {},
            set src(value) { srcWrites++; } });
        return nodes.get(selector);
    } };
    root.querySelector('[data-sepay-initial]').textContent = JSON.stringify(initial);
    const timers = [];
    const window = { setInterval() {}, setTimeout(fn) { timers.push(fn); }, addEventListener() {},
        location: { assign(url) { redirect = url; } } };
    vm.runInNewContext(source, { window, document: { querySelector: () => root },
        fetch: async () => { fetches++; return { ok: true, json: async () => initial }; },
        Intl, Date, navigator: {}, clearTimeout() {}, clearInterval() {} });
    return { root, timers, fetches: () => fetches, writes: () => srcWrites, redirect: () => redirect };
}

test('Initial payment state displays immediately without a status roundtrip or resetting QR src', async () => {
    const env = setup();
    assert.equal(env.fetches(), 0);
    assert.equal(env.root.querySelector('[data-sepay-memo]').textContent, 'DH123');
    assert.equal(env.root.querySelector('[data-sepay-qr]').hidden, false);
    await env.timers[0]();
    assert.equal(env.fetches(), 1);
    assert.equal(env.writes(), 0);
});

test('Expired bootstrap never leaves QR visible', () => {
    const env = setup('Pending', true);
    assert.equal(env.root.querySelector('[data-sepay-qr]').hidden, true);
    assert.equal(env.root.querySelector('[data-sepay-download]').hidden, true);
});

test('Paid bootstrap redirects without waiting for next poll', () => {
    const env = setup('Succeeded');
    assert.equal(env.redirect(), '/booking/success');
    assert.equal(env.timers.length, 0);
});
