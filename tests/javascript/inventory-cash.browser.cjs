const { chromium } = require('playwright');
const fs = require('node:fs');
const assert = require('node:assert/strict');
const web = 'src/DeLong.Web/';
const before = process.env.INVENTORY_BEFORE === '1';
const output = 'docs/qa/inventory-import-2026-10-28';
const vuePath = process.env.VUE_RUNTIME_PATH;
if (!vuePath) throw new Error('Set VUE_RUNTIME_PATH to the existing Vue 3 browser runtime.');
const branch = { id: 'branch', name: 'Chi nhánh mẫu 1', timeZoneId: 'Asia/Ho_Chi_Minh' };
const branch2 = { id: 'branch2', name: 'Chi nhánh mẫu 2', timeZoneId: 'Asia/Ho_Chi_Minh' };
const unit = { id: 'unit', name: 'kg' };
const item = { id: 'item', name: 'Vật tư mẫu', code: 'VT01', unitId: 'unit', unitName: 'kg', referencePrice: 20000, isActive: true };
const stock = { itemId: 'item', code: 'VT01', name: item.name, unitName: 'kg', opening: 10.5, received: 2, issued: 1.5, adjustments: 0, closing: 11 };
const inv = { warehouse: { id: 'warehouse', name: 'Kho chung', ownerName: branch.name, timeZoneId: branch.timeZoneId }, units: [unit], items: [item, {...item,id:'empty',name:'Hàng đã hết',code:'VT02'}], stock: [stock,{...stock,itemId:'empty',name:'Hàng đã hết',code:'VT02',opening:0,received:0,issued:0,closing:0}], documents: [], distribution: [] };
const entry = { id: 'payment', source: 'booking-receipt', propertyId: 'branch', propertyName: branch.name, roomId: 'room', roomName: 'Phòng mẫu', occurredAtUtc: '2026-10-01T02:00:00Z', description: 'BK-MAU', note: 'Dữ liệu kiểm thử', inflow: 210000, outflow: 0, isVoided: false };
const group = { key: 'branch', name: branch.name, inflow: 210000, refunds: 0, expenses: 0, net: 210000 };
const cash = { month: '2026-10', totals: { bookingReceipts: 210000, otherReceipts: 0, refunds: 0, expenses: 0, net: 210000 }, entries: [entry], byProperty: [group], byRoom: [], byDay: [], byExpense: [] };

async function mount(browser, kind, width) {
    const page = await browser.newPage({ viewport: { width, height: width===375?667:width===390?844:width===414?896:900 } });
    const errors = [];
    page.on('pageerror', e => errors.push(e.message));
    await page.route('http://localhost/**', route => route.fulfill({ contentType: 'text/html', body: '<html><head><meta name="viewport" content="width=device-width,initial-scale=1"></head><body></body></html>' }));
    await page.goto('http://localhost/');
    const source = fs.readFileSync(before && kind === 'inventory' ? '/private/tmp/inventory-1815.cshtml' : web + `Pages/Admin/${kind === 'inventory' ? 'Inventory' : 'Cash'}/Index.cshtml`, 'utf8');
    const markup = source.slice(source.indexOf(`<div id="${kind}-page"`), source.indexOf('@section Scripts'));
    const initial = kind === 'inventory' ? { month: '2026-10', canManage: true, canInitialize: true, properties: [branch, branch2], snapshot: inv }
        : { propertyId: 'branch', scope: 'all', month: '2026-10', canManage: true, properties: [branch, branch2], editableProperties: [branch, branch2], rooms: [{ id: 'room', propertyId: 'branch', name: 'Phòng mẫu' }], snapshot: cash };
    await page.setContent(`<body class="admin-body"><main style="padding:16px;max-width:1200px;margin:auto"><script id="${kind}-data" type="application/json">${JSON.stringify(initial)}</script>${markup}</main></body>`);
    for (const style of ['site.css', 'operational-polish.css', 'admin-shell-fixes.css', 'mobile-final.css', 'admin-operations-ledger.css']) await page.addStyleTag({ content: fs.readFileSync(before && style === 'admin-operations-ledger.css' ? '/private/tmp/inventory-1815.css' : web + 'wwwroot/css/' + style, 'utf8') });
    await page.evaluate(({ inv, cash }) => {
        window.calls = []; window.failNext = false;
        window.inventoryFixture = inv; window.cashFixture = cash;
        window.DeLongApi = {
            get: async url => url.includes('/history') ? [] : structuredClone(url.includes('/inventory/') ? window.inventoryFixture : window.cashFixture),
            postForm: async () => ({ token:'preview-token', sheet:'Mat hang',newItems:1,updatedItems:0,newUnits:1,openingItems:1,skipped:0,errors:0,rows:[{row:2,name:'Hàng import mẫu',unit:'kg',quantity:1.5,price:2000,action:'create',message:'Thêm mặt hàng; ghi tồn đầu.'}] }),
            post: async (url, body) => { window.calls.push({ url, body: structuredClone(body) }); if (window.failNext) { window.failNext = false; throw new Error('Mạng gián đoạn, gửi lại cùng nội dung.'); } if(body.recipientName)window.inventoryFixture.recipients=[{propertyId:null,name:body.recipientName}]; return { id: 'saved',createdItems:1,updatedItems:0,createdUnits:1,openingItems:1,skipped:0 }; },
            put: async (url, body) => { window.calls.push({ url, body: structuredClone(body) }); return { id: 'saved' }; }
        };
    }, { inv, cash });
    await page.addScriptTag({ path: vuePath });
    if(kind==='inventory'){await page.addStyleTag({path:web+'wwwroot/vendor/tom-select/tom-select.default.min.css'});await page.addScriptTag({path:web+'wwwroot/vendor/tom-select/tom-select.complete.min.js'});}
    await page.addScriptTag({ path: web + 'wwwroot/js/pages/operational-forms.js' });
    await page.addScriptTag({ path: before && kind === 'inventory' ? '/private/tmp/inventory-1815.js' : web + `wwwroot/js/pages/admin-${kind}.js` });
    await page.waitForFunction(kind => document.getElementById(kind + '-page').hasAttribute('data-v-app'), kind);
    return { page, errors };
}

