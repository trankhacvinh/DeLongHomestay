const { chromium } = require('playwright');
const fs = require('node:fs');
const assert = require('node:assert/strict');
const base = 'src/DeLong.Web/';

(async () => {
    const browser = await chromium.launch({ headless: true, ...(process.env.CHROME_EXECUTABLE ? { executablePath: process.env.CHROME_EXECUTABLE } : {}) });
    try {
        for (const view of ['Calendar', 'CalendarV2']) for (const width of [390, 1280]) {
            const page = await browser.newPage({ viewport: { width, height: 900 } });
            const errors = []; page.on('pageerror', error => errors.push(error.message));
            const source = fs.readFileSync(`${base}Pages/Admin/${view}.cshtml`, 'utf8');
            const start = source.indexOf('<section v-if="canManage && selectedBooking.balanceAmount');
            const end = source.indexOf('</section>', start) + 10;
            await page.setContent(`<meta name="viewport" content="width=device-width,initial-scale=1"><body class="admin-body"><main id="fixture"><div class="modal-backdrop" v-if="!bookingHistory.open"><section class="modal-card booking-editor" style="width:min(1100px,96vw)"><div class="modal-head"><h2>Chi tiết lượt đặt</h2></div>${source.slice(start, end)}</section></div>${fs.readFileSync(base + 'Pages/Shared/_BookingHistoryDialog.cshtml', 'utf8')}</main></body>`);
            for (const css of ['site.css', 'calendar.css', 'admin-shell-fixes.css', 'admin-booking-guest-details.css', 'admin-booking-history.css', ...(view === 'CalendarV2' ? ['admin-calendar-v2.css'] : [])]) await page.addStyleTag({ path: base + 'wwwroot/css/' + css });
            await page.addScriptTag({ path: process.env.VUE_RUNTIME_PATH });
            await page.addScriptTag({ path: base + 'wwwroot/js/core/money-input.js' });
            await page.addScriptTag({ path: base + 'wwwroot/js/pages/admin-booking-history.js' });
            await page.evaluate(() => {
                window.fixtureVm = Vue.createApp({
                    data: () => ({ canManage: true, saving: false, selectedBooking: { code: 'BK-260820-10109A1F03', balanceAmount: 250000, status: 2 }, additionalPayment: { amount: 0, method: 1 }, bookingHistory: { open: false, loading: false, error: '', more: false, selected: null, rows: [{ id: '1', action: 'StatusChanged', createdAtUtc: '2026-10-28T08:00:00Z', beforeJson: '{"status":1}', afterJson: '{"status":2}' }, { id: '2', action: 'Created', actorName: 'Nhân viên có tên dài để kiểm tra xuống dòng trên điện thoại', createdAtUtc: '2026-10-28T07:00:00Z', afterJson: JSON.stringify({ note: 'Nội dung chi tiết dài '.repeat(30) }) }] } }),
                    methods: { ...DeLongBookingHistory.methods, saveAdditionalPayment() { window.paymentClicked = true; } }
                }).mixin(window.DeLongMoneyInput || {}).mount('#fixture');
            });
            await page.waitForFunction(() => Number(getComputedStyle(document.querySelector('.modal-card')).opacity) >= .999);
            const panel = page.locator('.booking-payment-panel');
            const geometry = await panel.evaluate(el => {
                const title = el.querySelector(':scope > strong').getBoundingClientRect();
                const grid = el.querySelector('.booking-money-grid').getBoundingClientRect();
                const input = el.querySelector('input').getBoundingClientRect();
                const button = el.querySelector('button').getBoundingClientRect();
                return { titleGap: grid.top - title.bottom, mobileGap: button.top - input.bottom, inputRight: input.right, buttonLeft: button.left, scroll: el.scrollWidth, width: el.clientWidth };
            });
            assert.ok(geometry.titleGap >= 10); assert.ok(geometry.scroll <= geometry.width + 1);
            if (width === 390) assert.ok(geometry.mobileGap >= 10); else assert.ok(geometry.buttonLeft > geometry.inputRight);
            await panel.getByRole('button', { name: 'Ghi nhận khoản thu', exact: true }).click(); assert.equal(await page.evaluate(() => window.paymentClicked), true);
            const output = 'docs/qa/booking-modal-spacing-2026-10-28'; fs.mkdirSync(output, { recursive: true });
            await page.screenshot({ path: `${output}/payment-${view}-${width}.png`, animations: 'disabled' });
            await page.evaluate(() => { fixtureVm.bookingHistory.open = true; });
            const dialog = page.getByRole('dialog'); await dialog.waitFor();
            await page.waitForFunction(() => Number(getComputedStyle(document.querySelector('.booking-history-dialog')).opacity) >= .999);
            const history = await dialog.evaluate(el => {
                const box = el.getBoundingClientRect(), body = el.querySelector('.booking-history-body').getBoundingClientRect();
                const title = el.querySelector('.booking-history-entry strong').getBoundingClientRect(), meta = el.querySelector('.booking-history-meta').getBoundingClientRect();
                return { inset: body.left + parseFloat(getComputedStyle(el.querySelector('.booking-history-body')).paddingLeft) - box.left, gap: meta.top - title.bottom, overflow: el.scrollWidth > el.clientWidth + 1 };
            });
            assert.ok(history.inset >= 16); assert.ok(history.gap >= 4); assert.equal(history.overflow, false);
            await dialog.getByRole('button', { name: 'Chi tiết', exact: true }).first().click();
            assert.equal(await dialog.getByRole('button', { name: 'Chi tiết', exact: true }).first().getAttribute('aria-expanded'), 'true');
            await dialog.locator('.booking-history-changes').waitFor();
            await page.screenshot({ path: `${output}/history-${view}-${width}.png`, animations: 'disabled' });
            await dialog.getByRole('button', { name: 'Đóng lịch sử' }).click(); await dialog.waitFor({ state: 'detached' });
            assert.deepEqual(errors, []); await page.close(); console.log(`PASS payment/history spacing and controls ${view} ${width}px`);
        }
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
