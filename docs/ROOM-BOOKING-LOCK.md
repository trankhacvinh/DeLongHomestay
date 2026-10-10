# Khóa phòng tạm thời

## Sử dụng

Admin/Manager chọn **Khóa phòng** tại quản lý phòng hoặc lịch V1/V2, nhập lý do (1–500 ký tự) và xác nhận. Phòng chặn tất cả đơn mới, bao gồm ngày tương lai, đến khi được mở khóa thủ công. Staff/Media không có quyền khóa/mở khóa; API kiểm tra quyền chi nhánh và antiforgery.

Phòng vẫn xuất hiện trên website với nhãn “Tạm ngừng nhận đặt phòng”; khách không chọn giờ hoặc đặt được. Lý do/người khóa/thời điểm chỉ xuất hiện trong admin. Lịch vẫn giữ các đơn hiện có và lịch sử.

Đơn trước khi khóa vẫn thanh toán, tra cứu, nhận/trả phòng và sửa thông tin trên cùng phòng bình thường. Không thể chuyển đơn khác vào phòng khóa. Muốn nhập đơn mới phải mở khóa trước. Import Excel cũng không thể tạo đơn vào phòng khóa. Không gửi thông báo khóa/mở khóa cho khách.

## Dữ liệu và API

Migration `20261006150837_AddRoomBookingLock` thêm `is_booking_locked` (mặc định false), `booking_lock_reason`, `booking_locked_at_utc`, `booking_locked_by_user_id`; không sửa booking/payment cũ. Audit Room lưu `BookingLocked`/`BookingUnlocked`, người thao tác và trạng thái trước/sau.

`PUT /api/admin/properties/{propertyId}/rooms/{roomId}/booking-lock`

Body: `{ "isLocked": true, "reason": "Bảo trì điều hòa" }`; mở khóa dùng `{ "isLocked": false }`. Response là RoomDto cập nhật. Public DTO chỉ bổ sung `isBookingLocked`, không chứa lý do nội bộ. Tạo/chuyển đơn bị chặn trả lỗi nghiệp vụ `room_booking_locked` qua cơ chế lỗi hiện có.

Khóa/mở khóa và nhận đơn mới dùng `SELECT ... FOR UPDATE` trên cùng dòng phòng trong transaction PostgreSQL. Đơn được nhận trước thì commit trước khi khóa; khóa nhận trước thì chặn đơn mới. Transaction của voucher/import được giữ nguyên. Cache public bị xóa sau commit; lịch admin cập nhật qua sự kiện `room.booking-lock-changed` và poll. Public stream chỉ phát metadata thay đổi, không phát lý do; khách đang mở lịch được kiểm tra lại qua stream và poll.

## Deploy và nghiệm thu

- Backup database, chạy migration/bundle mới rồi triển khai application và static assets cùng phiên bản. Không dùng bundle cũ.
- Phòng hiện có phải mặc định mở; thử khóa một phòng, tạo đơn từ trang khách, admin booking, V1/V2 và import để xác nhận bị chặn.
- Kiểm tra một QR/đơn tạo trước khi khóa vẫn thanh toán và tra cứu bình thường; sau mở khóa có thể đặt lại.
- Kiểm tra role Admin/Manager, Staff/Media và quyền chi nhánh; ghi nhận lịch sử audit.
- Kiểm tra modal và nhãn khóa ở 320/390px; lịch vẫn hiển thị đơn cũ.
- PostgreSQL tests dùng `DELONG_TEST_CONNECTION` trỏ đến database thử nghiệm riêng; không dùng database production.

## Kết quả kiểm tra source (2026-10-06)

- Build thành công; 31 test PostgreSQL liên quan khóa phòng, phân quyền API, import, lịch và SePay đạt trên database thử nghiệm.
- Test tự động nhận/trả phòng đạt khi chạy trên database sạch riêng (test đếm toàn bộ đơn đến hạn, không dùng chung dữ liệu từ các test thanh toán).
- 6 test JavaScript đạt; node syntax checks và `git diff --check` sạch.
- Preview từ template quản lý phòng, modal và script Vue thật: khóa/mở khóa được ở 320px và 390px, không tràn ngang. Preview dùng API giả để kiểm tra UI; API thật được kiểm tra riêng bằng PostgreSQL/HTTP tests.
- Chưa triển khai hoặc nghiệm thu thiết bị thật trên production. Cần rebuild bundle migration và publish trước khi deploy.

## Lịch khóa theo thời gian (2026-10-09)

