# Roadmap

## Phase 0 — Demo UX bằng localStorage (hiện tại)

- [x] Chuẩn hóa room/rate từ Excel.
- [x] Trang khách: danh sách, chi tiết, gửi yêu cầu booking.
- [x] Admin dashboard.
- [x] Lịch phòng 7 ngày + tạo/sửa booking.
- [x] Chặn trùng lịch ở client.
- [x] Booking status.
- [x] Customer history cơ bản.
- [x] Housekeeping status.
- [x] Payment/công nợ.
- [x] Expense.
- [x] Report demo.
- [x] Settings + export/import backup JSON.
- [ ] User nghiệm thu UX thực tế trên desktop/mobile.
- [ ] Chốt thuật ngữ/trạng thái cuối cùng.

## Phase 1 — Freeze nghiệp vụ

- [ ] Test demo với người đang quản lý Excel.
- [ ] Chốt quy tắc giữ phòng/cọc/hủy/no-show.
- [ ] Chốt logic qua đêm và đổi phòng/pass phòng.
- [ ] Chốt phụ thu thêm người/gối/late checkout.
- [ ] Chốt role/permission.
- [ ] Chốt báo cáo quản trị tối thiểu.
- [ ] Chốt dữ liệu cần migrate từ Excel lịch sử.

## Phase 2 — Skeleton Razor Pages + PostgreSQL

- [ ] Tạo solution/projects.
- [ ] PostgreSQL schema + migrations.
- [ ] EF Core/Npgsql infrastructure.
- [ ] Authentication/authorization.
- [ ] Seed property/rooms/rates.
- [ ] Shared layout port từ demo.

## Phase 3 — Booking core

- [ ] Customer service.
- [ ] Booking create/edit/status.
- [ ] Calendar server-backed.
- [ ] Conflict check + transaction/concurrency test.
- [ ] Payments + balance.
- [ ] Booking audit log.

## Phase 4 — Operations

- [ ] Housekeeping workflow.
- [ ] Expenses.
- [ ] Reports.
- [ ] Settings/room rates.
- [ ] Multi-property access.

## Phase 5 — Public booking

- [ ] Room catalog.
- [ ] Availability query.
- [ ] Booking request.
- [ ] Anti-spam/rate limiting.
- [ ] Notification workflow (kênh sẽ chốt sau).

## Phase 6 — Migration & go-live

- [ ] Import master data từ Excel.
- [ ] Import booking lịch sử cần thiết.
- [ ] UAT với dữ liệu clone.
- [ ] Backup/restore rehearsal.
- [ ] Production deployment.
- [ ] Monitoring/logging.
- [ ] Hướng dẫn nhân viên + bàn giao.
