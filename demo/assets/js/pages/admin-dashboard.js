import { getState, bookingBalance } from '../store.js';
import { qs, initAdminShell, money, dateKey, bookingCardMini, formatDateTime, roomStatusPill, escapeHtml } from '../core.js';
import { openBookingForm } from './admin-booking-form.js';
initAdminShell('dashboard', 'Tổng quan');
function render() {
  const state = getState(); const today = dateKey(new Date()); const dayStart = new Date(`${today}T00:00`), dayEnd = new Date(`${today}T23:59`);
  const todayBookings = state.bookings.filter(b => !['cancelled','rejected'].includes(b.status) && new Date(b.checkIn) <= dayEnd && new Date(b.checkOut) >= dayStart);
  const checkins = state.bookings.filter(b => dateKey(b.checkIn) === today && !['cancelled','rejected','completed'].includes(b.status));
  const occupiedIds = new Set(todayBookings.filter(b => new Date() >= new Date(b.checkIn) && new Date() <= new Date(b.checkOut)).map(b => b.roomId));
  const outstanding = state.bookings.reduce((s,b) => s + bookingBalance(b,state),0);
  const todayRevenue = state.payments.filter(p => dateKey(p.paidAt) === today).reduce((s,p)=>s+Number(p.amount),0);
  qs('[data-kpis]').innerHTML = [
    ['Phòng đang có khách', `${occupiedIds.size}/${state.rooms.length}`, `${state.rooms.length-occupiedIds.size} phòng chưa có khách`, '▣'],
    ['Check-in hôm nay', checkins.length, 'Theo giờ check-in thực tế', '↘'],
    ['Thu hôm nay', money(todayRevenue), 'Theo giao dịch đã ghi nhận', '₫'],
    ['Còn phải thu', money(outstanding), 'Tất cả booking chưa thu đủ', '!']
  ].map(([l,v,n,i]) => `<div class="kpi"><div class="kpi-top"><span class="kpi-label">${l}</span><span class="kpi-icon">${i}</span></div><div class="kpi-value">${v}</div><div class="kpi-note">${n}</div></div>`).join('');
  qs('[data-today-bookings]').innerHTML = checkins.length ? checkins.sort((a,b)=>new Date(a.checkIn)-new Date(b.checkIn)).map(b=>bookingCardMini(b,state)).join('') : '<div class="notice">Hôm nay chưa có check-in.</div>';
  qs('[data-room-status]').innerHTML = state.rooms.map(r => `<div class="timeline-item"><div class="room-thumb"><img src="../${r.image}"></div><div><strong>${escapeHtml(r.name)}</strong><div class="tiny muted">${r.slots.length} khung giá</div></div><div>${roomStatusPill(r.status)}</div></div>`).join('');
  qs('[data-activity]').innerHTML = state.activity.slice(0,6).map(a => `<div class="timeline-item"><div class="timeline-time">${new Date(a.at).toLocaleTimeString('vi-VN',{hour:'2-digit',minute:'2-digit'})}</div><div><strong>${escapeHtml(a.text)}</strong><div class="tiny muted">${formatDateTime(a.at)}</div></div><span class="pill">${a.type}</span></div>`).join('');
}
qs('[data-new-booking]').addEventListener('click', () => openBookingForm({onSaved:render})); render();
