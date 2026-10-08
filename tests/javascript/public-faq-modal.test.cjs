const { readFileSync } = require('node:fs');
const { resolve } = require('node:path');
const { runInNewContext } = require('node:vm');
const { test } = require('node:test');
const assert = require('node:assert/strict');
const source = readFileSync(resolve(__dirname, '../../src/DeLong.Web/wwwroot/js/core/public-faq-modal.js'), 'utf8');
function fixture() {
    const events = {}, buttons = [0, 1].map(() => ({ events: {}, focusCount: 0, addEventListener(type, callback) { this.events[type] = callback; }, focus() { this.focusCount++; } }));
    const closeButton = { addEventListener(type, callback) { this.click = callback; } };
    const body = { style: { overflow: 'auto' } };
    const dialog = { open: false, showModal() { this.open = true; }, close() { this.open = false; events.close(); }, querySelector: () => closeButton,
        addEventListener(type, callback) { events[type] = callback; }, getBoundingClientRect: () => ({ left: 10, right: 300, top: 10, bottom: 400 }) };
    runInNewContext(source, { document: { body, querySelector: () => dialog, querySelectorAll: () => buttons } });
    return { events, buttons, closeButton, body, dialog };
}
test('desktop and mobile triggers open the dialog; closing restores scrolling and focus', () => {
    for (const index of [0, 1]) {
        const { buttons, closeButton, body, dialog } = fixture();
        buttons[index].events.click();
        assert.equal(dialog.open, true);
        assert.equal(body.style.overflow, 'hidden');
        buttons[index].events.click();
        closeButton.click();
        assert.equal(dialog.open, false);
        assert.equal(body.style.overflow, 'auto');
        assert.equal(buttons[index].focusCount, 1);
    }
});
test('clicking empty space within the dialog does not close it; backdrop does', () => {
    const { buttons, events, dialog } = fixture();
    buttons[0].events.click();
    events.click({ target: dialog, clientX: 50, clientY: 50 });
    assert.equal(dialog.open, true);
    events.click({ target: dialog, clientX: 1, clientY: 50 });
    assert.equal(dialog.open, false);
});
test('native Escape closure restores page scrolling and trigger focus', () => {
    const { buttons, dialog, body } = fixture();
    buttons[1].events.click();
    dialog.close();
    assert.equal(body.style.overflow, 'auto');
    assert.equal(buttons[1].focusCount, 1);
});
