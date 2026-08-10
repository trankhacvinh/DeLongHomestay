import { getState } from '../store.js';
import { qs, initAdminShell, dateKey, formatShortDate, formatTime, money, escapeHtml, statusLabel } from '../core.js';
import { openBookingForm } from './admin-booking-form.js';
initAdminShell('calendar', 'Lịch phòng');
let start = new Date(); start.setHours(0,0,0,0);
function addDays(d,n){ const x=new Date(d); x.setDate(x.getDate()+n); return x; }
function render() {
  const state = getState(); const days = Array.from({length:7},(_,i)=>addDays(start,i));
  qs('[data-range-label]').textContent = `${days[0].toLocaleDateString('vi-VN')} – ${days[6].toLocaleDateString('vi-VN')}`;
  const head = `<tr><th>Phòng</th>${days.map(d => `<th class="${dateKey(d)===dateKey(new Date())?'calendar-today':''}">${formatShortDate(d)}</th>`).join('')}</tr>`;
  const body = state.rooms.map(room => `<tr><th><div class="room-row-title"><div class="room-thumb"><img src="../${room.image}"></div><div><strong>${escapeHtml(room.name)}</strong><div class="tiny muted">${room.status==='clean'?'Đã dọn':room.status==='dirty'?'Bẩn':'Đang dọn'}</div></div></div></th>${days.map(day => {
    const key = dateKey(day); const dayStart = new Date(`${key}T00:00`), dayEnd = new Date(`${key}T23:59:59`); const bookings = state.bookings.filter(b => b.roomId===room.id && !['rejected'].includes(b.status) && new Date(b.checkIn) <= dayEnd && new Date(b.checkOut) >= dayStart);
    return `<td class="day-cell ${key===dateKey(new Date())?'calendar-today':''}" data-cell-room="${room.id}" data-cell-date="${key}">${bookings.map(b => `<div class="booking-chip ${b.status}" data-booking-id="${b.id}"><div class="chip-name">${escapeHtml(b.guestName)}</div><div>${formatTime(b.checkIn)}–${formatTime(b.checkOut)} · ${statusLabel(b.status)}</div><div class="chip-money">${money(b.total)}</div></div>`).join('')}${!bookings.length?'<span class="tiny muted">+ Đặt phòng</span>':''}</td>`;
  }).join('')}</tr>`).join('');
  qs('#calendarTable').innerHTML = `<thead>${head}</thead><tbody>${body}</tbody>`;
}
qs('[data-prev-week]').addEventListener('click',()=>{start=addDays(start,-7);render()});
qs('[data-next-week]').addEventListener('click',()=>{start=addDays(start,7);render()});
qs('[data-today]').addEventListener('click',()=>{start=new Date();start.setHours(0,0,0,0);render()});
qs('[data-new-booking]').addEventListener('click',()=>openBookingForm({onSaved:render}));
qs('#calendarTable').addEventListener('click', e => {
  const chip=e.target.closest('[data-booking-id]'); if(chip){ openBookingForm({bookingId:chip.dataset.bookingId,onSaved:render}); return; }
  const cell=e.target.closest('[data-cell-room]'); if(cell) openBookingForm({roomId:cell.dataset.cellRoom,date:cell.dataset.cellDate,onSaved:render});
});
render();
