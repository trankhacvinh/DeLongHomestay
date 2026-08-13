# Roadmap

## Phase 0 — Demo UX/localStorage

- [x] Chuẩn hóa dữ liệu Excel và 6 phòng.
- [x] Public/admin demo bằng localStorage.

## Phase 1 — Architecture freeze

- [x] 1 production project + 1 test project.
- [x] .NET 10 Razor Pages.
- [x] Vue 3 in-DOM progressive enhancement.
- [x] Minimal APIs cho interaction CRUD.
- [x] PostgreSQL + EF Core/Npgsql.
- [x] ASP.NET Core Identity.
- [x] Không Docker.
- [x] Soft-delete/archive/audit direction.
- [x] UTC + property timezone strategy.

## Phase 2 — Foundation

- [x] Solution/project skeleton.
- [x] EF Core/Npgsql/Identity.
- [x] Property, Room, RoomRate, UserPropertyAccess.
- [x] Seed De Long + 6 phòng + rates chuẩn theo Excel.
- [x] Vue + api.js + antiforgery pattern.
- [x] Rooms Razor + modal + Minimal API CRUD/archive.
- [x] CI restore/build/test + JavaScript syntax.
- [x] EF migrations + PostgreSQL local đã chạy được.
- [x] Seed admin bằng User Secrets và đăng nhập local.
- [x] PostgreSQL integration workflow không dùng Docker.

## Phase 3 — Customer + Booking core

- [x] Customer entity/service/API + normalized phone.
- [x] Booking entity/status/rules.
- [x] Create/edit booking + state transitions/cancel/no-show.
- [x] C# conflict validation.
- [x] PostgreSQL exclusion constraint chống booking overlap.
- [x] Database race conflict (`23P01`) → API `409 ProblemDetails`.
- [x] Calendar server-backed + Vue modal create/edit.
- [x] Booking list + search/filter/state actions.
- [x] Customers page + Vue add/edit.
- [x] Multi-property resolver + selector UI.
- [x] Booking audit timeline.
- [x] PostgreSQL constraint/integration tests.

## Phase 4 — Payments + Operations

- [x] Payment ledger Receipt/Refund + void giữ lịch sử.
- [x] Booking PaidAmount/BalanceAmount tính từ ledger.
- [x] Payment API + Vue modal không reload.
- [x] Check-out tự chuyển phòng sang Bẩn.
- [x] Housekeeping Bẩn → Đang dọn → Sạch.
- [x] Expenses + void.
- [x] Finance + Reports.
- [x] Settings/rates.
- [x] Dashboard vận hành.
- [x] UI/UX redesign desktop + mobile admin.

## Phase 5 — Public booking (đang thực hiện)

- [x] Public boutique landing page.
- [x] Public room catalog.
- [x] Room detail + rates.
- [x] Availability theo ngày/khung giờ từ PostgreSQL.
- [x] Booking request tạo trạng thái `Requested`.
- [x] Server-side price/time derivation từ RoomRate.
- [x] Conflict check trước khi nhận request.
- [x] Antiforgery + honeypot + rate limit 5 request/IP/10 phút.
- [x] Success page.
- [x] Dashboard Admin inbox cho yêu cầu website mới.
- [ ] Visual UAT public desktop/mobile.
- [ ] End-to-end UAT: public request → admin xử lý → Held/Confirmed → payment.
- [ ] Ảnh thật/gallery cho 6 phòng.
- [ ] Notification ngoài hệ thống (email/Zalo/SMS) — chỉ làm khi cần.

## Phase 6 — Migration & go-live

- [ ] Import dữ liệu Excel cần thiết.
- [ ] UAT tổng thể với dữ liệu gần thực tế.
- [ ] Backup/restore rehearsal.
- [ ] Production deployment.
- [ ] Logging/monitoring.
- [ ] Tài khoản/role nhân viên thật.
- [ ] Hướng dẫn nhân viên.
- [ ] Go-live.
