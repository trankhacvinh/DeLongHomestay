import { getState, roomHasConflict } from '../store.js';
import { qs, money, publicHeader, publicFooter, escapeHtml, getQuery, dateKey, parseSlotDate } from '../core.js';

const state = getState();
qs('[data-public-header]').innerHTML = publicHeader();
qs('[data-public-footer]').innerHTML = publicFooter();
const room = state.rooms.find(r => r.id === (getQuery('id') || 'R001')) || state.rooms[0];
let selectedDate = getQuery('date') || dateKey(new Date());

document.title = `${room.name} - De Long Homestay`;
qs('[data-detail]').innerHTML = `<div class="breadcrumb"><a href="rooms.html">Phòng</a> / ${escapeHtml(room.name)}</div><div class="detail-grid"><div class="stack"><div class="gallery-main"><img src="${room.image}" alt="${escapeHtml(room.name)}"></div><div class="detail-card"><div class="flex justify-between items-center gap-12"><div><h1 style="margin:0 0 5px">${escapeHtml(room.name)}</h1><div class="muted small">${room.capacity} khách · ${room.beds} giường · ${room.hasBathtub ? 'Có bồn tắm' : 'Không bồn tắm'}</div></div><span class="pill success">Đang mở bán</span></div><p class="muted" style="line-height:1.7">${escapeHtml(room.description)}</p><div class="amenity-list">${room.amenities.map(a => `<span class="pill">✓ ${escapeHtml(a)}</span>`).join('')}</div></div></div><aside class="booking-card sticky-card"><div class="field"><label>Ngày xem lịch</label><input id="detailDate" type="date" value="${selectedDate}"></div><div class="mt-16"><strong>Khung giờ gợi ý</strong><div class="slot-list mt-12" data-slots></div></div><div class="notice mt-16">Khung giờ chỉ là lựa chọn nhanh. Nếu cần nhận/trả phòng khác giờ, khách có thể gửi yêu cầu và nhân viên xác nhận lại.</div></aside></div>`;

function renderSlots() {
  selectedDate = qs('#detailDate').value;
  qs('[data-slots]').innerHTML = room.slots.map(slot => {
    const { checkIn, checkOut } = parseSlotDate(selectedDate, slot.start, slot.end);
    const conflict = roomHasConflict(room.id, checkIn, checkOut, null, state);
    const url = `booking.html?room=${room.id}&date=${selectedDate}&slot=${slot.id}`;
    return `<div class="slot ${conflict ? 'unavailable' : 'available'}"><div><strong>${slot.start} – ${slot.end}</strong><div class="tiny muted">${escapeHtml(slot.label)}</div></div><div class="text-right"><strong>${money(slot.price)}</strong><div>${conflict ? '<span class="tiny muted">Đã có lịch</span>' : `<a class="btn btn-primary btn-sm mt-12" href="${url}">Chọn</a>`}</div></div></div>`;
  }).join('');
}
qs('#detailDate').addEventListener('change', renderSlots);
renderSlots();
