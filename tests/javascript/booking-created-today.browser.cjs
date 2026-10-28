const { chromium } = require('playwright');
const fs = require('node:fs');
const assert = require('node:assert/strict');
const base = 'src/DeLong.Web/';

(async () => {
    const browser = await chromium.launch({ headless: true, ...(process.env.CHROME_EXECUTABLE ? { executablePath: process.env.CHROME_EXECUTABLE } : {}) });
    try {
        for (const width of [390, 1280]) {
            const page = await browser.newPage({ viewport: { width, height: 844 } });
            const errors = []; page.on('pageerror', error => errors.push(error.message));
            const razor = fs.readFileSync(base + 'Pages/Admin/Bookings/Index.cshtml', 'utf8');
            const start = razor.indexOf('        <div class="booking-debt-filters">');
            const end = razor.indexOf('        <div class="entity-toolbar', start);
            const booking = (id, createdAtUtc, source, status = 2) => ({ id, createdAtUtc, source, status, code: id, roomName: 'Mẫu', customerName: 'Synthetic', balanceAmount: 0, checkInUtc: '2027-01-10T03:00:00Z' });
            const initial = { propertyId: 'p', timeZoneId: 'Asia/Ho_Chi_Minh', bookings: [booking('online', '2026-10-28T03:00:00Z', 'Website'), booking('staff', '2026-10-28T04:00:00Z', 'Phone'), booking('cancelled', '2026-10-28T05:00:00Z', null, 5), booking('yesterday', '2026-10-27T16:59:59Z', 'Website')] };
            await page.setContent(`<meta name="viewport" content="width=device-width,initial-scale=1"><body class="admin-body"><script id="bookings-page-data" type="application/json">${JSON.stringify(initial)}</script><main id="bookings-page"><h1>Đặt phòng</h1><section class="panel booking-list-panel">${razor.slice(start, end)}</section></main></body>`);
            for (const css of ['site.css', 'booking-debt-filters.css']) await page.addStyleTag({ path: base + 'wwwroot/css/' + css });
            await page.addScriptTag({ path: process.env.VUE_RUNTIME_PATH });
            await page.evaluate(() => { window.DeLongBookingHistory = {}; });
            await page.addScriptTag({ path: base + 'wwwroot/js/pages/admin-bookings.js' });
            await page.evaluate(() => { window.fixtureVm = document.getElementById('bookings-page')._vnode.component.proxy; fixtureVm.todayDateKey = '2026-10-28'; });
            const counter = page.locator('[data-booking-created-today]');
            await counter.getByText('Hôm nay tạo mới: 3 lượt đặt', { exact: true }).waitFor();
            await page.getByRole('button', { name: 'Còn nợ', exact: true }).click();
            await page.locator('input[type="date"]').first().fill('2027-01-01');
            assert.match(await counter.textContent(), /3 lượt đặt/);
            const bounds = await counter.boundingBox(); assert.ok(bounds.x >= 0 && bounds.x + bounds.width <= width + 1);
            fs.mkdirSync('docs/qa/booking-created-today-2026-10-28', { recursive: true });
            await page.screenshot({ path: `docs/qa/booking-created-today-2026-10-28/summary-${width}.png`, animations: 'disabled' });
            await page.evaluate(() => fixtureVm.bookings.push({ id: 'new', createdAtUtc: '2026-10-28T10:00:00Z', status: 2, balanceAmount: 0, checkInUtc: '2027-01-10T03:00:00Z' }));
            await counter.getByText('Hôm nay tạo mới: 4 lượt đặt', { exact: true }).waitFor();
            assert.deepEqual(errors, []); await page.close();
            console.log(`PASS live count, filter independence and summary layout at ${width}px`);
        }
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
