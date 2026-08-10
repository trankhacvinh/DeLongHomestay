# Kết quả kiểm thử demo

Ngày kiểm thử: 2026-08-10.

## Automated smoke test

Lệnh:

```bash
npm test
npm run check:js
```

Kết quả:

- Seed đủ 6 phòng: PASS.
- Sửa dữ liệu Moon Stone / La Roman: PASS.
- Conflict detection với booking có sẵn: PASS.
- Tạo booking ở slot trống: PASS.
- Chặn booking overlap: PASS.
- Cọc/payment/balance: PASS.
- Housekeeping status mutation: PASS.
- Syntax check toàn bộ JavaScript: PASS.
- Kiểm tra link/source relative trong HTML: PASS.

## Manual UAT còn cần

- Xem trực tiếp UI desktop/mobile trên trình duyệt người dùng.
- Kiểm tra thuật ngữ và thứ tự thao tác với người đang vận hành Excel.
- Chốt các edge case: pass phòng, đổi phòng, no-show, late checkout, thêm người/gối.
