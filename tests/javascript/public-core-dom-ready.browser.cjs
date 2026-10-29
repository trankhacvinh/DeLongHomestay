const { chromium } = require('playwright');
const fs = require('node:fs');
const assert = require('node:assert/strict');

(async () => {
    const browser = await chromium.launch({ headless: true, executablePath: process.env.CHROME_EXECUTABLE });
    try {
        const page = await browser.newPage({ viewport: { width: 390, height: 844 } });
        const errors = []; page.on('pageerror', e => errors.push(e.message));
        await page.route('https://fixture.test/**', async route => {
            const pathname = new URL(route.request().url()).pathname;
            if (pathname === '/slow-image') return;
            if (pathname.startsWith('/js/')) return route.fulfill({ contentType: 'application/javascript', body: fs.readFileSync('src/DeLong.Web/wwwroot' + pathname, 'utf8') });
            if (pathname.startsWith('/api/')) return route.fulfill({ contentType: 'application/json', body: JSON.stringify({ requireIdentityDocuments: true, includedGuests: 2, policyVersion: 1, policyTitle: 'Nội quy', policyText: 'Quy định mẫu' }) });
            return route.fulfill({ contentType: 'text/html', body: `<html><body class="public-body dl-theme-standard"><img src="/slow-image"><script>window.addEventListener('load',()=>window.loadFired=true)</script><script id="public-booking-data" type="application/json">{}</script><main id="public-booking-page"><div class="public-embedded-selection-summary" data-room-capacity="4"></div><div class="public-contact-step"><div class="public-form-grid"><label>Tên<input data-booking-customer-name></label><label>Điện thoại<input data-booking-customer-phone></label><div class="public-form-full"></div></div></div></main><script src="/js/core/api.js"></script></body></html>` });
        });
        await page.goto('https://fixture.test/booking', { waitUntil: 'domcontentloaded' });
        await page.locator('#public-booking-page input[type="file"]').first().waitFor({ state: 'attached' });
        assert.equal(await page.evaluate(() => document.readyState), 'interactive');
        assert.equal(await page.evaluate(() => window.loadFired === true), false);
        assert.equal(await page.locator('script[data-public-booking-core-v2]').count(), 1);
        assert.deepEqual(errors, []);
        await page.close(); console.log('PASS real booking core/CCCD fields initialize before delayed image/window load');
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
