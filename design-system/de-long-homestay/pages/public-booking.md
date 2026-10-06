# Public Booking — Page Override

Extends `design-system/de-long-homestay/MASTER.md` for the guest-facing De Long Homestay experience.

## Intent

- Product: boutique homestay / hospitality booking.
- Tone: calm, private, warm, trustworthy; never look like an admin dashboard or generic SaaS landing page.
- Primary task: help a guest understand rooms quickly, see transparent rates, check a date, and send a low-friction request.

## Visual system (2026-10 redesign, `wwwroot/css/public-redesign.css`)

- Ink `#12292B` for text and primary buttons; warm paper ground `#F7F3EC` / `#EFE9DE`; white surfaces.
- Amber `#F2B35A` as the accent (selected slots, price sticker, marquee, CTA band); `#A35F15` for accent text on light grounds.
- One typeface: Be Vietnam Pro (self-hosted, OFL). Scale: h1 34–54px, h2 28–40px, h3 19–22px, body 16px; headings 800, sentence case, line-height ≥ 1.15 (Vietnamese diacritics). Eyebrows: 12px, 700, uppercase, +0.14em tracking, amber ink.
- Theme mode "Giao diện chuẩn" (default) keeps this system authoritative over visual-editor styling; see `docs/PUBLIC-REDESIGN-UAT.md`.
- Room cards are colour-blocked and numbered (01, 02…) with arch-shaped photos; pills (999px) for buttons; cards 20–28px radius; shadows only where something floats (selection bar, sticker).
- Calendar board: light, ink outlines; free = white outlined, selected = amber with offset shadow, booked = hatched sand, next valid slot = dashed amber `+`.
- Footer is light and shows one contact card per property (logo, address, hotline, Zalo, Facebook, Google Maps).
- Room imagery area may use stylized branded placeholders until real photos are supplied. Do not pretend placeholders are real photos.

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
