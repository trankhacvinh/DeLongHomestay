const { chromium } = require('playwright');
const fs = require('node:fs');
const assert = require('node:assert/strict');
const path = require('node:path');

const web = path.resolve('src/DeLong.Web');
const output = process.env.ARTIFACT_DIR || '/private/tmp/homestay-booking-creation-origin';

(async () => {
    fs.mkdirSync(output, { recursive: true });
    const browser = await chromium.launch({ headless: true, ...(process.env.CHROME_EXECUTABLE ? { executablePath: process.env.CHROME_EXECUTABLE } : {}) });
    try {
        for (const width of [390, 1280]) {
            const page = await browser.newPage({ viewport: { width, height: 960 } });
            const errors = [];
            page.on('pageerror', error => errors.push(error.message));
            const razor = fs.readFileSync(path.join(web, 'Pages/Admin/Bookings/Index.cshtml'), 'utf8');
            const start = razor.indexOf('    <section class="panel booking-list-panel">');
            const end = razor.indexOf('    </section>', start) + '    </section>'.length;
            const row = (id, creationOrigin, extra = {}) => ({ id, code: id, creationOrigin, customerName: 'Khách mẫu', customerPhone: '0900000000', roomName: 'Blue', source: 'Website',
                createdAtUtc: '2026-11-07T03:00:00Z', checkInUtc: '2026-11-20T07:00:00Z', checkOutUtc: '2026-11-20T10:00:00Z',
                status: 2, totalAmount: 250000, paidAmount: 150000, balanceAmount: 100000, ...extra });
            const initial = { propertyId: 'p', timeZoneId: 'Asia/Ho_Chi_Minh', bookings: [
                row('BK-KHACH', 1, { source: 'Zalo' }), row('BK-NHANVIEN', 2), row('BK-KHONGRO', 0), row('BK-CU', undefined),
                row('BK-DATHU', 1, { paidAmount: 250000, balanceAmount: 0 }), row('BK-HUY', 1, { status: 5 })
            ] };
            await page.setContent(`<meta name="viewport" content="width=device-width,initial-scale=1"><body class="admin-body"><script id="bookings-page-data" type="application/json">${JSON.stringify(initial)}</script><main id="bookings-page" v-cloak><h1>Đặt phòng</h1>${razor.slice(start, end)}</main></body>`);
            for (const css of ['site.css', 'entities.css', 'booking-debt-filters.css', 'booking-v2-polish.css', 'date-presentation.css']) {
                await page.addStyleTag({ path: path.join(web, 'wwwroot/css', css) });
            }
            for (const css of ['flatpickr.min.css', 'monthSelect.css']) await page.addStyleTag({ path: path.join(web, 'wwwroot/vendor/flatpickr', css) });
            await page.addStyleTag({ content: '#bookings-page{padding:16px;width:calc(100% - 260px);margin:auto;min-width:0}@media(max-width:700px){#bookings-page{width:100%}}' });
            await page.addScriptTag({ path: path.join(web, 'wwwroot/vendor/vue/vue.global.prod.js') });
            for (const script of ['flatpickr.min.js', 'vn.js', 'monthSelect.js']) await page.addScriptTag({ path: path.join(web, 'wwwroot/vendor/flatpickr', script) });
            await page.addScriptTag({ path: path.join(web, 'wwwroot/js/core/date-presentation.js') });
            await page.evaluate(() => { window.DeLongBookingHistory = {}; });
            await page.addScriptTag({ path: path.join(web, 'wwwroot/js/pages/admin-bookings.js') });
            await page.evaluate(() => {
                window.fixtureVm = document.getElementById('bookings-page')._vnode.component.proxy;
                fixtureVm.todayDateKey = '2026-11-07';
            });
            await page.waitForFunction(() => !!document.querySelector('[data-date-display="date"]'));

            const filter = page.getByRole('combobox', { name: 'Người tạo booking', exact: true });
            const rows = page.locator('tbody tr.clickable-row');
            const counter = page.locator('[data-booking-created-today]');
            assert.equal(await rows.count(), 6);
            await filter.selectOption('1');
            assert.equal(await rows.count(), 3);
            assert.deepEqual(await page.locator('[data-booking-creation-origin]').allTextContents(), ['Khách tự đặt', 'Khách tự đặt', 'Khách tự đặt']);
            await page.getByRole('button', { name: 'Còn nợ', exact: true }).click();
            await page.getByRole('combobox', { name: 'Lọc trạng thái', exact: true }).selectOption('2');
            await page.getByLabel('Từ ngày tạo booking', { exact: true }).fill('07/11/2026');
            await page.getByLabel('Từ ngày tạo booking', { exact: true }).press('Tab');
            await page.getByLabel('Đến ngày tạo booking', { exact: true }).fill('07/11/2026');
            await page.getByLabel('Đến ngày tạo booking', { exact: true }).press('Tab');
            await page.getByRole('textbox', { name: 'Tìm lượt đặt phòng', exact: true }).fill('Blue');
            assert.equal(await rows.count(), 1);
            assert.equal(await rows.first().locator('.booking-code').textContent(), 'BK-KHACH');
            assert.match(await counter.textContent(), /Hôm nay tạo mới: 6 lượt đặt/);

            await filter.selectOption('2');
            assert.equal(await rows.count(), 1);
            assert.equal(await rows.first().locator('.booking-code').textContent(), 'BK-NHANVIEN');
            await filter.selectOption('0');
            assert.equal(await rows.count(), 2);
            assert.deepEqual(await page.locator('[data-booking-creation-origin]').allTextContents(), ['Chưa xác định', 'Chưa xác định']);

            await filter.selectOption('');
            await page.getByRole('button', { name: 'Tất cả', exact: true }).click();
            await page.getByRole('button', { name: 'Tất cả thời gian', exact: true }).click();
            await page.getByRole('combobox', { name: 'Lọc trạng thái', exact: true }).selectOption('');
            await page.getByRole('textbox', { name: 'Tìm lượt đặt phòng', exact: true }).fill('');
            assert.equal(await rows.count(), 6);
            await filter.selectOption('1');
            await page.evaluate(() => fixtureVm.bookings.push({ ...fixtureVm.bookings[0], id: 'BK-MOI', code: 'BK-MOI' }));
            assert.equal(await rows.count(), 4);
            assert.match(await counter.textContent(), /Hôm nay tạo mới: 7 lượt đặt/);

            const debtSummary = page.locator('[data-booking-debt-summary]');
            assert.equal(await debtSummary.evaluate(el => getComputedStyle(el).color), 'rgb(180, 69, 69)');
            if (width > 1100) {
                const positions = await page.locator('.booking-toolbar-main > input, .booking-toolbar-main > select')
                    .evaluateAll(elements => elements.map(el => el.getBoundingClientRect().top));
                assert.equal(positions.length, 4);
                assert.ok(Math.max(...positions) - Math.min(...positions) <= 1, 'desktop search and filters must share one line');
            }

            assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), true, `page overflows at ${width}px`);
            const bounds = await filter.boundingBox();
            assert.ok(bounds.x >= 0 && bounds.x + bounds.width <= width + 1);
            await page.screenshot({ path: `${output}/booking-origin-${width}.png`, animations: 'disabled', fullPage: true });
            assert.deepEqual(errors, []);
            await page.close();
            console.log(`PASS creator filter, labels, combined filters and refreshed data at ${width}px.`);
        }
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
