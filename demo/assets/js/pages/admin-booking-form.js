import { getState, addBooking, updateBooking, addPayment, bookingPaid, bookingBalance } from '../store.js';
import { modal, escapeHtml, money, localDateTimeValue, parseSlotDate, toast, statusPill, formatDateTime } from '../core.js';

export function openBookingForm({ roomId = '', date = '', bookingId = '', onSaved } = {}) {
  const state = getState();
  const booking = bookingId ? state.bookings.find(b => b.id === bookingId) : null;
  const defaultRoom = state.rooms.find(r => r.id === (booking?.roomId || roomId)) || state.rooms[0];
  const today = date || new Date().toISOString().slice(0,10);
  const defaultSlot = defaultRoom.slots[defaultRoom.slots.length-1];
  const slotDt = parseSlotDate(today, defaultSlot.start, defaultSlot.end);
  const body = `<form id="adminBookingForm" class="stack">
    <div class="form-grid">
      <div class="field"><label>Phòng *</label><select id="abRoom">${state.rooms.map(r => `<option value="${r.id}">${escapeHtml(r.name)}</option>`).join('')}</select></div>
      <div class="field"><label>Preset khung giờ</label><select id="abSlot"></select></div>
      <div class="field"><label>Tên khách *</label><input id="abName" required></div>
      <div class="field"><label>Số điện thoại *</label><input id="abPhone" required></div>
      <div class="field"><label>Check-in *</label><input id="abIn" type="datetime-local" required></div>
      <div class="field"><label>Check-out *</label><input id="abOut" type="datetime-local" required></div>
      <div class="field"><label>Hình thức</label><select id="abMethod">${state.settings.roomMethods.map(x => `<option>${escapeHtml(x)}</option>`).join('')}</select></div>
      <div class="field"><label>Nguồn</label><select id="abSource">${state.settings.sources.map(x => `<option>${escapeHtml(x)}</option>`).join('')}</select></div>
      <div class="field"><label>Giá phòng</label><input id="abPrice" type="number" min="0" step="1000"></div>
      <div class="field"><label>Phụ phí</label><input id="abSurcharge" type="number" min="0" step="1000" value="0"></div>
      ${booking ? `<div class="field"><label>Trạng thái</label><select id="abStatus"><option value="pending">Chờ xác nhận</option><option value="confirmed">Đã xác nhận</option><option value="checked-in">Đang ở</option><option value="completed">Hoàn tất</option><option value="cancelled">Đã hủy</option></select></div>` : `<div class="field"><label>Tiền cọc khi tạo</label><input id="abDeposit" type="number" min="0" step="1000" value="0"></div>`}
      <div class="field full"><label>Ghi chú</label><textarea id="abNote"></textarea></div>
    </div>
    <div class="summary-box"><div class="summary-line"><span>Tổng tiền</span><strong id="abTotal">0đ</strong></div>${booking ? `<div class="summary-line"><span>Đã thu</span><strong>${money(bookingPaid(booking.id, state))}</strong></div><div class="summary-line total"><span>Còn lại</span><strong>${money(bookingBalance(booking, state))}</strong></div>` : ''}</div>
  </form>
  ${booking ? `<div class="mt-16"><strong>Thanh toán nhanh</strong><div class="input-inline mt-12"><div class="field"><input id="quickPayAmount" type="number" min="1000" step="1000" placeholder="Số tiền"></div><div class="field"><select id="quickPayMethod"><option>Chuyển khoản</option><option>Tiền mặt</option></select></div></div><button class="btn btn-success btn-sm mt-12" id="quickPayBtn">+ Ghi nhận thanh toán</button></div>` : ''}`;
  const actions = `<button class="btn btn-light" type="button" data-close>Hủy</button><button class="btn btn-primary" type="submit" form="adminBookingForm">${booking ? 'Lưu thay đổi' : 'Tạo booking'}</button>`;
  modal({ title: booking ? `Booking ${booking.id}` : 'Tạo đặt phòng nhanh', body, actions, onOpen: (wrap, close) => {
    const $ = s => wrap.querySelector(s);
    $('#abRoom').value = defaultRoom.id;
    const fillSlots = (preserve = false) => {
      const room = state.rooms.find(r => r.id === $('#abRoom').value);
      $('#abSlot').innerHTML = room.slots.map(s => `<option value="${s.id}">${s.start}–${s.end} · ${money(s.price)}</option>`).join('') + '<option value="custom">Tùy chỉnh giờ</option>';
      if (!preserve) $('#abSlot').value = room.slots[room.slots.length-1].id;
    };
    fillSlots();
    if (booking) {
      $('#abRoom').value = booking.roomId; fillSlots(); $('#abSlot').value = 'custom';
      $('#abName').value = booking.guestName; $('#abPhone').value = booking.phone; $('#abIn').value = booking.checkIn; $('#abOut').value = booking.checkOut;
      $('#abMethod').value = booking.method; $('#abSource').value = booking.source; $('#abPrice').value = booking.basePrice; $('#abSurcharge').value = booking.surcharge || 0;
      $('#abStatus').value = booking.status; $('#abNote').value = booking.note || '';
    } else {
      $('#abName').value = ''; $('#abPhone').value = ''; $('#abIn').value = slotDt.checkIn; $('#abOut').value = slotDt.checkOut;
      $('#abPrice').value = defaultSlot.price; $('#abMethod').value = 'Qua đêm';
    }
    const recalc = () => $('#abTotal').textContent = money(Number($('#abPrice').value || 0) + Number($('#abSurcharge').value || 0)); recalc();
    $('#abPrice').addEventListener('input', recalc); $('#abSurcharge').addEventListener('input', recalc);
    $('#abRoom').addEventListener('change', () => { fillSlots(); $('#abSlot').dispatchEvent(new Event('change')); });
    $('#abSlot').addEventListener('change', () => {
      if ($('#abSlot').value === 'custom') return;
      const room = state.rooms.find(r => r.id === $('#abRoom').value), slot = room.slots.find(s => s.id === $('#abSlot').value);
      const baseDate = ($('#abIn').value || `${today}T00:00`).slice(0,10); const dt = parseSlotDate(baseDate, slot.start, slot.end);
      $('#abIn').value = dt.checkIn; $('#abOut').value = dt.checkOut; $('#abPrice').value = slot.price; $('#abMethod').value = slot.label === 'Qua đêm' ? 'Qua đêm' : 'Theo khung'; recalc();
    });
    $('#adminBookingForm').addEventListener('submit', e => {
      e.preventDefault();
      try {
        const payload = { roomId: $('#abRoom').value, guestName: $('#abName').value.trim(), phone: $('#abPhone').value.trim(), checkIn: $('#abIn').value, checkOut: $('#abOut').value, method: $('#abMethod').value, source: $('#abSource').value, basePrice: Number($('#abPrice').value || 0), surcharge: Number($('#abSurcharge').value || 0), note: $('#abNote').value.trim() };
        if (booking) updateBooking(booking.id, { ...payload, status: $('#abStatus').value });
        else addBooking({ ...payload, deposit: Number($('#abDeposit').value || 0), status: 'confirmed', staff: 'Admin' });
        toast(booking ? 'Đã cập nhật booking.' : 'Đã tạo booking.', 'success'); close(); onSaved?.();
      } catch (error) { toast(error.message, 'danger'); }
    });
    $('#quickPayBtn')?.addEventListener('click', () => {
      try {
        addPayment(booking.id, Number($('#quickPayAmount').value), $('#quickPayMethod').value, 'Thanh toán nhanh từ booking');
        toast('Đã ghi nhận thanh toán.', 'success'); close(); onSaved?.();
      } catch (error) { toast(error.message, 'danger'); }
    });
  }});
}
