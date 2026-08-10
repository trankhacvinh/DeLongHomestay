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
- [x] UTC + Asia/Ho_Chi_Minh strategy.

## Phase 2 — Foundation

- [x] Tạo solution/project skeleton.
- [x] Cấu hình EF Core/Npgsql/Identity.
- [x] Domain ban đầu: Property, Room, RoomRate, UserPropertyAccess.
- [x] Seed definition De Long + 6 phòng + rates.
- [x] Vue + api.js + antiforgery pattern.
- [x] Feature mẫu Rooms: Razor + modal + API CRUD/archive.
- [x] CI restore/build/test + JavaScript syntax workflow.
- [x] Initial EF migration.
- [ ] Chạy migration trên PostgreSQL local.
- [ ] Seed admin bằng User Secrets.
- [ ] Integration tests dùng database `delong_test`.

## Phase 3 — Customer + Booking core (đang thực hiện)

- [x] Customer entity/service/API.
- [x] Booking entity/status/rules.
- [x] Create booking + status transitions/cancel/no-show.
- [x] Server conflict validation.
- [x] PostgreSQL exclusion constraint chống booking trùng phòng.
- [x] Calendar đọc Rooms + Bookings từ server.
- [x] Modal tạo booking trên Calendar không reload.
- [x] Trang danh sách Booking.
- [x] Trang quản lý Khách hàng.
- [ ] Edit booking (giờ/phòng/giá) + conflict protection.
- [ ] Booking audit log.
- [ ] Booking concurrency integration tests trên PostgreSQL thật.
- [ ] CurrentProperty resolver/selector thay property seed ID trong PageModel.

## Phase 4 — Payments + Operations

- [ ] Payment ledger/balance.
- [ ] Check-in/check-out side effects.
- [ ] Housekeeping workflow.
- [ ] Expenses.
- [ ] Reports.
- [ ] Settings/rates.

## Phase 5 — Public booking

- [ ] Public room catalog port từ demo.
- [ ] Availability.
- [ ] Booking request.
- [ ] Anti-spam/rate limiting.
- [ ] Notification workflow.

## Phase 6 — Migration & go-live

- [ ] Import Excel cần thiết.
- [ ] UAT.
- [ ] Backup/restore rehearsal.
- [ ] Production deployment.
- [ ] Logging/monitoring.
- [ ] Hướng dẫn nhân viên.
