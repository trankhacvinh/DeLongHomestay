// Read-only local replay. Fixtures contain anonymous public responses, never authenticated HAR data.
const fs = require('node:fs');
const path = require('node:path');
const http = require('node:http');
const zlib = require('node:zlib');
const { chromium } = require('playwright');
const assert = require('node:assert/strict');
const web = path.resolve('src/DeLong.Web/wwwroot');
const html = fs.readFileSync(process.env.PERF_HTML, 'utf8');
const previous = JSON.parse(fs.readFileSync(process.env.PERF_SOURCE_BEFORE, 'utf8'));
const fixtures = JSON.parse(fs.readFileSync(process.env.PERF_RESPONSE_MAP, 'utf8'));
const target = fs.readFileSync('src/DeLong.Web/Build/PublicHomeCss.targets', 'utf8');
const bundled = new Set([...target.matchAll(/\/wwwroot\/css\/([^"\s]+)"/g)].map(x => '/css/' + x[1]));
const layout = fs.readFileSync('src/DeLong.Web/Pages/Shared/_Layout.cshtml', 'utf8');
const fonts = [...layout.matchAll(/<link rel="preload"[^>]+\.woff2[^>]+>/g)].map(x => x[0].replace('href="~/', 'href="/'));
assert.equal(fonts.length, 4);
const bundleTag = '<link rel="stylesheet" href="/css/public-home.bundle.css" data-public-row-responsive data-public-row-templates data-public-row-advanced-elements data-public-custom-pages data-booking-core-v2>';
const optimized = html.replace(/<link\b[^>]+>/g, tag => {
    const href = tag.match(/href="([^"]+)"/)?.[1];
    if (!href) return tag;
    if (tag.includes('rel="stylesheet"') && bundled.has(new URL(href, 'http://fixture').pathname)) return '';
    if (tag.includes('rel="preload"') && href.includes('/fonts/brand/')) return '';
    return tag;
}).replace('</head>', fonts.join('') + bundleTag + '</head>');
let mode = 'before';
const mime = { '.css': 'text/css', '.js': 'application/javascript', '.woff': 'font/woff', '.woff2': 'font/woff2', '.webp': 'image/webp', '.svg': 'image/svg+xml', '.png': 'image/png', '.jpg': 'image/jpeg' };
const server = http.createServer((req, res) => {
    if (req.method !== 'GET') { res.writeHead(405); res.end(); return; }
    const url = new URL(req.url, 'http://fixture');
    if (url.pathname.endsWith('/stream')) { res.writeHead(204); res.end(); return; }
    let bytes, type;
    if (url.pathname === '/') { bytes = Buffer.from(mode === 'before' ? html : optimized); type = 'text/html'; }
    else {
        const oldKey = 'src/DeLong.Web/wwwroot' + url.pathname;
        const publicUrl = 'https://delonghomestay.com' + req.url;
        const stored = fixtures[publicUrl] || Object.entries(fixtures).find(([key]) => new URL(key).pathname === url.pathname)?.[1];
        const local = path.resolve(web, '.' + decodeURIComponent(url.pathname));
        if (mode === 'before' && previous[oldKey] !== undefined) bytes = Buffer.from(previous[oldKey]);
        else if (local.startsWith(web + path.sep) && fs.existsSync(local) && fs.statSync(local).isFile()) bytes = fs.readFileSync(local);
        else if (stored) bytes = fs.readFileSync(stored);
        else { res.writeHead(404); res.end(); return; }
        type = url.pathname.startsWith('/api/') ? 'application/json' : mime[path.extname(url.pathname)] || 'application/octet-stream';
    }
    const compress = /text|javascript|json/.test(type);
    const output = compress ? zlib.gzipSync(bytes) : bytes;
    res.writeHead(200, { 'Content-Type': type, 'Cache-Control': 'no-store', 'Content-Length': output.length, ...(compress ? { 'Content-Encoding': 'gzip' } : {}) });
    res.end(output);
});
(async () => {
    await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
    const origin = `http://127.0.0.1:${server.address().port}`;
    const browser = await chromium.launch({ headless: true, executablePath: process.env.CHROME_EXECUTABLE });
    const results = [];
    try {
        for (const width of [390, 1280]) for (const variant of ['before', 'after']) {
            mode = variant;
            const page = await browser.newPage({ viewport: { width, height: 844 } });
            await page.route('**/*', route => {
                const url = new URL(route.request().url());
                if (url.origin === origin) return route.continue();
                return route.abort();
            });
            const cdp = await page.context().newCDPSession(page);
            await cdp.send('Network.enable');
            await cdp.send('Network.emulateNetworkConditions', { offline: false, latency: 150, downloadThroughput: 200000, uploadThroughput: 93750 });
            await cdp.send('Emulation.setCPUThrottlingRate', { rate: 4 });
            await page.addInitScript(() => {
                window.vitals = { lcp: 0, cls: 0 };
                new PerformanceObserver(list => { for (const e of list.getEntries()) window.vitals.lcp = e.startTime; }).observe({ type: 'largest-contentful-paint', buffered: true });
                new PerformanceObserver(list => { for (const e of list.getEntries()) if (!e.hadRecentInput) window.vitals.cls += e.value; }).observe({ type: 'layout-shift', buffered: true });
            });
            await page.goto(origin, { waitUntil: 'domcontentloaded' });
            await page.waitForTimeout(10000);
            const result = await page.evaluate(() => {
                const resources = performance.getEntriesByType('resource');
                const layout = ['h1', '.public-hero-copy', '.public-hero-collage', '.public-hero-price-sticker', '.public-hotel-hero-metrics'].map(selector => {
                    const node = document.querySelector(selector);
                    if (!node) return null;
                    const rect = node.getBoundingClientRect(), style = getComputedStyle(node);
                    return { selector, x: Math.round(rect.x), y: Math.round(rect.y), width: Math.round(rect.width), height: Math.round(rect.height), fontSize: style.fontSize, fontWeight: style.fontWeight, color: style.color };
                });
                return { ...window.vitals, layout, fcp: performance.getEntriesByName('first-contentful-paint')[0]?.startTime, css: resources.filter(x => new URL(x.name).pathname.endsWith('.css')).length, totalBytes: resources.reduce((sum, x) => sum + x.transferSize, 0), fonts: resources.filter(x => /\.woff2?(\?|$)/.test(x.name)).map(x => new URL(x.name).pathname), hero: document.querySelector('.public-hero')?.textContent.trim(), resources: resources.map(x => ({ path: new URL(x.name).pathname, duration: x.duration, size: x.transferSize })), fontFamily: getComputedStyle(document.querySelector('h1')).fontFamily };
            });
            result.variant = variant; result.width = width;
            assert.ok(result.hero); assert.ok(result.fontFamily.includes('Be Vietnam Pro'));
            const output = process.env.PERF_OUTPUT || '/private/tmp/delong-public-replay'; fs.mkdirSync(output, { recursive: true });
            await page.screenshot({ path: `${output}/${variant}-${width}.png`, animations: 'disabled' });
            results.push(result); await page.close();
            console.log(JSON.stringify({ ...result, resources: undefined, hero: undefined, layout: undefined }));
        }
        for (const width of [390, 1280]) {
            const before = results.find(x => x.width === width && x.variant === 'before');
            const after = results.find(x => x.width === width && x.variant === 'after');
            assert.equal(after.hero, before.hero);
            assert.ok(after.css <= 3, 'homepage CSS must not fan out into late stylesheet loads');
            assert.ok(after.css < before.css);
            assert.ok(after.fonts.every(x => x.endsWith('.woff2')));
            if (width === 390) assert.ok(after.cls < .02, 'late stylesheet/font swaps must not move the mobile hero');
            assert.deepEqual(after.layout, before.layout, 'settled homepage geometry, typography and colors must remain identical');
        }
        fs.writeFileSync((process.env.PERF_OUTPUT || '/private/tmp/delong-public-replay') + '/metrics.json', JSON.stringify(results, null, 2));
    } finally { await browser.close(); await new Promise(resolve => server.close(resolve)); }
})().catch(error => { console.error(error); server.close(); process.exitCode = 1; });
