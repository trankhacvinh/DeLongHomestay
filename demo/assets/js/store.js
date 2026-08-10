import { seedData, STORAGE_KEY, DEMO_VERSION } from './data.js';

const clone = (value) => JSON.parse(JSON.stringify(value));

export function loadState() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return resetState(false);
    const state = JSON.parse(raw);
    if (!state?.meta?.version) return resetState(false);
    return state;
  } catch (error) {
    console.warn('Unable to load local demo state. Reseeding.', error);
    return resetState(false);
  }
}

export function saveState(state) {
  state.meta = state.meta || {};
  state.meta.version = DEMO_VERSION;
  state.meta.updatedAt = new Date().toISOString();
  localStorage.setItem(STORAGE_KEY, JSON.stringify(state));
  window.dispatchEvent(new CustomEvent('delong:state-changed', { detail: state }));
  return state;
}

export function resetState(notify = true) {
  const state = clone(seedData);
  localStorage.setItem(STORAGE_KEY, JSON.stringify(state));
  if (notify) window.dispatchEvent(new CustomEvent('delong:state-changed', { detail: state }));
  return state;
}

export function getState() { return loadState(); }

export function mutate(mutator) {
  const state = loadState();
  mutator(state);
  return saveState(state);
}

export function uid(prefix = 'ID') {
  return `${prefix}${Date.now().toString(36).toUpperCase()}${Math.random().toString(36).slice(2, 6).toUpperCase()}`;
}

export function exportState() {
  return JSON.stringify(loadState(), null, 2);
}

export function importState(jsonText) {
  const state = JSON.parse(jsonText);
  if (!state?.rooms || !Array.isArray(state.rooms) || !state?.bookings || !Array.isArray(state.bookings)) {
    throw new Error('File backup không đúng cấu trúc De Long Homestay demo.');
  }
  return saveState(state);
}

export function getRoom(roomId, state = loadState()) {
  return state.rooms.find(r => r.id === roomId);
}

export function getCustomer(customerId, state = loadState()) {
  return state.customers.find(c => c.id === customerId);
}

export function bookingPaid(bookingId, state = loadState()) {
  return state.payments.filter(p => p.bookingId === bookingId).reduce((sum, p) => sum + Number(p.amount || 0), 0);
}

export function bookingBalance(booking, state = loadState()) {
  return Math.max(0, Number(booking.total || 0) - bookingPaid(booking.id, state));
}

export function overlaps(startA, endA, startB, endB) {
  return new Date(startA).getTime() < new Date(endB).getTime() && new Date(endA).getTime() > new Date(startB).getTime();
}

export function roomHasConflict(roomId, checkIn, checkOut, ignoreBookingId = null, state = loadState()) {
  return state.bookings.some(b =>
    b.roomId === roomId &&
    b.id !== ignoreBookingId &&
    !['cancelled', 'rejected'].includes(b.status) &&
    overlaps(checkIn, checkOut, b.checkIn, b.checkOut)
  );
}

export function findCustomerByPhone(phone, state = loadState()) {
  const normalized = String(phone || '').replace(/\D/g, '');
  return state.customers.find(c => String(c.phone || '').replace(/\D/g, '') === normalized);
}

export function upsertCustomer({ name, phone, citizenId = '' }, state) {
  let customer = findCustomerByPhone(phone, state);
  if (customer) {
    customer.name = name || customer.name;
    if (citizenId) customer.citizenId = citizenId;
    return customer;
  }
  customer = { id: uid('C'), name, phone, citizenId, addedAt: new Date().toISOString() };
  state.customers.push(customer);
  return customer;
}

