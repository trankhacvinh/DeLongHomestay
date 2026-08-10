# Checklist nghiệm thu

## A. Trang khách

- [ ] Desktop/mobile không vỡ layout.
- [ ] Danh sách đủ 6 phòng.
- [ ] Lọc ngày/số khách/bồn tắm hoạt động.
- [ ] Chi tiết hiển thị đúng preset và giá.
- [ ] Slot đã có booking được báo không còn theo preset.
- [ ] Form bắt buộc tên + SĐT.
- [ ] Cho phép sửa check-in/check-out linh hoạt.
- [ ] Booking gửi từ web vào `pending`.
- [ ] Booking trùng giờ bị chặn.

## B. Lịch admin

- [ ] Hiển thị đủ 6 phòng × 7 ngày.
- [ ] Previous/Today/Next hoạt động.
- [ ] Bấm ô trống mở form đúng phòng/ngày.
- [ ] Chọn preset tự điền giờ + giá.
- [ ] Sửa giờ custom được.
- [ ] Bấm booking mở chi tiết.
- [ ] Status hiển thị phân biệt.

## C. Booking/customer/payment

- [ ] Search/filter booking.
- [ ] Xác nhận booking pending.
- [ ] Customer cùng SĐT không bị nhân bản không cần thiết.
- [ ] Ghi cọc khi tạo booking.
- [ ] Ghi thêm payment.
- [ ] Còn phải thu tính đúng.

## D. Housekeeping/finance

- [ ] Đổi Bẩn/Đang dọn/Đã dọn.
- [ ] Ghi expense.
- [ ] KPI thu/chi/công nợ cập nhật.

## E. Data safety demo

- [ ] Refresh trang vẫn giữ dữ liệu.
- [ ] Export JSON được.
- [ ] Reset rồi import JSON khôi phục được.
- [ ] Không có password thật trong source.

## F. Trước khi chuyển production

- [ ] User owner ký duyệt workflow.
- [ ] Chốt retention/backup.
- [ ] Chốt privacy cho CCCD/SĐT.
- [ ] Chốt roles.
- [ ] Chốt validation/edge cases.
