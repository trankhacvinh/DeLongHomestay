const { chromium } = require('playwright');
const fs = require('node:fs');
const assert = require('node:assert/strict');
const base = 'src/DeLong.Web/wwwroot/';
const out = 'docs/qa/mobile-identity-2026-10-28';

async function mount(browser, mode = 'normal') {
    const page = await browser.newPage({ viewport: { width: 390, height: 844 } }); const errors = [];
    page.on('pageerror', e => errors.push(e.message));
    await page.route('http://localhost/**', route => route.fulfill({ contentType: 'text/html', body: '<html><head><meta name="viewport" content="width=device-width,initial-scale=1"></head><body></body></html>' }));
    await page.goto('http://localhost/booking');
    await page.setContent(`<meta name="csrf-token" content="synthetic"><script id="public-booking-data" type="application/json">{}</script><main id="public-booking-page" style="padding:16px"><h1>Đặt phòng thử nghiệm</h1><div class="public-embedded-selection-summary" data-room-capacity="4"></div><div class="public-contact-step"><div class="public-form-grid"><label>Tên<input data-booking-customer-name value="Khách thử nghiệm"></label><label>Điện thoại<input data-booking-customer-phone value="0900000000"></label><div class="public-form-full"></div></div></div><button id="submit">Gửi yêu cầu đặt phòng</button><p id="error" role="alert"></p></main>`);
    await page.addStyleTag({ path: base + 'css/site.css' }); await page.addStyleTag({ path: base + 'css/booking-core-v2.css' });
    await page.addStyleTag({ content: '.public-form-grid{display:grid;gap:14px}.public-form-full{grid-column:1/-1}h1{font-size:22px}input{max-width:100%}' });
    await page.evaluate(mode => {
        const nativeTimer = window.setTimeout;
        window.setTimeout = (fn, delay, ...args) => nativeTimer(fn, mode === 'decode-stall' && delay === 20000 ? 80 : mode === 'network-stall' && delay === 30000 ? 100 : delay, ...args);
        window.realBitmap = window.createImageBitmap.bind(window);
        if (mode === 'decode-stall') window.createImageBitmap = () => new Promise(() => {});
        window.creates = 0; window.completes = 0; window.results = []; window.requestKeys = []; window.progress = [];
        const bookings = new Map();
        window.DeLongApi = {
            get: async url => url.includes('/booking-policy') ? { requireIdentityDocuments: true, includedGuests: 2, policyVersion: 1 } : {},
            post: async (url, body, headers) => {
                if (url.includes('/complete')) { window.completes++; return { complete: true }; }
                window.creates++; window.requestKeys.push(headers['Idempotency-Key']);
                if (!bookings.has(headers['Idempotency-Key'])) bookings.set(headers['Idempotency-Key'], { bookingId: 'synthetic-booking', code: 'BK-TEST' });
                return bookings.get(headers['Idempotency-Key']);
            }
        };
        document.getElementById('submit').onclick = async () => {
            const button = document.getElementById('submit'); if (button.disabled) return;
            button.disabled = true; document.getElementById('error').textContent = '';
            try { window.results.push(await DeLongApi.post('/api/public/booking-requests', { roomId: 'synthetic-room' }, { 'Idempotency-Key': 'stable-test-key' })); }
            catch (error) { document.getElementById('error').textContent = error.message; }
            finally { button.disabled = false; }
        };
        new MutationObserver(() => { const progress = document.querySelector('[data-booking-progress]'); if(progress?.textContent)window.progress.push(progress.textContent); }).observe(document.getElementById('public-booking-page'), { childList:true,subtree:true });
    }, mode);
    await page.addScriptTag({ path: base + 'js/core/image-upload.js' });
    await page.addScriptTag({ path: base + 'js/pages/public-booking-core-v2.js' });
    await page.locator('[data-booking-email]').fill('synthetic@example.test');
    await page.locator('[data-policy-accepted]').check();
    return { page, errors };
}

async function photos(page) {
    await page.evaluate(async () => {
        const canvas = document.createElement('canvas'); canvas.width = 1800; canvas.height = 1200;
        const ctx = canvas.getContext('2d'); ctx.fillStyle = '#fff';ctx.fillRect(0,0,1800,1200);ctx.fillStyle='#12383b';ctx.font='60px sans-serif';ctx.fillText('SYNTHETIC TEST IMAGE',100,300);
        const blob = await new Promise(resolve => canvas.toBlob(resolve,'image/jpeg',.9));
        for (const side of ['front','back']) {
            const data = new DataTransfer(); data.items.add(new File([blob],`${side}.jpg`,{type:'image/jpeg'}));
            const input = document.querySelector(`[data-identity-side="${side}"] [data-camera-input]`);input.files=data.files;input.dispatchEvent(new Event('change',{bubbles:true}));
        }
        canvas.width=canvas.height=1;
    });
}

