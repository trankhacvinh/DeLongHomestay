// Room photo sheet: lazy loading, swipe album, room list picker and the real admin booking-form markup.
// Run: node tests/javascript/room-photos.browser.cjs (uses the pre-installed Playwright Chromium).
const { chromium } = require('playwright');
const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');
const assert = require('node:assert/strict');
const { gzipSync } = require('node:zlib');
const root = path.resolve('src/DeLong.Web/wwwroot');
const output = path.join(os.tmpdir(), 'homestay-room-photos');
const adminSource = fs.readFileSync(path.join(root, '../Pages/Admin/CalendarV2.cshtml'), 'utf8');
const fieldStart = adminSource.indexOf('<div class="field" data-room-photo-field>');
const adminField = adminSource.slice(fieldStart, adminSource.indexOf('<div class="field">', fieldStart + 1));
const rooms = [
    { id: 'a', name: 'Blue', code: 'BLUE', capacity: 2, imageCount: 20, coverThumbnailUrl: '/thumb/a.svg' },
    { id: 'b', name: 'Green', code: 'GREEN', capacity: 2, imageCount: 1, coverThumbnailUrl: '/thumb/b.svg' },
    { id: 'c', name: 'Đà Lạt Pine', code: 'PINE', capacity: 3, imageCount: 0, coverThumbnailUrl: null },
    { id: 'locked', name: 'Phòng khóa', code: 'LOCK', capacity: 2, imageCount: 1, coverThumbnailUrl: '/thumb/l.svg', isBookingLocked: true }
];
function adminFixture() {
    return `<meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><link rel="stylesheet" href="/css/calendar.css"><body class="admin-tabler"><div id="vue-fixture" style="padding:16px;max-width:700px">${adminField}<input id="retained-phone" v-model="phone"><output id="selected">{{ form.roomId }}</output><output id="changes">{{ changes }}</output></div><script id="calendar-page-data" type="application/json">${JSON.stringify({ rooms })}</script><script src="/vendor/vue/vue.global.prod.js"></script><script src="/js/core/room-photo-entry.js"></script><script>Vue.createApp({ data: () => ({ phone:'0901234567', form:{roomId:'a'}, propertyId:'property', activeRooms:${JSON.stringify(rooms)}, editor:{mode:'create'}, selectedBooking:null, changes:0 }), computed:{ selectedRoom(){ return this.activeRooms.find(r => r.id === this.form.roomId); } }, methods:{ roomChanged(){this.changes++;} } }).mount('#vue-fixture');</script></body>`;
}
const svg = '<svg xmlns="http://www.w3.org/2000/svg" width="900" height="675"><rect width="900" height="675" fill="#b6d6cd"/><text x="80" y="300" font-size="50">Room photo</text></svg>';
const album = (id, count, siteSlug) => ({ roomId: id, name: id === 'b' ? 'Green' : 'Blue', propertyName: siteSlug || 'Staff property', images: Array.from({ length: count }, (_, index) => ({ cardUrl: `/card/${id}-${index}.svg`, thumbnailUrl: `/thumb/${id}-${index}.svg`, largeUrl: `/large/${id}-${index}.svg`, altText: `${id} photo ${index}` })) });

