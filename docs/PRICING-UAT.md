# UAT giá cuối tuần, combo và ngày đặc biệt

## Công thức chuẩn

1. Xác định bảng giá ngày thường/cuối tuần theo ngày phục vụ.
2. Nếu chọn đủ toàn bộ ca đang hoạt động, dùng giá combo cả ngày; nếu không, xét giảm combo đúng số ca liên tục được cấu hình.
3. Tính phụ thu ngày đặc biệt trên tiền phòng sau combo.
4. Voucher chỉ giảm `RoomAmount` sau combo, không giảm `SpecialSurchargeAmount` và phụ thu khách.
5. Tổng thanh toán: `RoomAmount + SpecialSurchargeAmount + ExtraAmount - DiscountAmount`.

## Ca kiểm thử vận hành

- Thứ 7 và Chủ nhật dùng giá cuối tuần; các ngày còn lại dùng giá ngày thường.
- Chọn đúng 3 ca liên tục trong cùng ngày được giảm theo cấu hình; 3 ca khác ngày không gom chung.
- Chọn đủ mọi ca của ngày dùng giá cả ngày tương ứng ngày thường/cuối tuần.
- Ngày đặc biệt có thể buộc dùng giá ngày thường, cuối tuần hoặc tự động theo thứ.
- Phụ thu 10%/15% được cộng sau combo và không bị voucher giảm.
- Ngày “Chỉ combo cả ngày” không cho server nhận ca lẻ; lịch public chọn cả hàng ngày khi còn đủ ca.
- Hai khoảng ngày đặc biệt đang bật không được giao nhau.
- Booking cũ giữ snapshot giá, combo, phụ thu và tên ngày đặc biệt sau khi cấu hình thay đổi.

## Dữ liệu giá cần nhập tại quản trị

Các số 780/910 trong yêu cầu được hiểu là 780.000/910.000 VND. Nhập tại **Quản lý → Giá & ngày đặc biệt → Bảng giá phòng** theo từng cơ sở; migration không tự sửa dữ liệu giá đang chạy.
