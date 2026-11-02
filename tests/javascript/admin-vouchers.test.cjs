const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const vm = require('node:vm');

const source = readFileSync('src/DeLong.Web/wwwroot/js/pages/admin-vouchers.js', 'utf8');

function page({ rows = [], failAt = 0, pickers = true } = {}) {
    let definition;
    const requests = [];
    const queries = [];
    const datePickers = [];
    const statuses = { Draft: 0, Active: 1, Paused: 2, Archived: 3 };
    const initial = { propertyId: 'property', timeZoneId: 'Asia/Ho_Chi_Minh', canManage: true, customers: [] };
    class FixedDate extends Date {
        constructor(...args) { super(...(args.length ? args : ['2026-11-01T05:00:00Z'])); }
    }

    async function save(method, url, body) {
        requests.push({ method, url, body });
        if (requests.length === failAt) throw new Error('Không thể lưu voucher thử nghiệm.');
        const id = method === 'PUT' ? url.split('/').at(-1) : `voucher-${rows.length + 1}`;
        const row = { id, ...body };
        const index = rows.findIndex(item => item.id === id);
        if (index >= 0) rows[index] = row;
        else rows.push(row);
        return row;
    }

    const context = {
        Date: FixedDate, Intl, URLSearchParams, Uint8Array,
        setTimeout: () => 1, clearTimeout() {},
        document: { getElementById: id => id.endsWith('-data') ? { textContent: JSON.stringify(initial) } : {} },
        Vue: { createApp: options => { definition = options; return { mount() {} }; } },
        DeLongApi: {
            post: (url, body) => save('POST', url, body),
            put: (url, body) => save('PUT', url, body),
            get: async url => {
                const query = new URL(url, 'http://localhost').searchParams;
                queries.push(query);
                return rows.filter(row =>
                    (!query.get('status') || row.status === statuses[query.get('status')]) &&
                    (!query.get('search') || row.code.includes(query.get('search'))) &&
                    (!query.get('fromUtc') || row.endsAtUtc >= query.get('fromUtc')) &&
                    (!query.get('toUtc') || row.startsAtUtc < query.get('toUtc')));
            }
        }
    };
    if (pickers) context.flatpickr = (_input, options) => {
        const picker = {
            value: options.defaultDate,
            altInput: { value: options.defaultDate },
            clears: [],
            clear(triggerChange = true) {
                this.value = '';
                this.altInput.value = '';
                this.clears.push(triggerChange);
                if (triggerChange) options.onChange([], '');
            },
            destroy() {}
        };
        datePickers.push(picker);
        return picker;
    };

    vm.runInNewContext(source, context);
    const state = definition.data();
    for (const [name, fn] of Object.entries(definition.methods)) state[name] = fn.bind(state);
    state.$refs = { filterFromPicker: {}, filterToPicker: {} };
    state.$nextTick = () => {};
    state.mountFilterPickers();
    state.openCreate();
    state.form.code = 'NEWVOUCHER';
    return { state, rows, requests, queries, datePickers };
}

test('new vouchers default to active and remain visible after creation', async () => {
    const { state, rows, requests } = page();
    assert.equal(state.form.status, 1);
    await state.save();
    assert.equal(requests[0].body.status, 1);
    assert.equal(rows[0].status, 1);
    assert.equal(state.filters.status, '');
    assert.equal(state.vouchers[0]?.code, 'NEWVOUCHER');
    assert.equal(state.editor.open, false);
    assert.equal(state.toast.message, 'Đã lưu voucher.');
});

test('initial filters include all statuses and dates', async () => {
    const rows = [
        { id: 'draft', code: 'DRAFT', status: 0, startsAtUtc: '2025-01-01T00:00:00Z', endsAtUtc: '2025-02-01T00:00:00Z' },
        { id: 'active', code: 'ACTIVE', status: 1, startsAtUtc: '2027-01-01T00:00:00Z', endsAtUtc: '2027-02-01T00:00:00Z' },
        { id: 'paused', code: 'PAUSED', status: 2 },
        { id: 'archived', code: 'ARCHIVED', status: 3 }
    ];
    const { state, queries } = page({ rows });
    assert.equal(state.filters.status, '');
    assert.equal(state.filters.from, '');
    assert.equal(state.filters.to, '');
    await state.load();
    assert.equal(queries[0].size, 0);
    assert.equal(state.vouchers.length, 4);
});

