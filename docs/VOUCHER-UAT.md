# Voucher UAT

## Chuẩn bị

- Áp dụng migration `AddVoucherSystem` trên môi trường test.
- Cấu hình Pay2S cho cơ sở và mẫu “Email voucher” trong Cấu hình.
- Tạo một voucher giới hạn 1 lượt và một voucher giảm 100%.

## Nghiệm thu quản trị

- Admin/Manager tạo, sửa, kích hoạt, tạm dừng và lưu trữ; Staff chỉ xem, lọc, sao chép và gửi mã.
- Mã không phân biệt hoa thường/khoảng trắng, không trùng trong cùng cơ sở nhưng được trùng giữa hai cơ sở.
- Bảng đúng số lượt giữ, đã dùng, còn lại; trang chi tiết hiện booking/khách/số tiền/trạng thái.
- Chỉ booking đã thanh toán và đã hủy mới cho hoàn lượt thủ công; bắt buộc lý do và có audit.
- Email thành công/thất bại được lưu, lỗi SMTP không thay đổi voucher hay booking.

## Nghiệm thu booking công khai

- Giá nhiều khung/cả ngày được tính trước; voucher giảm tiền phòng và không giảm phụ thu.
- Mã sai, chưa hiệu lực, hết hạn, tạm dừng, sai phạm vi, hết lượt, vượt lượt khách và sai khách nhận trả thông báo riêng.
- Hai trình duyệt gửi cùng lượt cuối: đúng một booking vào thanh toán, booking còn lại nhận `voucher_usage_exhausted`.
- Pay2S thành công đổi `Reserved` thành `Redeemed`; hủy/chưa trả hết hạn đổi thành `Released`.
- IPN đến muộn không tự xác nhận booking cũ và không đổi lại lượt voucher đã giải phóng.
- Voucher 100% xác nhận booking ngay, gửi hướng dẫn check-in, không tạo Pay2S và không tạo `Payment` giả.
