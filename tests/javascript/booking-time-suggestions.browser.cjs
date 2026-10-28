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
            const source = fs.readFileSync(base + 'Pages/Admin/CalendarV2.cshtml', 'utf8');
            const start = source.indexOf('                <section class="booking-form-section">');
            const end = source.indexOf('</section>', start) + '</section>'.length;
            const rate = { id: 'third', name: 'Khung 3', startTime: '18:00', endTime: '21:00', price: 210000, type: 0, isActive: true, sortOrder: 3 };
            const room = { id: 'room', name: 'Phòng mẫu', capacity: 2, isActive: true, rates: [rate] };
            const initial = { propertyId: 'property', today: '2031-01-10', startDate: '2031-01-10', rooms: [room] };
            await page.setContent(`<meta name="viewport" content="width=device-width,initial-scale=1"><body class="admin-body"><script id="calendar-page-data" type="application/json">${JSON.stringify(initial)}</script><main id="calendar-page"><div class="modal-card booking-modal" style="margin:20px auto;max-width:1000px"><h2>Đặt phòng</h2>${source.slice(start, end)}</div></main></body>`);
            for (const css of ['site.css', 'calendar.css', 'admin-calendar-v2.css', 'admin-shell-fixes.css']) await page.addStyleTag({ path: base + 'wwwroot/css/' + css });
            await page.addScriptTag({ path: process.env.VUE_RUNTIME_PATH });
            await page.evaluate(() => {
                window.DeLongCalendarCanManage = true;
                window.DeLongBookingHistory = {}; window.DeLongRoomBookingLock = {}; window.DeLongCustomerAutocomplete = {};
                window.nextStart = '2031-01-10T11:20:00Z';
                window.DeLongApi = { get: async () => ({ rates: [{ rateId: 'third', defaultStartUtc: '2031-01-10T11:00:00Z', suggestedStartUtc: window.nextStart, endUtc: '2031-01-10T14:00:00Z' }] }) };
            });
            await page.addScriptTag({ path: base + 'wwwroot/js/pages/admin-calendar-nightly.js' });
            await page.addScriptTag({ path: base + 'wwwroot/js/pages/admin-calendar.js' });
            await page.evaluate(() => document.getElementById('calendar-page').__delongCalendarVm.openCreate());
            const select = page.locator('select').nth(1);
            await page.waitForFunction(() => document.querySelectorAll('select')[1].selectedOptions[0].textContent.includes('18:20–21:00'));
            assert.equal(await page.locator('input[type="datetime-local"]').first().inputValue(), '2031-01-10T18:20');
            assert.equal(await page.locator('input[type="datetime-local"]').last().inputValue(), '2031-01-10T21:00');
            fs.mkdirSync('docs/qa/booking-time-suggestions-2026-10-28', { recursive: true });
            await page.waitForFunction(() => Number(getComputedStyle(document.querySelector('.modal-card')).opacity) >= .999);
            await page.screenshot({ path: `docs/qa/booking-time-suggestions-2026-10-28/adjusted-${width}.png` });
            await select.selectOption(''); await select.selectOption('third');
            assert.equal(await page.locator('input[type="datetime-local"]').first().inputValue(), '2031-01-10T18:20');
            await page.evaluate(async () => { window.nextStart = '2031-01-10T11:40:00Z'; await document.getElementById('calendar-page').__delongCalendarVm.loadRateSuggestions(); });
            assert.match(await select.locator('option:checked').textContent(), /18:40–21:00/);
            await page.locator('input[type="datetime-local"]').first().fill('2031-01-10T19:00');
            await page.locator('input[type="datetime-local"]').first().dispatchEvent('change');
            await page.evaluate(async () => { window.nextStart = '2031-01-10T11:20:00Z'; await document.getElementById('calendar-page').__delongCalendarVm.loadRateSuggestions(); });
            assert.equal(await page.locator('input[type="datetime-local"]').first().inputValue(), '2031-01-10T19:00');
            assert.equal(await page.evaluate(() => document.getElementById('calendar-page').__delongCalendarVm.form.roomAmount), 210000);
            assert.deepEqual(errors, []); await page.close();
            console.log(`PASS adjusted dropdown, preset, custom time and unchanged price/end at ${width}px`);
        }
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