(async () => {
    assert.ok(gzipSync(fs.readFileSync(path.join(root, 'js/core/room-photo-entry.js'))).length < 1400, 'initial photo loader must remain small');
    const browser = await chromium.launch({ headless: true, ...(process.env.CHROME_EXECUTABLE ? { executablePath: process.env.CHROME_EXECUTABLE } : {}) });
    fs.mkdirSync(output, { recursive: true });
    try {
        for (const width of [390, 1280]) {
            const page = await browser.newPage({ viewport: { width, height: 900 }, hasTouch: width < 600 });
            page.setDefaultTimeout(5000);
            const calls = [], errors = [];
            let failViewer = false;
            page.on('pageerror', error => errors.push(error.message));
            await page.route('http://photos.test/**', async route => {
                const url = new URL(route.request().url()); calls.push(url.pathname);
                if (failViewer && url.pathname.endsWith('/room-photo-viewer.js')) return route.abort();
                if (url.pathname === '/admin-fixture') return route.fulfill({ contentType: 'text/html', body: adminFixture() });
                if (url.pathname === '/') return route.fulfill({ contentType: 'text/html', body: `<meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><body><main style="padding:20px;max-width:600px"><h1>Đặt phòng</h1><input id="phone" value="0900000000"><button id="a" data-room-photos="/api/public/rooms/a/photos?siteSlug=one">Xem ảnh Blue</button><button id="b" data-room-photos="/api/public/rooms/b/photos?siteSlug=two">Xem ảnh Green</button><button id="empty" data-room-photos="/api/public/rooms/empty/photos">Phòng chưa có ảnh</button><button id="slow" data-room-photos="/api/public/rooms/slow/photos">Ảnh mạng chậm</button><output id="chosen"></output></main><script src="/js/core/room-photo-entry.js"></script></body>` });
                if (url.pathname.includes('/photos')) {
                    const id = url.pathname.split('/').at(-2);
                    if (id === 'slow') await new Promise(resolve => setTimeout(resolve, 400));
                    return route.fulfill({ json: album(id, id === 'empty' ? 0 : id === 'a' ? 20 : 1, url.searchParams.get('siteSlug')) }).catch(() => {});
                }
                if (url.pathname.endsWith('.svg')) return route.fulfill({ contentType: 'image/svg+xml', body: svg });
                const file = path.join(root, url.pathname);
                if (!file.startsWith(root) || !fs.existsSync(file)) return route.abort();
                return route.fulfill({ path: file, contentType: file.endsWith('.css') ? 'text/css' : 'application/javascript' });
            });

            await page.goto('http://photos.test/');
            assert.equal(calls.filter(value => value.includes('/photos') || value.includes('room-photo-viewer') || value.startsWith('/card/')).length, 0, 'no album code, metadata or images on initial load');

            // Album: only the visible photo and its neighbour load; swipe/next moves; counter follows.
            await page.locator('#a').click();
            await page.getByRole('heading', { name: 'Blue' }).waitFor();
            await page.locator('.rp-slide.is-loaded').first().waitFor();
            await page.waitForTimeout(150);
            assert.ok(calls.filter(value => value.startsWith('/card/') || value.startsWith('/large/')).length <= 2, '20-photo room loads only the visible photo and the next one');
            assert.equal(await page.locator('.rp-count').textContent(), '1/20');
            if (width >= 900) {
                await page.getByRole('button', { name: 'Ảnh tiếp' }).click();
                assert.equal(calls.filter(value => value.startsWith('/card/')).length, 0, 'wide screens use the large rendition');
            } else {
                assert.equal(calls.filter(value => value.startsWith('/large/')).length, 0, 'phones never load the large rendition');
                await page.locator('.rp-track').evaluate(track => track.scrollTo({ left: track.clientWidth }));
            }
            await page.waitForFunction(() => document.querySelector('.rp-count').textContent === '2/20');
            assert.equal(await page.locator('.rp-foot').isHidden(), true, 'plain album has no choose button');
            assert.equal(await page.evaluate(() => document.documentElement.style.overflow), 'hidden', 'page is locked behind the sheet');
            await page.screenshot({ path: `${output}/album-${width}.png` });
            await page.keyboard.press('Escape');
            await page.waitForFunction(() => document.documentElement.style.overflow !== 'hidden');
            assert.equal(await page.locator('#phone').inputValue(), '0900000000');

            await page.locator('#a').click();
            await page.getByRole('heading', { name: 'Blue' }).waitFor();
            assert.equal(calls.filter(value => value === '/api/public/rooms/a/photos').length, 1, 'reopening uses the metadata cache');
            await page.keyboard.press('Escape');
            await page.locator('#empty').click(); await page.getByText('Phòng chưa có hình ảnh.', { exact: true }).waitFor(); await page.keyboard.press('Escape');
            await page.locator('#slow').click(); await page.getByText('Đang tải ảnh phòng…', { exact: true }).waitFor(); await page.keyboard.press('Escape');
            await page.locator('#b').click(); await page.getByRole('heading', { name: 'Green' }).waitFor();
            await page.waitForTimeout(500);
            assert.equal(await page.locator('.rp-titles h2').textContent(), 'Green', 'stale room response must not replace current room');
            assert.equal(await page.locator('.rp-titles small').textContent(), 'two', 'branch comes from the album data');
            assert.equal(await page.locator('.rp-nav').count(), 0, 'single photo has no arrows');
            await page.keyboard.press('Escape');

            // Inline album data + choose callback (public calendar path): no request, choose runs once.
            await page.evaluate(() => window.DeLongRoomPhotos.open({ name: 'Inline', images: [{ cardUrl: '/card/i-0.svg' }, { cardUrl: '/card/i-1.svg' }] }, null, { onChoose: () => { document.querySelector('#chosen').textContent += 'x'; } }));
            await page.getByRole('heading', { name: 'Inline' }).waitFor();
            await page.getByRole('button', { name: 'Chọn phòng này' }).click();
            assert.equal(await page.locator('#chosen').textContent(), 'x');
            await page.waitForFunction(() => !document.querySelector('dialog'));

            // Admin booking form: real CalendarV2 markup + Vue v-model on the hidden <select>.
            await page.goto('http://photos.test/admin-fixture');
            assert.equal(await page.locator('select').isVisible(), false);
            assert.equal((await page.locator('.room-pick-main strong').textContent()).trim(), 'Blue');
            await page.locator('[data-room-photo-picker]').click();
            await page.getByRole('heading', { name: 'Chọn phòng' }).waitFor();
            assert.equal(await page.locator('.rp-item.is-selected .rp-item-copy strong').textContent(), 'Blue');
            assert.equal(await page.getByRole('button', { name: /Phòng khóa/ }).last().isDisabled(), true, 'locked room cannot be chosen');
            await page.screenshot({ path: `${output}/picker-${width}.png` });
            // Album inside the same sheet: no stacked dialog, preview does not choose, back returns to list.
            await page.getByRole('button', { name: 'Xem 1 ảnh Green' }).click();
            await page.getByRole('heading', { name: 'Green' }).waitFor();
            assert.equal(await page.locator('dialog').count(), 1, 'album opens inside the picker, not on top of it');
            assert.equal(await page.locator('#selected').textContent(), 'a', 'preview must not choose room');
            await page.getByRole('button', { name: 'Quay lại danh sách phòng' }).click();
            await page.getByRole('heading', { name: 'Chọn phòng' }).waitFor();
            await page.getByRole('button', { name: 'Xem 1 ảnh Green' }).click();
            await page.getByRole('button', { name: 'Chọn phòng này' }).click();
            await page.waitForFunction(() => document.querySelector('#selected').textContent === 'b');
            assert.equal(await page.locator('#changes').textContent(), '1', 'Vue roomChanged must run exactly once');
            assert.equal((await page.locator('.room-pick-main strong').textContent()).trim(), 'Green');
            // Search ignores Vietnamese accents (incl. đ) and picks from the list row.
            await page.locator('[data-room-photo-picker]').click();
            const rowsBefore = await page.locator('.rp-item').count();
            assert.equal(rowsBefore, 4);
            assert.equal(await page.locator('.rp-search').count(), 0, 'short lists skip the search box');
            await page.getByRole('button', { name: /Đà Lạt Pine/ }).click();
            await page.waitForFunction(() => document.querySelector('#selected').textContent === 'c');
            assert.equal(await page.locator('.room-pick-photo.is-empty').count(), 1, 'room without photos shows placeholder');
            assert.equal(await page.locator('#retained-phone').inputValue(), '0901234567');
            await page.evaluate(() => window.DeLongRoomPhotos.list({ groups: [{ name: 'A', rooms: Array.from({ length: 10 }, (_, i) => ({ id: String(i), name: i === 7 ? 'Đà Lạt Mist' : `Room ${i}`, imageCount: 0 })) }], onSelect: () => {} }));
            await page.getByRole('searchbox', { name: 'Tìm phòng' }).fill('da lat');
            assert.equal(await page.locator('.rp-item').count(), 1);
            await page.keyboard.press('Escape');
            assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), true);
            assert.deepEqual(errors, []);

            failViewer = true;
            await page.goto('http://photos.test/admin-fixture');
            await page.locator('[data-room-photo-picker]').click();
            await page.getByText('Không tải được ảnh phòng. Vui lòng thử lại.', { exact: true }).waitFor();
            assert.equal(await page.locator('select').isVisible(), true, 'failed photo module must restore native room selection');
            assert.equal(await page.locator('.room-pick').isVisible(), false);
            await page.locator('select').selectOption('b');
            await page.waitForFunction(() => document.querySelector('#selected').textContent === 'b');
            assert.equal(await page.locator('#changes').textContent(), '1');
            await page.close();
            console.log(`PASS ${width}px: lazy album, swipe/next, cache, stale response, inline album + choose, admin picker sheet, accent-free search, fallback select.`);
        }
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
