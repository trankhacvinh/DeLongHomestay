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
- [x] Seed definition De Long + 6 phòng + rates.
- [x] Vue + api.js + antiforgery pattern.
- [x] Rooms Razor + modal + Minimal API CRUD/archive.
- [x] CI restore/build/test + JavaScript syntax.
- [x] Initial EF migration.
- [ ] Apply migrations trên PostgreSQL local `delong_dev`.
- [ ] Seed admin bằng User Secrets.
- [ ] Integration tests trên `delong_test`.

## Phase 3 — Customer + Booking core

- [x] Customer entity/service/API + normalized phone.
- [x] Booking entity/status/rules.
- [x] Create booking + state transitions/cancel/no-show.
- [x] Edit booking room/time/amount/customer.
- [x] C# conflict validation.
- [x] PostgreSQL exclusion constraint chống booking overlap.
- [x] Database race conflict (`23P01`) → API `409 ProblemDetails`.
- [x] Calendar server-backed + Vue modal create/edit.
- [x] Booking list + search/filter/state actions.
- [x] Customers page + Vue add/edit.
- [x] CurrentProperty resolver thay seed property ID trong các PageModel vận hành.
- [ ] Property selector UI khi user có nhiều cơ sở.
- [ ] Booking audit log.
- [ ] Booking concurrency integration tests trên PostgreSQL thật.

## Phase 4 — Payments + Operations (đang thực hiện)

- [x] Payment ledger: Receipt/Refund.
- [x] Void payment giữ lịch sử + lý do/người thao tác.
- [x] Booking PaidAmount/BalanceAmount tính từ ledger.
- [x] Payment API + Vue modal không reload.
- [x] EF migration `AddPayments`.
- [ ] Check-out tự chuyển phòng sang Bẩn.
- [ ] Housekeeping workflow Bẩn → Đang dọn → Sạch.
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
