const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const { File } = require('node:buffer');
const source = fs.readFileSync('src/DeLong.Web/wwwroot/js/core/image-upload.js', 'utf8');

test('a stalled camera decoder releases the queue so retry can process the next photo', { timeout: 500 }, async () => {
    let calls = 0;
    const window = { location: { origin: 'https://delonghomestay.com' }, createImageBitmap: async () => {
        calls++; if (calls === 1) return new Promise(() => {});
        return { width: 800, height: 600, close() {} };
    } };
    const document = { createElement: () => ({ width: 0, height: 0, getContext: () => ({ drawImage() {}, fillRect() {} }), toBlob: callback => callback(new Blob(['small'], { type: 'image/jpeg' })) }) };
    vm.runInNewContext(source, { window, document, URL, File, Blob, FormData, Image: class {}, setTimeout: fn => setTimeout(fn, 25), clearTimeout });
    const prepare = window.DeLongImageUpload.prepareFile;
    const url = '/api/public/booking-requests/b/identity-documents/front';
    await assert.rejects(prepare(url, new File(['one'], 'camera1.jpg', { type: 'image/jpeg' })), /ảnh|thời gian/i);
    const second = await prepare(url, new File(['two'], 'camera2.jpg', { type: 'image/jpeg' }));
    assert.ok(second); assert.equal(calls, 2);
});

function jpegHeader(width, height, orientation = 1) {
    const bytes = new Uint8Array(49); const data = new DataView(bytes.buffer);
    data.setUint16(0, 0xffd8); data.setUint16(2, 0xffe1); data.setUint16(4, 34);
    data.setUint32(6, 0x45786966); data.setUint16(10, 0); data.setUint16(12, 0x4949);
    data.setUint16(14, 42, true); data.setUint32(16, 8, true); data.setUint16(20, 1, true);
    data.setUint16(22, 0x0112, true); data.setUint16(24, 3, true); data.setUint32(26, 1, true); data.setUint16(30, orientation, true);
    data.setUint16(38, 0xffc0); data.setUint16(40, 7); bytes[42] = 8; data.setUint16(43, height); data.setUint16(45, width);
    data.setUint16(47, 0xffd9); return bytes;
}

test('large camera JPEG is downsampled in the decoder and EXIF rotation keeps portrait aspect', async () => {
    let options;
    const window = { location: { origin: 'https://delonghomestay.com' }, createImageBitmap: async (_, value) => {
        options = value; return { width: value.resizeWidth, height: value.resizeHeight, close() {} };
    } };
    const document = { createElement: () => ({ getContext: () => ({ drawImage() {}, fillRect() {} }), toBlob: callback => callback(new Blob(['small'], { type: 'image/jpeg' })) }) };
    vm.runInNewContext(source, { window, document, URL, File, Blob, FormData, setTimeout, clearTimeout });
    await window.DeLongImageUpload.prepareFile('/api/public/booking-requests/b/identity-documents/front', new File([jpegHeader(8000, 6000, 6)], 'camera.jpg', { type: 'image/jpeg' }));
    assert.equal(options.resizeWidth, 1500); assert.equal(options.resizeHeight, 2000); assert.equal(options.imageOrientation, 'from-image');
});

test('decoder resize never falls back to the high-resolution original even if it was already highly compressed',async()=>{
    const window={location:{origin:'https://delonghomestay.com'},createImageBitmap:async(_,options)=>({width:options.resizeWidth,height:options.resizeHeight,close(){}})};
    const document={createElement:()=>({getContext:()=>({drawImage(){},fillRect(){}}),toBlob:callback=>callback(new Blob([new Uint8Array(2000)],{type:'image/jpeg'}))})};
    vm.runInNewContext(source,{window,document,URL,File,Blob,FormData,setTimeout,clearTimeout});
    const original=new File([jpegHeader(8000,6000)],'compressed.jpg',{type:'image/jpeg'});
    const prepared=await window.DeLongImageUpload.prepareFile('/api/public/booking-requests/b/identity-documents/front',original);
    assert.notEqual(prepared,original);assert.equal(prepared.size,2000);
});

