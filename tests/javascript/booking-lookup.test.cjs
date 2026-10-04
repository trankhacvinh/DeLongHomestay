const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const { resolve } = require('node:path');
const vm = require('node:vm');

function setup() {
    let options;
    const calls = [];
    const Vue = { createApp(value) { options = value; return { mount() {} }; } };
    const window = { Vue };
    vm.runInNewContext(readFileSync(resolve(__dirname, '../../src/DeLong.Web/wwwroot/js/pages/booking-lookup.js'), 'utf8'), {
        window, Vue,
        document: { getElementById: () => ({ dataset: { siteSlug: 'de-long' } }) },
        DeLongApi: { async post(url, data) { calls.push({ url, data }); return { message: 'Kiểm tra email', code: 'BK7M4P9X2A' }; } }
    });
    const state = options.data();
    for (const [name, method] of Object.entries(options.methods)) state[name] = method.bind(state);
    Object.defineProperty(state, 'canSubmit', { get: () => options.computed.canSubmit.call(state) });
    return { state, calls };
}

test('Email lookup sends only email and never displays a booking result', async () => {
    const { state, calls } = setup();
    state.mode = 'email';
    state.email = 'guest@example.com';
    state.form.code = 'old-code';
    state.form.phone = '0901234567';
    await state.lookup();
    assert.equal(calls[0].url, '/api/public/booking-lookup/email?siteSlug=de-long');
    assert.deepEqual(JSON.parse(JSON.stringify(calls[0].data)), { email: 'guest@example.com' });
    assert.equal(state.result, null);
    assert.equal(state.message, 'Kiểm tra email');
    assert.equal(state.loading, false);
});

test('Code and phone lookup keeps existing endpoint and displays its result', async () => {
    const { state, calls } = setup();
    state.form.code = 'BK-260815-XXXXXXXXXX';
    state.form.phone = '0901234567';
    await state.lookup();
    assert.equal(calls[0].url, '/api/public/booking-lookup?siteSlug=de-long');
    assert.equal(state.result.code, 'BK7M4P9X2A');
    state.resetResult();
    assert.equal(state.result, null);
    assert.equal(state.message, '');
});

test('Invalid email does not send a request', async () => {
    const { state, calls } = setup();
    state.mode = 'email';
    state.email = 'invalid';
    await state.lookup();
    assert.equal(calls.length, 0);
});
