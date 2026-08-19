# Booking Core V2 — UAT

Branch: `feat/booking-core-v2-secure-identity`

## Khóa mã hóa CCCD — tự động

Không cần tạo secret hay nhập khóa thủ công. Khi Booking Core V2 được mở lần đầu, ứng dụng tự tạo khóa master 256-bit tại:

`DataRoot/security/identity-master.key`

Admin → Cấu hình → Quy tắc đặt phòng online phải báo **Khóa mã hóa CCCD được hệ thống tự quản lý**.

Khi chuyển server, sao lưu và khôi phục **toàn bộ `DataRoot`**. Không chỉ copy riêng thư mục ảnh CCCD. Bản sao cần có cả `DataRoot/private/identity-documents` và `DataRoot/security/identity-master.key`.

Nếu đã từng UAT bản cũ với `Security__IdentityDocumentEncryptionKeyBase64`, cứ giữ secret đó trong lần chạy đầu sau khi cập nhật branch. Ứng dụng sẽ tự ghi khóa cũ vào `DataRoot/security/identity-master.key`; sau đó chỉ cần backup toàn bộ DataRoot.

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

1. Bật `Bắt buộc CCCD mặt trước và mặt sau`.
2. Gửi booking có đủ hai ảnh.
3. Kiểm tra `DataRoot/security/identity-master.key` đã tồn tại và có đúng 32 byte.
4. Trên ổ đĩa, file CCCD nằm dưới `DataRoot/private/identity-documents/{property}/{booking}/front.dlid|back.dlid` và không phải JPG/PNG/WebP đọc trực tiếp được.
5. Đổi một byte trong `.dlid` hoặc chép file sang booking khác → API quản trị phải từ chối giải mã.
6. Admin/Manager/Staff có quyền quản lý booking → mở Chi tiết booking sẽ xem được ảnh qua endpoint giải mã; response dùng `Cache-Control: private,no-store`.
7. Người không có quyền booking hoặc request public không có đúng `Idempotency-Key` không được đọc/tải thay ảnh.

## Test chuyển server / backup

1. Tạo một booking có CCCD và xác nhận xem ảnh được trong Admin.
2. Dừng app và sao lưu toàn bộ `DataRoot`.
3. Khởi động app với một DataRoot mới được restore từ bản sao đó, không cấu hình secret khóa nào.
4. Mở booking cũ → ảnh CCCD phải giải mã bình thường.
5. Test an toàn: trong một bản copy thử nghiệm, xóa `DataRoot/security/identity-master.key` nhưng giữ các `.dlid`, rồi restart. Hệ thống phải báo kho mã hóa không khả dụng và **không được tự sinh key mới**. Khôi phục lại key từ backup để đọc ảnh cũ.
