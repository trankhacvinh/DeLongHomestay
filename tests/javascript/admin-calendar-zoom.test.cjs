const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const vm = require('node:vm');
const source = readFileSync('src/DeLong.Web/wwwroot/js/pages/admin-calendar-v2.js', 'utf8');
const block = source.slice(source.indexOf('    let calendarScale ='), source.indexOf('\n    const legendColors'));
function setup(mobile) {
    const nodes = new Map();
    const styles = new Map();
    let fit;
    const panel = { style: { setProperty: (key, value) => styles.set(key, value) }, querySelector(selector) {
        if (!nodes.has(selector)) nodes.set(selector, { addEventListener(_, handler) { this.click = handler; }, setAttribute(key, value) { this[key] = value; } });
        return nodes.get(selector);
    } };
    vm.runInNewContext(block, { panel, scroll: { classList: { toggle(_, value) { fit = value; } } }, window: { matchMedia: () => ({ matches: mobile }) } });
    return { panel, scale: () => Number(styles.get('--calendar-scale')), fit: () => fit };
}

test('Mobile starts fitted to all slots and zoom changes the whole table', () => {
    const env = setup(true);
    assert.equal(env.fit(), true);
    env.panel.querySelector('[data-v2-zoom-in]').click();
    assert.equal(env.fit(), false);
    assert.equal(env.scale(), 1.1);
    env.panel.querySelector('[data-v2-zoom-fit]').click();
    assert.equal(env.fit(), true);
});

test('Desktop starts at 100% and zoom stays inside 60–140%', () => {
    const env = setup(false);
    assert.equal(env.scale(), 1);
    assert.equal(env.fit(), false);
    for (let i = 0; i < 20; i++) env.panel.querySelector('[data-v2-zoom-out]').click();
    assert.equal(env.scale(), 0.6);
    assert.equal(env.panel.querySelector('[data-v2-zoom-out]').disabled, true);
    for (let i = 0; i < 20; i++) env.panel.querySelector('[data-v2-zoom-in]').click();
    assert.equal(env.scale(), 1.4);
    assert.equal(env.panel.querySelector('[data-v2-zoom-in]').disabled, true);
});
