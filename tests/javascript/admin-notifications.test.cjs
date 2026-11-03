const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const vm = require('node:vm');

function node() {
    return {
        children: [], dataset: {}, listeners: {}, hidden: false, textContent: '',
        classList: { add() {}, remove() {} },
        append(...children) { this.children.push(...children); },
        appendChild(child) { this.children.push(child); },
        replaceChildren(...children) { this.children = children; },
        addEventListener(name, callback) { this.listeners[name] = callback; },
        setAttribute(name, value) { this[name] = value; },
        contains() { return false; }
    };
}

function notifications() {
    const center = node(), toastRoot = node(), events = [], posts = [], navigations = [];
    const controls = Object.fromEntries(['toggle', 'popover', 'badge', 'list', 'empty', 'read-all'].map(key => [key, node()]));
    center.dataset.propertyId = 'branch';
    center.querySelector = selector => controls[selector.slice('[data-notification-'.length, -1)];
    const streamListeners = {};
    class EventSource {
        addEventListener(name, callback) { streamListeners[name] = callback; }
        close() {}
    }
    const feed = {
        items: [{ id: 'cancel', type: 'booking-cancelled:record', title: 'Booking bị hủy · BK001', message: 'Khách · Phòng', actionUrl: '/Admin/Bookings?bookingId=booking', createdAtUtc: new Date().toISOString(), isRead: false }],
        unreadCount: 1
    };
    const window = { location: { hostname: 'example.test', assign: url => navigations.push(url) }, addEventListener() {}, EventSource };
    const DeLongApi = { get: async () => feed, post: async (url, body) => posts.push({ url, body }) };
    window.DeLongApi = DeLongApi;
    vm.runInNewContext(readFileSync('src/DeLong.Web/wwwroot/js/core/admin-notifications.js', 'utf8'), {
        window, DeLongApi, EventSource, Intl, Date,
        CustomEvent: class { constructor(type, init) { this.type = type; this.detail = init.detail; } },
        document: {
            readyState: 'loading', head: node(), documentElement: node(),
            querySelector: selector => selector === '[data-notification-center]' ? center : null,
            getElementById: id => id === 'global-toast-root' ? toastRoot : null,
            createElement: node, addEventListener() {}, dispatchEvent: event => events.push(event)
        },
        requestAnimationFrame: callback => callback(), setTimeout: () => 1
    });
    return { controls, toastRoot, events, posts, navigations, streamListeners };
}

for (const type of ['booking-cancelled', 'booking-cancelled:7bf78331e3934872888a5b48a9ae3c81']) {
    test(`${type} updates the bell and shows cancellation toast`, async () => {
        const fixture = notifications();
        await fixture.streamListeners.notification({ data: JSON.stringify({ type }) });
        assert.equal(fixture.controls.badge.textContent, '1');
        assert.equal(fixture.controls.badge.hidden, false);
        assert.equal(fixture.controls.list.children[0].children[1].children[0].textContent, 'Booking bị hủy · BK001');
        assert.match(fixture.toastRoot.children[0].innerHTML, /hủy/);
        assert.doesNotMatch(fixture.toastRoot.children[0].innerHTML, /yêu cầu đặt phòng mới/);
        assert.equal(fixture.events[0].type, 'delong:booking-notification');
        assert.equal(fixture.events[0].detail.propertyId, 'branch');
    });
}

test('new booking toast keeps its existing message', async () => {
    const { streamListeners, toastRoot } = notifications();
    await streamListeners.notification({ data: JSON.stringify({ type: 'booking-requested' }) });
    assert.match(toastRoot.children[0].innerHTML, /Có yêu cầu đặt phòng mới/);
});

test('clicking cancellation marks it read and navigates to its booking', async () => {
    const { streamListeners, controls, posts, navigations } = notifications();
    await streamListeners.notification({ data: '{invalid-json' });
    await controls.list.children[0].listeners.click({ preventDefault() {} });
    assert.equal(posts[0].url, '/api/admin/properties/branch/notifications/cancel/read');
    assert.deepEqual(navigations, ['/Admin/Bookings?bookingId=booking']);
});
