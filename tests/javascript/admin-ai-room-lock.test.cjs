const { readFileSync } = require('node:fs');
const { resolve } = require('node:path');
const assert = require('node:assert/strict');
const { test } = require('node:test');
const source = readFileSync(resolve(__dirname, '../../src/DeLong.Web/wwwroot/js/core/admin-ai-chat.js'), 'utf8');
const slice = source.slice(source.indexOf('    function roomLockChanges('), source.indexOf('    function reportPeriod('));
class Element {
    constructor(tag) { this.tag = tag; this.children = []; this.listeners = {}; this.checked = false; this.classList = { add() {} }; }
    append(...children) { this.children.push(...children); }
    addEventListener(name, callback) { this.listeners[name] = callback; }
    async fire(name) { return this.listeners[name]?.(); }
}
function fixture() {
    const messages = new Element('main'), requests = [];
    const document = { createElement: tag => new Element(tag) };
    const functions = new Function('document', 'messages', 'DeLongApi', 'propertyId', 'addMessage', 'createPayloadTable', 'operationLabels', 'fieldLabels', 'displayValue',
        `${slice}; return { addProposal, createRoomLockPreview, roomLockChanges };`)(
        document, messages, { post: async (url, body) => { requests.push({ url, body }); } }, 'branch', () => {},
        values => Object.assign(new Element('table'), { values }), {}, {}, String);
    return { ...functions, messages, requests };
}
function find(element, predicate) { return predicate(element) ? element : element.children.map(child => find(child, predicate)).find(Boolean); }
const change = {
    roomIds: ['blue'], beforeRooms: [{ id: 'blue', name: 'Blue', code: 'B01' }], beforeBlocks: [], timeZoneId: 'Asia/Ho_Chi_Minh',
    schedule: { reason: 'Bảo trì', repeatDaily: false, start: '2031-01-10T07:00:00Z', end: '2031-01-10T10:00:00Z' },
    conflicts: [{ id: 'order', code: 'BK123', roomName: 'Blue', checkInUtc: '2031-01-10T07:00:00Z', checkOutUtc: '2031-01-10T10:00:00Z' }]
};
test('room closure preview shows branch-local times, reason and existing booking code', () => {
    const f = fixture(), preview = f.createRoomLockPreview(change);
    const table = find(preview, element => element.tag === 'table');
    assert.match(table.values['Khoảng khóa'], /14:00/);
    assert.match(table.values['Khoảng khóa'], /17:00/);
    assert.equal(table.values['Lý do'], 'Bảo trì');
    assert.match(find(preview, element => element.tag === 'li').textContent, /BK123 · Blue/);
});
test('applying a batch closure requires an explicit human checkbox and sends acknowledgement', async () => {
    const f = fixture();
    f.addProposal({ id: 'proposal', status: 'Pending', summary: 'Khóa phòng', payload: { operations: [{ type: 'CreateRoomBookingSchedule', payload: { preparedRoomLock: change } }] } });
    const card = f.messages.children[0], apply = find(card, x => x.tag === 'button' && x.textContent === 'Xác nhận áp dụng');
    const checkbox = find(card, x => x.tag === 'input');
    assert.equal(apply.disabled, true);
    await apply.fire('click');
    assert.equal(f.requests.length, 0);
    checkbox.checked = true;
    await checkbox.fire('change');
    assert.equal(apply.disabled, false);
    await apply.fire('click');
    assert.deepEqual(f.requests[0].body, { acknowledgeExistingBookings: true });
    assert.equal(checkbox.disabled, true);
});
test('a closure without conflicts uses the existing apply flow without a checkbox', async () => {
    const f = fixture();
    f.addProposal({ id: 'proposal', status: 'Pending', summary: 'Khóa phòng', payload: { preparedRoomLock: { ...change, conflicts: [] } } });
    const card = f.messages.children[0];
    assert.equal(find(card, x => x.tag === 'input'), undefined);
    await find(card, x => x.tag === 'button' && x.textContent === 'Xác nhận áp dụng').fire('click');
    assert.deepEqual(f.requests[0].body, { acknowledgeExistingBookings: false });
});
test('daily overnight preview explicitly says checkout occurs the next day', () => {
    const f = fixture(), preview = f.createRoomLockPreview({ ...change, conflicts: [], schedule: { reason: 'Sửa chữa', repeatDaily: true, fromDate: '2031-01-10', toDate: '2031-01-11', windows: [{ start: '21:00:00', end: '09:30:00' }] } });
    assert.match(find(preview, element => element.tag === 'table').values['Khung mỗi ngày'], /21:00 → 09:30 \(sang ngày hôm sau\)/);
});
