# Implementation Status

Cập nhật cho production sau Admin UI redesign và branch `agent/public-booking`.

## Kiến trúc đã chốt

- .NET 10 ASP.NET Core Razor Pages.
- Một project production: `src/DeLong.Web`.
- Một project test: `tests/DeLong.Tests`.
- Vue 3 in-DOM progressive enhancement trong Razor (`v-on`, `v-model`, `v-bind`).
- Minimal APIs + shared `fetch`/antiforgery wrapper.
- EF Core/Npgsql + PostgreSQL.
- ASP.NET Core Identity + Role + `UserPropertyAccess`.
- Không Docker.

## Admin/back-office đã triển khai

- Identity/login/logout và role nền.
- Multi-property model + resolver/selector UI.
- Phòng, RoomRate, archive phòng.
- Khách hàng + normalized phone.
- Booking create/edit/status workflow.
- Calendar 7 ngày + Vue modal.
- PostgreSQL exclusion constraint chống overlap + `23P01` → `409`.
- Payment Receipt/Refund + void; Paid/Balance từ ledger.
- Housekeeping Clean/Dirty/Cleaning; checkout → Dirty.
- Expense + void, Finance, Reports.
- Audit timeline cho Booking.
- Admin UI/UX desktop/mobile đã visual UAT và merge.

## Public booking đang triển khai

- Public home theo phong cách boutique hospitality.
- Room catalog + room detail + rates.
- Availability theo ngày/khung giờ từ PostgreSQL.
- Public request tạo Booking `Requested`; public không tự giữ/xác nhận phòng.
- Giá và giờ lấy server-side từ RoomRate.
- Conflict check với Held/Confirmed/CheckedIn trước khi nhận request.
- Antiforgery + honeypot + fixed-window rate limit 5 request/IP/10 phút.
- Success page sau khi gửi.
- Dashboard Admin inbox cho `Requested` mới từ website.
- Integration test riêng cho public booking flow.
- Ảnh thật/gallery chưa triển khai; UI hiện dùng branded visual placeholders có chủ đích.

## EF migrations hiện có

1. `InitialCreate`
2. `AddPayments`
3. `AddHousekeepingState`
4. `AddAuditAndExpenses`
5. data-only migration sửa preset seed theo Excel gốc

Public booking hiện không cần schema migration mới.

## CI

- Workflow `.NET`: restore, Release build, xUnit, JavaScript syntax.
- Workflow `PostgreSQL Integration`: PostgreSQL cài sẵn trên GitHub runner, apply migrations và kiểm tra DB-level booking overlap.
- Public flow integration test kiểm tra server-authoritative rate, trạng thái `Requested` và availability khi slot bị khóa.

## Việc tiếp theo

1. Visual UAT public: home, room catalog/detail, booking form, success — desktop + mobile.
2. End-to-end UAT: website request → Admin inbox → Held/Confirmed → Payment.
3. Bổ sung ảnh thật/gallery cho 6 phòng khi có asset.
4. Sau đó mới chuyển sang import Excel cần thiết và go-live hardening.