(async () => {
    fs.mkdirSync(output, { recursive: true });
    const browser = await chromium.launch({ headless: true, ...(process.env.CHROME_EXECUTABLE ? { executablePath: process.env.CHROME_EXECUTABLE } : {}) });
    try {
        for (const width of (before ? [390] : [375, 390, 414, 1280])) {
            for (const kind of (before ? ['inventory'] : ['inventory', 'cash'])) {
                const { page, errors } = await mount(browser, kind, width);
                assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), true, `${kind} global overflow ${width}`);
                await page.screenshot({ animations:'disabled', path: `${output}/${before ? 'before-' : ''}${kind}-${width}.png`, fullPage: true });
                if (before) {
                    await page.getByRole('button',{name:'Xuất kho',exact:true}).click();
                    await page.getByRole('dialog').waitFor();
                    await page.screenshot({ animations:'disabled',path:`${output}/before-document-390.png`});
                    await page.close(); continue;
                }
                if (kind === 'inventory') {
                    assert.equal(await page.locator('.inventory-zero').count(),1,'empty stock highlighted');
                    assert.equal(await page.locator('.inventory-zero').getByText('Hết hàng',{exact:true}).count(),0);
                    assert.ok(await page.locator('h1').evaluate(el=>parseFloat(getComputedStyle(el).fontSize))<=24,'compact title');
                    assert.equal((await page.locator('.inventory-stock-table').innerText()).includes('VT01'),false,'item codes are hidden');
                    if(width<650)assert.ok(await page.locator('.inventory-stock-table tbody td').first().evaluate(el=>el.getBoundingClientRect().width)<=111,'compact item column');
                    assert.equal(await page.locator('.inventory-stock-table td').first().evaluate(el=>getComputedStyle(el).position),'sticky');
                    await page.getByRole('button', { name: 'Xuất kho', exact: true }).click();
                    const dialog = page.getByRole('dialog');
                    await dialog.waitFor();
                    assert.equal(await dialog.evaluate(el=>el.scrollWidth<=el.clientWidth+1),true,'modal overflows');
                    const gaps=await dialog.locator('.ops-fieldset').evaluate(el=>parseFloat(getComputedStyle(el).rowGap));
                    assert.ok(gaps>=12,'document sections need separation');
                    await page.screenshot({ animations:'disabled',path:`${output}/document-${width}.png`});
                    const note=dialog.getByLabel('Ghi chú (không bắt buộc)',{exact:true});
                    assert.equal(await note.getAttribute('required'),null);
                    assert.ok(await note.evaluate(el=>el.compareDocumentPosition(document.querySelector('.inventory-add-line'))&Node.DOCUMENT_POSITION_PRECEDING),'note follows items');
                    await dialog.getByLabel('Ngày giờ ghi nhận').fill('2026-10-01T09:00');
                    await dialog.getByLabel('Nơi nhận', { exact: true }).fill(branch2.name);
                    await dialog.getByRole('combobox',{name:'Tìm mặt hàng'}).first().fill('Vật tư');
                    await dialog.locator('.ts-dropdown .option').filter({hasText:'Vật tư mẫu'}).click();
                    await dialog.getByLabel('Số lượng', { exact: true }).fill('1.5');
                    await page.evaluate(() => { window.failNext = true; });
                    await dialog.getByRole('button', { name: 'Ghi nhận', exact: true }).click();
                    await dialog.getByRole('alert').waitFor();
                    await dialog.getByRole('button', { name: 'Ghi nhận', exact: true }).click();
                    await dialog.waitFor({ state: 'detached' });
                    const calls = await page.evaluate(() => window.calls);
                    assert.equal(calls.length, 2); assert.equal(calls[0].body.requestKey, calls[1].body.requestKey);
                    assert.equal(calls[1].body.lines[0].quantity, 1.5); assert.equal(calls[1].body.recipientPropertyId, 'branch2');
                    await page.getByRole('button',{name:'Xuất kho',exact:true}).click();
                    await dialog.getByLabel('Nơi nhận',{exact:true}).fill('home1');
                    await dialog.getByRole('combobox',{name:'Tìm mặt hàng'}).first().fill('Vật tư');
                    await dialog.locator('.ts-dropdown .option').filter({hasText:'Vật tư mẫu'}).click();
                    await dialog.getByRole('button',{name:'Ghi nhận',exact:true}).click();
                    await dialog.waitFor({state:'detached'});
                    assert.equal(await page.evaluate(()=>window.calls.at(-1).body.recipientPropertyId),null);
                    assert.equal(await page.evaluate(()=>window.calls.at(-1).body.recipientName),'home1');
                    await page.getByRole('button',{name:'Xuất kho',exact:true}).click();
                    assert.equal(await dialog.locator('datalist option[value="home1"]').count(),1,'saved external destination is offered again');
                    await dialog.getByRole('button',{name:'Hủy',exact:true}).click();
                    await page.getByRole('button', { name: 'Mặt hàng / ĐVT', exact: true }).click();
                    await page.getByRole('button', { name: 'Sửa', exact: true }).first().click();
                    await page.getByRole('dialog').getByLabel('Đang sử dụng').uncheck();
                    await page.getByRole('dialog').getByRole('button', { name: 'Ghi nhận', exact: true }).click();
                    await page.getByRole('dialog').waitFor({ state: 'detached' });
                    assert.equal(await page.evaluate(() => window.calls.at(-1).body.isActive), false);
                    await page.getByRole('button',{name:'Thêm mặt hàng',exact:true}).click();
                    assert.equal(await dialog.getByLabel('Mã hàng',{exact:true}).count(),0);
                    await dialog.getByLabel('Tên mặt hàng').fill('Mặt hàng tự sinh mã');
                    await dialog.getByLabel(/^Đơn vị/).selectOption('unit');
                    await page.screenshot({ animations:'disabled',path:`${output}/item-${width}.png`});
                    await dialog.getByRole('button',{name:'Ghi nhận',exact:true}).click();
                    await dialog.waitFor({state:'detached'});
                    assert.equal(await page.evaluate(()=>Object.hasOwn(window.calls.at(-1).body,'code')),false);
                    await page.getByRole('button',{name:'Import mặt hàng',exact:true}).click();
                    await dialog.locator('input[type=file]').setInputFiles({name:'items.xlsx',mimeType:'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',buffer:Buffer.from('fixture')});
                    await dialog.getByRole('button',{name:'Xem trước',exact:true}).click();
                    await dialog.getByRole('button',{name:'Xác nhận import',exact:true}).waitFor();
                    assert.equal(await dialog.evaluate(el=>el.scrollWidth<=el.clientWidth+1),true,'import modal overflows');
                    await page.screenshot({ animations:'disabled',path:`${output}/import-${width}.png`});
                    await dialog.getByRole('button',{name:'Xác nhận import',exact:true}).click();
                    await dialog.waitFor({state:'detached'});
                    await page.getByRole('button',{name:'Import mặt hàng',exact:true}).click();
                    await page.keyboard.press('Escape');
                    await dialog.waitFor({state:'detached'});
                    assert.equal(await page.getByRole('button',{name:'Import mặt hàng',exact:true}).evaluate(el=>el===document.activeElement),true,'focus returns after Escape');

                } else {
                    await page.getByRole('button', { name: 'Theo chi nhánh', exact: true }).click();
                    await page.getByRole('button', { name: branch.name, exact: true }).click();
                    assert.match(await page.getByRole('dialog').innerText(), /BK-MAU/);
                    await page.getByRole('dialog').getByRole('button', { name: 'Đóng', exact: true }).click();
                    await page.getByRole('button', { name: 'Thêm thu ngoài', exact: true }).click();
                    const dialog = page.getByRole('dialog');
                    await dialog.getByLabel('Số tiền', { exact: true }).fill('50000');
                    await dialog.getByLabel('Nội dung', { exact: true }).fill('Thu ngoài mẫu');
                    await dialog.getByRole('button', { name: 'Ghi nhận', exact: true }).click();
                    await dialog.waitFor({ state: 'detached' });
                    assert.match(await page.evaluate(() => window.calls.at(-1).url), /\/cash\/receipts$/);
                    await page.getByRole('button', { name: 'Thêm chi tiền mặt', exact: true }).click();
                    await dialog.getByLabel('Số tiền', { exact: true }).fill('30000');
                    await dialog.getByLabel('Nội dung', { exact: true }).fill('Mua vật tư mẫu');
                    await dialog.getByRole('button', { name: 'Ghi nhận', exact: true }).click();
                    await dialog.waitFor({ state: 'detached' });
                    assert.equal(await page.evaluate(() => window.calls.at(-1).body.method), 0);
                }
                assert.deepEqual(errors, [], `${kind} runtime errors`);
                await page.close();
                process.stdout.write(`PASS ${kind} ${width}px\n`);
            }
        }
    } finally { await browser.close(); }
})().catch(e => { console.error(e); process.exitCode = 1; });
