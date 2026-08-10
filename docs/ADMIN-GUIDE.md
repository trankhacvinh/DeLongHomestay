# Hướng dẫn quản trị demo

## Đăng nhập

Vào `demo/admin/login.html`.

- User: `admin`
- Password: `demo123`

Đây chỉ là giả lập giao diện, không phải bảo mật thật.

## Lịch phòng

- Đây là màn hình chính của vận hành.
- Bấm ô trống tại giao điểm Phòng × Ngày để tạo booking.
- Chọn preset để tự điền giờ và giá.
- Có thể sửa giờ check-in/check-out sau đó.
- Hệ thống chặn booking trùng giờ của cùng phòng.
- Bấm vào booking trên lịch để sửa trạng thái, giá, ghi chú hoặc ghi nhận thanh toán.

## Đặt phòng

- Tìm theo mã booking, tên, SĐT hoặc phòng.
- Lọc theo trạng thái.
- Booking từ website khách được tạo ở trạng thái `Chờ xác nhận`.
- Nhân viên có thể bấm `Xác nhận` hoặc mở chi tiết để sửa.

## Khách hàng

Khách được nhận diện theo số điện thoại. Khi tạo booking mới với SĐT đã có, hệ thống dùng lại customer record.

## Dọn phòng

Trạng thái: `Bẩn → Đang dọn → Đã dọn`.

Demo cho phép đổi thủ công. Production sẽ tự tạo tác vụ/tự chuyển Bẩn sau checkout.

## Thu chi

- Payment được lưu riêng booking để hỗ trợ cọc + thanh toán nhiều lần.
- Có form ghi chi phí.
- `Còn phải thu` tính từ tổng booking trừ các payment.

## Backup demo

Vào `Cấu hình`:

- `Xuất backup JSON`: tải toàn bộ localStorage state.
- `Nhập backup`: khôi phục state từ file JSON.
- `Khôi phục demo`: quay về seed data.

LocalStorage chỉ tồn tại trên trình duyệt/domain hiện tại. Xóa browser data sẽ mất dữ liệu nếu chưa backup.
