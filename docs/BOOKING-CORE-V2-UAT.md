# Booking Core V2 — UAT

Branch: `feat/booking-core-v2-secure-identity`

## Chuẩn bị khóa mã hóa CCCD

Cấu hình secret `Security__IdentityDocumentEncryptionKeyBase64` bằng **32 byte ngẫu nhiên ở dạng Base64**. Không lưu khóa này trong repository hoặc cùng thư mục dữ liệu CCCD.

Ví dụ tạo khóa trên máy quản trị:

```bash
openssl rand -base64 32
```

Sau khi cấu hình, restart ứng dụng. Admin → Cấu hình → Quy tắc đặt phòng online phải báo **Kho lưu CCCD đã có khóa mã hóa**.

## Quy tắc booking

1. Admin → Cấu hình → Quy tắc đặt phòng online.
2. Đặt `Đặt tối đa = 3 đêm`, `Đã gồm trong giá = 2 khách`, `Phụ thu = 100.000đ`.
3. Nhập Nội quy & Chính sách và lưu.
4. Thử đặt public 4 đêm → phải bị từ chối.
5. Với phòng sức chứa 5: đặt 3 khách → phụ thu 100.000đ; 5 khách → phụ thu 300.000đ; 6 khách → bị từ chối.
6. Sau khi gửi thành công, booking chuyển sang **Giữ phòng** trong 3 phút. Một lượt public khác không được đặt trùng khoảng thời gian đó.
7. Sau khi hold hết hạn, lần kiểm tra availability tiếp theo phải giải phóng hold về **Yêu cầu** và khung được mở lại nếu chưa được nhân viên xác nhận.

## Thông tin khách

1. Form public phải có email, số khách, CCCD mặt trước/sau và checkbox đồng ý Nội quy & Chính sách.
2. Bấm tên Nội quy mở modal đọc nội dung.
3. CCCD có preview; có thể bỏ ảnh và chọn lại.
4. Sau một lượt gửi thành công, reload/mở lượt đặt mới trên cùng trình duyệt: tên, SĐT và email được điền lại; ảnh CCCD tuyệt đối không được lưu trong localStorage.

## Mã hóa CCCD

1. Bật `Bắt buộc CCCD mặt trước và mặt sau` sau khi khóa mã hóa đã sẵn sàng.
2. Gửi booking có đủ hai ảnh.
3. Trên ổ đĩa, file nằm dưới `DataRoot/private/identity-documents/{property}/{booking}/front.dlid|back.dlid` và không phải JPG/PNG/WebP đọc trực tiếp được.
4. Đổi một byte trong file hoặc chép file sang booking khác → API quản trị phải từ chối giải mã.
5. Admin/Manager/Staff có quyền quản lý booking → mở Chi tiết booking sẽ xem được ảnh qua endpoint giải mã; response dùng `Cache-Control: private,no-store`.
6. Người không có quyền booking hoặc request public không có đúng `Idempotency-Key` không được đọc/tải thay ảnh.
7. Gỡ secret mã hóa rồi restart: file vẫn ở dạng mã hóa và hệ thống không được fallback lưu plaintext; admin hiển thị trạng thái chưa có khóa.