export function addBooking(input) {
  const checkIn = input.checkIn;
  const checkOut = input.checkOut;
  if (!checkIn || !checkOut || new Date(checkOut) <= new Date(checkIn)) {
    throw new Error('Thời gian checkout phải sau check-in.');
  }
  const state = loadState();
  if (roomHasConflict(input.roomId, checkIn, checkOut, input.id || null, state)) {
    throw new Error('Phòng đã có booking trùng thời gian.');
  }
  const customer = upsertCustomer({ name: input.guestName, phone: input.phone, citizenId: input.citizenId || '' }, state);
  const total = Number(input.basePrice || 0) + Number(input.surcharge || 0);
  const booking = {
    id: input.id || uid('BK'),
    roomId: input.roomId,
    customerId: customer.id,
    guestName: input.guestName,
    phone: input.phone,
    source: input.source || 'Facebook',
    staff: input.staff || 'Admin',
    method: input.method || 'Theo khung',
    createdAt: input.createdAt || new Date().toISOString(),
    checkIn,
    checkOut,
    basePrice: Number(input.basePrice || 0),
    surcharge: Number(input.surcharge || 0),
    total,
    status: input.status || 'pending',
    note: input.note || '',
    colorKey: input.colorKey || 'booking'
  };
  state.bookings.push(booking);
  if (Number(input.deposit || 0) > 0) {
    state.payments.push({ id: uid('PAY'), bookingId: booking.id, paidAt: new Date().toISOString(), amount: Number(input.deposit), method: input.paymentMethod || 'Chuyển khoản', note: 'Cọc khi tạo booking' });
  }
  state.activity.unshift({ id: uid('ACT'), at: new Date().toISOString(), text: `Tạo booking ${booking.id} - ${booking.guestName}`, type: 'booking' });
  return saveState(state) && booking;
}

export function updateBooking(id, patch) {
  const state = loadState();
  const booking = state.bookings.find(b => b.id === id);
  if (!booking) throw new Error('Không tìm thấy booking.');
  const next = { ...booking, ...patch };
  if (patch.checkIn || patch.checkOut || patch.roomId) {
    if (roomHasConflict(next.roomId, next.checkIn, next.checkOut, id, state)) {
      throw new Error('Phòng đã có booking trùng thời gian.');
    }
  }
  Object.assign(booking, patch);
  if (patch.basePrice !== undefined || patch.surcharge !== undefined) booking.total = Number(booking.basePrice || 0) + Number(booking.surcharge || 0);
  state.activity.unshift({ id: uid('ACT'), at: new Date().toISOString(), text: `Cập nhật ${booking.id} - ${booking.guestName}`, type: 'booking' });
  saveState(state);
  return booking;
}

export function addPayment(bookingId, amount, method, note = '') {
  const state = loadState();
  const booking = state.bookings.find(b => b.id === bookingId);
  if (!booking) throw new Error('Không tìm thấy booking.');
  const payment = { id: uid('PAY'), bookingId, paidAt: new Date().toISOString(), amount: Number(amount), method, note };
  if (!payment.amount || payment.amount <= 0) throw new Error('Số tiền thanh toán phải lớn hơn 0.');
  state.payments.push(payment);
  state.activity.unshift({ id: uid('ACT'), at: payment.paidAt, text: `Ghi nhận ${payment.amount.toLocaleString('vi-VN')}đ cho ${booking.id}`, type: 'payment' });
  saveState(state);
  return payment;
}

export function addExpense(input) {
  const state = loadState();
  const expense = {
    id: uid('EXP'), spentAt: input.spentAt || new Date().toISOString(), propertyId: state.settings.property.id,
    category: input.category, content: input.content, amount: Number(input.amount || 0), note: input.note || ''
  };
  state.expenses.push(expense);
  state.activity.unshift({ id: uid('ACT'), at: expense.spentAt, text: `Ghi chi ${expense.amount.toLocaleString('vi-VN')}đ - ${expense.content}`, type: 'expense' });
  saveState(state);
  return expense;
}

export function updateRoomStatus(roomId, status, staff = 'Cô Thúy', note = '') {
  const state = loadState();
  const room = state.rooms.find(r => r.id === roomId);
  if (!room) throw new Error('Không tìm thấy phòng.');
  room.status = status;
  let task = state.housekeeping.find(h => h.roomId === roomId && h.status !== 'clean');
  if (!task) {
    task = { id: uid('HK'), roomId, status, staff, updatedAt: new Date().toISOString(), note };
    state.housekeeping.push(task);
  } else {
    task.status = status; task.staff = staff; task.updatedAt = new Date().toISOString(); task.note = note || task.note;
  }
  state.activity.unshift({ id: uid('ACT'), at: task.updatedAt, text: `${room.name}: ${status === 'clean' ? 'đã dọn' : status === 'cleaning' ? 'đang dọn' : 'bẩn'}`, type: 'housekeeping' });
  saveState(state);
  return room;
}
