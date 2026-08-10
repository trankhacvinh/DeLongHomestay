import { getState, bookingBalance, updateBooking } from '../store.js';
import { qs, initAdminShell, money, formatDateTime, statusPill, escapeHtml, toast } from '../core.js';
import { openBookingForm } from './admin-booking-form.js';
initAdminShell('bookings', 'Đặt phòng');
function render() {
  const state=getState(), q=qs('#bookingSearch').value.trim().toLowerCase(), status=qs('#bookingStatus').value;
  const rows=state.bookings.filter(b => (!q || [b.id,b.guestName,b.phone,state.rooms.find(r=>r.id===b.roomId)?.name].join(' ').toLowerCase().includes(q)) && (!status || b.status===status)).sort((a,b)=>new Date(b.checkIn)-new Date(a.checkIn));
  qs('[data-booking-count]').textContent=`${rows.length} booking`;
  qs('[data-booking-table]').innerHTML=rows.map(b=>{ const room=state.rooms.find(r=>r.id===b.roomId), bal=bookingBalance(b,state); return `<tr><td><strong>${escapeHtml(b.id)}</strong><div class="tiny muted">${escapeHtml(b.source)}</div></td><td><strong>${escapeHtml(b.guestName)}</strong><div class="tiny muted">${escapeHtml(b.phone)}</div></td><td>${escapeHtml(room?.name||'')}</td><td>${formatDateTime(b.checkIn)}<div class="tiny muted">→ ${formatDateTime(b.checkOut)}</div></td><td>${statusPill(b.status)}</td><td><strong>${money(b.total)}</strong><div class="tiny ${bal?'muted':''}">${bal?`Còn ${money(bal)}`:'Đã thu đủ'}</div></td><td><div class="table-actions"><button class="btn btn-light btn-sm" data-edit="${b.id}">Sửa</button>${b.status==='pending'?`<button class="btn btn-success btn-sm" data-confirm="${b.id}">Xác nhận</button>`:''}</div></td></tr>`}).join('');
}
qs('#bookingSearch').addEventListener('input',render); qs('#bookingStatus').addEventListener('change',render);
qs('[data-new-booking]').addEventListener('click',()=>openBookingForm({onSaved:render}));
qs('[data-booking-table]').addEventListener('click',e=>{ const edit=e.target.closest('[data-edit]'); if(edit) openBookingForm({bookingId:edit.dataset.edit,onSaved:render}); const confirm=e.target.closest('[data-confirm]'); if(confirm){updateBooking(confirm.dataset.confirm,{status:'confirmed'});toast('Đã xác nhận booking.','success');render();}}); render();
