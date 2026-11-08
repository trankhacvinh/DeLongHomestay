const { chromium } = require('playwright');
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const { gzipSync } = require('node:zlib');
const root = path.resolve('src/DeLong.Web/wwwroot');
const output = '/private/tmp/homestay-room-photos';
const publicSource = fs.readFileSync(path.join(root, '../Pages/Booking/Index.cshtml'), 'utf8');
const publicList = publicSource.slice(publicSource.indexOf('<div class="public-booking-room-list"'), publicSource.indexOf('<div class="public-booking-room-mobile-actions"')).replace('v-else-if="roomList.length"', 'v-if="roomList.length"');
const adminSource = fs.readFileSync(path.join(root, '../Pages/Admin/CalendarV2.cshtml'), 'utf8');
const fieldStart = adminSource.lastIndexOf('<div class="field">', adminSource.indexOf('<label>Phòng *</label>'));
const adminField = adminSource.slice(fieldStart, adminSource.indexOf('<div class="field">', fieldStart + 1));
const rooms = [{ id: 'a', name: 'Blue', code: 'BLUE', capacity: 2, imageCount: 20, coverThumbnailUrl: '/thumb/a.svg' }, { id: 'b', name: 'Green', code: 'GREEN', capacity: 2, imageCount: 1, coverThumbnailUrl: '/thumb/b.svg' }];
function vueFixture(admin) {
    return `<meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><link rel="stylesheet" href="/css/site.css"><link rel="stylesheet" href="/css/public.css"><div id="vue-fixture" style="padding:16px;max-width:700px">${admin ? adminField : publicList}<input id="retained-phone" v-model="phone"><output id="selected">{{ ${admin ? 'form.roomId' : 'selectedRoomId'} }}</output><output id="changes">{{ changes }}</output></div><script id="calendar-page-data" type="application/json">${JSON.stringify({ rooms })}</script><script src="/vendor/vue/vue.global.prod.js"></script><script src="/js/core/room-photo-entry.js"></script><script>Vue.createApp({ data: () => ({ phone:'0901234567', form:{roomId:'a'}, selectedRoomId:'a', propertyId:'property', activeRooms:${JSON.stringify(rooms)}, roomList:${JSON.stringify(rooms)}, editor:{mode:'create'}, selectedBooking:null, bookingType:0, changes:0 }), computed:{ selectedRoom(){ return this.activeRooms.find(r => r.id === this.form.roomId); } }, methods:{ roomChanged(){this.changes++;}, chooseRoom(room){this.selectedRoomId=room.id;this.changes++;}, roomSelectable(){return true;}, roomImage(room){return {thumbnailUrl:room.coverThumbnailUrl};}, availableCount(){return 4;}, money(){return '250.000 đ';}, apiUrl(url){return url+'?siteSlug=one';} } }).mount('#vue-fixture');</script>`;
}
const svg = '<svg xmlns="http://www.w3.org/2000/svg" width="900" height="600"><rect width="900" height="600" fill="#b6d6cd"/><text x="80" y="300" font-size="50">Room photo</text></svg>';

