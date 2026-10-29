const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');

function setup({ booking = true, admin = false, readyState = 'loading' } = {}) {
    const inserted = [], domEvents = {}, windowEvents = {};
    const document = {
        readyState, body: { classList: { contains: x => (admin ? ['admin-body'] : ['public-body', 'dl-theme-standard']).includes(x) } },
        querySelector: () => null,
        getElementById: id => booking && id === 'public-booking-page' ? {} : null,
        createElement: tagName => ({ tagName, dataset: {}, setAttribute(name, value) { this[name] = value; } }),
        head: { appendChild: node => inserted.push(node) },
        addEventListener: (name, callback) => { domEvents[name] = callback; }
    };
    const window = { location: { hostname: 'delonghomestay.com', origin: 'https://delonghomestay.com' }, addEventListener: (name, callback) => { windowEvents[name] = callback; } };
    vm.runInNewContext(fs.readFileSync('src/DeLong.Web/wwwroot/js/core/api.js', 'utf8'), { window, document, URL, FormData: class {}, setTimeout: fn => fn() });
    return { inserted, document, domEvents, windowEvents };
}

test('booking core loads at DOM ready even while window load is waiting for images', () => {
    const { inserted, document, domEvents, windowEvents } = setup();
    assert.equal(inserted.filter(x => x['data-public-booking-core-v2']).length, 0);
    document.readyState = 'interactive'; domEvents.DOMContentLoaded?.();
    assert.equal(inserted.filter(x => x['data-public-booking-core-v2']).length, 1);
    windowEvents.load?.();
    assert.equal(inserted.filter(x => x['data-public-booking-core-v2']).length, 1);
});

test('home without a booking form does not download the unused booking core', () => {
    const { inserted, domEvents, windowEvents } = setup({ booking: false });
    domEvents.DOMContentLoaded?.(); windowEvents.load?.();
    assert.equal(inserted.filter(x => x['data-public-booking-core-v2']).length, 0);
});

test('a booking page whose DOM is already ready initializes without waiting for load', () => {
    const { inserted } = setup({ readyState: 'interactive' });
    assert.equal(inserted.filter(x => x['data-public-booking-core-v2']).length, 1);
});

test('admin deferred loading retains its existing window-load behavior', () => {
    const { inserted, domEvents, windowEvents } = setup({ admin: true, booking: false });
    domEvents.DOMContentLoaded?.();
    assert.equal(inserted.filter(x => x['data-admin-booking-policy']).length, 0);
    windowEvents.load?.();
    assert.equal(inserted.filter(x => x['data-admin-booking-policy']).length, 1);
});
