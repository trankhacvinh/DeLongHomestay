const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const { resolve } = require('node:path');

const source = readFileSync(resolve(__dirname, '../../src/DeLong.Web/wwwroot/js/core/api.js'), 'utf8');
const start = source.indexOf('    function assetUrl(value) {');
const end = source.indexOf('\n    function appendStyle', start);
assert.ok(start >= 0 && end > start);
const createAssetUrl = new Function('window', 'localAssetNonce', `${source.slice(start, end)}\nreturn assetUrl;`);

test('Production lazy assets do not reuse permanently cached legacy versions', () => {
    const assetUrl = createAssetUrl({ location: { origin: 'https://delonghomestay.com' } }, '');
    assert.equal(assetUrl('/js/pages/public-booking-core-v2.js?v=20260819-5'), '/js/pages/public-booking-core-v2.js');
    assert.equal(assetUrl('/js/pages/public-rich-editor.js?v=20260817-3'), '/js/pages/public-rich-editor.js');
    assert.equal(assetUrl('/css/booking-core-v2.css?v=old&theme=light'), '/css/booking-core-v2.css?theme=light');
});

test('Development keeps its cache bypass', () => {
    const assetUrl = createAssetUrl({ location: { origin: 'http://localhost:5080' } }, 'dev123');
    assert.equal(assetUrl('/js/pages/editor.js?v=old'), '/js/pages/editor.js?v=old&dev=dev123');
});
