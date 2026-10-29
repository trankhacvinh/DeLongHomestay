const test = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const path = 'src/DeLong.Web/wwwroot/js/pages/';

function component(name, initial, api, overrides = {}) {
    let options;
    const context = vm.createContext({ window: {}, Intl, Date, URLSearchParams, FormData, AbortController, setTimeout, clearTimeout, crypto: require('node:crypto').webcrypto,
        document: { getElementById: id => id.endsWith('-data') ? { textContent: JSON.stringify(initial) } : {} },
        DeLongApi: api, Vue: { createApp: value => { options = value; return { directive() { return this; }, mount() {} }; } }, ...overrides });
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

test('cash cancels old month requests and releases loading on timeout without showing old totals', async () => {
    const pending = [], timers = [];
    const { state } = component('admin-cash.js', { propertyId:'a',scope:'all',month:'2026-10',snapshot:{month:'2026-10'},rooms:[],properties:[],editableProperties:[] }, {
        get: (url, options) => new Promise((resolve,reject) => {
            pending.push({url,signal:options?.signal,resolve});
            options?.signal.addEventListener('abort', () => reject(Object.assign(new Error('aborted'), {name:'AbortError'})));
        })
    }, { setTimeout: (fn, ms) => { assert.equal(ms,30000);timers.push(fn);return timers.length; }, clearTimeout(){} });
    const first=state.load();assert.ok(pending[0].signal,'month loads must have an abort signal');
    state.month='2026-09';const next=state.load();assert.equal(pending[0].signal.aborted,true);
    await first;assert.equal(state.loading,true);
    timers.at(-1)();await next;
    assert.equal(state.loading,false);assert.equal(state.loadedQuery,'');assert.match(state.error,/quá lâu/);
    const retry=state.load();pending.at(-1).resolve({month:'2026-09',entries:[]});await retry;
    assert.equal(state.snapshot.month,'2026-09');assert.equal(state.loadedQuery,state.query);assert.equal(state.error,'');
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


test('recipient suggestions reuse external names across months, resolve branches, and totals summarize received issued and closing', () => {
    const {state}=component('admin-inventory.js',{month:'2026-10',properties:[{id:'p',name:'De Long'}],snapshot:{
        items:[{id:'a',isActive:true},{id:'b',isActive:true}],
        stock:[{itemId:'a',name:'A',unitName:'kg',opening:3,received:2,issued:5,closing:0},{itemId:'b',name:'B',unitName:'chai',opening:5,received:0,issued:1,closing:4}],
        documents:[{type:1},{type:2},{type:2,isVoided:true}],distribution:[],
        recipients:[{propertyId:null,name:'home1'},{propertyId:null,name:' HOME1 '},{propertyId:'p',name:'De Long'}]
    }},{});
    assert.equal(state.recipientOptions.length,2);
    state.openDocument(2);state.documentForm.recipientText=' de long ';
    assert.equal(state.documentRecipient().recipientPropertyId,'p');
    state.documentForm.recipientText='home1';assert.equal(state.documentRecipient().recipientName,'home1');
    assert.equal(state.documentRecipient().recipientPropertyId,null);
    assert.equal(state.quickSummary.empty,1);assert.equal(state.quickSummary.receipts,1);assert.equal(state.quickSummary.issues,1);
    assert.equal(state.stockTotals.received,2);assert.equal(state.stockTotals.issued,6);assert.equal(state.stockTotals.closing,4);
    state.search='A';assert.equal(state.stockTotals.issued,5);assert.equal(state.stockTotals.closing,0);
    assert.equal(state.recipientKey({recipientName:' HOME1 '}),state.recipientKey({recipientName:'home1'}));
});


test('shared API GET forwards cancellation and propagates abort while reading JSON', async () => {
    const controller=new AbortController();let received;
    const window={location:{hostname:'example.com',origin:'https://example.com'},addEventListener(){}};
    const context={window,URL,FormData,document:{body:null,getElementById:()=>null,querySelector:()=>null},
        fetch:async(url,config)=>{received=config;return {ok:true,status:200,headers:{get:()=> 'application/json'},json:async()=>{throw Object.assign(new Error('aborted'),{name:'AbortError'});}};}};
    vm.runInNewContext(fs.readFileSync('src/DeLong.Web/wwwroot/js/core/api.js','utf8'),context);
    await assert.rejects(window.DeLongApi.get('/api/admin/cash/',{signal:controller.signal}),{name:'AbortError'});
    assert.equal(received.signal,controller.signal);assert.equal(received.credentials,'same-origin');
});
