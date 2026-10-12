const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
function fixture() {
    const window = {}, handlers = {}, calls = [];
    vm.runInNewContext(fs.readFileSync('src/DeLong.Web/wwwroot/js/core/money-input.js', 'utf8'), { window });
    const element = { value: '', addEventListener: (name, fn) => handlers[name] = fn, removeEventListener: name => delete handlers[name] };
    const binding = value => ({ value: { value, set: value => calls.push(value) } });
    return { directive: window.DeLongMoneyInput.directives.moneyInput, element, handlers, binding, calls };
}
test('formatted entry becomes numeric money even when mask cannot load', () => {
    const { directive, element, handlers, binding, calls } = fixture();
    directive.mounted(element, binding(0)); element.value = '250.000'; handlers.input();
    assert.deepEqual(calls, [250000]);
    element.value = ''; handlers.input(); assert.deepEqual(calls, [250000]);
    element.value = '1.250.000'; handlers.change(); assert.equal(calls.at(-1), 1250000);
    directive.beforeUnmount(element); assert.equal(Object.keys(handlers).length, 0);
});
test('model updates do not count as a user change and subsequent edits use latest setter', () => {
    const { directive, element, handlers, binding, calls } = fixture();
    directive.mounted(element, binding(250000));
    directive.updated(element, binding(360000)); handlers.input();
    assert.equal(calls.length, 0); assert.equal(element.value, '360000');
    element.value = '100.000'; handlers.input(); assert.deepEqual(calls, [100000]);
});
