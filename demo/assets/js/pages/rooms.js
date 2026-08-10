import { getState, roomHasConflict } from '../store.js';
import { qs, money, publicHeader, publicFooter, escapeHtml, getQuery, dateKey, parseSlotDate } from '../core.js';

const state = getState();
qs('[data-public-header]').innerHTML = publicHeader();
qs('[data-public-footer]').innerHTML = publicFooter();

const today = dateKey(new Date());
qs('#filterDate').value = getQuery('checkin') || today;
qs('#filterGuests').value = getQuery('guests') || '2';

function render() {
  const date = qs('#filterDate').value || today;
  const guests = Number(qs('#filterGuests').value || 2);
  const bathtub = qs('#filterBathtub').value;
  const rows = state.rooms.filter(r => r.capacity >= guests && (bathtub !== 'yes' || r.hasBathtub));
  qs('[data-room-count]').textContent = `${rows.length} phòng phù hợp`;
  qs('[data-room-list]').innerHTML = rows.map(room => {
    const availableSlots = room.slots.filter(slot => {
      const dt = parseSlotDate(date, slot.start, slot.end);
      return !roomHasConflict(room.id, dt.checkIn, dt.checkOut, null, state);
    });
    const lowest = Math.min(...room.slots.map(s => s.price));
    return `<article class="room-card"><a class="room-image" href="room-detail.html?id=${room.id}&date=${date}"><img src="${room.image}" alt="${escapeHtml(room.name)}"><span class="pill ${availableSlots.length ? 'success' : 'danger'}">${availableSlots.length ? `${availableSlots.length} khung còn` : 'Đã kín theo preset'}</span></a><div class="room-body"><div class="room-title"><h3>${escapeHtml(room.name)}</h3><div class="price">Từ ${money(lowest)}</div></div><p class="small muted">${escapeHtml(room.description)}</p><div class="room-meta">${room.amenities.slice(0,4).map(x => `<span>${escapeHtml(x)}</span>`).join('<span>•</span>')}</div><div class="room-actions"><a class="btn btn-light btn-sm" href="room-detail.html?id=${room.id}&date=${date}">Xem lịch</a><a class="btn btn-primary btn-sm" href="booking.html?room=${room.id}&date=${date}">Đặt phòng</a></div></div></article>`;
  }).join('');
}
['filterDate','filterGuests','filterBathtub'].forEach(id => qs(`#${id}`).addEventListener('change', render));
render();