Menu **Lịch khóa phòng** (`/Admin/RoomBlocks`) chỉ dành cho Admin/Manager trong chi nhánh được phân quyền. Chọn một/nhiều phòng, nhập lý do và chọn khoảng liên tục hoặc các khung giờ lặp mỗi ngày trong một khoảng ngày. Khung có giờ kết thúc ≤ bắt đầu đi qua nửa đêm; 00:00–00:00 là cả ngày. Các khung áp dụng chung cho các phòng đã chọn, theo múi giờ chi nhánh. Tối đa 366 ngày, 24 khung/ngày, 50 phòng và 5.000 khoảng cho mỗi lần lưu.

- Bảng mới `room_booking_blocks` lưu từng khoảng UTC, nhóm bằng `batch_id`. Migration `20261009142839_AddRoomBookingSchedules` chỉ thêm bảng/index/FK/check constraint; không sửa phòng, booking hoặc payment cũ.
- Khóa không thời hạn vẫn dùng cờ hiện có và được liệt kê cùng lịch khóa. Khóa hữu hạn không bật cờ này: các ngày/giờ khác vẫn đặt được.
- API `GET/POST /api/admin/properties/{propertyId}/room-blocks/`, `PUT /{batchId}`, `POST /{batchId}/end`; mutation yêu cầu antiforgery. Không hard-delete khoảng cũ: sửa/kết thúc ghi thời điểm hủy và audit, bản sửa tạo các khoảng mới cùng nhóm.
- Nếu có đơn Requested/Held/Confirmed/CheckedIn trùng khoảng khóa, trả `existing_bookings` với danh sách đơn. Khi lưu lại, phải xác nhận đúng ID các đơn đã xem. Đơn mới xuất hiện trong lúc xác nhận sẽ được báo lại. Không tự hủy đơn, thay đổi payment hoặc gửi thông báo khóa cho khách.
- Tạo đơn, chuyển phòng/thời gian và import khóa cùng dòng phòng trong transaction rồi kiểm tra overlap `[start,end)`. Đơn cũ giữ nguyên; sửa nội dung hoặc rút ngắn thời gian được phép. Mở rộng thời gian/chuyển phòng không được giao lịch khóa. Khách đã có QR vẫn thanh toán được.
- Lịch admin hiển thị vùng khóa và lý do. Public chỉ nhận `kind=locked`, không có lý do/người khóa. Nếu phần còn trống của một khung là một khoảng liên tục, khách được thấy giờ sử dụng thực tế và có thể chọn; phần giao khóa không được đặt. Nhiều khung liền nhau không được bắc qua khoảng khóa.
- Khóa tự hết hiệu lực khi hết giờ bằng truy vấn khoảng thời gian; không cần job mở khóa. Khi hủy sớm hoặc sửa, invalidation/realtime làm mới lịch và cache. Lịch sử đã kết thúc giữ nguyên.

Deployment: rebuild application và **efbundle linux-x64** có migration mới, chạy migration trước khi khởi động bản mới. Kiểm tra `__EFMigrationsHistory` có `20261009142839_AddRoomBookingSchedules`. Không dùng bundle cũ trong thư mục upload.

### Kiểm tra source cho lịch khóa theo thời gian

- 26 kiểm thử .NET/PostgreSQL đạt, không bỏ qua: khoảng khóa/biên thời gian, đơn cũ/thanh toán, quyền chi nhánh/antiforgery, tạo đơn đồng thời, sửa/kết thúc lịch và projection lịch.
- 15 kiểm thử JavaScript đạt; có kiểm thử làm mới lịch tương lai khi phiên bản lịch khóa thay đổi nhưng cờ khóa vô thời hạn không đổi. Public availability trả mốc cập nhật lịch khóa, không trả lý do/người thao tác.
- Build application thành công; kiểm tra cú pháp JavaScript và `git diff --check` đạt. Test project còn cảnh báo xUnit2031 có sẵn.
- Preview dùng Razor template, CSS và Vue/script thật với API giả đạt ở 320px, 390px và 1280px: chọn khung qua đêm, xác nhận đơn trùng, sửa/kết thúc, không tràn ngang. Đây không phải nghiệm thu production hoặc điện thoại thật.
- Migration `20261009142839_AddRoomBookingSchedules` đã chạy trên PostgreSQL thử nghiệm riêng. Chưa publish/deploy hoặc chạy migration production; cần rebuild Linux migration bundle cùng application trước triển khai.
