const { chromium } = require('playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const root = path.resolve(__dirname, '../../src/DeLong.Web');
const read = name => fs.readFileSync(path.join(root, name), 'utf8');

async function verifyAlignment(page) {
    const measurements = await page.evaluate(() => {
        const head = document.querySelector('.public-v2-grid-head');
        const rows = [...document.querySelectorAll('.public-v2-grid-row')];
        const headDate = head.querySelector('.public-v2-date-column').getBoundingClientRect();
        return rows.map(row => {
            const date = row.querySelector('.public-v2-date-column').getBoundingClientRect();
            return {
                headerTracks: getComputedStyle(head).gridTemplateColumns,
                rowTracks: getComputedStyle(row).gridTemplateColumns,
                headerWidth: head.getBoundingClientRect().width,
                rowWidth: row.getBoundingClientRect().width,
                headerDateRight: headDate.right,
                rowDateRight: date.right
            };
        });
    });
    assert.ok(measurements.length > 0);
    for (const measurement of measurements) {
        assert.equal(measurement.headerTracks, measurement.rowTracks, 'hour headings and body must share column sizes');
        assert.ok(Math.abs(measurement.headerWidth - measurement.rowWidth) < 1);
        assert.ok(Math.abs(measurement.headerDateRight - measurement.rowDateRight) < 1, 'sticky date boundaries must align');
    }
}

async function verifyTimeLabels(page) {
    const labels = await page.locator('.public-v2-head-slot').evaluateAll(cells => cells.map(cell => {
        const text = cell.querySelector('strong');
        const range = document.createRange(); range.selectNodeContents(text);
        const bounds = range.getBoundingClientRect(), column = cell.getBoundingClientRect();
        const style = getComputedStyle(text), container = getComputedStyle(cell);
        return { value: text.textContent, width: column.width, leftGap: bounds.left - column.left, rightGap: column.right - bounds.right,
            fontSize: parseFloat(style.fontSize), lineHeight: parseFloat(style.lineHeight), textHeight: bounds.height,
            whiteSpace: style.whiteSpace, textOverflow: style.textOverflow, overflow: style.overflowX,
            padding: parseFloat(container.paddingLeft), mobile: matchMedia('(max-width:760px)').matches };
    }));
    assert.ok(labels.some(label => label.value === '21:00–09:30'), 'overnight hours must remain complete');
    for (const label of labels.filter(item => item.mobile)) {
        assert.ok(label.width >= 128, `${label.value}: mobile column must be at least 128px`);
        assert.ok(label.fontSize >= 11 && label.fontSize <= 13);
        assert.ok(label.padding >= 8);
        assert.ok(label.leftGap >= 8 && label.rightGap >= 8, `${label.value}: full text must fit with breathing room`);
        assert.ok(label.textHeight <= label.lineHeight + 1, 'time range must stay on one line');
        assert.equal(label.whiteSpace, 'nowrap');
        assert.notEqual(label.overflow, 'hidden');
        assert.notEqual(label.textOverflow, 'ellipsis');
    }
}

(async () => {
    const browser = await chromium.launch({ headless: true, ...(process.env.CHROME_EXECUTABLE ? { executablePath: process.env.CHROME_EXECUTABLE } : {}) });
    try {
        for (const [width, roomCount, mobileAllRooms] of [[375, 6, true], [390, 6, true], [414, 6, true], [760, 2, true], [1024, 6, true], [1440, 6, true], [390, 2, false]]) {
            const page = await browser.newPage({ viewport: { width, height: 900 }, hasTouch: true });
            try {
                const errors = [];
                page.on('pageerror', error => errors.push(error.message));
                const rooms = Array.from({ length: roomCount }, (_, index) => ({ id: `r${index}`, name: `Coco Blue Homestay with a long room title #${index + 1}`,
                    code: `R${index}`, siteSlug: 'synthetic', propertyName: 'Synthetic Home', bookingUrl: '/booking', mobileAllRooms }));
                await page.setContent(`<meta name="viewport" content="width=device-width,initial-scale=1"><style>${read('wwwroot/css/public-availability-calendar.css')}body{margin:0;font-family:Arial}section{width:100%;min-width:0}</style><section data-public-availability-calendar><script type="application/json" data-availability-rooms>${JSON.stringify(rooms)}</script><div data-availability-calendar-host></div></section>`);
                await page.evaluate(() => {
                    window.EventSource = class { addEventListener() {} close() {} };
                    window.fetch = async url => {
                        const query = new URL(url, 'https://fixture.invalid');
                        if (query.pathname.includes('booking-policy')) return { ok: true, json: async () => ({}) };
                        const ids = (query.searchParams.get('roomIds') || query.searchParams.get('roomId') || 'r0').split(',');
                        const from = query.searchParams.get('from') || '2026-10-21';
                        const rows = ids.map(roomId => ({ roomId, timeZoneId: 'Asia/Ho_Chi_Minh', calendar: Array.from({ length: 14 }, (_, index) => {
                            const date = new Date(`${from}T00:00:00Z`); date.setUTCDate(date.getUTCDate() + index);
                            const key = date.toISOString().slice(0, 10);
                            const next = new Date(date); next.setUTCDate(next.getUTCDate() + 1);
                            return { date: key, slots: ['03:30', '07:00', '10:30', '14:00'].map((start, slot) => ({
                                rateId: `rate${slot}`, rateName: slot === 3 ? 'Qua đêm' : `Khung ${slot + 1}`, rateType: 0, state: 'available',
                                startUtc: `${key}T${start}:00Z`, endUtc: slot === 3 ? `${next.toISOString().slice(0, 10)}T02:30:00Z` : `${key}T${['06:30', '10:00', '13:30'][slot]}:00Z`,
                                bookableStartUtc: `${key}T${start}:00Z`, bookableEndUtc: slot === 3 ? `${next.toISOString().slice(0, 10)}T02:30:00Z` : `${key}T${['06:30', '10:00', '13:30'][slot]}:00Z`, free: [], occupied: []
                            })) };
                        }) }));
                        return { ok: true, json: async () => query.pathname.includes('rooms-availability') ? { rooms: rows } : rows[0] };
                    };
                });
                await page.addScriptTag({ content: read('wwwroot/js/pages/public-availability-calendar.js') });
                await page.locator('.public-v2-slot-bar').first().waitFor();
                await verifyAlignment(page);
                await verifyTimeLabels(page);
                if (process.env.CALENDAR_SCREENSHOT_PATH && width === 390 && roomCount === 6 && mobileAllRooms) {
                    fs.mkdirSync(path.dirname(process.env.CALENDAR_SCREENSHOT_PATH), { recursive: true });
                    await page.screenshot({ path: process.env.CALENDAR_SCREENSHOT_PATH });
                }
                const multi = width >= 761 || mobileAllRooms;
                assert.equal(await page.locator('.public-v2-viewport.is-multi').count(), multi ? 1 : 0);
                const viewport = page.locator('.public-v2-viewport');
                if (multi) {
                    assert.equal(await page.locator('.public-v2-head-room').count(), roomCount);
                    if (width === 390) {
                        const box = await viewport.boundingBox();
                        const session = await page.context().newCDPSession(page);
                        const y = Math.round(box.y + 140);
                        await session.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [{ x: 340, y }] });
                        for (let x = 310; x >= 100; x -= 30) await session.send('Input.dispatchTouchEvent', { type: 'touchMove', touchPoints: [{ x, y }] });
                        await session.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
                        await page.waitForTimeout(250);
                        assert.ok(await viewport.evaluate(node => node.scrollLeft > 0));
                        assert.equal(await page.locator('.public-v2-slot-bar.is-selected').count(), 0);
                        await verifyAlignment(page);
                        await verifyTimeLabels(page);
                        if (process.env.CALENDAR_SCREENSHOT_PATH && roomCount === 6 && mobileAllRooms) {
                            await page.waitForTimeout(900);
                            await viewport.evaluate(node => { node.scrollTo({ left: 256, behavior: 'instant' }); });
                            await page.waitForTimeout(100);
                            await page.screenshot({ path: process.env.CALENDAR_SCREENSHOT_PATH.replace(/\.png$/, '-scrolled.png') });
                        }
                    }
                }
                await viewport.evaluate(node => { node.scrollTop = 180; });
                await page.waitForTimeout(100);
                await verifyAlignment(page);
                assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false);
                assert.deepEqual(errors, []);
                console.log(`PASS ${width}px, ${roomCount} rooms, mobileAllRooms=${mobileAllRooms}: shared columns, sticky date and scroll`);
            } finally { await page.close(); }
        }
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
