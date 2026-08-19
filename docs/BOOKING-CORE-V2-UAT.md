# Booking Core V2 — UAT

Branch: `feat/booking-core-v2-secure-identity`

## Khóa mã hóa CCCD — tự động

Không cần tạo secret hay nhập khóa thủ công. Khi Booking Core V2 được mở lần đầu, ứng dụng tự tạo khóa master 256-bit tại:

`DataRoot/security/identity-master.key`

Admin → Cấu hình → Quy tắc đặt phòng online phải báo **Khóa mã hóa CCCD được hệ thống tự quản lý**.

Khi chuyển server, sao lưu và khôi phục **toàn bộ `DataRoot`**. Không chỉ copy riêng thư mục ảnh CCCD. Bản sao cần có cả `DataRoot/private/identity-documents`, `DataRoot/private/booking-details` và `DataRoot/security/identity-master.key`.

Nếu đã từng UAT bản cũ với `Security__IdentityDocumentEncryptionKeyBase64`, cứ giữ secret đó trong lần chạy đầu sau khi cập nhật branch. Ứng dụng sẽ tự ghi khóa cũ vào `DataRoot/security/identity-master.key`; sau đó chỉ cần backup toàn bộ DataRoot.

## Quy tắc booking public

1. Admin → Cấu hình → Quy tắc đặt phòng online.
2. Đặt `Đặt tối đa = 3 đêm`, `Đã gồm trong giá = 2 khách`, `Phụ thu = 100.000đ`.
3. Nhập Nội quy & Chính sách và lưu.
4. Thử đặt public 4 đêm → phải bị từ chối.
5. Với phòng sức chứa 5: đặt 3 khách → phụ thu 100.000đ; 5 khách → phụ thu 300.000đ; 6 khách → bị từ chối.
6. Sau khi gửi thành công, booking chuyển sang **Giữ phòng** trong 3 phút. Một lượt public khác không được đặt trùng khoảng thời gian đó.
7. Sau khi hold hết hạn, lần kiểm tra availability tiếp theo phải giải phóng hold về **Yêu cầu** và khung được mở lại nếu chưa được nhân viên xác nhận.

## Thông tin khách đặt từ website

1. Form public phải có đủ: tên, SĐT, email, số khách, CCCD mặt trước, CCCD mặt sau và checkbox đồng ý Nội quy & Chính sách.
2. Email, hai mặt CCCD và checkbox chính sách đều là **bắt buộc** với khách tự đặt.
3. Bấm tên Nội quy mở modal đọc nội dung.
4. CCCD có preview; có thể bỏ ảnh và chọn lại trước khi gửi.
5. Sau một lượt gửi thành công, reload/mở lượt đặt mới trên cùng trình duyệt: tên, SĐT và email được điền lại; ảnh CCCD tuyệt đối không được lưu trong localStorage.
6. Mở booking vừa tạo trong Admin → Lịch phòng hoặc Admin → Đặt phòng: phải thấy email, số khách, trạng thái đồng ý chính sách và hai ảnh CCCD.
7. Ghi chú booking chỉ chứa đúng nội dung khách/nhân viên nhập. Không được tự chèn `[Đặt web]`, số khách hoặc `đồng ý Nội quy & Chính sách vX` vào ô Ghi chú.
8. Với booking web cũ còn dòng hệ thống trong Ghi chú, mở chi tiết booking phải tách metadata sang phần Thông tin khách và làm sạch Ghi chú.

## Nhân viên tự đặt phòng

1. Admin → Lịch phòng → Đặt phòng.
2. Form phải có thêm Email, Số lượng khách và khu vực CCCD / giấy tờ tùy thân.
3. Tên + SĐT + số khách vẫn là thông tin vận hành chính; số khách không được vượt sức chứa phòng.
4. **Email và CCCD không bắt buộc** với booking do nhân viên tạo.
5. Booking nhân viên không yêu cầu tick Nội quy & Chính sách; màn chi tiết phải hiển thị trạng thái này là không áp dụng.
6. Có thể chọn một hoặc hai ảnh CCCD, lưu booking rồi mở lại để xác nhận ảnh đã được mã hóa và hiển thị đúng.
7. Sửa booking có CCCD: có thể thay ảnh hoặc xóa từng mặt; thao tác vẫn qua API `ManageBookings` và antiforgery.

## Mã hóa CCCD

1. Gửi booking public có đủ hai ảnh CCCD.
2. Kiểm tra `DataRoot/security/identity-master.key` đã tồn tại và có đúng 32 byte.
3. Trên ổ đĩa, file CCCD nằm dưới `DataRoot/private/identity-documents/{property}/{booking}/front.dlid|back.dlid` và không phải JPG/PNG/WebP đọc trực tiếp được.
4. Đổi một byte trong `.dlid` hoặc chép file sang booking khác → API quản trị phải từ chối giải mã.
5. Admin/Manager/Staff có quyền quản lý booking → mở Chi tiết booking sẽ xem được ảnh qua endpoint giải mã; response dùng `Cache-Control: private,no-store`.
6. Người không có quyền booking hoặc request public không có đúng `Idempotency-Key` không được đọc/tải thay ảnh.

## Test chuyển server / backup

1. Tạo một booking có CCCD và xác nhận xem ảnh + email + số khách + consent được trong Admin.
2. Dừng app và sao lưu toàn bộ `DataRoot`.
3. Khởi động app với một DataRoot mới được restore từ bản sao đó, không cấu hình secret khóa nào.
4. Mở booking cũ → ảnh CCCD phải giải mã bình thường và guest details vẫn còn.
5. Test an toàn: trong một bản copy thử nghiệm, xóa `DataRoot/security/identity-master.key` nhưng giữ các `.dlid`, rồi restart. Hệ thống phải báo kho mã hóa không khả dụng và **không được tự sinh key mới**. Khôi phục lại key từ backup để đọc ảnh cũ.
