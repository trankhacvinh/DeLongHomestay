const {test} = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const source = fs.readFileSync('src/DeLong.Web/wwwroot/js/pages/admin-bookings.js','utf8');
const dateSource = source.slice(source.indexOf('    function localDateKey('),source.indexOf('    function zonedLocalToIso('));
const localDateKey = new Function('timeZone', `${dateSource};return localDateKey;`)('Asia/Ho_Chi_Minh');
const block = source.slice(source.indexOf('            filteredDebtSummary()'), source.indexOf('            editableStayRooms()'));
const functions = new Function('localDateKey',`return ({${block}});`)(localDateKey);
function app(bookings) {
    const a = {bookings,search:'',statusFilter:'',debtOnly:true,dateFrom:'',dateTo:'',sortMode:'debt-desc'};
    Object.defineProperty(a,'filteredBookings',{get:()=>functions.filteredBookings.call(a)});
    return a;
}
function booking(id, balance, status=2, checkInUtc='2026-10-17T03:00:00Z') {
    return {id,code:id,customerName:'Synthetic '+id,customerPhone:'0900000000',roomName:'R',status,balanceAmount:balance,checkInUtc};
}
test('debt includes completed bookings, excludes cancelled/no-show/paid/overpaid and totals only displayed rows',()=>{
    const a=app([booking('a',100),booking('b',200,4),booking('c',900,5),booking('d',800,6),booking('e',0),booking('f',-50)]);
    assert.deepEqual(a.filteredBookings.map(x=>x.id),['b','a']);
    assert.deepEqual(functions.filteredDebtSummary.call(a),{count:2,amount:300});
    a.search='Synthetic a';assert.equal(a.filteredBookings.length,1);assert.equal(functions.filteredDebtSummary.call(a).amount,100);
});
test('date filter uses inclusive local check-in days and clearing it retains old debts',()=>{
    const a=app([booking('before',100,4,'2026-10-16T16:59:59Z'),booking('start',200,4,'2026-10-16T17:00:00Z'),booking('end',300,2,'2026-10-17T16:59:59Z'),booking('after',400,2,'2026-10-17T17:00:00Z')]);
    a.dateFrom=a.dateTo='2026-10-17';assert.deepEqual(a.filteredBookings.map(x=>x.id),['end','start']);
    assert.equal(functions.filteredDebtSummary.call(a).amount,500);
    a.dateFrom=a.dateTo='';assert.equal(functions.filteredDebtSummary.call(a).amount,1000);
    a.dateFrom='2026-10-20';a.dateTo='2026-10-17';assert.deepEqual(a.filteredBookings,[]);
});
test('receipt/refund/void balance refresh updates debt list and respects status filter',()=>{
    const a=app([booking('active',100),booking('completed',200,4)]);
    a.statusFilter='4';assert.equal(functions.filteredDebtSummary.call(a).amount,200);
    a.bookings[1].balanceAmount=0;assert.equal(a.filteredBookings.length,0);
    a.bookings[1].balanceAmount=50;assert.equal(functions.filteredDebtSummary.call(a).amount,50);
    a.statusFilter='';a.debtOnly=false;assert.equal(a.filteredBookings.length,2);
});
test('V1 uses the same debt-first and configured-color rules as V2',()=>{
    const calendar=fs.readFileSync('src/DeLong.Web/wwwroot/js/pages/admin-calendar.js','utf8');
    const block=calendar.slice(calendar.indexOf('            bookingVisual(booking)'),calendar.indexOf('            bookingStyle('));
    const methods=new Function(`return ({${block}});`)();
    const a={calendarColors:{unpaidColor:'#FF0000',confirmedColor:'#112233',specialRequestColor:'#880088'},...methods};
    for(const status of [1,2,3,4])assert.equal(a.bookingVisual({status,balanceAmount:1,note:'Special',displayColor:'#00FF00'}).color,'#FF0000');
    assert.equal(a.bookingVisual({status:4,balanceAmount:0,displayColor:'#ABCDEF'}).color,'#ABCDEF');
    assert.equal(a.bookingVisual({status:4,balanceAmount:0}).color,'#112233');
});
