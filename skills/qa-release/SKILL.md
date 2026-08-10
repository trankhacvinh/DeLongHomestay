---
name: qa-release
description: Checklist test và release cho De Long Homestay.
---

# QA & Release Skill

## Demo smoke test

1. Load trang khách.
2. Vào danh sách/chi tiết phòng.
3. Tạo request booking.
4. Đăng nhập admin demo.
5. Xác nhận booking từ Bookings.
6. Tạo booking từ Calendar.
7. Thử tạo booking overlap và xác nhận bị chặn.
8. Ghi payment.
9. Đổi housekeeping status.
10. Ghi expense.
11. Export backup, reset, import backup.
12. Kiểm tra mobile viewport.

## Production gate

- Unit tests domain.
- Integration tests database.
- Concurrent booking test.
- Authorization matrix test.
- Backup/restore test.
- Migration rehearsal.
- Error logging/monitoring.
- UAT checklist signed off.