test('PNG identity becomes opaque JPEG while public media retains transparency', async () => {
    const types = [], paints = [];
    const window = { location: { origin: 'https://delonghomestay.com' }, createImageBitmap: async () => ({ width: 4000, height: 3000, close() {} }) };
    const document = { createElement: () => ({ getContext: () => ({ drawImage() {}, fillRect() { paints.push('white'); } }), toBlob: (callback, type) => { types.push(type);callback(new Blob(['small'], { type })); } }) };
    vm.runInNewContext(source, { window, document, URL, File, Blob, FormData, setTimeout, clearTimeout });
    const file = new File(['synthetic'], 'scan.png', { type: 'image/png' });
    await window.DeLongImageUpload.prepareFile('/api/public/booking-requests/b/identity-documents/front', file);
    await window.DeLongImageUpload.prepareFile('/api/admin/site/global/assets/logo', file);
    assert.deepEqual(types, ['image/jpeg','image/png']); assert.equal(paints.length, 1);
});

test('a late bitmap is closed after timeout', { timeout: 500 }, async () => {
    let release; let closed = false;
    const window = { location: { origin: 'https://delonghomestay.com' }, createImageBitmap: () => new Promise(resolve => { release = resolve; }) };
    vm.runInNewContext(source, { window, document: {}, URL, File, Blob, FormData, setTimeout: fn => setTimeout(fn, 25), clearTimeout });
    const task = window.DeLongImageUpload.prepareFile('/api/public/booking-requests/b/identity-documents/front', new File(['one'], 'camera.jpg', { type: 'image/jpeg' }));
    await assert.rejects(task, /ảnh|thời gian/i);
    release({ width: 800, height: 600, close() { closed = true; } });
    await new Promise(resolve => setImmediate(resolve)); assert.equal(closed, true);
});

test('stalled canvas encoding times out and releases canvas memory', { timeout: 500 }, async () => {
    const canvas = { width: 0, height: 0, getContext: () => ({ drawImage() {}, fillRect() {} }), toBlob() {} };
    const window = { location: { origin: 'https://delonghomestay.com' }, createImageBitmap: async () => ({ width: 2000, height: 1500, close() {} }) };
    vm.runInNewContext(source, { window, document: { createElement: () => canvas }, URL, File, Blob, FormData, setTimeout: fn => setTimeout(fn, 25), clearTimeout });
    await assert.rejects(window.DeLongImageUpload.prepareFile('/api/public/booking-requests/b/identity-documents/front', new File(['one'], 'camera.jpg', { type: 'image/jpeg' })), /ảnh|thời gian/i);
    assert.equal(canvas.width, 1); assert.equal(canvas.height, 1);
});

test('discarding a retaken photo cancels the stale job and skips stale queued photos', {timeout:500},async()=>{
    let calls=0;
    const window={location:{origin:'https://delonghomestay.com'},createImageBitmap:async()=>{calls++;if(calls===1)return new Promise(()=>{});return{width:800,height:600,close(){}};}};
    const document={createElement:()=>({getContext:()=>({drawImage(){},fillRect(){}}),toBlob:callback=>callback(new Blob(['small'],{type:'image/jpeg'}))})};
    vm.runInNewContext(source,{window,document,URL,File,Blob,FormData,setTimeout,clearTimeout});
    const url='/api/public/booking-requests/b/identity-documents/front';const file=new File(['first'],'first.jpg',{type:'image/jpeg'}),second=new File(['second'],'second.jpg',{type:'image/jpeg'});
    const first=window.DeLongImageUpload.prepareFile(url,file);const queued=window.DeLongImageUpload.prepareFile(url,second);
    await new Promise(resolve=>setImmediate(resolve));
    const firstError=assert.rejects(first,/thay|bỏ/),secondError=assert.rejects(queued,/thay|bỏ/);
    window.DeLongImageUpload.discardFile(file);window.DeLongImageUpload.discardFile(second);await Promise.all([firstError,secondError]);
    await window.DeLongImageUpload.prepareFile(url,new File(['latest'],'latest.jpg',{type:'image/jpeg'}));assert.equal(calls,2);
});
