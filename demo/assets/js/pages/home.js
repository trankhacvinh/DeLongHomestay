import { getState } from '../store.js';
import { qs, money, publicHeader, publicFooter, escapeHtml, dateKey } from '../core.js';

const state = getState();
qs('[data-public-header]').innerHTML = publicHeader();
qs('[data-public-footer]').innerHTML = publicFooter();

const roomHost = qs('[data-featured-rooms]');
roomHost.innerHTML = state.rooms.map(room => {
  const lowest = Math.min(...room.slots.map(s => s.price));
  return `<article class="room-card"><a class="room-image" href="room-detail.html?id=${room.id}"><img src="${room.image}" alt="${escapeHtml(room.name)}"><span class="pill ${room.status === 'clean' ? 'success' : 'warning'}">${room.hasBathtub ? 'Có bồn tắm' : 'Phòng riêng'}</span></a><div class="room-body"><div class="room-title"><h3>${escapeHtml(room.name)}</h3><div class="price">${money(lowest)}</div></div><div class="room-meta"><span>${room.capacity} khách</span><span>•</span><span>${room.beds} giường</span><span>•</span><span>Từ ${room.slots.length} khung</span></div><div class="room-actions"><a class="btn btn-light btn-sm" href="room-detail.html?id=${room.id}">Xem chi tiết</a><a class="btn btn-primary btn-sm" href="booking.html?room=${room.id}">Đặt phòng</a></div></div></article>`;
}).join('');

const today = new Date();
const tomorrow = new Date(today); tomorrow.setDate(today.getDate()+1);
qs('#searchCheckin').value = dateKey(today);
qs('#searchCheckout').value = dateKey(tomorrow);
qs('#homeSearch').addEventListener('submit', e => {
  e.preventDefault();
  const params = new URLSearchParams({ checkin: qs('#searchCheckin').value, checkout: qs('#searchCheckout').value, guests: qs('#searchGuests').value });
  location.href = `rooms.html?${params}`;
});
