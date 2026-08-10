---
name: domain-booking
description: Quy tắc nghiệp vụ booking, lịch phòng, thanh toán và housekeeping của De Long Homestay.
---

# Domain Booking Skill

## Đọc khi

Sửa booking, lịch phòng, room rate, payment, customer matching, housekeeping hoặc report liên quan booking.

## Invariants

1. `checkIn/checkOut` thực tế là nguồn sự thật; preset không phải slot cứng.
2. `checkOut > checkIn`.
3. Một room không có 2 booking active overlap nhau.
4. Booking `cancelled/rejected` không chặn availability.
5. `total = basePrice + surcharge`.
6. `balance = total - SUM(payments)`; payment không lưu JSON trong booking.
7. Customer ưu tiên match theo normalized phone.
8. Status core: pending, confirmed, checked-in, completed, cancelled/rejected.
9. Housekeeping status không đồng nhất với booking status.
10. Màu là metadata hiển thị, không phải business state.

## UX rule

Nhân viên phải tạo booking từ lịch trong ít bước nhất có thể: click phòng/ngày → chọn preset → nhập khách/SĐT → lưu. Giờ luôn được phép chỉnh.

## Khi chuyển production

Conflict check phải chạy server-side trong transaction và có test concurrent booking.
