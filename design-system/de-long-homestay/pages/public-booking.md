# Public Booking — Page Override

Extends `design-system/de-long-homestay/MASTER.md` for the guest-facing De Long Homestay experience.

## Intent

- Product: boutique homestay / hospitality booking.
- Tone: calm, private, warm, trustworthy; never look like an admin dashboard or generic SaaS landing page.
- Primary task: help a guest understand rooms quickly, see transparent rates, check a date, and send a low-friction request.

## Visual system

- Keep De Long deep teal as the brand anchor: primary `#15585C`, deep `#0D2B2E`.
- Use warm off-white page backgrounds (`#F7F6F1`) rather than the cooler admin canvas.
- Use muted sand/amber only as a hospitality accent, not as the primary CTA.
- Large editorial headings with tight tracking; body copy stays high-contrast and compact.
- Room imagery area may use stylized branded placeholders until real photos are supplied. Do not pretend placeholders are real photos.
- Rounded corners are restrained (12–24px), with soft low-opacity shadows.

## Structure

### Landing
1. Hero with one primary CTA: check availability.
2. Date quick-check card.
3. Six-room catalog preview.
4. Short hospitality/value proposition.

### Room catalog/detail
- Clear capacity, bathtub flag where applicable, rate count and price-from.
- Rate rows expose actual preset start/end times and price.
- CTA always leads to booking request, not direct confirmation.

### Booking request
- Sequential progressive disclosure: date → room → rate → contact.
- Availability is server-backed; unavailable rates are disabled.
- Price and time are derived server-side from RoomRate.
- Explicitly state that submitting a request does not lock the room.
- On conflict, refresh availability and ask the guest to choose another slot.

## Interaction rules

- Vue is used only for the interactive request flow and availability refresh.
- No SPA router or global client state.
- All write requests use shared `DeLongApi` + antiforgery.
- Touch targets >= 44px; focus-visible states required.
- Hover effects must not shift layout.
- Respect `prefers-reduced-motion`.
- No horizontal scroll at 375px for public content.

## Anti-patterns

- No admin cards/KPI visual language on public pages.
- No emoji icons.
- No auto-confirm booking from public form.
- No client-authoritative price/status.
- No fake urgency/countdown timers.
- No excessive glassmorphism or decorative animation.
