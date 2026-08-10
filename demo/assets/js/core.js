import { getState, getRoom, bookingBalance } from './store.js';

export const qs = (selector, root = document) => root.querySelector(selector);
export const qsa = (selector, root = document) => [...root.querySelectorAll(selector)];
export const money = value => `${Number(value || 0).toLocaleString('vi-VN')}đ`;
export const pad = n => String(n).padStart(2, '0');
export const dateKey = value => {
  const d = value instanceof Date ? value : new Date(value);
  return `${d.getFullYear()}-${pad(d.getMonth()+1)}-${pad(d.getDate())}`;
};
export const localDateTimeValue = value => {
  const d = value instanceof Date ? value : new Date(value);
  return `${dateKey(d)}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
};
export const formatDate = value => new Intl.DateTimeFormat('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' }).format(new Date(value));
export const formatShortDate = value => new Intl.DateTimeFormat('vi-VN', { weekday: 'short', day: '2-digit', month: '2-digit' }).format(new Date(value));
export const formatTime = value => new Intl.DateTimeFormat('vi-VN', { hour: '2-digit', minute: '2-digit', hour12: false }).format(new Date(value));
export const formatDateTime = value => `${formatDate(value)} ${formatTime(value)}`;
export const escapeHtml = value => String(value ?? '').replace(/[&<>'"]/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;',"'":'&#039;','"':'&quot;'}[c]));

export function parseSlotDate(date, start, end) {
  const startDt = new Date(`${date}T${start}:00`);
  let endDt = new Date(`${date}T${end}:00`);
  if (endDt <= startDt) endDt.setDate(endDt.getDate() + 1);
  return { checkIn: localDateTimeValue(startDt), checkOut: localDateTimeValue(endDt) };
}

export function statusLabel(status) {
  return ({
    pending: 'Chờ xác nhận', confirmed: 'Đã xác nhận', 'checked-in': 'Đang ở', completed: 'Hoàn tất',
    cancelled: 'Đã hủy', rejected: 'Từ chối'
  })[status] || status;
}

export function statusPill(status) {
  const cls = ({pending:'warning', confirmed:'info', 'checked-in':'success', completed:'success', cancelled:'danger', rejected:'danger'})[status] || '';
  return `<span class="pill ${cls}">${statusLabel(status)}</span>`;
}

export function roomStatusLabel(status) {
  return ({clean:'Đã dọn', dirty:'Bẩn', cleaning:'Đang dọn'})[status] || status;
}

export function roomStatusPill(status) {
  const cls = status === 'clean' ? 'success' : status === 'dirty' ? 'danger' : 'warning';
  return `<span class="pill ${cls}">${roomStatusLabel(status)}</span>`;
}

export function toast(message, type = '') {
  let stack = qs('.toast-stack');
  if (!stack) {
    stack = document.createElement('div'); stack.className = 'toast-stack'; document.body.appendChild(stack);
  }
  const el = document.createElement('div'); el.className = `toast ${type}`; el.textContent = message; stack.appendChild(el);
  setTimeout(() => el.remove(), 3200);
}

export function modal({ title, body, actions = '', onOpen }) {
  const wrap = document.createElement('div');
  wrap.className = 'modal-backdrop';
  wrap.innerHTML = `<div class="modal"><div class="modal-head"><h3>${title}</h3><button class="close-btn" data-close>×</button></div><div class="modal-body">${body}</div>${actions ? `<div class="modal-foot">${actions}</div>` : ''}</div>`;
  const close = () => wrap.remove();
  wrap.addEventListener('click', e => { if (e.target === wrap || e.target.closest('[data-close]')) close(); });
  document.body.appendChild(wrap);
  onOpen?.(wrap, close);
  return { el: wrap, close };
}

export function getQuery(name) { return new URLSearchParams(location.search).get(name); }

export function publicHeader(active = '') {
  const state = getState();
  return `<header class="site-header"><div class="container site-nav">
    <a class="brand" href="${active === 'nested' ? '../' : './'}index.html"><span class="brand-mark">DL</span><span>${escapeHtml(state.settings.property.name)}<small>Retreat Homestay</small></span></a>
    <nav class="site-links"><a href="${active === 'nested' ? '../' : './'}index.html">Trang chủ</a><a href="${active === 'nested' ? '../' : './'}rooms.html">Phòng</a><a href="${active === 'nested' ? '../' : './'}index.html#tien-ich">Tiện ích</a><a href="${active === 'nested' ? '../' : './'}index.html#lien-he">Liên hệ</a></nav>
    <div class="nav-actions"><a class="btn btn-light btn-sm" href="${active === 'nested' ? './' : './admin/'}login.html">Quản trị</a><a class="btn btn-primary btn-sm" href="${active === 'nested' ? '../' : './'}rooms.html">Đặt phòng</a></div>
    <button class="btn btn-light btn-sm mobile-menu-btn" type="button" onclick="document.querySelector('.site-links')?.classList.toggle('mobile-open')" aria-label="Mở menu">☰</button>
  </div></header>`;
}

export function publicFooter() {
  const s = getState().settings.property;
  return `<footer class="site-footer" id="lien-he"><div class="container footer-grid">
    <div><div class="brand"><span class="brand-mark">DL</span><span>${escapeHtml(s.name)}<small>Retreat Homestay</small></span></div><p>${escapeHtml(s.address)}</p></div>
    <div><h4>Liên hệ</h4><p>Điện thoại: ${escapeHtml(s.phone)}<br>Fanpage: ${escapeHtml(s.fanpage)}</p></div>
    <div><h4>Demo</h4><p>Dữ liệu lưu bằng localStorage trên trình duyệt. Bản demo không phải hệ thống bảo mật/production.</p></div>
  </div></footer>`;
}

export function adminSidebar(active) {
  const items = [
    ['dashboard','index.html','⌂','Tổng quan'], ['calendar','calendar.html','▦','Lịch phòng'], ['bookings','bookings.html','▤','Đặt phòng'],
    ['customers','customers.html','◎','Khách hàng'], ['housekeeping','housekeeping.html','✦','Dọn phòng'], ['finance','finance.html','₫','Thu chi'],
    ['reports','reports.html','↗','Báo cáo'], ['settings','settings.html','⚙','Cấu hình']
  ];
  return `<aside class="sidebar" id="adminSidebar"><a class="brand" href="index.html"><span class="brand-mark">DL</span><span>De Long<small>Admin Demo</small></span></a>
    <div class="sidebar-section">Vận hành</div>${items.slice(0,6).map(([k,url,icon,label]) => `<a class="side-link ${active===k?'active':''}" href="${url}"><span class="side-icon">${icon}</span>${label}</a>`).join('')}
    <div class="sidebar-section">Quản lý</div>${items.slice(6).map(([k,url,icon,label]) => `<a class="side-link ${active===k?'active':''}" href="${url}"><span class="side-icon">${icon}</span>${label}</a>`).join('')}
    <div class="sidebar-bottom"><a class="side-link" href="../index.html"><span class="side-icon">↗</span>Xem trang khách</a><button class="side-link" style="width:100%;border:0;background:transparent" data-admin-logout><span class="side-icon">⇥</span>Đăng xuất demo</button></div>
  </aside>`;
}

export function adminTopbar(title) {
  return `<div class="admin-topbar"><button class="btn btn-light btn-sm admin-menu-btn hidden" data-admin-menu>☰</button><div class="admin-title">${title}</div><div class="spacer"></div><span class="pill success">● LocalStorage</span><span class="small muted">Admin</span></div>`;
}

export function initAdminShell(active, title) {
  if (sessionStorage.getItem('delong_demo_auth') !== '1' && !location.pathname.endsWith('/login.html')) location.href = 'login.html';
  const sidebarHost = qs('[data-admin-sidebar]'); if (sidebarHost) sidebarHost.innerHTML = adminSidebar(active);
  const topHost = qs('[data-admin-topbar]'); if (topHost) topHost.innerHTML = adminTopbar(title);
  document.addEventListener('click', e => {
    if (e.target.closest('[data-admin-menu]')) qs('#adminSidebar')?.classList.toggle('open');
    if (e.target.closest('[data-admin-logout]')) { sessionStorage.removeItem('delong_demo_auth'); location.href = 'login.html'; }
  });
}

export function bookingCardMini(booking, state = getState()) {
  const room = getRoom(booking.roomId, state);
  const balance = bookingBalance(booking, state);
  return `<div class="timeline-item"><div class="timeline-time">${formatTime(booking.checkIn)}</div><div><strong>${escapeHtml(booking.guestName)}</strong><div class="tiny muted">${escapeHtml(room?.name || '')} · ${formatTime(booking.checkIn)}–${formatTime(booking.checkOut)}</div></div><div class="text-right">${statusPill(booking.status)}${balance ? `<div class="tiny muted mt-12">Còn ${money(balance)}</div>` : ''}</div></div>`;
}
