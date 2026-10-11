const {test} = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
function fixture() {
    const events = [], calls = []; let options;
    const initial = {propertyId:'property',timeZoneId:'Asia/Ho_Chi_Minh',canUseAi:true,result:{items:[
        {id:'open',status:'Open',isDetected:true,title:'Công nợ',evidenceJson:'{"booking":{"code":"BKBLUE","roomName":"Blue"}}'},
        {id:'accepted',status:'Accepted',isDetected:true,title:'Phí hủy',evidenceJson:'{}'},
        {id:'cleared',status:'Open',isDetected:false,title:'Đã hết nợ',evidenceJson:'{}'}
    ]}};
    const api = {get:async url=>{calls.push(['get',url]);return url.endsWith('/history') ? [] : initial.result;},post:async(url,payload)=>{calls.push(['post',url,payload]);return initial.result;},put:async(url,payload)=>{calls.push(['put',url,payload]);}};
    const sandbox = {URLSearchParams, setTimeout, clearTimeout, document:{getElementById:()=>({textContent:JSON.stringify(initial)})},window:{Vue:{},dispatchEvent:e=>events.push(e)},
        Vue:{createApp: config=>{options=config;return {mount(){}};}},DeLongApi:api,CustomEvent:class {constructor(type,options){this.type=type;this.detail=options.detail;}}};
    vm.runInNewContext(fs.readFileSync('src/DeLong.Web/wwwroot/js/pages/admin-data-quality.js','utf8'),sandbox);
    const page = options.data(); Object.assign(page,options.methods);
    for (const [key,getter] of Object.entries(options.computed)) Object.defineProperty(page,key,{get:()=>getter.call(page)});
    return {page,api,events,calls};
}
test('pending filter excludes accepted and no-longer-detected findings; searches booking code',()=>{
    const {page}=fixture(); assert.equal(page.visible.length,1);assert.equal(page.visible[0].id,'open');
    page.search='bkblue';assert.equal(page.visible.length,1);page.search='not-found';assert.equal(page.visible.length,0);
    page.search='';page.filter='cleared';assert.equal(page.visible[0].id,'cleared');
    page.filter='Accepted';assert.equal(page.visible[0].id,'accepted');
});
test('AI button opens a scoped prompt without calling external AI automatically',()=>{
    const {page,events,calls}=fixture();page.askAi(page.result.items[0]);
    assert.equal(events[0].type,'delong:ai-prompt');assert.equal(events[0].detail.propertyId,'property');
    assert.ok(events[0].detail.prompt.includes('open'));assert.equal(calls.length,0);
});
test('scan ignores repeated clicks while running and preserves issues on failure',async()=>{
    const {page,api,calls}=fixture();let release;api.post=()=>new Promise(resolve=>{release=resolve;});
    const running=page.scan();await page.scan();assert.equal(page.busy,true);release(page.result);await running;assert.equal(page.busy,false);
    api.post=async()=>{throw new Error('Server unavailable');};await page.scan();assert.equal(page.error,'Server unavailable');assert.equal(page.result.items.length,3);
});
test('review requires a note and writes only to the quality endpoint',async()=>{
    const {page,calls}=fixture();await page.open(page.result.items[0]);page.form.note='';await page.save();assert.ok(page.error);
    page.form.note='Đã đối soát';await page.save();assert.equal(calls.filter(x=>x[0]==='put').length,1);
    assert.equal(calls.find(x=>x[0]==='put')[1],'/api/admin/properties/property/data-quality/open');
});
