const { chromium } = require('playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const web = path.resolve('src/DeLong.Web');
const output = process.env.ARTIFACT_DIR || '/private/tmp/homestay-date-presentation';
const asset = file => path.join(web, 'wwwroot', file);

async function scripts(page) {
    for (const file of ['vendor/vue/vue.global.prod.js', 'vendor/flatpickr/flatpickr.min.js', 'vendor/flatpickr/vn.js', 'vendor/flatpickr/monthSelect.js']) {
        await page.addScriptTag({ path: asset(file) });
    }
}

async function styles(page, voucher = false) {
    for (const file of ['vendor/flatpickr/flatpickr.min.css', 'vendor/flatpickr/monthSelect.css', 'css/site.css', 'css/calendar.css', 'css/operational-polish.css', 'css/admin-shell-fixes.css', ...(voucher ? ['css/entities.css', 'css/admin-vouchers.css'] : []), 'css/date-presentation.css']) {
        if (fs.existsSync(asset(file))) await page.addStyleTag({ path: asset(file) });
    }
}

async function enhance(page) {
    if (fs.existsSync(asset('js/core/date-presentation.js'))) await page.addScriptTag({ path: asset('js/core/date-presentation.js') });
}

async function dateFields(browser) {
    const page = await browser.newPage({ locale: 'en-US', timezoneId: 'Asia/Ho_Chi_Minh', viewport: { width: 1280, height: 900 } });
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    await page.setContent(`<body class="admin-body"><main id="fixture" style="padding:24px;max-width:700px">
        <form id="dates"><label for="day">Ngày làm việc</label><input id="day" name="date" type="date" v-model="day" v-bind:min="minimum" v-bind:max="maximum" required v-on:change="changes++">
        <label>Ngày giờ<input id="when" name="when" type="datetime-local" v-model="when" required></label>
        <label>Tháng<input id="month" name="month" type="month" v-model="month"></label>
        <input id="optional" type="date" v-model="optional">
        <input id="disabled" type="date" v-model="day" v-bind:disabled="disabled">
        <section v-if="open"><input id="dynamic" type="date" v-model="day"></section></form>
        </main></body>`);
    await styles(page);
    await scripts(page);
    await page.evaluate(() => {
        window.state = Vue.reactive({ day: '2026-11-03', when: '2026-11-03T21:15', month: '2026-11', minimum: '2026-11-01', maximum: '2026-11-30', optional: '', open: false, disabled: true, changes: 0 });
        Vue.createApp({ data: () => window.state }).mount('#fixture');
    });
    await enhance(page);
    await page.waitForFunction(() => !!document.getElementById('day')._flatpickr);
    assert.equal(await page.locator('#day').evaluate(el => el._flatpickr.altInput.value), '03/11/2026');
    assert.equal(await page.locator('#when').evaluate(el => el._flatpickr.altInput.value), '03/11/2026 21:15');
    assert.equal(await page.locator('#month').evaluate(el => el._flatpickr.altInput.value), '11/2026');
    assert.equal(await page.locator('#optional').evaluate(el => el._flatpickr.altInput.value), '');
    assert.equal(await page.locator('#disabled').evaluate(el => el._flatpickr.altInput.disabled), true);

    const displayedDate = page.getByLabel('Ngày làm việc', { exact: true });
    await displayedDate.fill('05/11/2026');
    await displayedDate.press('Tab');
    await page.waitForFunction(() => state.day === '2026-11-05');
    assert.equal(await page.evaluate(() => state.changes), 1, 'manual date commits once');
    const displayedTime = page.locator('input[data-date-display="datetime-local"]');
    await displayedTime.fill('04/11/2026 22:45');
    await displayedTime.press('Tab');
    await page.waitForFunction(() => state.when === '2026-11-04T22:45');
    const displayedMonth = page.locator('input[data-date-display="month"]');
    await displayedMonth.fill('12/2026');
    await displayedMonth.press('Tab');
    await page.waitForFunction(() => state.month === '2026-12');
    const data = await page.evaluate(() => Object.fromEntries(new FormData(document.getElementById('dates'))));
    assert.equal(data.date, '2026-11-05');
    assert.equal(data.when, '2026-11-04T22:45');
    assert.equal(data.month, '2026-12');

    await page.evaluate(async () => { state.day = '2026-11-06'; state.when = '2026-11-07T08:30'; state.month = '2027-02'; state.disabled = false; await Vue.nextTick(); });
    assert.equal(await displayedDate.inputValue(), '06/11/2026');
    assert.equal(await page.locator('#when').evaluate(el => el._flatpickr.altInput.value), '07/11/2026 08:30');
    assert.equal(await page.locator('#month').evaluate(el => el._flatpickr.altInput.value), '02/2027');
    assert.equal(await page.locator('#disabled').evaluate(el => el._flatpickr.altInput.disabled), false);
    assert.equal(await page.evaluate(() => state.changes), 1, 'programmatic synchronization must not submit changes');

    await page.evaluate(async () => { state.minimum = '2026-11-04'; state.maximum = '2026-11-20'; await Vue.nextTick(); });
    await page.waitForFunction(() => document.getElementById('day')._flatpickr.config.minDate.getDate() === 4);
    assert.equal(await page.locator('#day').evaluate(el => el._flatpickr.config.maxDate.getDate()), 20);
    await page.evaluate(async () => { state.maximum = '2026-11-05'; await Vue.nextTick(); });
    await page.waitForFunction(() => document.getElementById('day')._flatpickr.config.maxDate.getDate() === 5);
    assert.equal(await displayedDate.inputValue(), '06/11/2026', 'changed bounds must not clear an existing model value');
    assert.equal(await page.evaluate(() => state.day), '2026-11-06');
    assert.equal(await displayedDate.evaluate(el => el.checkValidity()), false);
    await page.evaluate(async () => { state.maximum = '2026-11-20'; await Vue.nextTick(); });
    await page.waitForFunction(() => document.getElementById('day')._flatpickr.config.maxDate.getDate() === 20);
    assert.equal(await displayedDate.evaluate(el => el.checkValidity()), true);

    await page.evaluate(() => { state.open = true; });
    await page.waitForFunction(() => !!document.getElementById('dynamic')?._flatpickr);
    assert.equal(await page.locator('#dynamic').evaluate(el => el._flatpickr.altInput.value), '06/11/2026');
    await page.evaluate(() => { window.removedDateInput = document.getElementById('dynamic'); state.open = false; });
    await page.waitForFunction(() => !window.removedDateInput._flatpickr);

    await displayedDate.fill('31/02/2026');
    await displayedDate.press('Tab');
    assert.notEqual(await page.evaluate(() => state.day), '2026-03-03', 'invalid date must not silently roll forward');
    assert.equal(await page.evaluate(() => document.getElementById('dates').checkValidity()), false,
        JSON.stringify(await page.evaluate(() => ({ model: state.day, value: document.getElementById('day').value, display: document.getElementById('day')._flatpickr.altInput.value }))) + JSON.stringify(errors));
    await page.evaluate(() => { state.day = ''; });
    await page.waitForFunction(() => document.getElementById('day')._flatpickr.altInput.value === '');

    await page.evaluate(() => {
        document.body.insertAdjacentHTML('beforeend', '<div id="late-page" v-cloak><input id="late-date" type="date" v-model="day"></div><form id="static-form"><input id="static-date" type="date" name="date" value="2026-11-03"></form>');
    });
    await page.waitForFunction(() => !!document.getElementById('static-date')._flatpickr);
    assert.equal(await page.locator('#late-date').getAttribute('type'), 'date', 'unmounted Vue template must stay untouched');
    await page.evaluate(() => { Vue.createApp({ data: () => ({ day: '2026-12-04' }) }).mount('#late-page'); });
    await page.waitForFunction(() => !!document.getElementById('late-date')._flatpickr);
    assert.equal(await page.locator('#late-date').evaluate(el => el._flatpickr.altInput.value), '04/12/2026');
    await page.evaluate(() => {
        document.getElementById('static-date').value = '2026-12-05';
        document.getElementById('static-form').reset();
    });
    await page.waitForFunction(() => document.getElementById('static-date').value === '2026-11-03');
    assert.equal(await page.locator('#static-date').evaluate(el => el._flatpickr.altInput.value), '03/11/2026');
    assert.deepEqual(errors, []);
    await page.close();
}

async function voucherModal(browser, width) {
    const page = await browser.newPage({ locale: 'en-US', timezoneId: 'Asia/Ho_Chi_Minh', viewport: { width, height: 900 } });
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    const file = fs.readFileSync(path.join(web, 'Pages/Admin/Vouchers/Index.cshtml'), 'utf8');
    const initial = { propertyId: 'property', propertyName: 'Chi nhánh', timeZoneId: 'Asia/Ho_Chi_Minh', canManage: true, customers: [] };
    const markup = file.slice(file.indexOf('<div id="vouchers-page"'), file.indexOf('@section Scripts'))
        .replace('@Html.Raw(Model.PageDataJson)', JSON.stringify(initial));
    await page.setContent(`<body class="admin-body">${markup}</body>`);
    await styles(page, true);
    await scripts(page);
    await page.evaluate(() => {
        const voucher = { id: 'voucher', code: 'G100', status: 1, discountPercent: 10, appliesTo: 7, startsAtUtc: '2026-11-01T00:00:00Z', endsAtUtc: '2026-12-01T00:00:00Z', reservedCount: 0, redeemedCount: 0 };
        window.DeLongApi = { get: async url => url.includes('/history') ? { redemptions: [], emailDeliveries: [] } : [voucher] };
    });
    await enhance(page);
    await page.addScriptTag({ path: asset('js/pages/admin-vouchers.js') });
    await page.locator('.voucher-code-cell strong').click();
    const detail = page.locator('.voucher-detail');
    await detail.waitFor();
    const gaps = await detail.evaluate(el => {
        const outer = el.getBoundingClientRect();
        return ['.voucher-detail-metrics', '.voucher-history-wrap', '.voucher-email-history'].map(selector => {
            const rect = el.querySelector(selector).getBoundingClientRect();
            return { left: rect.left - outer.left, right: outer.right - rect.right };
        });
    });
    assert.ok(gaps.every(gap => gap.left >= 16 && gap.right >= 16), `voucher content touches modal edge at ${width}: ${JSON.stringify(gaps)}`);
    assert.equal(await detail.evaluate(el => el.scrollWidth <= el.clientWidth + 1), true);
    await page.screenshot({ animations: 'disabled', path: `${output}/voucher-detail-${width}.png` });
    await detail.getByRole('button', { name: 'Đóng', exact: true }).click();
    await page.getByRole('button', { name: 'Tạo voucher', exact: true }).click();
    const editor = page.locator('.voucher-editor');
    const visibleDate = editor.locator('input[placeholder="dd/MM/yyyy HH:mm"]:not([type="hidden"])').first();
    await visibleDate.click();
    assert.match(await visibleDate.inputValue(), /^\d{2}\/\d{2}\/\d{4} \d{2}:\d{2}$/);
    const calendar = page.locator('.flatpickr-calendar.open');
    assert.equal(await calendar.locator('.cur-year').inputValue(), String(new Date().getFullYear()));
    assert.ok(await calendar.locator('.cur-year').evaluate(el => el.getBoundingClientRect().width) > 40, 'calendar year remains readable');
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), true);
    await page.screenshot({ animations: 'disabled', path: `${output}/voucher-editor-${width}.png` });
    assert.deepEqual(errors, []);
    await page.close();
}

(async () => {
    fs.mkdirSync(output, { recursive: true });
    const browser = await chromium.launch({ headless: true, ...(process.env.CHROME_EXECUTABLE ? { executablePath: process.env.CHROME_EXECUTABLE } : {}) });
    try {
        await dateFields(browser);
        for (const width of [390, 1280]) await voucherModal(browser, width);
        console.log('PASS: Vietnamese date fields, Vue/form synchronization, constraints, dynamic cleanup and voucher modals at 390/1280px.');
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
