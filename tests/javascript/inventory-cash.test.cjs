const test = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const path = 'src/DeLong.Web/wwwroot/js/pages/';

function component(name, initial, api) {
    let options;
    const context = vm.createContext({ window: {}, Intl, Date, URLSearchParams, FormData, crypto: require('node:crypto').webcrypto,
        document: { getElementById: id => id.endsWith('-data') ? { textContent: JSON.stringify(initial) } : {} },
        DeLongApi: api, Vue: { createApp: value => { options = value; return { mount() {} }; } } });
    vm.runInContext(fs.readFileSync(path + 'operational-forms.js', 'utf8'), context);
    vm.runInContext(fs.readFileSync(path + name, 'utf8'), context);
    const state = options.data();
    for (const [key, fn] of Object.entries(options.methods)) state[key] = fn.bind(state);
    for (const [key, fn] of Object.entries(options.computed)) Object.defineProperty(state, key, { get: () => fn.call(state) });
    return { state, helpers: context.window.DeLongOperations };
}

test('local date conversion uses branch timezone and rejects nonexistent DST times', () => {
    const { helpers } = component('admin-cash.js', { propertyId: 'a', scope: 'a', month: '2026-10', snapshot: {}, rooms: [], properties: [], editableProperties: [] }, {});
    assert.equal(helpers.toUtc('2026-10-01T00:00', 'Asia/Ho_Chi_Minh'), '2026-09-30T17:00:00.000Z');
    assert.equal(helpers.localInput('2026-09-30T17:00:00Z', 'Asia/Ho_Chi_Minh'), '2026-10-01T00:00');
    assert.throws(() => helpers.toUtc('2026-03-08T02:30', 'America/New_York'));
    assert.throws(() => helpers.toUtc('', 'Asia/Ho_Chi_Minh'));
    assert.match(helpers.quantity(1.125), /1,125/);
});

test('inventory retry keeps request key, decimal quantity and UTC time', async () => {
    const bodies = [];
    const { state } = component('admin-inventory.js', { month: '2026-10', snapshot: { warehouse: { timeZoneId: 'Asia/Ho_Chi_Minh' }, items: [], stock: [] } }, {
        post: async (url, body) => { bodies.push(body); if (bodies.length === 1) throw new Error('Mạng gián đoạn'); },
        get: async () => ({ warehouse: {}, items: [], stock: [] })
    });
    state.openDocument(2);
    Object.assign(state.documentForm, { localAt: '2026-10-01T09:00', recipientPropertyId: 'branch', lines: [{ itemId: 'item', quantity: 1.5, unitPrice: 2000 }] });
    await state.saveDocument(); assert.equal(state.editor, 'document');
    await state.saveDocument(); assert.equal(state.editor, '');
    assert.equal(bodies[0].requestKey, bodies[1].requestKey);
    assert.equal(bodies[1].lines[0].quantity, 1.5);
    assert.equal(bodies[1].occurredAt, '2026-10-01T02:00:00.000Z');
});

test('cash expense uses cash method and retry key; stale requests cannot overwrite latest filter', async () => {
    const bodies = [], pending = [];
    const { state } = component('admin-cash.js', { month: '2026-10', propertyId: 'branch', scope: 'branch', rooms: [], snapshot: {},
        properties: [{ id: 'branch', timeZoneId: 'Asia/Ho_Chi_Minh' }], editableProperties: [{ id: 'branch' }] }, {
        post: async (url, body) => { bodies.push({ url, body }); throw new Error('Mạng gián đoạn'); },
        get: () => new Promise(resolve => pending.push(resolve))
    });
    state.openEditor('expense'); Object.assign(state.form, { amount: 12345, description: 'Vật tư', localAt: '2026-10-01T09:00' });
    await state.save(); await state.save();
    assert.equal(bodies[0].body.requestKey, bodies[1].body.requestKey);
    assert.equal(bodies[0].body.method, 0); assert.match(bodies[0].url, /\/expenses$/);
    const old = state.load(); state.month = '2026-11'; const latest = state.load();
    pending[1]({ month: '2026-11' }); await latest;
    pending[0]({ month: '2026-10' }); await old;
    assert.equal(state.snapshot.month, '2026-11'); assert.equal(state.loadedQuery, state.query);
});

test('switching item history discards old responses and loads selected item', async () => {
    const pending = [];
    const { state } = component('admin-inventory.js', { month: '2026-10', snapshot: {} }, { get: () => new Promise(resolve => pending.push(resolve)) });
    const old = state.openHistory({ itemId: 'a' });
    const latest = state.openHistory({ itemId: 'b' });
    pending[1]([{ id: 'b-doc' }]); await latest;
    pending[0]([{ id: 'a-doc' }]); await old;
    assert.equal(state.historyRows.length, 1); assert.equal(state.historyRows[0].id, 'b-doc');
});

test('item form omits code; import preview uses timezone and retry preserves signed token and request key', async () => {
    const bodies = [], forms = [];
    const { state } = component('admin-inventory.js', { month: '2026-10', snapshot: { warehouse: { timeZoneId: 'Asia/Ho_Chi_Minh' }, items: [] } }, {
        get: async () => ({ warehouse: { timeZoneId: 'Asia/Ho_Chi_Minh' }, items: [] }),
        postForm: async (url, data) => { forms.push(data); return { token: 'signed', errors: 0 }; },
        post: async (url, body) => { bodies.push({ url, body }); if (url.endsWith('/commit') && bodies.filter(x => x.url.endsWith('/commit')).length === 1) throw new Error('Mạng gián đoạn'); return { createdItems: 1, updatedItems: 0, createdUnits: 1, openingItems: 1, skipped: 0 }; }
    });
    state.openItem(); Object.assign(state.itemForm, { name: 'Mẫu', unitId: 'u' }); await state.saveItem();
    assert.equal(Object.hasOwn(bodies[0].body, 'code'), false);
    state.openImport('opening');
    state.importFile = new (require('node:buffer').File)(['fixture'], 'inventory.xlsx');
    await state.previewImport(); assert.equal(forms[0].get('openingAt'), '2026-09-30T17:00:00.000Z');
    await state.commitImport(); assert.equal(state.editor, 'import');
    await state.commitImport(); assert.equal(state.editor, '');
    assert.equal(bodies[1].body.requestKey, bodies[2].body.requestKey); assert.equal(bodies[2].body.token, 'signed');
    state.openImport('catalog'); state.importPreview = { token: 'old' }; const oldKey = state.importKey;
    state.clearPreview(); assert.equal(state.importPreview, null); assert.notEqual(state.importKey, oldKey);
});
