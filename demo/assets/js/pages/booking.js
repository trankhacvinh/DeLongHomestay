import { getState, addBooking, findCustomerByPhone } from '../store.js';
import { qs, money, publicHeader, publicFooter, getQuery, dateKey, parseSlotDate, localDateTimeValue, toast, escapeHtml } from '../core.js';

const state = getState();
qs('[data-public-header]').innerHTML = publicHeader();
qs('[data-public-footer]').innerHTML = publicFooter();
const room = state.rooms.find(r => r.id === (getQuery('room') || 'R001')) || state.rooms[0];
const selectedDate = getQuery('date') || dateKey(new Date());
const selectedSlot = room.slots.find(s => s.id === getQuery('slot')) || room.slots[room.slots.length - 1];
const dt = parseSlotDate(selectedDate, selectedSlot.start, selectedSlot.end);

qs('#roomId').value = room.id;
qs('#bookingRoomName').textContent = room.name;
qs('#checkIn').value = dt.checkIn;
qs('#checkOut').value = dt.checkOut;
qs('#basePrice').value = selectedSlot.price;
qs('#method').value = selectedSlot.label === 'Qua đêm' ? 'Qua đêm' : 'Theo khung';
qs('#bookingThumb').src = room.image;
qs('#source').innerHTML = state.settings.sources.map(x => `<option>${escapeHtml(x)}</option>`).join('');
qs('#method').innerHTML = state.settings.roomMethods.map(x => `<option>${escapeHtml(x)}</option>`).join('');
qs('#method').value = selectedSlot.label === 'Qua đêm' ? 'Qua đêm' : 'Theo khung';

function updateSummary() {
  const base = Number(qs('#basePrice').value || 0), surcharge = Number(qs('#surcharge').value || 0);
  qs('#sumBase').textContent = money(base); qs('#sumSurcharge').textContent = money(surcharge); qs('#sumTotal').textContent = money(base+surcharge);
}
['basePrice','surcharge'].forEach(id => qs(`#${id}`).addEventListener('input', updateSummary)); updateSummary();

qs('#phone').addEventListener('blur', () => {
  const customer = findCustomerByPhone(qs('#phone').value, state);
  if (customer && !qs('#guestName').value) { qs('#guestName').value = customer.name; toast(`Đã nhận diện khách cũ: ${customer.name}`, 'success'); }
});

qs('#bookingForm').addEventListener('submit', e => {
  e.preventDefault();
  try {
    const booking = addBooking({
      roomId: room.id, guestName: qs('#guestName').value.trim(), phone: qs('#phone').value.trim(),
      source: qs('#source').value, method: qs('#method').value, checkIn: qs('#checkIn').value,
      checkOut: qs('#checkOut').value, basePrice: qs('#basePrice').value, surcharge: qs('#surcharge').value,
      deposit: 0, status: 'pending', note: qs('#note').value.trim(), staff: 'Website'
    });
    location.href = `booking-success.html?id=${booking.id}`;
  } catch (error) { toast(error.message, 'danger'); }
});