(async () => {
    assert.ok(gzipSync(fs.readFileSync(path.join(root, 'js/core/room-photo-entry.js'))).length < 1400, 'initial photo loader must remain small');
    const browser = await chromium.launch({ headless: true, ...(process.env.CHROME_EXECUTABLE ? { executablePath: process.env.CHROME_EXECUTABLE } : {}) });
    fs.mkdirSync(output, { recursive: true });
    try {
        for (const width of [390, 1280]) {
            const page = await browser.newPage({ viewport: { width, height: 900 } });
            page.setDefaultTimeout(5000);
            const calls = [], errors = [];
            let failViewer = false;
            page.on('pageerror', error => errors.push(error.message));
            await page.route('http://photos.test/**', async route => {
                const url = new URL(route.request().url()); calls.push(url.pathname);
                if (failViewer && url.pathname.endsWith('/room-photo-viewer.js')) return route.abort();
                if (url.pathname === '/admin-fixture' || url.pathname === '/public-fixture') return route.fulfill({ contentType: 'text/html', body: vueFixture(url.pathname === '/admin-fixture') });
                if (url.pathname === '/') return route.fulfill({ contentType: 'text/html', body: `<meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><link rel="stylesheet" href="/css/site.css"><body><main style="padding:20px;max-width:600px"><h1>Đặt phòng</h1><label>Số điện thoại<input id="phone" value="0900000000"></label><input id="time" value="2026-11-20T14:00"><button id="a" data-room-photos="/api/public/rooms/a/photos?siteSlug=one">Xem ảnh Blue</button><button id="b" data-room-photos="/api/public/rooms/b/photos?siteSlug=two">Xem ảnh Green</button><button id="empty" data-room-photos="/api/public/rooms/empty/photos">Phòng chưa có ảnh</button><button id="slow" data-room-photos="/api/public/rooms/slow/photos">Ảnh mạng chậm</button><div><select id="room"><option value="a">Blue</option><option value="b">Green</option><option value="locked" disabled>Phòng khóa</option></select><button id="picker" data-room-photo-picker data-property-id="property">Chọn phòng</button></div><button id="submit">Tiếp tục đặt phòng</button><script id="calendar-page-data" type="application/json">${JSON.stringify({ rooms: [{ id: 'a', code: 'BLUE', coverThumbnailUrl: '/thumb/a.svg', imageCount: 20 }, { id: 'b', code: 'GREEN', coverThumbnailUrl: '/thumb/b.svg', imageCount: 1 }] })}</script></main><script src="/js/core/room-photo-entry.js"></script></body>` });
                if (url.pathname.includes('/photos')) {
                    const id = url.pathname.split('/').at(-2);
                    if (id === 'slow') await new Promise(resolve => setTimeout(resolve, 150));
                    const data = { roomId: id, name: id === 'b' ? 'Green' : 'Blue', propertyName: url.searchParams.get('siteSlug') || 'Staff property', images: Array.from({ length: id === 'empty' ? 0 : id === 'a' ? 20 : 1 }, (_, index) => ({ cardUrl: `/card/${id}-${index}.svg`, thumbnailUrl: `/thumb/${id}-${index}.svg`, largeUrl: `/large/${id}-${index}.svg`, altText: `${id} photo ${index}` })) };
                    return route.fulfill({ json: data }).catch(() => {});
                }
                if (url.pathname.endsWith('.svg')) return route.fulfill({ contentType: 'image/svg+xml', body: svg });
                const file = path.join(root, url.pathname);
                if (!file.startsWith(root) || !fs.existsSync(file)) return route.abort();
                return route.fulfill({ path: file, contentType: file.endsWith('.css') ? 'text/css' : 'application/javascript' });
            });
            await page.goto('http://photos.test/');
            assert.equal(calls.filter(value => value.includes('/photos') || value.includes('room-photo-viewer') || value.startsWith('/card/')).length, 0, 'no album code, metadata or images on initial load');
            await page.locator('#a').click();
            try { await page.getByRole('heading', { name: 'Blue · one' }).waitFor(); }
            catch { throw new Error(JSON.stringify({ calls, errors, text: await page.locator('body').innerText() })); }
            await page.locator('dialog figure img').waitFor();
            await page.waitForFunction(() => document.querySelector('dialog figure img').complete);
            assert.equal(calls.filter(value => value.startsWith('/card/')).length, 1, '20-photo room only loads the visible photo');
            assert.equal(calls.filter(value => value.startsWith('/large/')).length, 0, 'viewer must not load original/full-size images');
            await page.getByRole('button', { name: 'Ảnh tiếp ›' }).click();
            await page.waitForFunction(() => document.querySelector('dialog figcaption').textContent === '2/20 ảnh');
            await page.locator('dialog figure').dispatchEvent('pointerdown', { clientX: 240, clientY: 200 });
            await page.locator('dialog figure').dispatchEvent('pointerup', { clientX: 80, clientY: 205 });
            assert.equal(await page.locator('dialog figcaption').textContent(), '3/20 ảnh');
            await page.keyboard.press('Escape');
            assert.equal(await page.locator('#phone').inputValue(), '0900000000');
            assert.equal(await page.locator('#time').inputValue(), '2026-11-20T14:00');
            assert.equal(await page.locator('#room').inputValue(), 'a');
            await page.locator('#a').click();
            await page.getByRole('heading', { name: 'Blue · one' }).waitFor();
            assert.equal(calls.filter(value => value === '/api/public/rooms/a/photos').length, 1, 'reopening uses bounded metadata cache');
            await page.keyboard.press('Escape');
            await page.locator('#empty').click(); await page.getByText('Phòng chưa có hình ảnh.', { exact: true }).waitFor(); await page.keyboard.press('Escape');
            await page.locator('#slow').click(); await page.getByText('Đang tải ảnh phòng…', { exact: true }).waitFor(); await page.keyboard.press('Escape');
            await page.locator('#b').click(); await page.getByRole('heading', { name: 'Green · two' }).waitFor();
            await page.waitForTimeout(200);
            assert.equal(await page.locator('dialog h2').textContent(), 'Green · two', 'stale room response must not replace current room');
            assert.equal(await page.getByRole('button', { name: 'Ảnh tiếp ›' }).isVisible(), false);
            await page.screenshot({ path: `${output}/gallery-${width}.png` });
            await page.evaluate(() => window.DeLongRoomPhotos.open('/api/public/rooms/a/photos?siteSlug=one', document.querySelector('#a')));
            await page.getByRole('heading', { name: 'Blue · one' }).waitFor();
            assert.equal(await page.evaluate(() => document.documentElement.style.overflow), 'hidden', 'replacing an open album must keep the page locked');
            await page.keyboard.press('Escape');
            await page.waitForFunction(() => document.documentElement.style.overflow !== 'hidden');
            await page.locator('#picker').click();
            await page.getByRole('heading', { name: 'Chọn phòng' }).waitFor();
            await page.getByRole('searchbox', { name: 'Tìm phòng' }).fill('green');
            await page.getByRole('button', { name: 'Green', exact: true }).click();
            assert.equal(await page.locator('#room').inputValue(), 'b');
            await page.locator('#picker').click(); await page.getByRole('heading', { name: 'Chọn phòng' }).waitFor();
            assert.equal(await page.getByRole('button', { name: 'Phòng khóa', exact: true }).isDisabled(), true);
            await page.screenshot({ path: `${output}/picker-${width}.png` });
            await page.keyboard.press('Escape');
            assert.equal(await page.locator('#submit').isEnabled(), true);
            assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), true);
            assert.deepEqual(errors, []);
            await page.goto('http://photos.test/admin-fixture');
            await page.locator('[data-room-photo-picker]').click();
            await page.getByRole('button', { name: 'Ảnh (1)', exact: true }).click();
            await page.getByRole('heading', { name: 'Green · Staff property' }).waitFor();
            assert.equal(await page.locator('#selected').textContent(), 'a', 'preview must not choose room');
            await page.keyboard.press('Escape');
            await page.getByRole('button', { name: 'Green', exact: true }).click();
            await page.waitForFunction(() => document.querySelector('#selected').textContent === 'b');
            assert.equal(await page.locator('#changes').textContent(), '1', 'Vue roomChanged must run exactly once');
            assert.equal(await page.locator('[data-room-photo-picker]').textContent().then(x => x.trim()), 'Green');
            assert.equal(await page.locator('#retained-phone').inputValue(), '0901234567');
            assert.equal(await page.locator('select').isVisible(), false);
            await page.goto('http://photos.test/public-fixture');
            const beforePreview = calls.filter(x => x.includes('/photos')).length;
            await page.getByRole('button', { name: 'Xem ảnh Green', exact: true }).click();
            await page.getByRole('heading', { name: 'Green · one' }).waitFor();
            assert.equal(await page.locator('#selected').textContent(), 'a');
            await page.keyboard.press('Escape');
            await page.locator('.public-booking-room').filter({ hasText: 'Green' }).click();
            await page.waitForFunction(() => document.querySelector('#selected').textContent === 'b');
            assert.equal(await page.locator('#changes').textContent(), '1');
            assert.equal(calls.filter(x => x.includes('/photos')).length, beforePreview + 1, 'choosing room must not fetch album');
            assert.equal(await page.locator('#retained-phone').inputValue(), '0901234567');
            assert.deepEqual(errors, []);
            failViewer = true;
            await page.goto('http://photos.test/admin-fixture');
            await page.locator('[data-room-photo-picker]').click();
            await page.getByText('Không tải được ảnh phòng. Vui lòng thử lại.', { exact: true }).waitFor();
            assert.equal(await page.locator('select').isVisible(), true, 'failed photo module must restore native room selection');
            await page.locator('select').selectOption('b');
            await page.waitForFunction(() => document.querySelector('#selected').textContent === 'b');
            assert.equal(await page.locator('#changes').textContent(), '1');
            assert.equal(await page.locator('#retained-phone').inputValue(), '0901234567');
            await page.close();
            console.log(`PASS ${width}px: no initial album requests; one visible card, swipe, cache, cancellation/stale response, actual Vue room picker and retained booking fields.`);
        }
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
