const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const source = fs.readFileSync('src/DeLong.Web/wwwroot/js/pages/admin-housekeeping.js', 'utf8');
const textBlock = source.slice(source.indexOf('            scheduleText()'), source.indexOf('            conditionTagGroups()'));
const text = new Function(`return {${textBlock}}.scheduleText;`)();
test('text matches customer example: one date and blank lines only between distinct times', () => {
    const tasks = [
        { time: '10:00', shiftOrder: 0, text: 'Dọn phòng Abaus #2 + Mở đèn' },
        { time: '10:30', shiftOrder: 0, text: 'Dọn phòng Moon Stone #4 + Mở đèn' },
        { time: '11:00', shiftOrder: 1, text: 'Dọn phòng Amber Stay #5 + Mở đèn' },
        { time: '11:00', shiftOrder: null, text: 'Dọn phòng La Roman #6 + Tắt đèn' }
    ];
    const result = text.call({ scheduleDays: [{ date: '2026-10-12', tasks }], dateText: () => '12/10', taskTime: x => x.time });
    assert.equal(result, '12/10\n\n10:00 Dọn phòng Abaus #2 + Mở đèn\n\n10:30 Dọn phòng Moon Stone #4 + Mở đèn\n\n11:00 Dọn phòng Amber Stay #5 + Mở đèn\n11:00 Dọn phòng La Roman #6 + Tắt đèn');
});
test('time grouping sorts unsorted tasks, preserves same-time order and handles multiple days', () => {
    const result = text.call({ scheduleDays: [
        { date: '2026-10-12', tasks: [{ time: '11:00', text: 'A' }, { time: '00:30', text: 'B' }, { time: '11:00', text: 'C' }] },
        { date: '2026-10-13', tasks: [] },
        { date: '2026-10-14', tasks: [{ time: '09:30', text: 'D' }] }
    ], dateText: date => date.slice(8) + '/10', taskTime: x => x.time });
    assert.equal(result, '12/10\n\n00:30 B\n\n11:00 A\n11:00 C\n\n14/10\n\n09:30 D');
});
test('empty schedule text is empty', () => {
    assert.equal(text.call({ scheduleDays: [] }), '');
});
test('copy uses exactly the displayed schedule text', async () => {
    const start = source.indexOf('            async copyScheduleText()');
    const end = source.indexOf('            async startTask(', start);
    const copied = [];
    const copy = new Function('navigator', 'setTimeout', `return {${source.slice(start, end)}}.copyScheduleText;`)(
        { clipboard: { writeText: async value => copied.push(value) } }, () => {});
    const vm = { scheduleText: '12/10\n\n11:00 A\n11:00 B', notify() {} };
    await copy.call(vm);
    assert.deepEqual(copied, [vm.scheduleText]);
    assert.equal(vm.copied, true);
});
test('manual scheduling warning is visible in both display and copied text', () => {
    const result = text.call({ scheduleDays: [{ date: '2026-10-12', tasks: [{ time: '10:30', text: 'Dọn phòng', schedulingWarning: 'Trùng lao công' }] }], dateText: () => '12/10', taskTime: x => x.time });
    assert.equal(result, '12/10\n\n10:30 Dọn phòng [Cần xử lý: Trùng lao công]');
});
const loadBlock = source.slice(source.indexOf('            async loadSchedule()'), source.indexOf('            async copyScheduleText()'));
function loader(api) { return new Function('DeLongApi', `return {${loadBlock}}.loadSchedule;`)(api); }
test('refresh preserves textarea scroll when changed and retains data on failure', async () => {
    const area = { scrollTop: 230, scrollLeft: 12 }, next = { calendar: [{ date: '2026-10-11', tasks: [] }] };
    const old = { calendar: [] };
    const vm = { selectedDate: '2026-10-11', propertyId: 'p', schedule: old, $refs: { scheduleTextArea: area },
        $nextTick: async () => { area.scrollTop = 0; area.scrollLeft = 0; }, notify() {} };
    await loader({ get: async () => next }).call(vm);
    assert.equal(area.scrollTop, 230); assert.equal(area.scrollLeft, 12); assert.equal(vm.schedule, next);
    await loader({ get: async () => { throw new Error('network'); } }).call(vm);
    assert.equal(vm.schedule, next); assert.equal(vm.scheduleLoading, false);
});
test('unchanged response does not replace schedule and page keeps DOM while loading', async () => {
    const schedule = { calendar: [] };
    const vm = { selectedDate: '2026-10-11', schedule, notify() {} };
    await loader({ get: async () => ({ calendar: [] }) }).call(vm);
    assert.equal(vm.schedule, schedule);
    const page = fs.readFileSync('src/DeLong.Web/Pages/Admin/Housekeeping/Index.cshtml', 'utf8');
    assert.doesNotMatch(page, /v-if="mode === '(schedule|text)' && !scheduleLoading"/);
});
