const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const vm = require('node:vm');
const source = readFileSync('src/DeLong.Web/wwwroot/js/pages/public-booking-core-v2.js', 'utf8');
const capacity = source.slice(source.indexOf('    function currentCapacity()'), source.indexOf('    function currentSelectionSignature()'));
function roomCapacity(embedded, active) {
    return vm.runInNewContext(`${capacity}; currentCapacity()`, { root: { querySelector: selector => selector.includes('embedded')
        ? { dataset: { roomCapacity: embedded } } : selector === '.public-booking-room.active' ? { dataset: { roomCapacity: active } } : null } });
}
test('Guest limit follows configured room capacity, with no permissive missing-data fallback', () => {
    assert.equal(roomCapacity('2', '3'), 2);
    assert.equal(roomCapacity('3', '2'), 3);
    assert.equal(roomCapacity('', '3'), 3);
    assert.equal(roomCapacity('', ''), 1);
});
const timeout = source.slice(source.indexOf('    async function withBookingTimeout('), source.indexOf('    async function uploadIdentity('));
test('Slow upload is aborted and produces a retry message instead of success', async () => {
    let timer, cleared = false;
    const action = vm.runInNewContext(`${timeout}; withBookingTimeout`, {
        AbortController, setTimeout: callback => { timer = callback; return 1; }, clearTimeout: () => { cleared = true; }
    });
    const pending = action(signal => new Promise((_, reject) => signal.addEventListener('abort', () => {
        const error = new Error(); error.name = 'AbortError'; reject(error);
    })));
    timer();
    await assert.rejects(pending, /gửi lại/);
    assert.equal(cleared, true);
});
test('Retry preserves the original key for server failures and incomplete uploads', () => {
    const submission = readFileSync('src/DeLong.Web/wwwroot/js/pages/public-booking.js', 'utf8');
    const rule = submission.match(/if \(error.status >= 400[^\n]+/)[0];
    const apply = new Function('error', rule + '; return this.requestKey;');
    for (const error of [{ status: 500 }, { status: 503 }, {}, { status: 409, identityUploadFailed: true }])
        assert.equal(apply.call({ requestKey: 'same-key' }, error), 'same-key');
    assert.equal(apply.call({ requestKey: 'same-key' }, { status: 400 }), null);
});
const wrapper = source.slice(source.indexOf('    function wrapBookingPost()'), source.indexOf('    const observer ='));
function submitHarness({ uploadFailure = false, completionFailure = false, preparationFailure = false } = {}) {
    const calls = [], progress = [];
    const sandbox = {
        root: { querySelectorAll: () => [], querySelector:()=>null, classList:{remove(){}} },
        showPaymentStage:()=>({querySelector:()=>({textContent:''})}),warmPaymentQr:async()=>{},
        window: { DeLongImageUpload: { prepareFile: async (_,file) => { if(preparationFailure)throw new Error('preparation failed');return file; } }, DeLongApi: { post: async url => {
            calls.push(url);
            if (url.includes('/complete')) {
                if (completionFailure) { const error = new Error('missing CCCD'); error.status = 409; throw error; }
                return { complete: true };
            }
            return { bookingId: 'booking-1', totalAmount: 250000 };
        } } },
        state: { policy: {}, front: {}, back: {}, guestCount: 2 }, siteSlug: 'demo',
        validateExtraFields: () => null, createPendingCustomerAccount: async () => {},
        setProgress: message => progress.push(message), withBookingTimeout: action => action(undefined),
        uploadIdentity: async () => { if (uploadFailure) throw new Error('upload failed'); }
    };
    vm.runInNewContext(`${wrapper}; wrapBookingPost();`, sandbox);
    return { post: sandbox.window.DeLongApi.post, calls, progress };
}
test('Successful submit verifies saved CCCD before returning the booking to redirect', async () => {
    const harness = submitHarness();
    const result = await harness.post('/api/public/booking-requests', {}, {'Idempotency-Key':'key'});
    assert.equal(result.bookingId, 'booking-1');
    assert.deepEqual(harness.calls, ['/api/public/booking-requests','/api/public/booking-requests/booking-1/complete?siteSlug=demo']);
    assert.equal(harness.progress.at(-1), '');
});
test('Upload or completion failure never returns a successful booking result', async () => {
    const uploading = submitHarness({uploadFailure:true});
    await assert.rejects(uploading.post('/api/public/booking-requests', {}, {'Idempotency-Key':'key'}), /upload failed/);
    assert.equal(uploading.calls.length,1);
    const completing = submitHarness({completionFailure:true});
    await assert.rejects(completing.post('/api/public/booking-requests', {}, {'Idempotency-Key':'key'}), error => error.identityUploadFailed === true && error.status === 409);
    assert.equal(completing.progress.at(-1),'');
});
test('Retry skips a successfully saved image but uploads a changed image or a different booking', async () => {
    const upload = source.slice(source.indexOf('    const uploadedIdentityFiles'),source.indexOf('    function wrapBookingPost()'));
    let requests = 0;
    class UploadRequest {
        constructor(){this.upload={};}
        open(_,url){this.url=url;}
        setRequestHeader(){}
        send(){requests++;this.status=200;this.responseText=JSON.stringify({side:this.url.split('/').at(-1)});this.onload();}
        abort(){this.onabort?.();}
    }
    const sandbox = { Map, siteSlug: '', FormData, XMLHttpRequest:UploadRequest,setTimeout,clearTimeout,setProgress:()=>{},document:{querySelector:()=>({content:'csrf'})},
        window:{DeLongImageUpload:{prepareFile:async(_,file)=>file}} };
    const run = vm.runInNewContext(`${upload}; uploadIdentity`, sandbox);
    const file = new Blob(['synthetic'],{type:'image/jpeg'}); file.name='test.jpg';
    await run('b','front',file,'key');
    await run('b','front',file,'key');
    assert.equal(requests,1);
    const changed = new Blob(['new synthetic'],{type:'image/jpeg'}); changed.name='new.jpg';
    await run('b','front',changed,'key');
    await run('b2','front',file,'key2');
    assert.equal(requests,3);
});

test('Customer errors identify and focus the relevant contact field', () => {
    const pageSource = require('node:fs').readFileSync('src/DeLong.Web/wwwroot/js/pages/public-booking.js', 'utf8');
    const start = pageSource.indexOf('                    const contactField = error.problem?.code');
    const end = pageSource.indexOf('                    // A timeout', start);
    const apply = new Function('error', 'message', pageSource.slice(start, end));
    for (const [code, field] of [['customer_name_invalid', 'customerName'], ['customer_phone_invalid', 'customerPhone'], ['customer_inactive', 'customerPhone']]) {
        let focused;
        const state = { fieldErrors: {}, $refs: { customerName: { focus() { focused = 'customerName'; } }, customerPhone: { focus() { focused = 'customerPhone'; } } } };
        apply.call(state, { problem: { code } }, 'Synthetic validation message');
        assert.equal(state.fieldErrors[field], 'Synthetic validation message');
        assert.equal(focused, field);
    }
});

test('Unresponsive action times out even when it ignores AbortSignal', async()=>{
    let timer;
    const action=vm.runInNewContext(`${timeout}; withBookingTimeout`,{AbortController,setTimeout:callback=>{timer=callback;return 1;},clearTimeout:()=>{}});
    const pending=action(()=>new Promise(()=>{}));timer();await assert.rejects(pending,/gửi lại/);
});

test('Image preparation failure prevents booking creation and always clears progress',async()=>{
    const harness=submitHarness({preparationFailure:true});
    await assert.rejects(harness.post('/api/public/booking-requests',{}, {'Idempotency-Key':'key'}),/preparation failed/);
    assert.equal(harness.calls.length,0);assert.equal(harness.progress.at(-1),'');
});

test('CCCD completion conflict stays on the same booking instead of reopening room selection',async()=>{
    const pageSource=readFileSync('src/DeLong.Web/wwwroot/js/pages/public-booking.js','utf8');
    for(const identityUploadFailed of [true,false]){
        let options,conflicts=0;
        const window={location:{search:''}};window.parent=window;
        const error=Object.assign(new Error('Synthetic conflict'),{status:409,identityUploadFailed});
        vm.runInNewContext(pageSource,{window,URLSearchParams,document:{getElementById:id=>id==='public-booking-data'?{textContent:'{}'}:{}},Vue:{createApp:value=>{options=value;return{mount(){}};}},DeLongApi:{post:async()=>{throw error;}}});
        const state={selectedRoom:{id:'room'},selectedRoomId:'room',selectedRate:{id:'rate'},validateContact:()=>true,requestKey:'same-key',bookingType:0,date:'2026-11-01',embeddedSlotSelection:true,selectedSlots:[],form:{customerName:'Synthetic',customerPhone:'0900000000'},voucher:{result:null},apiUrl:value=>value,handleBookingConflict:async()=>{conflicts++;}};
        await options.methods.submitRequest.call(state);
        assert.equal(state.submitting,false);
        assert.equal(conflicts,identityUploadFailed?0:1);
        assert.equal(state.requestKey,identityUploadFailed?'same-key':null);
        if(identityUploadFailed)assert.equal(state.errorMessage,'Synthetic conflict');
    }
});