test('saving clears stale search and dates, including visible date picker inputs', async () => {
    const { state, queries, datePickers } = page();
    state.filters.q = 'OTHER';
    state.filters.status = 'Paused';
    state.filters.from = '2026-11-01';
    state.filters.to = '2026-11-30';
    state.mountFilterPickers();
    state.form.status = 1;
    state.form.startsAtLocal = '2027-01-01T09:00';
    state.form.endsAtLocal = '2027-01-31T09:00';
    await state.save();
    assert.equal(state.filters.q, '');
    assert.equal(state.filters.from, '');
    assert.equal(state.filters.to, '');
    assert.equal(state.filters.status, '');
    assert.equal(queries.at(-1).size, 0);
    assert.equal(state.vouchers.length, 1);
    for (const picker of datePickers.slice(-2)) {
        assert.equal(picker.value, '');
        assert.equal(picker.altInput.value, '');
        assert.deepEqual(picker.clears, [false]);
    }
});

test('create and edit reveal the saved draft, active or paused voucher', async () => {
    for (const mode of ['create', 'edit']) {
        for (const status of [0, 1, 2]) {
            const { state, requests } = page();
            state.editor.mode = mode;
            state.editor.voucherId = 'existing';
            state.filters.q = 'OTHER';
            state.form.status = status;
            await state.save();
            assert.equal(requests[0].method, mode === 'edit' ? 'PUT' : 'POST');
            assert.equal(state.filters.status, '');
            assert.equal(state.vouchers[0]?.status, status);
            assert.equal(state.editor.error, '');
        }
    }
});

test('batch creation defaults every new voucher to active', async () => {
    const { state, rows, requests } = page();
    state.form.customerIds = ['customer-a', 'customer-b'];
    state.filters.q = 'OTHER';
    await state.save();
    assert.equal(rows.length, 2);
    assert.deepEqual(requests.map(request => request.body.status), [1, 1]);
    assert.deepEqual(Array.from(state.vouchers, row => row.code), ['NEWVOUCHER-01', 'NEWVOUCHER-02']);
    assert.equal(state.filters.status, '');
    assert.equal(state.editor.open, false);
    assert.equal(state.toast.message, 'Đã tạo 2 voucher cho 2 khách.');
});

test('failed create or edit keeps dialog, form and filters intact and shows API error', async () => {
    for (const mode of ['create', 'edit']) {
        const { state, rows, queries, datePickers } = page({ failAt: 1 });
        state.editor.mode = mode;
        state.editor.voucherId = 'existing';
        state.filters.q = 'ORIGINAL';
        const filters = JSON.stringify(state.filters);
        await state.save();
        assert.equal(rows.length, 0);
        assert.equal(state.editor.open, true);
        assert.equal(state.editor.error, 'Không thể lưu voucher thử nghiệm.');
        assert.equal(state.form.code, 'NEWVOUCHER');
        assert.equal(JSON.stringify(state.filters), filters);
        assert.equal(queries.length, 0);
        assert.equal(state.saving, false);
        assert.ok(datePickers.every(picker => picker.clears.length === 0));
    }
});

test('batch failure preserves dialog and filters and reports partial completion', async () => {
    for (const failAt of [1, 2]) {
        const { state, rows, queries, datePickers } = page({ failAt });
        state.form.customerIds = ['customer-a', 'customer-b'];
        const filters = JSON.stringify(state.filters);
        await state.save();
        assert.equal(rows.length, failAt - 1);
        assert.equal(state.editor.open, true);
        assert.match(state.editor.error, /Không thể lưu voucher thử nghiệm/);
        if (failAt === 2) assert.match(state.editor.error, /Đã tạo 1\/2 voucher/);
        assert.equal(JSON.stringify(state.filters), filters);
        assert.equal(queries.length, 0);
        assert.equal(state.saving, false);
        assert.ok(datePickers.every(picker => picker.clears.length === 0));
    }
});

test('save also reveals draft when flatpickr is unavailable', async () => {
    const { state } = page({ pickers: false });
    state.form.status = 0;
    await state.save();
    assert.equal(state.vouchers[0]?.code, 'NEWVOUCHER');
    assert.equal(state.editor.error, '');
});
