const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const { resolve } = require('node:path');
const vm = require('node:vm');
const { File } = require('node:buffer');
const source = readFileSync(resolve(__dirname, '../../src/DeLong.Web/wwwroot/js/core/image-upload.js'), 'utf8');

function setup(width, height, encodedSize = 100) {
    const draws = [];
    let closed = false;
    const window = {
        location: { origin: 'https://delonghomestay.com' },
        createImageBitmap: async () => ({ width, height, close() { closed = true; } })
    };
    const document = { createElement: () => ({
        width: 0, height: 0,
        getContext: () => ({ drawImage: (...args) => draws.push(args.slice(1)) }),
        toBlob(callback, type, quality) {
            draws.push({ type, quality });
            callback(new Blob([new Uint8Array(encodedSize)], { type }));
        }
    }) };
    vm.runInNewContext(source, { window, document, URL, File, FormData });
    return { prepare: window.DeLongImageUpload.prepareForm, draws, closed: () => closed };
}

for (const [url, edge, quality] of [
    ['/api/admin/properties/p/rooms/r/content/images', 2560, 0.85],
    ['/api/admin/site/global/assets/section', 2560, 0.85],
    ['/api/admin/site/global/media/upload', 2560, 0.85],
    ['/api/public/booking-requests/b/identity-documents/front?siteSlug=x', 2000, 0.9]
]) {
    test(`Resize before upload: ${url}`, async () => {
        const env = setup(10000, 7500);
        const form = new FormData();
        form.append('file', new File([new Uint8Array(1000)], 'phone.jpg', { type: 'image/jpeg' }));
        form.append('caption', 'Phòng');
        const prepared = await env.prepare(url, form);
        assert.deepEqual(env.draws[0], [0, 0, edge, edge * 0.75]);
        assert.equal(env.draws[1].quality, quality);
        assert.equal(prepared.get('caption'), 'Phòng');
        assert.equal(prepared.get('file').size, 100);
        assert.equal(env.closed(), true);
        assert.equal(form.get('file').size, 1000);
    });
}

test('Portrait PNG keeps transparency and aspect ratio', async () => {
    const env = setup(7000, 10000);
    const form = new FormData();
    form.append('file', new File(['image'], 'logo.png', { type: 'image/png' }));
    const prepared = await env.prepare('/api/admin/site/global/assets/logo', form);
    assert.deepEqual(env.draws[0], [0, 0, 1792, 2560]);
    assert.equal(prepared.get('file').type, 'image/png');
});

test('Small images are not enlarged or replaced with a heavier file', async () => {
    const env = setup(800, 600, 2000);
    const form = new FormData();
    form.append('file', new File(['small'], 'small.jpg', { type: 'image/jpeg' }));
    const prepared = await env.prepare('/api/admin/site/global/assets/section', form);
    assert.deepEqual(env.draws[0], [0, 0, 800, 600]);
    assert.equal(prepared.get('file').size, 5);
});

test('Unrelated document import is untouched', async () => {
    const env = setup(10000, 7500);
    const form = new FormData();
    form.append('file', new File(['data'], 'import.xlsx'));
    assert.equal(await env.prepare('/api/admin/import', form), form);
    assert.equal(env.draws.length, 0);
});

for (const side of ['second-front', 'second-back']) {
    test(`Second guest identity is resized: ${side}`, async () => {
        const env = setup(10000, 7500);
        const form = new FormData();
        form.append('file', new File(['large'], 'id.jpg', { type: 'image/jpeg' }));
        await env.prepare(`/api/public/booking-requests/b/identity-documents/${side}`, form);
        assert.deepEqual(env.draws[0], [0, 0, 2000, 1500]);
    });
}

test('Preparing the same File twice reuses its compressed result', async () => {
    const env = setup(10000, 7500);
    const form = new FormData();
    form.append('file', new File(['large'], 'id.jpg', { type: 'image/jpeg' }));
    const url = '/api/public/booking-requests/b/identity-documents/front';
    await Promise.all([env.prepare(url, form), env.prepare(url, form)]);
    assert.equal(env.draws.length, 2);
});