(async () => {
    fs.mkdirSync(out,{recursive:true});
    const browser = await chromium.launch({headless:true,...(process.env.CHROME_EXECUTABLE?{executablePath:process.env.CHROME_EXECUTABLE}:{})});
    try {
        // Native decoder benchmark: no CCCD or production network traffic.
        const perf = await browser.newPage();await perf.route('http://localhost/**',route=>route.fulfill({contentType:'text/html',body:'<html></html>'}));await perf.goto('http://localhost/benchmark');
        await perf.evaluate(async()=>{
            const canvas=document.createElement('canvas');canvas.width=6000;canvas.height=4000;const ctx=canvas.getContext('2d');ctx.fillStyle='#fff';ctx.fillRect(0,0,6000,4000);
            for(let y=0;y<4000;y+=40){ctx.fillStyle=y%80?'#193e4d':'#dca977';ctx.fillRect(0,y,6000,20);}ctx.fillStyle='#111';ctx.font='120px sans-serif';ctx.fillText('SYNTHETIC CAMERA PHOTO',100,500);
            const blob=await new Promise(resolve=>canvas.toBlob(resolve,'image/jpeg',.96));window.photo=new File([blob],'camera.jpg',{type:'image/jpeg'});canvas.width=canvas.height=1;
            window.nativeBitmap=window.createImageBitmap.bind(window);window.createImageBitmap=async(...args)=>{const bitmap=await window.nativeBitmap(...args);window.decodePixels.push(bitmap.width*bitmap.height);return bitmap;};
        });
        const timings = {};
        for (const version of (process.env.IMAGE_UPLOAD_BASELINE_PATH?['before','after']:['after'])) {
            await perf.addScriptTag({path:version==='before'?process.env.IMAGE_UPLOAD_BASELINE_PATH:base+'js/core/image-upload.js'});
            timings[version]=await perf.evaluate(async()=>{window.decodePixels=[];const start=performance.now();const file=await DeLongImageUpload.prepareFile('/api/public/booking-requests/b/identity-documents/front',window.photo);return {ms:Math.round(performance.now()-start),sourceBytes:window.photo.size,uploadBytes:file.size,decodedPixels:Math.max(...window.decodePixels)};});
        }
        if(timings.before)assert.equal(timings.before.decodedPixels,24000000);assert.ok(timings.after.decodedPixels<=4000000);
        const rotated = await perf.evaluate(async()=>{
            const data=new Uint8Array(await window.photo.arrayBuffer());const exif=new Uint8Array(36);const view=new DataView(exif.buffer);
            view.setUint16(0,0xffe1);view.setUint16(2,34);view.setUint32(4,0x45786966);view.setUint16(10,0x4949);view.setUint16(12,42,true);view.setUint32(14,8,true);view.setUint16(18,1,true);view.setUint16(20,0x0112,true);view.setUint16(22,3,true);view.setUint32(24,1,true);view.setUint16(28,6,true);
            const file=new File([data.slice(0,2),exif,data.slice(2)],'rotated.jpg',{type:'image/jpeg'});
            const prepared=await DeLongImageUpload.prepareFile('/api/public/booking-requests/b/identity-documents/front',file);
            const bitmap=await window.nativeBitmap(prepared);const size={width:bitmap.width,height:bitmap.height};bitmap.close();return size;
        });
        assert.equal(rotated.height,2000);assert.equal(rotated.width,1333);
        fs.writeFileSync(out+(timings.before?'/benchmark.json':'/benchmark-current.json'),JSON.stringify(timings,null,2)); console.log('Native benchmark',JSON.stringify(timings));await perf.close();

        {
            const {page,errors}=await mount(browser); const calls=[];let failBack=true;
            await page.route('**/identity-documents/*', async route=>{const side=route.request().url().split('/').at(-1);calls.push(side);await route.fulfill({status:side==='back'&&failBack?503:200,contentType:'application/json',body:JSON.stringify(side==='back'&&failBack?{detail:'Lỗi thử nghiệm'}:{side})});if(side==='back')failBack=false;});
            await photos(page);await page.locator('#submit').click();await page.locator('#error').filter({hasText:'Lỗi thử nghiệm'}).waitFor();
            assert.equal(await page.locator('#submit').isEnabled(),true);
            await page.locator('#submit').click();await page.waitForFunction(()=>window.completes===1);
            assert.deepEqual(calls,['front','back','back']);assert.deepEqual(await page.evaluate(()=>window.requestKeys),['stable-test-key','stable-test-key']);
            assert.equal(await page.locator('[data-booking-progress]').isHidden(),true);assert.deepEqual(errors,[]);await page.close();console.log('PASS retry reuses booking key and only reuploads failed side');
        }
        {
            const {page,errors}=await mount(browser,'decode-stall');await photos(page);await page.locator('#submit').click();
            await page.locator('#error').filter({hasText:/Xử lý ảnh/}).waitFor();
            assert.equal(await page.evaluate(()=>window.creates),0);assert.equal(await page.locator('#submit').isEnabled(),true);
            await page.screenshot({path:out+'/decode-timeout-390.png',animations:'disabled',fullPage:true});assert.deepEqual(errors,[]);await page.close();console.log('PASS decoder stall exits without creating booking');
        }
        {
            const {page,errors}=await mount(browser,'network-stall');let delayed=true;
            await page.route('**/identity-documents/*',async route=>{const side=route.request().url().split('/').at(-1);if(delayed){delayed=false;await new Promise(resolve=>setTimeout(resolve,400));}await route.fulfill({contentType:'application/json',body:JSON.stringify({side})}).catch(()=>{});});
            await photos(page);await page.locator('#submit').click();await page.locator('#error').filter({hasText:/không có tiến triển/}).waitFor();
            assert.equal(await page.locator('#submit').isEnabled(),true);assert.equal(await page.evaluate(()=>window.completes),0);
            await page.screenshot({path:out+'/network-timeout-390.png',animations:'disabled',fullPage:true});
            await page.locator('#submit').click();await page.waitForFunction(()=>window.completes===1);assert.deepEqual(errors,[]);await page.close();console.log('PASS stalled upload aborts and retry completes');
        }
    } finally {await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});
