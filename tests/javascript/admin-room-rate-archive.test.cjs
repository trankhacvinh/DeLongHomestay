const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const vm = require('node:vm');

function loadPage(file, initial, api = {}) {
    let options;
    const context = {
        document: { getElementById: id => id.endsWith('-data') ? { textContent: JSON.stringify(initial) } : {} },
        Vue: { createApp: definition => {
            options = definition;
            const app = { mixin: () => app, mount: () => {} };
            return app;
        } },
        window: {}, DeLongApi: api, Intl, setTimeout: () => 1, clearTimeout: () => {}
    };
    vm.runInNewContext(readFileSync(file, 'utf8'), context);
    const state = options.data();
    for (const [name, fn] of Object.entries(options.methods)) state[name] = fn.bind(state);
    for (const [name, fn] of Object.entries(options.computed || {})) Object.defineProperty(state, name, { get: () => fn.call(state) });
    return state;
}
const settingsFile = 'src/DeLong.Web/wwwroot/js/pages/admin-settings.js';
const roomsFile = 'src/DeLong.Web/wwwroot/js/pages/admin-rooms.js';
const rates = [
    { id: 'old', name: 'Khung 1', sortOrder: 1, isActive: true, price: 210000, type: 0, startTime: '11:00', endTime: '14:00' },
    { id: 'copied', name: 'Khung 1', sortOrder: 0, isActive: false, price: 250000 }
];

test('archived rates are hidden by default and can be inspected without deleting their records', () => {
    const room = { id: 'room', isActive: true, rates };
    const state = loadPage(settingsFile, { rooms: [room] });
    assert.deepEqual(Array.from(state.sortedRates(room), x => x.id), ['old']);
    state.showArchivedRates = true;
    assert.deepEqual(Array.from(state.sortedRates(room), x => x.id), ['copied', 'old']);
    assert.equal(room.rates.length, 2);
});

test('archiving hides a rate immediately while keeping its price and allowing edit restoration', async () => {
    const calls = [];
    const room = { id: 'room', isActive: true, rates: [{ ...rates[0] }] };
    const state = loadPage(settingsFile, { propertyId: 'property', rooms: [room] }, {
        delete: async url => calls.push(url),
        put: async (url, payload) => {
            calls.push(url);
            return { id: 'old', ...payload };
        }
    });
    const rate = state.rooms[0].rates[0];
    state.openArchive(state.rooms[0], rate);
    await state.confirmArchive();
    assert.deepEqual(calls, ['/api/admin/properties/property/rooms/room/rates/old']);
    assert.equal(state.sortedRates(state.rooms[0]).length, 0);
    assert.equal(state.rooms[0].rates[0].price, 210000);
    assert.equal(state.rooms[0].rates.length, 1);
    state.showArchivedRates = true;
    state.openEdit(state.rooms[0], state.sortedRates(state.rooms[0])[0]);
    assert.equal(state.form.isActive, false);
    state.form.isActive = true;
    assert.equal(state.form.price, 210000);
    await state.saveRate();
    state.showArchivedRates = false;
    assert.equal(state.sortedRates(state.rooms[0]).length, 1);
    assert.equal(state.rooms[0].rates[0].startTime, '11:00');
    assert.equal(state.rooms[0].rates[0].price, 210000);
});

test('failed archive leaves the active rate visible', async () => {
    const state = loadPage(settingsFile, { rooms: [{ id: 'room', isActive: true, rates: [{ ...rates[0] }] }] }, {
        delete: async () => { throw new Error('Connection failed'); }
    });
    state.openArchive(state.rooms[0], state.rooms[0].rates[0]);
    await state.confirmArchive();
    assert.equal(state.sortedRates(state.rooms[0]).length, 1);
    assert.equal(state.rooms[0].rates[0].isActive, true);
    assert.equal(state.toast.type, 'error');
});

test('rooms can be archived and restored with existing update API without dropping rate data', async () => {
    const calls = [];
    const state = loadPage(roomsFile, { propertyId: 'property', rooms: [
        { id: 'room', name: 'Test', code: 'TEST', capacity: 2, sortOrder: 1, isActive: true, isPublished: false, rates }
    ] }, { put: async (url, payload) => {
        calls.push({ url, payload });
        return { ...state.rooms[0], ...payload };
    } });
    await state.toggleActive(state.rooms[0]);
    assert.equal(state.filteredRooms.length, 0);
    state.showInactive = true;
    assert.equal(state.filteredRooms.length, 1);
    assert.equal(state.rooms[0].rates.length, 2);
    assert.deepEqual(Array.from(state.activeRates(state.rooms[0]), x => x.id), ['old']);
    await state.toggleActive(state.rooms[0]);
    state.showInactive = false;
    assert.equal(state.filteredRooms.length, 1);
    assert.equal(calls[0].payload.isActive, false);
    assert.equal(calls[1].payload.isActive, true);
    assert.equal(calls[1].payload.capacity, 2);
});

test('settings page exposes archive filter and no longer loads bulk-rate creation', () => {
    const page = readFileSync('src/DeLong.Web/Pages/Admin/Settings/Index.cshtml', 'utf8');
    assert.ok(page.includes('v-model="showArchivedRates"'));
    assert.ok(!page.includes('data-quick-rate-open'));
    assert.ok(!page.includes('admin-settings-quick-rate.js'));
    assert.ok(page.includes('.quick-rate-backdrop { display: none !important; }'));
});
