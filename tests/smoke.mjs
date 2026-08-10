import assert from 'node:assert/strict';

const mem = new Map();
globalThis.localStorage = {
  getItem: k => mem.has(k) ? mem.get(k) : null,
  setItem: (k,v) => mem.set(k,String(v)),
  removeItem: k => mem.delete(k),
  clear: () => mem.clear()
};
globalThis.window = { dispatchEvent() {} };
if (!globalThis.CustomEvent) globalThis.CustomEvent = class { constructor(type, init={}) { this.type=type; this.detail=init.detail; } };

const store = await import('../demo/assets/js/store.js');

let state = store.resetState(false);
assert.equal(state.rooms.length, 6, 'seed must have 6 rooms');
assert.equal(state.rooms.find(r => r.id === 'R004').slots[0].end, '14:30', 'Moon Stone corrected rate');
assert.equal(state.rooms.find(r => r.id === 'R006').slots[0].start, '12:00', 'La Roman corrected rate');

assert.equal(store.roomHasConflict('R001', '2026-08-10T14:30', '2026-08-10T16:00'), true, 'existing booking conflict must be detected');
assert.equal(store.roomHasConflict('R002', '2026-08-10T14:30', '2026-08-10T17:30'), false, 'free slot must be available');

const booking = store.addBooking({
  roomId: 'R002', guestName: 'Smoke Test', phone: '0900000001', source: 'Walk-in', method: 'Theo khung',
  checkIn: '2026-08-10T14:30', checkOut: '2026-08-10T17:30', basePrice: 210000, surcharge: 10000,
  deposit: 50000, status: 'confirmed', staff: 'Test'
});
assert.equal(booking.total, 220000);
state = store.getState();
assert.equal(store.bookingPaid(booking.id, state), 50000);
assert.equal(store.bookingBalance(booking, state), 170000);

assert.throws(() => store.addBooking({
  roomId: 'R002', guestName: 'Overlap', phone: '0900000002', checkIn: '2026-08-10T16:00', checkOut: '2026-08-10T18:00', basePrice: 210000
}), /trùng thời gian/);

store.addPayment(booking.id, 170000, 'Tiền mặt', 'Test pay');
state = store.getState();
assert.equal(store.bookingBalance(state.bookings.find(b => b.id === booking.id), state), 0);

store.updateRoomStatus('R002', 'clean');
assert.equal(store.getState().rooms.find(r => r.id === 'R002').status, 'clean');

console.log('Smoke tests passed');
