const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const { resolve } = require('node:path');
const vm = require('node:vm');
const source = readFileSync(resolve(__dirname, '../../src/DeLong.Web/wwwroot/js/pages/public-room-gallery.js'), 'utf8');

function setup(urls) {
    const created = [];
    const events = {};
    const image = { src: '/cover.webp', getAttribute: () => '/cover.webp' };
    const link = {
        dataset: { roomGallery: JSON.stringify(urls) }, style: {},
        querySelector: () => image, before() {},
        hasPointerCapture: () => false, setPointerCapture() {},
        addEventListener(name, handler) { events[name] = handler; }
    };
    vm.runInNewContext(source, { document: {
        querySelectorAll: () => [link],
        createElement(type) {
            const node = { type, append() {}, setAttribute() {}, handlers: {},
                addEventListener(name, handler) { this.handlers[name] = handler; } };
            created.push(node);
            return node;
        }
    } });
    function pointer(name, x, y) { events[name]({ isPrimary: true, button: 0, pointerId: 1, clientX: x, clientY: y }); }
    function click() {
        let prevented = false;
        events.click({ preventDefault() { prevented = true; }, stopImmediatePropagation() {} });
        return prevented;
    }
    return { image, created, pointer, click };
}

test('Gallery buttons change photos and wrap back to the cover', () => {
    const env = setup(['/cover.webp', '/second.webp', '/third.webp']);
    const next = env.created.find(x => x.className?.endsWith('next'));
    const counter = env.created.find(x => x.className === 'public-room-gallery-count');
    assert.equal(counter.textContent, '1/3 ảnh');
    next.handlers.click();
    assert.equal(env.image.src, '/second.webp');
    next.handlers.click();
    assert.equal(env.image.src, '/third.webp');
    next.handlers.click();
    assert.equal(env.image.src, '/cover.webp');
});

test('Horizontal swipe changes photo and blocks booking navigation; a later tap still works', () => {
    const env = setup(['/cover.webp', '/second.webp']);
    env.pointer('pointerdown', 160, 100);
    env.pointer('pointermove', 80, 103);
    env.pointer('pointerup', 80, 103);
    assert.equal(env.image.src, '/second.webp');
    assert.equal(env.click(), true);
    env.pointer('pointerdown', 100, 100);
    env.pointer('pointerup', 101, 101);
    assert.equal(env.click(), false);
});

test('Vertical scrolling does not change the image', () => {
    const env = setup(['/cover.webp', '/second.webp']);
    env.pointer('pointerdown', 100, 200);
    env.pointer('pointermove', 105, 100);
    env.pointer('pointerup', 105, 100);
    assert.equal(env.image.src, '/cover.webp');
});

test('A single photo needs no gallery controls', () => {
    assert.equal(setup(['/cover.webp']).created.length, 0);
});
