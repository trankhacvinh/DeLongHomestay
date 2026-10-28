const { chromium } = require('playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');

(async () => {
    const browser = await chromium.launch({ headless: true, ...(process.env.CHROME_EXECUTABLE ? { executablePath: process.env.CHROME_EXECUTABLE } : {}) });
    try {
        for (const missingPayload of [false, true]) {
            const page = await browser.newPage({ viewport: { width: 1280, height: 900 } });
            const errors = [];
            page.on('pageerror', error => errors.push(error.message));
            await page.setContent('<div id="calendar-page" data-calendar-v2-page="true" data-property-id="01985a50-1000-7000-8000-000000000001" data-start-date="2026-10-21" data-today="2026-10-21" data-range-days="1"><div class="calendar-page-head"></div></div>');
            await page.evaluate(missing => {
                const colors = { mixedSlotColor: '#FF8800', unpaidColor: '#FF2929', specialRequestColor: '#F34DFF',
                    flexibleTimeColor: '#FAFE01', confirmedColor: '#FFA55F', checkedInColor: '#123456', heldColor: '#FFFF00', requestedColor: '#FFFF8B' };
                const rooms = [{ id: 'room1', name: 'Synthetic room', code: 'R1', capacity: 2, isActive: true }];
                const root = document.getElementById('calendar-page');
                root.__delongCalendarVm = { rooms, calendarColors: colors, bookings: [] };
                if (!missing) {
                    const payload = document.createElement('script');
                    payload.id = 'calendar-page-data'; payload.type = 'application/json';
                    payload.textContent = JSON.stringify({ calendarDisplaySettings: colors, rooms });
                    root.before(payload);
                }
                window.fixtureColors = colors;
                window.fixtureSummary = 1;
                window.DeLongCalendarDailySummary = { create: () => ({
                    load: async () => [{ date: '2026-10-21', bookingCount: window.fixtureSummary }],
                    button: () => {
                        const button = document.createElement('button');
                        button.className = 'fixture-summary'; button.textContent = String(window.fixtureSummary);
                        return button;
                    }
                }) };
                window.DeLongApi = { get: async url => {
                    await new Promise(resolve => setTimeout(resolve, 50));
                    if (url.includes('booking-calendar-settings')) return { ...window.fixtureColors };
                    if (url.includes('/availability/')) return {
                        roomName: 'Synthetic room', timeZoneId: 'Asia/Ho_Chi_Minh', calendar: [{ date: '2026-10-21', slots: [
                            { rateId: 'r1', rateName: 'Khung 1', rateType: 0, state: 'occupied', startUtc: '2026-10-21T03:30:00Z', endUtc: '2026-10-21T06:30:00Z', free: [], occupied: [
                                { bookingId: 'multi', status: 2, isMixedSlot: true, customerName: 'Synthetic multi', startUtc: '2026-10-21T03:30:00Z', endUtc: '2026-10-21T06:30:00Z' }
                            ] },
                            { rateId: 'r2', rateName: 'Khung 2', rateType: 0, state: 'occupied', startUtc: '2026-10-21T07:00:00Z', endUtc: '2026-10-21T10:00:00Z', free: [], occupied: [
                                { bookingId: 'checked', status: 3, customerName: 'Synthetic checked', startUtc: '2026-10-21T07:00:00Z', endUtc: '2026-10-21T10:00:00Z' }
                            ] },
                            { rateId: 'r3', rateName: 'Khung 3', rateType: 0, state: 'partial', startUtc: '2026-10-21T10:30:00Z', endUtc: '2026-10-21T13:30:00Z', free: [], occupied: [
                                { bookingId: 'flexible', status: 2, isFlexibleTime: true, customerName: 'Synthetic flexible', startUtc: '2026-10-21T10:30:00Z', endUtc: '2026-10-21T13:00:00Z' }
                            ] }
                        ] }] };
                    throw new Error(`Unexpected fixture URL: ${url}`);
                } };
            }, missingPayload);
            await page.addScriptTag({ content: fs.readFileSync('src/DeLong.Web/wwwroot/js/pages/admin-calendar-v2.js', 'utf8') });
            await page.waitForFunction(() => document.querySelector('[data-booking-id="multi"]'));
            assert.equal(await page.locator('[data-booking-id="multi"]').evaluate(el => el.style.backgroundColor), 'rgb(255, 136, 0)');
            assert.equal(await page.locator('[data-booking-id="checked"]').evaluate(el => el.style.backgroundColor), 'rgb(18, 52, 86)');
            assert.equal(await page.locator('[data-booking-id="flexible"]').evaluate(el => el.style.backgroundColor), 'rgb(250, 254, 1)');
            assert.equal(await page.locator('[data-v2-color="mixed"]').evaluate(el => el.style.backgroundColor), 'rgb(255, 136, 0)');
            assert.doesNotMatch(await page.locator('.calendar-v2-legend').innerText(), /Đã xác nhận/);
            await page.evaluate(() => {
                document.body.style.minHeight = '2400px';
                document.getElementById('calendar-page').style.marginTop = '400px';
                const scroll = document.querySelector('[data-v2-scroll]');
                Object.assign(scroll.style, { width: '400px', height: '120px', overflow: 'auto' });
                const style = document.createElement('style');
                style.textContent = '.calendar-v2-table { min-width:1400px; height:300px; }';
                document.head.append(style);
                const table = scroll.querySelector('table');
                scroll.scrollLeft = 220; scroll.scrollTop = 30; window.scrollTo(0, 250);
                scroll.querySelector('[data-booking-id="multi"]').focus({ preventScroll: true });
                window.fixtureTable = table;
                document.documentElement.dataset.calendarV2 = 'waiting';
                window.dispatchEvent(new Event('focus'));
            });
            assert.equal(await page.locator('.calendar-v2-panel.loading').count(), 0, 'background refresh must not dim the calendar');
            assert.equal(await page.locator('[data-v2-status]').evaluate(el => el.classList.contains('show')), false, 'background refresh must not insert a loading row');
            await page.waitForFunction(() => document.documentElement.dataset.calendarV2 === 'focus');
            assert.equal(await page.evaluate(() => window.fixtureTable === document.querySelector('.calendar-v2-table')), true, 'unchanged payload must preserve DOM nodes');
            assert.equal(await page.evaluate(() => document.querySelector('[data-v2-scroll]').scrollLeft), 220);
            assert.equal(await page.evaluate(() => window.scrollY), 250);
            await page.evaluate(() => {
                window.fixtureColors.mixedSlotColor = '#945600';
                window.dispatchEvent(new Event('focus'));
            });
            await page.waitForFunction(() => document.querySelector('[data-booking-id="multi"]').style.backgroundColor === 'rgb(148, 86, 0)');
            assert.equal(await page.locator('[data-v2-color="mixed"]').evaluate(el => el.style.backgroundColor), 'rgb(148, 86, 0)');
            assert.equal(await page.evaluate(() => document.querySelector('[data-v2-scroll]').scrollLeft), 220, 'changed colors must preserve horizontal scroll');
            assert.equal(await page.evaluate(() => window.scrollY), 250, 'changed colors must preserve page scroll');
            assert.equal(await page.evaluate(() => document.activeElement.dataset.bookingId), 'multi', 'focused booking must survive refresh');
            await page.evaluate(() => { window.fixtureSummary = 2; window.dispatchEvent(new Event('focus')); });
            await page.waitForFunction(() => document.querySelector('.fixture-summary').textContent === '2');
            assert.deepEqual(errors, []);
            await page.close();
        }
        console.log('PASS: configured colors, silent unchanged refresh, scroll/focus preservation and summary updates (2 browser cases).');
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
